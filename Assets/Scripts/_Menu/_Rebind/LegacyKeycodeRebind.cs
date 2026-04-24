using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[DisallowMultipleComponent]
public class LegacyKeycodeRebind : MonoBehaviour
{
    public const string DEFAULT_PLAYER_PREFS_KEY = "LEGACY_INPUT_BINDINGS_V4_GAMEPAD_AXES";

    public static LegacyKeycodeRebind I { get; private set; }

    public enum Device
    {
        Keyboard,
        Gamepad
    }

    public enum Action
    {
        MoveLeft,
        MoveRight,
        Jump,
        UpAction,
        DownAction,
        Interact,
        Pause,
        Back
    }

    public enum BindingKind
    {
        None,
        Button,
        AxisPositive,
        AxisNegative
    }

    [Serializable]
    public class InputBinding
    {
        public BindingKind kind = BindingKind.Button;
        public KeyCode key = KeyCode.None;
        public string axisName = "";

        [Range(0.05f, 0.99f)]
        public float axisThreshold = 0.5f;

        public static InputBinding None()
        {
            return new InputBinding
            {
                kind = BindingKind.None,
                key = KeyCode.None,
                axisName = "",
                axisThreshold = 0.5f
            };
        }

        public static InputBinding Button(KeyCode key)
        {
            return new InputBinding
            {
                kind = BindingKind.Button,
                key = key,
                axisName = "",
                axisThreshold = 0.5f
            };
        }

        public static InputBinding AxisPositive(string axisName, float threshold = 0.5f)
        {
            return new InputBinding
            {
                kind = BindingKind.AxisPositive,
                key = KeyCode.None,
                axisName = axisName,
                axisThreshold = Mathf.Clamp(threshold, 0.05f, 0.99f)
            };
        }

        public static InputBinding AxisNegative(string axisName, float threshold = 0.5f)
        {
            return new InputBinding
            {
                kind = BindingKind.AxisNegative,
                key = KeyCode.None,
                axisName = axisName,
                axisThreshold = Mathf.Clamp(threshold, 0.05f, 0.99f)
            };
        }

        public InputBinding Clone()
        {
            return new InputBinding
            {
                kind = kind,
                key = key,
                axisName = axisName,
                axisThreshold = axisThreshold
            };
        }

        public bool SameAs(InputBinding other)
        {
            if (other == null)
                return false;

            if (kind != other.kind)
                return false;

            switch (kind)
            {
                case BindingKind.None:
                    return true;

                case BindingKind.Button:
                    return key == other.key;

                case BindingKind.AxisPositive:
                case BindingKind.AxisNegative:
                    return string.Equals(axisName, other.axisName, StringComparison.OrdinalIgnoreCase);

                default:
                    return false;
            }
        }
    }

    [Serializable]
    public class KeyboardBinds
    {
        public InputBinding moveLeft = InputBinding.Button(KeyCode.A);
        public InputBinding moveRight = InputBinding.Button(KeyCode.D);
        public InputBinding jump = InputBinding.Button(KeyCode.Space);
        public InputBinding upAction = InputBinding.Button(KeyCode.W);
        public InputBinding downAction = InputBinding.Button(KeyCode.S);
        public InputBinding interact = InputBinding.Button(KeyCode.F);
        public InputBinding pause = InputBinding.Button(KeyCode.Escape);
        public InputBinding back = InputBinding.Button(KeyCode.Escape);
    }

    [Serializable]
    public class GamepadBinds
    {
        public InputBinding moveLeft = InputBinding.AxisNegative("GamepadHorizontal", 0.45f);
        public InputBinding moveRight = InputBinding.AxisPositive("GamepadHorizontal", 0.45f);
        public InputBinding jump = InputBinding.Button(KeyCode.JoystickButton0);
        public InputBinding upAction = InputBinding.AxisPositive("GamepadVertical", 0.55f);
        public InputBinding downAction = InputBinding.AxisNegative("GamepadVertical", 0.55f);
        public InputBinding interact = InputBinding.Button(KeyCode.JoystickButton2);
        public InputBinding pause = InputBinding.Button(KeyCode.JoystickButton7);
        public InputBinding back = InputBinding.Button(KeyCode.JoystickButton1);
    }

    [Serializable]
    private class SaveData
    {
        public KeyboardBinds keyboard = new KeyboardBinds();
        public GamepadBinds gamepad = new GamepadBinds();
    }

    [Serializable]
    public class RebindRow
    {
        [Header("Что ребиндим")]
        public Device device = Device.Keyboard;
        public Action action = Action.Jump;

        [Header("UI строки")]
        public Graphic actionLabel;
        public Graphic keyLabel;
        public Button changeButton;
        public Button resetButton;

        [Header("Кастомное имя")]
        public string customActionName;
    }

    [Serializable]
    public class ResetConfirmUI
    {
        [Header("Открытие")]
        public Button openResetButton;

        [Header("Панель подтверждения")]
        public GameObject confirmPanel;

        [Header("Кнопки")]
        public Button confirmYesButton;
        public Button cancelButton;

        [Header("Автовыделение")]
        [Tooltip("Что выделить при открытии панели. Лучше ставить кнопку НЕТ.")]
        public Button firstSelectedButton;

        [Tooltip("Если firstSelectedButton не задан, сначала пробуем выделить cancelButton.")]
        public bool preferCancelButton = true;

        [Tooltip("Если кнопки явно не заданы, ищем первую активную кнопку внутри панели.")]
        public bool findFirstButtonInPanel = true;

        [Tooltip("Сколько кадров подождать после SetActive(true), перед тем как выделять кнопку.")]
        [Min(0)]
        public int selectDelayFrames = 1;
    }

    [Serializable]
    public class AxisCaptureCandidate
    {
        public Device device = Device.Gamepad;
        public string axisName = "GamepadVertical";
        public string displayName = "Gamepad Vertical";
        public bool allowPositive = true;
        public bool allowNegative = true;

        [Range(0.1f, 0.99f)]
        public float captureThreshold = 0.65f;

        [Range(0.05f, 0.99f)]
        public float runtimeThreshold = 0.5f;
    }

    private struct RuntimeState
    {
        public int frame;
        public bool previousHeld;
        public bool currentHeld;
    }

    [Header("Жизнь между сценами")]
    [SerializeField]
    private bool dontDestroyOnLoad = true;

    [Header("Сохранение")]
    [SerializeField]
    private string playerPrefsKey = DEFAULT_PLAYER_PREFS_KEY;

    [Header("Дефолтные бинды")]
    [SerializeField]
    private KeyboardBinds defaultKeyboard = new KeyboardBinds();

    [SerializeField]
    private GamepadBinds defaultGamepad = new GamepadBinds();

    [Header("Текущие бинды")]
    [SerializeField]
    private KeyboardBinds keyboard = new KeyboardBinds();

    [SerializeField]
    private GamepadBinds gamepad = new GamepadBinds();

    [Header("UI: Reset Keyboard")]
    [SerializeField]
    private ResetConfirmUI keyboardResetUI = new ResetConfirmUI();

    [Header("UI: Reset Gamepad")]
    [SerializeField]
    private ResetConfirmUI gamepadResetUI = new ResetConfirmUI();

    [Header("UI: строки ребинда")]
    [SerializeField]
    private List<RebindRow> rows = new List<RebindRow>();

    [Header("UI: ожидание новой кнопки")]
    [SerializeField]
    private GameObject waitingOverlay;

    [SerializeField]
    private Graphic waitingText;

    [SerializeField, TextArea(2, 6)]
    private string waitingMessage =
        "Нажми кнопку, стик, крестовину или курок...\nОчистить: Backspace/Delete\nОтмена: Esc / B";

    [Header("Оси, которые можно ловить при ребинде")]
    [SerializeField]
    private List<AxisCaptureCandidate> axisCaptureCandidates = new List<AxisCaptureCandidate>
    {
        new AxisCaptureCandidate { device = Device.Gamepad, axisName = "GamepadHorizontal", displayName = "Left Stick X", allowPositive = true, allowNegative = true, captureThreshold = 0.65f, runtimeThreshold = 0.45f },
        new AxisCaptureCandidate { device = Device.Gamepad, axisName = "GamepadVertical", displayName = "Left Stick Y", allowPositive = true, allowNegative = true, captureThreshold = 0.65f, runtimeThreshold = 0.45f },
        new AxisCaptureCandidate { device = Device.Gamepad, axisName = "DPadX", displayName = "D-Pad X", allowPositive = true, allowNegative = true, captureThreshold = 0.65f, runtimeThreshold = 0.5f },
        new AxisCaptureCandidate { device = Device.Gamepad, axisName = "DPadY", displayName = "D-Pad Y", allowPositive = true, allowNegative = true, captureThreshold = 0.65f, runtimeThreshold = 0.5f },
        new AxisCaptureCandidate { device = Device.Gamepad, axisName = "LT", displayName = "LT / L2", allowPositive = true, allowNegative = false, captureThreshold = 0.65f, runtimeThreshold = 0.5f },
        new AxisCaptureCandidate { device = Device.Gamepad, axisName = "RT", displayName = "RT / R2", allowPositive = true, allowNegative = false, captureThreshold = 0.65f, runtimeThreshold = 0.5f },
        new AxisCaptureCandidate { device = Device.Gamepad, axisName = "Triggers", displayName = "Triggers Axis", allowPositive = true, allowNegative = true, captureThreshold = 0.65f, runtimeThreshold = 0.5f }
    };

    [Header("Правила")]
    [SerializeField]
    private bool preventDuplicatesPerDevice = true;

    [SerializeField]
    private int gamepadButtonsCount = 20;

    [Header("Отмена / очистка")]
    [SerializeField]
    private KeyCode cancelKeyboardKey = KeyCode.Escape;

    [SerializeField]
    private KeyCode cancelGamepadKey = KeyCode.JoystickButton1;

    [SerializeField]
    private KeyCode clearKeyboardKey1 = KeyCode.Backspace;

    [SerializeField]
    private KeyCode clearKeyboardKey2 = KeyCode.Delete;

    [Header("UI защита")]
    [SerializeField]
    private float menuInputBlockDuration = 0.12f;

    [SerializeField]
    private float captureStartDelay = 0.12f;

    [SerializeField]
    private bool clearCurrentSelectedOnRebind = true;

    [SerializeField]
    private bool forceOverlayRaycastBlock = true;

    [SerializeField]
    private bool ignoreSpaceSubmitOnRebindButtons = true;

    [SerializeField]
    private List<KeyCode> forbiddenBindingKeys = new List<KeyCode>
    {
        KeyCode.Return,
        KeyCode.KeypadEnter
    };

    public event System.Action OnBindsChanged;

    private static bool s_runtimeLoaded = false;
    private static string s_runtimePrefsKey = DEFAULT_PLAYER_PREFS_KEY;
    private static KeyboardBinds s_keyboard = new KeyboardBinds();
    private static GamepadBinds s_gamepad = new GamepadBinds();
    private static readonly Dictionary<string, RuntimeState> s_runtimeStates = new Dictionary<string, RuntimeState>();
    private static readonly Dictionary<string, bool> s_lastHeldStates = new Dictionary<string, bool>();

    private bool _isRebinding;
    private int _rebindRowIndex = -1;
    private Device _rebindDevice;
    private Action _rebindAction;

    private bool _isResetConfirmOpen;
    private Device _activeResetConfirmDevice;
    private GameObject _selectedBeforeResetConfirm;

    private KeyCode[] _keyboardKeysCache;
    private KeyCode[] _gamepadKeysCache;

    private readonly Dictionary<string, float> _axisCaptureBaseline = new Dictionary<string, float>();

    private float _blockOtherUiUntilUnscaled = -1f;
    private float _ignoreCaptureUntilUnscaled = -1f;
    private GameObject _selectedBeforeRebind;

    public bool IsRebinding => _isRebinding;

    public static bool RuntimeReady
    {
        get
        {
            EnsureRuntimeLoaded();
            return true;
        }
    }

    public static bool IsAnyRebinding => I != null && I.IsRebinding;

    public bool IsBlockingOtherUi =>
        _isRebinding ||
        _isResetConfirmOpen ||
        Time.unscaledTime < _blockOtherUiUntilUnscaled;

    public KeyboardBinds Keyboard => keyboard;
    public GamepadBinds Gamepad => gamepad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RuntimeInit()
    {
        EnsureRuntimeLoaded();
    }

    private void Awake()
    {
        if (I != null && I != this)
        {
            I.AbsorbUIFromDuplicate(this);
            Destroy(this);
            return;
        }

        I = this;

        if (dontDestroyOnLoad && transform.parent == null)
            DontDestroyOnLoad(gameObject);

        BuildKeyCaches();

        LoadRuntimeFromPrefs(playerPrefsKey, defaultKeyboard, defaultGamepad);
        SyncInstanceFromRuntime();

        EnsureOverlayBlocksRaycasts();
        WireUI();
        RefreshAllRows();
        SetOverlay(false);
        CloseAllResetConfirmPanelsImmediate();
    }

    private void OnDisable()
    {
        CancelRebind();
        CloseAllResetConfirmPanelsImmediate();
        _isResetConfirmOpen = false;
        _selectedBeforeResetConfirm = null;
    }

    private void Update()
    {
        if (_isResetConfirmOpen)
        {
            if (Input.GetKeyDown(cancelKeyboardKey) || Input.GetKeyDown(cancelGamepadKey))
                CloseActiveResetConfirm(true);

            return;
        }

        if (!_isRebinding)
            return;

        if (Input.GetKeyDown(cancelKeyboardKey) || Input.GetKeyDown(cancelGamepadKey))
        {
            CancelRebind();
            return;
        }

        if (Input.GetKeyDown(clearKeyboardKey1) || Input.GetKeyDown(clearKeyboardKey2))
        {
            TryApplyRebind(InputBinding.None());
            return;
        }

        if (Time.unscaledTime < _ignoreCaptureUntilUnscaled)
            return;

        if (_rebindDevice == Device.Keyboard)
        {
            if (TryGetPressedKeyboardKey(out KeyCode key))
                TryApplyRebind(InputBinding.Button(key));
        }
        else
        {
            if (TryGetPressedGamepadKey(out KeyCode key))
            {
                TryApplyRebind(InputBinding.Button(key));
                return;
            }

            if (TryGetMovedAxis(_rebindDevice, out InputBinding axisBinding))
                TryApplyRebind(axisBinding);
        }
    }

    public static InputBinding GetBindingStatic(Device device, Action action)
    {
        EnsureRuntimeLoaded();

        return device == Device.Keyboard
            ? GetKeyboardBindingStatic(action)
            : GetGamepadBindingStatic(action);
    }

    public InputBinding GetBinding(Device device, Action action)
    {
        return GetBindingStatic(device, action);
    }

    public KeyCode GetKey(Device device, Action action)
    {
        InputBinding binding = GetBindingStatic(device, action);

        if (binding == null || binding.kind != BindingKind.Button)
            return KeyCode.None;

        return binding.key;
    }

    public static bool GetDownStatic(Device device, Action action)
    {
        RuntimeState state = GetRuntimeStateStatic(device, action);
        return state.currentHeld && !state.previousHeld;
    }

    public static bool GetHeldStatic(Device device, Action action)
    {
        RuntimeState state = GetRuntimeStateStatic(device, action);
        return state.currentHeld;
    }

    public static bool GetUpStatic(Device device, Action action)
    {
        RuntimeState state = GetRuntimeStateStatic(device, action);
        return !state.currentHeld && state.previousHeld;
    }

    public bool GetDown(Device device, Action action)
    {
        return GetDownStatic(device, action);
    }

    public bool GetHeld(Device device, Action action)
    {
        return GetHeldStatic(device, action);
    }

    public bool GetUp(Device device, Action action)
    {
        return GetUpStatic(device, action);
    }

    public bool GetDownAny(Action action)
    {
        return GetDownStatic(Device.Keyboard, action) || GetDownStatic(Device.Gamepad, action);
    }

    public bool GetHeldAny(Action action)
    {
        return GetHeldStatic(Device.Keyboard, action) || GetHeldStatic(Device.Gamepad, action);
    }

    public bool GetUpAny(Action action)
    {
        return GetUpStatic(Device.Keyboard, action) || GetUpStatic(Device.Gamepad, action);
    }

    public static bool GetDownAnyStatic(Action action)
    {
        return GetDownStatic(Device.Keyboard, action) || GetDownStatic(Device.Gamepad, action);
    }

    public static bool GetHeldAnyStatic(Action action)
    {
        return GetHeldStatic(Device.Keyboard, action) || GetHeldStatic(Device.Gamepad, action);
    }

    public static bool GetUpAnyStatic(Action action)
    {
        return GetUpStatic(Device.Keyboard, action) || GetUpStatic(Device.Gamepad, action);
    }

    public int GetKeyboardMoveDir()
    {
        int dir = 0;

        if (GetHeldStatic(Device.Keyboard, Action.MoveLeft))
            dir -= 1;

        if (GetHeldStatic(Device.Keyboard, Action.MoveRight))
            dir += 1;

        return Mathf.Clamp(dir, -1, 1);
    }

    public int GetGamepadButtonMoveDir()
    {
        int dir = 0;

        if (GetHeldStatic(Device.Gamepad, Action.MoveLeft))
            dir -= 1;

        if (GetHeldStatic(Device.Gamepad, Action.MoveRight))
            dir += 1;

        return Mathf.Clamp(dir, -1, 1);
    }

    private static RuntimeState GetRuntimeStateStatic(Device device, Action action)
    {
        EnsureRuntimeLoaded();

        string id = device + "_" + action;

        if (s_runtimeStates.TryGetValue(id, out RuntimeState cached) && cached.frame == Time.frameCount)
            return cached;

        bool previousHeld = false;
        s_lastHeldStates.TryGetValue(id, out previousHeld);

        bool currentHeld = EvaluateBindingHeldStatic(GetBindingStatic(device, action));

        RuntimeState state = new RuntimeState
        {
            frame = Time.frameCount,
            previousHeld = previousHeld,
            currentHeld = currentHeld
        };

        s_runtimeStates[id] = state;
        s_lastHeldStates[id] = currentHeld;

        return state;
    }

    private static bool EvaluateBindingHeldStatic(InputBinding binding)
    {
        if (binding == null)
            return false;

        switch (binding.kind)
        {
            case BindingKind.Button:
                return binding.key != KeyCode.None && Input.GetKey(binding.key);

            case BindingKind.AxisPositive:
                return SafeGetAxisRawStatic(binding.axisName) >= Mathf.Clamp(binding.axisThreshold, 0.05f, 0.99f);

            case BindingKind.AxisNegative:
                return SafeGetAxisRawStatic(binding.axisName) <= -Mathf.Clamp(binding.axisThreshold, 0.05f, 0.99f);

            case BindingKind.None:
            default:
                return false;
        }
    }

    private static float SafeGetAxisRawStatic(string axisName)
    {
        if (string.IsNullOrWhiteSpace(axisName))
            return 0f;

        try
        {
            return Input.GetAxisRaw(axisName);
        }
        catch
        {
            return 0f;
        }
    }

    private static void EnsureRuntimeLoaded()
    {
        if (s_runtimeLoaded)
            return;

        LoadRuntimeFromPrefs(DEFAULT_PLAYER_PREFS_KEY, new KeyboardBinds(), new GamepadBinds());
    }

    private static void LoadRuntimeFromPrefs(string key, KeyboardBinds defaultKeyboard, GamepadBinds defaultGamepad)
    {
        s_runtimePrefsKey = string.IsNullOrWhiteSpace(key) ? DEFAULT_PLAYER_PREFS_KEY : key;

        if (!PlayerPrefs.HasKey(s_runtimePrefsKey))
        {
            s_keyboard = Copy(defaultKeyboard);
            s_gamepad = Copy(defaultGamepad);
            s_runtimeLoaded = true;
            ClearRuntimeStateStatic();
            return;
        }

        string json = PlayerPrefs.GetString(s_runtimePrefsKey);

        if (string.IsNullOrWhiteSpace(json))
        {
            s_keyboard = Copy(defaultKeyboard);
            s_gamepad = Copy(defaultGamepad);
            s_runtimeLoaded = true;
            ClearRuntimeStateStatic();
            return;
        }

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            if (data == null)
            {
                s_keyboard = Copy(defaultKeyboard);
                s_gamepad = Copy(defaultGamepad);
            }
            else
            {
                s_keyboard = data.keyboard != null ? Copy(data.keyboard) : Copy(defaultKeyboard);
                s_gamepad = data.gamepad != null ? Copy(data.gamepad) : Copy(defaultGamepad);
            }
        }
        catch
        {
            s_keyboard = Copy(defaultKeyboard);
            s_gamepad = Copy(defaultGamepad);
        }

        s_runtimeLoaded = true;
        ClearRuntimeStateStatic();
    }

    private static void SaveRuntimeToPrefs()
    {
        EnsureRuntimeLoaded();

        SaveData data = new SaveData
        {
            keyboard = Copy(s_keyboard),
            gamepad = Copy(s_gamepad)
        };

        string json = JsonUtility.ToJson(data, false);

        PlayerPrefs.SetString(s_runtimePrefsKey, json);
        PlayerPrefs.Save();
    }

    private void SyncInstanceFromRuntime()
    {
        EnsureRuntimeLoaded();

        keyboard = Copy(s_keyboard);
        gamepad = Copy(s_gamepad);
    }

    private void SyncRuntimeFromInstance()
    {
        s_keyboard = Copy(keyboard);
        s_gamepad = Copy(gamepad);
        s_runtimeLoaded = true;
        ClearRuntimeStateStatic();
    }

    private static InputBinding GetKeyboardBindingStatic(Action action)
    {
        EnsureRuntimeLoaded();

        switch (action)
        {
            case Action.MoveLeft:
                return s_keyboard.moveLeft;

            case Action.MoveRight:
                return s_keyboard.moveRight;

            case Action.Jump:
                return s_keyboard.jump;

            case Action.UpAction:
                return s_keyboard.upAction;

            case Action.DownAction:
                return s_keyboard.downAction;

            case Action.Interact:
                return s_keyboard.interact;

            case Action.Pause:
                return s_keyboard.pause;

            case Action.Back:
                return s_keyboard.back;

            default:
                return InputBinding.None();
        }
    }

    private static InputBinding GetGamepadBindingStatic(Action action)
    {
        EnsureRuntimeLoaded();

        switch (action)
        {
            case Action.MoveLeft:
                return s_gamepad.moveLeft;

            case Action.MoveRight:
                return s_gamepad.moveRight;

            case Action.Jump:
                return s_gamepad.jump;

            case Action.UpAction:
                return s_gamepad.upAction;

            case Action.DownAction:
                return s_gamepad.downAction;

            case Action.Interact:
                return s_gamepad.interact;

            case Action.Pause:
                return s_gamepad.pause;

            case Action.Back:
                return s_gamepad.back;

            default:
                return InputBinding.None();
        }
    }

    private void WireUI()
    {
        WireResetConfirmUI(keyboardResetUI, Device.Keyboard);
        WireResetConfirmUI(gamepadResetUI, Device.Gamepad);

        for (int i = 0; i < rows.Count; i++)
        {
            int index = i;
            RebindRow row = rows[index];
            Device rowDevice = row.device;
            Action rowAction = row.action;

            if (row.changeButton != null)
            {
                row.changeButton.onClick.RemoveAllListeners();
                row.changeButton.onClick.AddListener(() => OnChangeButtonClicked(index));
            }

            if (row.resetButton != null)
            {
                row.resetButton.onClick.RemoveAllListeners();
                row.resetButton.onClick.AddListener(() => OnResetOneClicked(rowDevice, rowAction));
            }
        }
    }

    private void WireResetConfirmUI(ResetConfirmUI ui, Device device)
    {
        if (ui == null)
            return;

        if (ui.openResetButton != null)
        {
            ui.openResetButton.onClick.RemoveAllListeners();
            ui.openResetButton.onClick.AddListener(() => OnOpenResetConfirmClicked(device));
        }

        if (ui.confirmYesButton != null)
        {
            ui.confirmYesButton.onClick.RemoveAllListeners();
            ui.confirmYesButton.onClick.AddListener(() => OnConfirmResetClicked(device));
        }

        if (ui.cancelButton != null)
        {
            ui.cancelButton.onClick.RemoveAllListeners();
            ui.cancelButton.onClick.AddListener(() => OnCancelResetClicked(device));
        }
    }

    public void RefreshAllRows()
    {
        SyncInstanceFromRuntime();

        for (int i = 0; i < rows.Count; i++)
            RefreshRow(i);
    }

    private void RefreshRow(int index)
    {
        if (index < 0 || index >= rows.Count)
            return;

        RebindRow row = rows[index];

        string actionText = string.IsNullOrWhiteSpace(row.customActionName)
            ? GetDefaultActionName(row.action)
            : row.customActionName;

        SetGraphicText(row.actionLabel, actionText);
        SetGraphicText(row.keyLabel, PrettyBinding(GetBindingStatic(row.device, row.action)));
    }

    public void BeginRebind(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= rows.Count)
            return;

        if (_isRebinding)
            return;

        if (_isResetConfirmOpen)
            return;

        if (ShouldIgnoreSpaceSubmitThisFrame())
            return;

        RebindRow row = rows[rowIndex];

        if (row.changeButton == null)
            return;

        _isRebinding = true;
        _rebindRowIndex = rowIndex;
        _rebindDevice = row.device;
        _rebindAction = row.action;
        _ignoreCaptureUntilUnscaled = Time.unscaledTime + Mathf.Max(0f, captureStartDelay);

        CacheAxisBaseline(_rebindDevice);
        BlockOtherUiForAWhile();
        CacheAndClearSelectedObject();

        SetOverlay(true, waitingMessage);
        SetRowsInteractable(false);
    }

    public void CancelRebind()
    {
        if (!_isRebinding)
            return;

        Button buttonToRestore = GetButtonToRestoreSelection();

        _isRebinding = false;
        _rebindRowIndex = -1;
        _ignoreCaptureUntilUnscaled = -1f;
        _axisCaptureBaseline.Clear();

        BlockOtherUiForAWhile();
        SetOverlay(false);
        SetRowsInteractable(true);
        RestoreSelectionNextFrame(buttonToRestore);
    }

    private void TryApplyRebind(InputBinding binding)
    {
        if (binding == null)
            return;

        if (binding.kind == BindingKind.Button)
        {
            if (binding.key == cancelKeyboardKey || binding.key == cancelGamepadKey)
            {
                CancelRebind();
                return;
            }

            if (IsForbiddenBindingKey(binding.key))
            {
                SetOverlay(true, $"Кнопку {PrettyKey(binding.key)} нельзя назначить.\nНажми другую.\nОтмена: Esc / B");
                BlockOtherUiForAWhile();
                return;
            }
        }

        if (preventDuplicatesPerDevice && IsUsed(_rebindDevice, binding, _rebindAction))
        {
            SetOverlay(true, $"Бинд {PrettyBinding(binding)} уже занят.\nНажми другой.\nОтмена: Esc / B");
            BlockOtherUiForAWhile();
            return;
        }

        SetBinding(_rebindDevice, _rebindAction, binding);
        SaveRuntimeToPrefs();

        if (_rebindRowIndex >= 0)
            RefreshRow(_rebindRowIndex);

        OnBindsChanged?.Invoke();
        CancelRebind();
    }

    private void SetRowsInteractable(bool enabled)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            RebindRow row = rows[i];

            if (row.changeButton != null)
                row.changeButton.interactable = enabled;

            if (row.resetButton != null)
                row.resetButton.interactable = enabled;
        }

        if (keyboardResetUI != null && keyboardResetUI.openResetButton != null)
            keyboardResetUI.openResetButton.interactable = enabled;

        if (gamepadResetUI != null && gamepadResetUI.openResetButton != null)
            gamepadResetUI.openResetButton.interactable = enabled;
    }

    private void SetOverlay(bool enabled, string message = "")
    {
        if (waitingOverlay != null)
            waitingOverlay.SetActive(enabled);

        if (waitingOverlay != null)
        {
            CanvasGroup group = waitingOverlay.GetComponent<CanvasGroup>();

            if (group != null)
            {
                group.interactable = enabled;
                group.blocksRaycasts = enabled;
            }
        }

        SetGraphicText(waitingText, enabled ? message : "");
    }

    public void ResetKeyboardToDefaults()
    {
        s_keyboard = Copy(defaultKeyboard);
        keyboard = Copy(s_keyboard);

        ClearRuntimeStateStatic();
        SaveRuntimeToPrefs();
        OnBindsChanged?.Invoke();
        RefreshAllRows();
    }

    public void ResetGamepadToDefaults()
    {
        s_gamepad = Copy(defaultGamepad);
        gamepad = Copy(s_gamepad);

        ClearRuntimeStateStatic();
        SaveRuntimeToPrefs();
        OnBindsChanged?.Invoke();
        RefreshAllRows();
    }

    public void ResetOneToDefault(Device device, Action action)
    {
        SyncInstanceFromRuntime();

        if (device == Device.Keyboard)
            ApplyKeyboardBinding(action, GetKeyboardBinding(action, defaultKeyboard));
        else
            ApplyGamepadBinding(action, GetGamepadBinding(action, defaultGamepad));

        SyncRuntimeFromInstance();
        SaveRuntimeToPrefs();

        OnBindsChanged?.Invoke();
        RefreshAllRows();
    }

    private void SetBinding(Device device, Action action, InputBinding binding)
    {
        SyncInstanceFromRuntime();

        if (device == Device.Keyboard)
            ApplyKeyboardBinding(action, binding);
        else
            ApplyGamepadBinding(action, binding);

        SyncRuntimeFromInstance();
    }

    private InputBinding GetKeyboardBinding(Action action, KeyboardBinds source)
    {
        switch (action)
        {
            case Action.MoveLeft:
                return source.moveLeft;

            case Action.MoveRight:
                return source.moveRight;

            case Action.Jump:
                return source.jump;

            case Action.UpAction:
                return source.upAction;

            case Action.DownAction:
                return source.downAction;

            case Action.Interact:
                return source.interact;

            case Action.Pause:
                return source.pause;

            case Action.Back:
                return source.back;

            default:
                return InputBinding.None();
        }
    }

    private InputBinding GetGamepadBinding(Action action, GamepadBinds source)
    {
        switch (action)
        {
            case Action.MoveLeft:
                return source.moveLeft;

            case Action.MoveRight:
                return source.moveRight;

            case Action.Jump:
                return source.jump;

            case Action.UpAction:
                return source.upAction;

            case Action.DownAction:
                return source.downAction;

            case Action.Interact:
                return source.interact;

            case Action.Pause:
                return source.pause;

            case Action.Back:
                return source.back;

            default:
                return InputBinding.None();
        }
    }

    private void ApplyKeyboardBinding(Action action, InputBinding binding)
    {
        InputBinding copy = SafeCopyBinding(binding);

        switch (action)
        {
            case Action.MoveLeft:
                keyboard.moveLeft = copy;
                break;

            case Action.MoveRight:
                keyboard.moveRight = copy;
                break;

            case Action.Jump:
                keyboard.jump = copy;
                break;

            case Action.UpAction:
                keyboard.upAction = copy;
                break;

            case Action.DownAction:
                keyboard.downAction = copy;
                break;

            case Action.Interact:
                keyboard.interact = copy;
                break;

            case Action.Pause:
                keyboard.pause = copy;
                break;

            case Action.Back:
                keyboard.back = copy;
                break;
        }
    }

    private void ApplyGamepadBinding(Action action, InputBinding binding)
    {
        InputBinding copy = SafeCopyBinding(binding);

        switch (action)
        {
            case Action.MoveLeft:
                gamepad.moveLeft = copy;
                break;

            case Action.MoveRight:
                gamepad.moveRight = copy;
                break;

            case Action.Jump:
                gamepad.jump = copy;
                break;

            case Action.UpAction:
                gamepad.upAction = copy;
                break;

            case Action.DownAction:
                gamepad.downAction = copy;
                break;

            case Action.Interact:
                gamepad.interact = copy;
                break;

            case Action.Pause:
                gamepad.pause = copy;
                break;

            case Action.Back:
                gamepad.back = copy;
                break;
        }
    }

    private bool IsUsed(Device device, InputBinding binding, Action except)
    {
        if (binding == null || binding.kind == BindingKind.None)
            return false;

        foreach (Action action in Enum.GetValues(typeof(Action)))
        {
            if (action == except)
                continue;

            InputBinding other = GetBindingStatic(device, action);

            if (other != null && other.SameAs(binding))
                return true;
        }

        return false;
    }

    private bool TryGetPressedKeyboardKey(out KeyCode key)
    {
        if (!Input.anyKeyDown)
        {
            key = KeyCode.None;
            return false;
        }

        for (int i = 0; i < _keyboardKeysCache.Length; i++)
        {
            KeyCode current = _keyboardKeysCache[i];

            if (Input.GetKeyDown(current))
            {
                key = current;
                return true;
            }
        }

        key = KeyCode.None;
        return false;
    }

    private bool TryGetPressedGamepadKey(out KeyCode key)
    {
        for (int i = 0; i < _gamepadKeysCache.Length; i++)
        {
            KeyCode current = _gamepadKeysCache[i];

            if (Input.GetKeyDown(current))
            {
                key = current;
                return true;
            }
        }

        key = KeyCode.None;
        return false;
    }

    private void CacheAxisBaseline(Device device)
    {
        _axisCaptureBaseline.Clear();

        for (int i = 0; i < axisCaptureCandidates.Count; i++)
        {
            AxisCaptureCandidate candidate = axisCaptureCandidates[i];

            if (candidate == null || candidate.device != device)
                continue;

            if (string.IsNullOrWhiteSpace(candidate.axisName))
                continue;

            _axisCaptureBaseline[AxisBaselineId(candidate)] = SafeGetAxisRawStatic(candidate.axisName);
        }
    }

    private bool TryGetMovedAxis(Device device, out InputBinding binding)
    {
        for (int i = 0; i < axisCaptureCandidates.Count; i++)
        {
            AxisCaptureCandidate candidate = axisCaptureCandidates[i];

            if (candidate == null || candidate.device != device)
                continue;

            if (string.IsNullOrWhiteSpace(candidate.axisName))
                continue;

            float value = SafeGetAxisRawStatic(candidate.axisName);
            float baseline = 0f;
            _axisCaptureBaseline.TryGetValue(AxisBaselineId(candidate), out baseline);

            float threshold = Mathf.Clamp(candidate.captureThreshold, 0.1f, 0.99f);

            if (candidate.allowPositive && value >= threshold && Mathf.Abs(value - baseline) >= 0.35f)
            {
                binding = InputBinding.AxisPositive(candidate.axisName, candidate.runtimeThreshold);
                return true;
            }

            if (candidate.allowNegative && value <= -threshold && Mathf.Abs(value - baseline) >= 0.35f)
            {
                binding = InputBinding.AxisNegative(candidate.axisName, candidate.runtimeThreshold);
                return true;
            }
        }

        binding = null;
        return false;
    }

    private string AxisBaselineId(AxisCaptureCandidate candidate)
    {
        return candidate.device + "_" + candidate.axisName;
    }

    private void BuildKeyCaches()
    {
        List<KeyCode> keyboardKeys = new List<KeyCode>(256);

        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
        {
            if (key == KeyCode.None)
                continue;

            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
                continue;

            string keyName = key.ToString();

            if (keyName.StartsWith("Joystick", StringComparison.OrdinalIgnoreCase))
                continue;

            keyboardKeys.Add(key);
        }

        _keyboardKeysCache = keyboardKeys.ToArray();

        int count = Mathf.Clamp(gamepadButtonsCount, 1, 30);
        _gamepadKeysCache = new KeyCode[count];

        for (int i = 0; i < count; i++)
            _gamepadKeysCache[i] = (KeyCode)((int)KeyCode.JoystickButton0 + i);
    }

    private static InputBinding SafeCopyBinding(InputBinding source)
    {
        if (source == null)
            return InputBinding.None();

        InputBinding copy = source.Clone();

        if (copy.kind == BindingKind.Button)
            copy.axisName = "";

        if (copy.kind == BindingKind.AxisPositive || copy.kind == BindingKind.AxisNegative)
            copy.key = KeyCode.None;

        copy.axisThreshold = Mathf.Clamp(copy.axisThreshold, 0.05f, 0.99f);

        if (copy.kind == BindingKind.AxisPositive || copy.kind == BindingKind.AxisNegative)
        {
            if (copy.axisName == "Horizontal")
                copy.axisName = "GamepadHorizontal";

            if (copy.axisName == "Vertical")
                copy.axisName = "GamepadVertical";
        }

        return copy;
    }

    private static KeyboardBinds Copy(KeyboardBinds source)
    {
        if (source == null)
            return new KeyboardBinds();

        return new KeyboardBinds
        {
            moveLeft = SafeCopyBinding(source.moveLeft),
            moveRight = SafeCopyBinding(source.moveRight),
            jump = SafeCopyBinding(source.jump),
            upAction = SafeCopyBinding(source.upAction),
            downAction = SafeCopyBinding(source.downAction),
            interact = SafeCopyBinding(source.interact),
            pause = SafeCopyBinding(source.pause),
            back = SafeCopyBinding(source.back)
        };
    }

    private static GamepadBinds Copy(GamepadBinds source)
    {
        if (source == null)
            return new GamepadBinds();

        return new GamepadBinds
        {
            moveLeft = SafeCopyBinding(source.moveLeft),
            moveRight = SafeCopyBinding(source.moveRight),
            jump = SafeCopyBinding(source.jump),
            upAction = SafeCopyBinding(source.upAction),
            downAction = SafeCopyBinding(source.downAction),
            interact = SafeCopyBinding(source.interact),
            pause = SafeCopyBinding(source.pause),
            back = SafeCopyBinding(source.back)
        };
    }

    private static void ClearRuntimeStateStatic()
    {
        s_runtimeStates.Clear();
        s_lastHeldStates.Clear();
    }

    private void OnChangeButtonClicked(int rowIndex)
    {
        if (ShouldIgnoreSpaceSubmitThisFrame())
            return;

        BeginRebind(rowIndex);
    }

    private void OnResetOneClicked(Device device, Action action)
    {
        if (_isResetConfirmOpen)
            return;

        if (ShouldIgnoreSpaceSubmitThisFrame())
            return;

        ResetOneToDefault(device, action);
    }

    private void OnOpenResetConfirmClicked(Device device)
    {
        if (_isRebinding)
            return;

        if (ShouldIgnoreSpaceSubmitThisFrame())
            return;

        OpenResetConfirm(device);
    }

    private void OnConfirmResetClicked(Device device)
    {
        if (!_isResetConfirmOpen)
            return;

        if (device == Device.Keyboard)
            ResetKeyboardToDefaults();
        else
            ResetGamepadToDefaults();

        CloseActiveResetConfirm(true);
    }

    private void OnCancelResetClicked(Device device)
    {
        if (!_isResetConfirmOpen)
            return;

        CloseActiveResetConfirm(true);
    }

    private void OpenResetConfirm(Device device)
    {
        ResetConfirmUI ui = GetResetConfirmUI(device);

        if (ui == null || ui.confirmPanel == null)
            return;

        if (_isResetConfirmOpen)
            CloseActiveResetConfirm(false);

        _isResetConfirmOpen = true;
        _activeResetConfirmDevice = device;

        _selectedBeforeResetConfirm = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        CloseAllResetConfirmPanelsImmediate();
        SetResetConfirmPanel(ui, true);
        BlockOtherUiForAWhile();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        Button buttonToSelect = GetResetConfirmFirstSelectedButton(ui);

        if (buttonToSelect != null)
            RestoreSelectionNextFrame(buttonToSelect, Mathf.Max(0, ui.selectDelayFrames));
    }

    private void CloseActiveResetConfirm(bool restoreSelection)
    {
        Button buttonToRestore = null;

        if (_isResetConfirmOpen)
        {
            ResetConfirmUI currentUI = GetResetConfirmUI(_activeResetConfirmDevice);

            if (currentUI != null &&
                currentUI.openResetButton != null &&
                currentUI.openResetButton.isActiveAndEnabled)
            {
                buttonToRestore = currentUI.openResetButton;
            }
            else if (_selectedBeforeResetConfirm != null)
            {
                Button selectedButton = _selectedBeforeResetConfirm.GetComponent<Button>();

                if (selectedButton != null && selectedButton.isActiveAndEnabled)
                    buttonToRestore = selectedButton;
            }
        }

        CloseAllResetConfirmPanelsImmediate();

        _isResetConfirmOpen = false;
        _selectedBeforeResetConfirm = null;

        BlockOtherUiForAWhile();

        if (restoreSelection)
            RestoreSelectionNextFrame(buttonToRestore);
    }

    private void CloseAllResetConfirmPanelsImmediate()
    {
        SetResetConfirmPanel(keyboardResetUI, false);
        SetResetConfirmPanel(gamepadResetUI, false);
    }

    private void SetResetConfirmPanel(ResetConfirmUI ui, bool enabled)
    {
        if (ui == null || ui.confirmPanel == null)
            return;

        ui.confirmPanel.SetActive(enabled);
    }

    private ResetConfirmUI GetResetConfirmUI(Device device)
    {
        return device == Device.Keyboard ? keyboardResetUI : gamepadResetUI;
    }

    private Button GetResetConfirmFirstSelectedButton(ResetConfirmUI ui)
    {
        if (ui == null)
            return null;

        if (IsValidSelectableButton(ui.firstSelectedButton))
            return ui.firstSelectedButton;

        if (ui.preferCancelButton && IsValidSelectableButton(ui.cancelButton))
            return ui.cancelButton;

        if (IsValidSelectableButton(ui.confirmYesButton))
            return ui.confirmYesButton;

        if (IsValidSelectableButton(ui.cancelButton))
            return ui.cancelButton;

        if (ui.findFirstButtonInPanel && ui.confirmPanel != null)
        {
            Button[] buttons = ui.confirmPanel.GetComponentsInChildren<Button>(true);

            for (int i = 0; i < buttons.Length; i++)
            {
                if (IsValidSelectableButton(buttons[i]))
                    return buttons[i];
            }
        }

        return null;
    }

    private bool IsValidSelectableButton(Button button)
    {
        if (button == null)
            return false;

        if (!button.gameObject.activeInHierarchy)
            return false;

        if (!button.interactable)
            return false;

        return true;
    }

    private bool ShouldIgnoreSpaceSubmitThisFrame()
    {
        return ignoreSpaceSubmitOnRebindButtons && Input.GetKeyDown(KeyCode.Space);
    }

    private bool IsForbiddenBindingKey(KeyCode key)
    {
        if (forbiddenBindingKeys == null)
            return false;

        for (int i = 0; i < forbiddenBindingKeys.Count; i++)
        {
            if (forbiddenBindingKeys[i] == key)
                return true;
        }

        return false;
    }

    private void BlockOtherUiForAWhile()
    {
        _blockOtherUiUntilUnscaled = Mathf.Max(
            _blockOtherUiUntilUnscaled,
            Time.unscaledTime + Mathf.Max(0f, menuInputBlockDuration));
    }

    private void CacheAndClearSelectedObject()
    {
        if (EventSystem.current == null)
            return;

        _selectedBeforeRebind = EventSystem.current.currentSelectedGameObject;

        if (clearCurrentSelectedOnRebind)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private Button GetButtonToRestoreSelection()
    {
        Button preferredResetButton = GetPreferredResetButtonForDevice(_rebindDevice);

        if (preferredResetButton != null)
            return preferredResetButton;

        if (_rebindRowIndex >= 0 && _rebindRowIndex < rows.Count)
        {
            Button rowButton = rows[_rebindRowIndex].changeButton;

            if (rowButton != null && rowButton.isActiveAndEnabled)
                return rowButton;
        }

        if (_selectedBeforeRebind != null)
        {
            Button selectedButton = _selectedBeforeRebind.GetComponent<Button>();

            if (selectedButton != null && selectedButton.isActiveAndEnabled)
                return selectedButton;
        }

        if (keyboardResetUI != null &&
            keyboardResetUI.openResetButton != null &&
            keyboardResetUI.openResetButton.isActiveAndEnabled)
        {
            return keyboardResetUI.openResetButton;
        }

        if (gamepadResetUI != null &&
            gamepadResetUI.openResetButton != null &&
            gamepadResetUI.openResetButton.isActiveAndEnabled)
        {
            return gamepadResetUI.openResetButton;
        }

        return null;
    }

    private Button GetPreferredResetButtonForDevice(Device device)
    {
        ResetConfirmUI ui = GetResetConfirmUI(device);

        if (ui == null)
            return null;

        if (ui.openResetButton != null &&
            ui.openResetButton.isActiveAndEnabled &&
            ui.openResetButton.interactable)
        {
            return ui.openResetButton;
        }

        return null;
    }

    private void RestoreSelectionNextFrame(Button target)
    {
        RestoreSelectionNextFrame(target, 1);
    }

    private void RestoreSelectionNextFrame(Button target, int delayFrames)
    {
        if (!isActiveAndEnabled)
            return;

        if (target == null)
            return;

        if (EventSystem.current == null)
            return;

        StartCoroutine(RestoreSelectionCoroutine(target, delayFrames));
    }

    private IEnumerator RestoreSelectionCoroutine(Button target, int delayFrames)
    {
        int frames = Mathf.Max(0, delayFrames);

        for (int i = 0; i < frames; i++)
            yield return null;

        if (target == null)
            yield break;

        if (EventSystem.current == null)
            yield break;

        if (!target.gameObject.activeInHierarchy || !target.interactable)
            yield break;

        Canvas.ForceUpdateCanvases();

        EventSystem.current.SetSelectedGameObject(null);
        yield return null;

        EventSystem.current.SetSelectedGameObject(target.gameObject);
        target.Select();
    }

    private void EnsureOverlayBlocksRaycasts()
    {
        if (!forceOverlayRaycastBlock || waitingOverlay == null)
            return;

        CanvasGroup group = waitingOverlay.GetComponent<CanvasGroup>();

        if (group == null)
            group = waitingOverlay.AddComponent<CanvasGroup>();

        group.interactable = waitingOverlay.activeSelf;
        group.blocksRaycasts = waitingOverlay.activeSelf;

        Image image = waitingOverlay.GetComponent<Image>();

        if (image == null)
            image = waitingOverlay.AddComponent<Image>();

        Color color = image.color;

        if (color.a <= 0f)
            color.a = 0.001f;

        image.color = color;
        image.raycastTarget = true;
    }

    private void AbsorbUIFromDuplicate(LegacyKeycodeRebind duplicate)
    {
        bool changed = false;

        if (TryAbsorbResetConfirmUI(ref keyboardResetUI, duplicate.keyboardResetUI))
            changed = true;

        if (TryAbsorbResetConfirmUI(ref gamepadResetUI, duplicate.gamepadResetUI))
            changed = true;

        if (duplicate.waitingOverlay != null)
        {
            waitingOverlay = duplicate.waitingOverlay;
            changed = true;
        }

        if (duplicate.waitingText != null)
        {
            waitingText = duplicate.waitingText;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(duplicate.waitingMessage))
        {
            waitingMessage = duplicate.waitingMessage;
            changed = true;
        }

        if (duplicate.rows != null && duplicate.rows.Count > 0)
        {
            rows = duplicate.rows;
            changed = true;
        }

        if (changed)
        {
            EnsureOverlayBlocksRaycasts();
            WireUI();
            RefreshAllRows();
            SetOverlay(false);
            CloseAllResetConfirmPanelsImmediate();
        }
    }

    private bool TryAbsorbResetConfirmUI(ref ResetConfirmUI target, ResetConfirmUI source)
    {
        if (source == null)
            return false;

        if (target == null)
            target = new ResetConfirmUI();

        bool changed = false;

        if (source.openResetButton != null)
        {
            target.openResetButton = source.openResetButton;
            changed = true;
        }

        if (source.confirmPanel != null)
        {
            target.confirmPanel = source.confirmPanel;
            changed = true;
        }

        if (source.confirmYesButton != null)
        {
            target.confirmYesButton = source.confirmYesButton;
            changed = true;
        }

        if (source.cancelButton != null)
        {
            target.cancelButton = source.cancelButton;
            changed = true;
        }

        if (source.firstSelectedButton != null)
        {
            target.firstSelectedButton = source.firstSelectedButton;
            changed = true;
        }

        target.preferCancelButton = source.preferCancelButton;
        target.findFirstButtonInPanel = source.findFirstButtonInPanel;
        target.selectDelayFrames = source.selectDelayFrames;

        return changed;
    }

    private string GetDefaultActionName(Action action)
    {
        switch (action)
        {
            case Action.MoveLeft:
                return "Влево";

            case Action.MoveRight:
                return "Вправо";

            case Action.Jump:
                return "Прыжок";

            case Action.UpAction:
                return "Действие вверх";

            case Action.DownAction:
                return "Действие вниз / Pounce";

            case Action.Interact:
                return "Взаимодействие";

            case Action.Pause:
                return "Пауза";

            case Action.Back:
                return "Назад";

            default:
                return action.ToString();
        }
    }

    private string PrettyBinding(InputBinding binding)
    {
        if (binding == null || binding.kind == BindingKind.None)
            return "None";

        switch (binding.kind)
        {
            case BindingKind.Button:
                return PrettyKey(binding.key);

            case BindingKind.AxisPositive:
                return PrettyAxis(binding.axisName) + " +";

            case BindingKind.AxisNegative:
                return PrettyAxis(binding.axisName) + " -";

            default:
                return "None";
        }
    }

    private string PrettyAxis(string axisName)
    {
        if (string.IsNullOrWhiteSpace(axisName))
            return "Axis";

        for (int i = 0; i < axisCaptureCandidates.Count; i++)
        {
            AxisCaptureCandidate candidate = axisCaptureCandidates[i];

            if (candidate == null)
                continue;

            if (string.Equals(candidate.axisName, axisName, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(candidate.displayName))
                    return candidate.displayName;
            }
        }

        return axisName;
    }

    private string PrettyKey(KeyCode key)
    {
        if (key == KeyCode.None)
            return "None";

        if (key == KeyCode.Space)
            return "Space";

        if (key == KeyCode.Escape)
            return "Esc";

        if (key == KeyCode.UpArrow)
            return "Up Arrow";

        if (key == KeyCode.DownArrow)
            return "Down Arrow";

        if (key == KeyCode.LeftArrow)
            return "Left Arrow";

        if (key == KeyCode.RightArrow)
            return "Right Arrow";

        if (key == KeyCode.JoystickButton0)
            return "A / Cross";

        if (key == KeyCode.JoystickButton1)
            return "B / Circle";

        if (key == KeyCode.JoystickButton2)
            return "X / Square";

        if (key == KeyCode.JoystickButton3)
            return "Y / Triangle";

        if (key == KeyCode.JoystickButton4)
            return "LB / L1";

        if (key == KeyCode.JoystickButton5)
            return "RB / R1";

        if (key == KeyCode.JoystickButton6)
            return "Back / Select";

        if (key == KeyCode.JoystickButton7)
            return "Start / Options";

        if (key == KeyCode.JoystickButton8)
            return "L3 / Left Stick Press";

        if (key == KeyCode.JoystickButton9)
            return "R3 / Right Stick Press";

        if (key == KeyCode.LeftShift)
            return "Left Shift";

        if (key == KeyCode.RightShift)
            return "Right Shift";

        if (key == KeyCode.LeftControl)
            return "Left Ctrl";

        if (key == KeyCode.RightControl)
            return "Right Ctrl";

        return key.ToString();
    }

    private void SetGraphicText(Graphic target, string value)
    {
        if (target == null)
            return;

        TMP_Text tmp = target as TMP_Text;

        if (tmp != null)
        {
            tmp.text = value;
            return;
        }

        Text legacyText = target as Text;

        if (legacyText != null)
            legacyText.text = value;
    }
}