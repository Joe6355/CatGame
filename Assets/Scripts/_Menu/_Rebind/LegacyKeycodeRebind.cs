using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Legacy (old Input) in-game rebinding using KeyCode + Input.GetKey*.
/// Работает без Input System.
///
/// ВАЖНО:
/// - Во время ребинда отмена: ESC (клава) или B/Circle (JoystickButton1).
///   Эти кнопки НЕ могут быть назначены через ребинд.
/// - Аналоговые стики / оси в рантайме стандартно не ребиндятся.
/// </summary>
public class LegacyKeycodeRebind : MonoBehaviour
{
    public static LegacyKeycodeRebind I { get; private set; }

    public enum Device { Keyboard, Gamepad }

    public enum Action
    {
        MoveLeft,
        MoveRight,
        JumpHold,
        JumpShort,
        Pause,
        Back
    }

    [Serializable]
    public class KeyboardBinds
    {
        [Tooltip("Клавиша движения влево.")]
        public KeyCode moveLeft = KeyCode.A;

        [Tooltip("Клавиша движения вправо.")]
        public KeyCode moveRight = KeyCode.D;

        [Tooltip("Клавиша обычного/зарядного прыжка.")]
        public KeyCode jumpHold = KeyCode.Space;

        [Tooltip("Клавиша короткого прыжка.")]
        public KeyCode jumpShort = KeyCode.LeftShift;

        [Tooltip("Клавиша Pause.")]
        public KeyCode pause = KeyCode.Escape;

        [Tooltip("Клавиша Back.")]
        public KeyCode back = KeyCode.Escape;
    }

    [Serializable]
    public class GamepadBinds
    {
        [Tooltip("Кнопка обычного/зарядного прыжка на геймпаде.")]
        public KeyCode jumpHold = KeyCode.JoystickButton0;

        [Tooltip("Кнопка короткого прыжка на геймпаде.")]
        public KeyCode jumpShort = KeyCode.JoystickButton1;

        [Tooltip("Кнопка Pause на геймпаде.")]
        public KeyCode pause = KeyCode.JoystickButton7;

        [Tooltip("Кнопка Back на геймпаде.")]
        public KeyCode back = KeyCode.JoystickButton1;
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
        [Tooltip("Устройство: Keyboard или Gamepad.")]
        public Device device = Device.Keyboard;

        [Tooltip("Действие, для которого меняем кнопку.")]
        public Action action = Action.JumpHold;

        [Header("UI строки")]
        [Tooltip("Текст названия действия.")]
        public Graphic actionLabel;

        [Tooltip("Текст текущей назначенной кнопки.")]
        public Graphic keyLabel;

        [Tooltip("Кнопка Изменить.")]
        public Button changeButton;

        [Tooltip("Кнопка Сброс только этой строки.")]
        public Button resetButton;

        [Header("Кастомное имя")]
        [Tooltip("Красивое имя вместо action.ToString().")]
        public string customActionName;
    }

    [Header("Режим жизни между сценами")]
    [SerializeField, Tooltip(
        "Если включено — объект станет Singleton и переживёт загрузку сцен.\n" +
        "ВАЖНО: если компонент висит НЕ на root-объекте, DontDestroyOnLoad пропускается,\n" +
        "чтобы не ловить ошибку Unity.")]
    private bool dontDestroyOnLoad = true;

    [Header("Сохранение")]
    [SerializeField, Tooltip("Ключ PlayerPrefs, в котором хранятся бинды в JSON.")]
    private string playerPrefsKey = "LEGACY_KEYCODE_BINDS_JSON";

    [Header("Дефолтные бинды")]
    [SerializeField] private KeyboardBinds defaultKeyboard = new KeyboardBinds();
    [SerializeField] private GamepadBinds defaultGamepad = new GamepadBinds();

    [Header("Текущие бинды (runtime)")]
    [SerializeField] private KeyboardBinds keyboard = new KeyboardBinds();
    [SerializeField] private GamepadBinds gamepad = new GamepadBinds();

    [Header("UI: Reset All")]
    [SerializeField] private Button resetAllButton;

    [Header("UI: строки ребинда")]
    [SerializeField] private List<RebindRow> rows = new List<RebindRow>();

    [Header("UI: оверлей ожидания")]
    [SerializeField] private GameObject waitingOverlay;
    [SerializeField] private Graphic waitingText;

    [SerializeField, TextArea(2, 6)]
    private string waitingMessage =
        "Нажми кнопку для переназначения...\nОтмена: Esc / B";

    [Header("Правила")]
    [SerializeField] private bool preventDuplicatesPerDevice = true;

    [Header("Геймпад: диапазон кнопок")]
    [SerializeField] private int gamepadButtonsCount = 20;

    [Header("Отмена ребинда")]
    [SerializeField] private KeyCode cancelKeyboardKey = KeyCode.Escape;
    [SerializeField] private KeyCode cancelGamepadKey = KeyCode.JoystickButton1;

    [Header("Доп. защита UI")]
    [SerializeField] private float menuInputBlockDuration = 0.12f;
    [SerializeField] private float captureStartDelay = 0.12f;
    [SerializeField] private bool clearCurrentSelectedOnRebind = true;
    [SerializeField] private bool forceOverlayRaycastBlock = true;
    [SerializeField] private bool ignoreSpaceSubmitOnRebindButtons = true;

    [SerializeField]
    private List<KeyCode> forbiddenBindingKeys = new List<KeyCode>
    {
        KeyCode.Return,
        KeyCode.KeypadEnter
    };

    public event System.Action OnBindsChanged;

    private bool _isRebinding;
    private int _rebindRowIndex = -1;
    private Device _rebindDevice;
    private Action _rebindAction;

    private KeyCode[] _keyboardKeysCache;
    private KeyCode[] _gamepadKeysCache;

    private float _blockOtherUiUntilUnscaled = -1f;
    private float _ignoreCaptureUntilUnscaled = -1f;
    private GameObject _selectedBeforeRebind;

    public bool IsRebinding => _isRebinding;
    public bool IsBlockingOtherUi => _isRebinding || Time.unscaledTime < _blockOtherUiUntilUnscaled;
    public KeyboardBinds Keyboard => keyboard;
    public GamepadBinds Gamepad => gamepad;

    private void Awake()
    {
        if (I != null && I != this)
        {
            I.AbsorbUIFromDuplicate(this);
            Destroy(this);
            return;
        }

        I = this;

        TryApplyDontDestroyOnLoad();

        BuildKeyCaches();
        LoadOrDefaults();
        EnsureOverlayBlocksRaycasts();

        WireUI();
        RefreshAllRows();
        SetOverlay(false);
    }

    private void OnDisable()
    {
        CancelRebind();
    }

    private void Update()
    {
        if (!_isRebinding) return;

        if (Input.GetKeyDown(cancelKeyboardKey) || Input.GetKeyDown(cancelGamepadKey))
        {
            CancelRebind();
            return;
        }

        if (Time.unscaledTime < _ignoreCaptureUntilUnscaled)
            return;

        if (_rebindDevice == Device.Keyboard)
        {
            if (TryGetPressedKeyboardKey(out KeyCode k))
                TryApplyRebind(k);
        }
        else
        {
            if (TryGetPressedGamepadKey(out KeyCode k))
                TryApplyRebind(k);
        }
    }

    private void TryApplyDontDestroyOnLoad()
    {
        if (!dontDestroyOnLoad)
            return;

        // DontDestroyOnLoad работает только на root object
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    public KeyCode GetKey(Device device, Action action)
    {
        return device == Device.Keyboard ? GetKeyboardKey(action) : GetGamepadKey(action);
    }

    public bool GetDown(Device device, Action action)
    {
        KeyCode k = GetKey(device, action);
        return k != KeyCode.None && Input.GetKeyDown(k);
    }

    public bool GetHeld(Device device, Action action)
    {
        KeyCode k = GetKey(device, action);
        return k != KeyCode.None && Input.GetKey(k);
    }

    public bool GetUp(Device device, Action action)
    {
        KeyCode k = GetKey(device, action);
        return k != KeyCode.None && Input.GetKeyUp(k);
    }

    public bool GetDownAny(Action action)
    {
        return GetDown(Device.Keyboard, action) || GetDown(Device.Gamepad, action);
    }

    public bool GetHeldAny(Action action)
    {
        return GetHeld(Device.Keyboard, action) || GetHeld(Device.Gamepad, action);
    }

    public bool GetUpAny(Action action)
    {
        return GetUp(Device.Keyboard, action) || GetUp(Device.Gamepad, action);
    }

    public int GetKeyboardMoveDir()
    {
        int dir = 0;
        if (Input.GetKey(keyboard.moveLeft)) dir -= 1;
        if (Input.GetKey(keyboard.moveRight)) dir += 1;
        return dir;
    }

    private void WireUI()
    {
        if (resetAllButton != null)
        {
            resetAllButton.onClick.RemoveAllListeners();
            resetAllButton.onClick.AddListener(OnResetAllClicked);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            int idx = i;
            RebindRow row = rows[idx];

            if (row.changeButton != null)
            {
                row.changeButton.onClick.RemoveAllListeners();
                row.changeButton.onClick.AddListener(() => OnChangeButtonClicked(idx));
            }

            if (row.resetButton != null)
            {
                row.resetButton.onClick.RemoveAllListeners();
                row.resetButton.onClick.AddListener(() => OnResetOneClicked(row.device, row.action));
            }
        }
    }

    public void RefreshAllRows()
    {
        for (int i = 0; i < rows.Count; i++)
            RefreshRow(i);
    }

    private void RefreshRow(int index)
    {
        if (index < 0 || index >= rows.Count) return;

        RebindRow row = rows[index];

        string actionText = string.IsNullOrWhiteSpace(row.customActionName)
            ? row.action.ToString()
            : row.customActionName;

        SetGraphicText(row.actionLabel, actionText);
        SetGraphicText(row.keyLabel, PrettyKey(GetKey(row.device, row.action)));
    }

    public void BeginRebind(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= rows.Count) return;
        if (_isRebinding) return;
        if (ShouldIgnoreSpaceSubmitThisFrame()) return;

        RebindRow row = rows[rowIndex];
        if (row.changeButton == null) return;

        _isRebinding = true;
        _rebindRowIndex = rowIndex;
        _rebindDevice = row.device;
        _rebindAction = row.action;
        _ignoreCaptureUntilUnscaled = Time.unscaledTime + Mathf.Max(0f, captureStartDelay);

        BlockOtherUiForAWhile();
        CacheAndClearSelectedObject();

        SetOverlay(true, waitingMessage);
        SetRowsInteractable(false);
    }

    public void CancelRebind()
    {
        if (!_isRebinding) return;

        Button buttonToRestore = GetButtonToRestoreSelection();

        _isRebinding = false;
        _rebindRowIndex = -1;
        _ignoreCaptureUntilUnscaled = -1f;

        BlockOtherUiForAWhile();
        SetOverlay(false);
        SetRowsInteractable(true);
        RestoreSelectionNextFrame(buttonToRestore);
    }

    private void TryApplyRebind(KeyCode pressed)
    {
        if (pressed == cancelKeyboardKey || pressed == cancelGamepadKey)
        {
            CancelRebind();
            return;
        }

        if (IsForbiddenBindingKey(pressed))
        {
            SetOverlay(true, $"Клавишу {PrettyKey(pressed)} нельзя назначить.\nНажми другую.\nОтмена: Esc / B");
            BlockOtherUiForAWhile();
            return;
        }

        if (preventDuplicatesPerDevice && IsUsed(_rebindDevice, pressed, _rebindAction))
        {
            SetOverlay(true, $"Кнопка {PrettyKey(pressed)} уже занята.\nНажми другую.\nОтмена: Esc / B");
            BlockOtherUiForAWhile();
            return;
        }

        SetKey(_rebindDevice, _rebindAction, pressed);
        Save();

        if (_rebindRowIndex >= 0)
            RefreshRow(_rebindRowIndex);

        OnBindsChanged?.Invoke();
        CancelRebind();
    }

    private void SetRowsInteractable(bool on)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            RebindRow r = rows[i];
            if (r.changeButton != null) r.changeButton.interactable = on;
            if (r.resetButton != null) r.resetButton.interactable = on;
        }

        if (resetAllButton != null)
            resetAllButton.interactable = on;
    }

    private void SetOverlay(bool on, string msg = "")
    {
        if (waitingOverlay != null)
            waitingOverlay.SetActive(on);

        if (waitingOverlay != null)
        {
            CanvasGroup group = waitingOverlay.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.interactable = on;
                group.blocksRaycasts = on;
            }
        }

        SetGraphicText(waitingText, on ? msg : "");
    }

    public void ResetAllToDefaults()
    {
        keyboard = Copy(defaultKeyboard);
        gamepad = Copy(defaultGamepad);

        Save();
        OnBindsChanged?.Invoke();
        RefreshAllRows();
    }

    public void ResetOneToDefault(Device device, Action action)
    {
        if (device == Device.Keyboard)
            ApplyKeyboardKey(action, GetKeyboardKey(action, defaultKeyboard));
        else
            ApplyGamepadKey(action, GetGamepadKey(action, defaultGamepad));

        Save();
        OnBindsChanged?.Invoke();
        RefreshAllRows();
    }

    private void Save()
    {
        SaveData data = new SaveData
        {
            keyboard = keyboard,
            gamepad = gamepad
        };

        string json = JsonUtility.ToJson(data, false);
        PlayerPrefs.SetString(playerPrefsKey, json);
        PlayerPrefs.Save();
    }

    private void LoadOrDefaults()
    {
        if (!PlayerPrefs.HasKey(playerPrefsKey))
        {
            keyboard = Copy(defaultKeyboard);
            gamepad = Copy(defaultGamepad);
            return;
        }

        string json = PlayerPrefs.GetString(playerPrefsKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            keyboard = Copy(defaultKeyboard);
            gamepad = Copy(defaultGamepad);
            return;
        }

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
            {
                keyboard = Copy(defaultKeyboard);
                gamepad = Copy(defaultGamepad);
                return;
            }

            keyboard = data.keyboard ?? Copy(defaultKeyboard);
            gamepad = data.gamepad ?? Copy(defaultGamepad);
        }
        catch
        {
            keyboard = Copy(defaultKeyboard);
            gamepad = Copy(defaultGamepad);
        }
    }

    private void AbsorbUIFromDuplicate(LegacyKeycodeRebind duplicate)
    {
        bool changed = false;

        if (duplicate.resetAllButton != null)
        {
            resetAllButton = duplicate.resetAllButton;
            changed = true;
        }

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
        }
    }

    private void OnChangeButtonClicked(int rowIndex)
    {
        if (ShouldIgnoreSpaceSubmitThisFrame())
            return;

        BeginRebind(rowIndex);
    }

    private void OnResetOneClicked(Device device, Action action)
    {
        if (ShouldIgnoreSpaceSubmitThisFrame())
            return;

        ResetOneToDefault(device, action);
    }

    private void OnResetAllClicked()
    {
        if (ShouldIgnoreSpaceSubmitThisFrame())
            return;

        ResetAllToDefaults();
    }

    private bool ShouldIgnoreSpaceSubmitThisFrame()
    {
        return ignoreSpaceSubmitOnRebindButtons && Input.GetKeyDown(KeyCode.Space);
    }

    private bool IsForbiddenBindingKey(KeyCode key)
    {
        if (forbiddenBindingKeys == null) return false;

        for (int i = 0; i < forbiddenBindingKeys.Count; i++)
        {
            if (forbiddenBindingKeys[i] == key)
                return true;
        }

        return false;
    }

    private void BlockOtherUiForAWhile()
    {
        _blockOtherUiUntilUnscaled =
            Mathf.Max(_blockOtherUiUntilUnscaled,
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

        return resetAllButton;
    }

    private void RestoreSelectionNextFrame(Button target)
    {
        if (!isActiveAndEnabled) return;
        if (target == null) return;
        if (EventSystem.current == null) return;

        StartCoroutine(RestoreSelectionCoroutine(target));
    }

    private IEnumerator RestoreSelectionCoroutine(Button target)
    {
        yield return null;

        if (target == null) yield break;
        if (EventSystem.current == null) yield break;
        if (!target.gameObject.activeInHierarchy || !target.interactable) yield break;

        Canvas.ForceUpdateCanvases();
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target.gameObject);
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

        Color c = image.color;
        if (c.a <= 0f)
            c.a = 0.001f;

        image.color = c;
        image.raycastTarget = true;
    }

    private KeyCode GetKeyboardKey(Action a, KeyboardBinds src = null)
    {
        KeyboardBinds k = src ?? keyboard;
        switch (a)
        {
            case Action.MoveLeft: return k.moveLeft;
            case Action.MoveRight: return k.moveRight;
            case Action.JumpHold: return k.jumpHold;
            case Action.JumpShort: return k.jumpShort;
            case Action.Pause: return k.pause;
            case Action.Back: return k.back;
            default: return KeyCode.None;
        }
    }

    private KeyCode GetGamepadKey(Action a, GamepadBinds src = null)
    {
        GamepadBinds g = src ?? gamepad;
        switch (a)
        {
            case Action.JumpHold: return g.jumpHold;
            case Action.JumpShort: return g.jumpShort;
            case Action.Pause: return g.pause;
            case Action.Back: return g.back;
            default: return KeyCode.None;
        }
    }

    private void SetKey(Device device, Action action, KeyCode key)
    {
        if (device == Device.Keyboard) ApplyKeyboardKey(action, key);
        else ApplyGamepadKey(action, key);
    }

    private void ApplyKeyboardKey(Action a, KeyCode key)
    {
        switch (a)
        {
            case Action.MoveLeft: keyboard.moveLeft = key; break;
            case Action.MoveRight: keyboard.moveRight = key; break;
            case Action.JumpHold: keyboard.jumpHold = key; break;
            case Action.JumpShort: keyboard.jumpShort = key; break;
            case Action.Pause: keyboard.pause = key; break;
            case Action.Back: keyboard.back = key; break;
        }
    }

    private void ApplyGamepadKey(Action a, KeyCode key)
    {
        switch (a)
        {
            case Action.JumpHold: gamepad.jumpHold = key; break;
            case Action.JumpShort: gamepad.jumpShort = key; break;
            case Action.Pause: gamepad.pause = key; break;
            case Action.Back: gamepad.back = key; break;
        }
    }

    private bool IsUsed(Device device, KeyCode key, Action except)
    {
        foreach (Action a in Enum.GetValues(typeof(Action)))
        {
            if (a == except) continue;
            if (GetKey(device, a) == key) return true;
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
            KeyCode k = _keyboardKeysCache[i];
            if (Input.GetKeyDown(k))
            {
                key = k;
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
            KeyCode k = _gamepadKeysCache[i];
            if (Input.GetKeyDown(k))
            {
                key = k;
                return true;
            }
        }

        key = KeyCode.None;
        return false;
    }

    private void BuildKeyCaches()
    {
        List<KeyCode> list = new List<KeyCode>(256);

        foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
        {
            if (k == KeyCode.None) continue;

            if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) continue;

            string name = k.ToString();
            if (name.StartsWith("Joystick", StringComparison.OrdinalIgnoreCase)) continue;

            list.Add(k);
        }

        _keyboardKeysCache = list.ToArray();

        int count = Mathf.Clamp(gamepadButtonsCount, 1, 30);
        _gamepadKeysCache = new KeyCode[count];

        for (int i = 0; i < count; i++)
            _gamepadKeysCache[i] = KeyCode.JoystickButton0 + i;
    }

    private static KeyboardBinds Copy(KeyboardBinds src)
    {
        return new KeyboardBinds
        {
            moveLeft = src.moveLeft,
            moveRight = src.moveRight,
            jumpHold = src.jumpHold,
            jumpShort = src.jumpShort,
            pause = src.pause,
            back = src.back
        };
    }

    private static GamepadBinds Copy(GamepadBinds src)
    {
        return new GamepadBinds
        {
            jumpHold = src.jumpHold,
            jumpShort = src.jumpShort,
            pause = src.pause,
            back = src.back
        };
    }

    private string PrettyKey(KeyCode k)
    {
        if (k == KeyCode.JoystickButton0) return "A / Cross (0)";
        if (k == KeyCode.JoystickButton1) return "B / Circle (1)";
        if (k == KeyCode.JoystickButton2) return "X / Square (2)";
        if (k == KeyCode.JoystickButton3) return "Y / Triangle (3)";
        if (k == KeyCode.JoystickButton7) return "Start / Options (7)";
        return k.ToString();
    }

    private void SetGraphicText(Graphic target, string value)
    {
        if (target == null) return;

        if (target is TMP_Text tmp)
        {
            tmp.text = value;
            return;
        }

        if (target is Text legacyText)
        {
            legacyText.text = value;
            return;
        }
    }
}