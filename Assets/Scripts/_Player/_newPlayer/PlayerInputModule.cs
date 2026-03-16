using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerInputModule : MonoBehaviour
{
    public enum HoldSource
    {
        None,
        Keyboard,
        GamepadCharge,
        Mobile
    }

    public struct DesktopInputSnapshot
    {
        public float MoveX;
        public bool IsRebinding;
        public bool ShortJumpDown;
        public HoldSource ChargeDownSource;
    }

    public struct MobileInputSnapshot
    {
        public float MoveX;
        public bool JumpHeld;
    }

    [Header("Клавиатура и мышь (PC)")]
    [SerializeField, Tooltip("Клавиша движения влево (PC).\nРекоменд: A или LeftArrow.")]
    private KeyCode leftKey = KeyCode.A;

    [SerializeField, Tooltip("Клавиша движения вправо (PC).\nРекоменд: D или RightArrow.")]
    private KeyCode rightKey = KeyCode.D;

    [SerializeField, Tooltip("Клавиша сильного/зарядного прыжка (PC). По умолчанию Space.")]
    private KeyCode jumpKey = KeyCode.Space;

    [SerializeField, Tooltip("Клавиша отдельного слабого прыжка (PC). По умолчанию LeftShift.")]
    private KeyCode shortJumpKey = KeyCode.LeftShift;

    [SerializeField, Tooltip("Дополнительная клавиша отдельного слабого прыжка (PC). Можно оставить None.")]
    private KeyCode alternateShortJumpKey = KeyCode.RightShift;

    [Header("Геймпад (desktop)")]
    [SerializeField, Tooltip("Если ВКЛ — в desktop-режиме (useMobileControls = false) будут работать и кнопки геймпада.")]
    private bool useGamepadJump = true;

    [SerializeField, Tooltip("Кнопка геймпада для сильного/зарядного прыжка. Обычно A / Cross (JoystickButton0).")]
    private KeyCode gamepadChargeJumpKey = KeyCode.JoystickButton0;

    [SerializeField, Tooltip("Отдельная кнопка геймпада для слабого прыжка. Обычно B / Circle (JoystickButton1).")]
    private KeyCode gamepadShortJumpKey = KeyCode.JoystickButton1;

    [Header("Legacy Rebind (KeyCode)")]
    [SerializeField, Tooltip("If ON and LegacyKeycodeRebind exists, this module reads keys from it (UI rebind). If OFF or missing, uses local KeyCode fields below.")]
    private bool useLegacyKeycodeRebind = true;

    [SerializeField, Tooltip("If ON, when no keyboard move key is pressed we use InputManager axis 'Horizontal' as fallback (usually gamepad stick). NOTE: if your InputManager Horizontal still has A/D, those may move unless we block the common keys below.")]
    private bool useInputManagerAxisFallback = true;

    [Header("Мобильное управление")]
    [SerializeField, Tooltip("Если ВКЛ — используются мобильные элементы управления (джойстик + кнопка), PC-ввод при этом игнорируется.\nРекоменд: true для телефона, false для ПК.")]
    private bool useMobileControls = false;

    [SerializeField, Tooltip("Ссылка на Joystick (обычно UI-джойстик на Canvas).\nРекоменд: назначить, если useMobileControls = true.")]
    private Joystick mobileJoystick;

    [SerializeField, Tooltip("UI-кнопка прыжка для мобилки (удержание/отпускание).\nРекоменд: назначить, если useMobileControls = true.")]
    private Button mobileJumpButton;

    [Header("Безопасное включение ввода (после меню)")]
    [SerializeField, Tooltip("Если ВКЛ — после возврата из меню/паузы ввод не включится, пока игрок не отпустит уже зажатые кнопки управления/прыжка.\nЭто защищает от 'наследования' ввода после UI.")]
    private bool waitReleaseAfterInputEnable = true;

    [SerializeField, Tooltip("Задержка в секундах после разрешения ввода, прежде чем геймплей снова начнёт читать кнопки.\nНужно, чтобы избежать случайного нажатия из UI.\nРекоменд: 0.03–0.12 сек (часто 0.05–0.08).")]
    private float postMenuInputUnlockDelay = 0.06f;

    [SerializeField, Tooltip("Порог абсолютного значения для оси Horizontal, выше которого считаем, что ось/стик ещё удерживается.\nРекоменд: 0.15–0.35 (часто 0.2).")]
    private float inputReleaseAxisDeadZone = 0.2f;

    private bool prevUseMobileControls = false;
    private bool mobileJumpHeld = false;

    private bool gameplayInputEnabled = true;
    private bool waitForGameplayInputRelease = false;
    private float gameplayInputUnlockAtUnscaled = -1f;

    private Button hookedMobileJumpButton = null;
    private PointerHoldHandler mobileJumpHoldHandler = null;

    public bool UseMobileControls => useMobileControls;
    public bool UseGamepadJump => useGamepadJump;
    public bool MobileJumpHeld => mobileJumpHeld;

    private void Awake()
    {
        EnsureMobileButtonHooked();
        prevUseMobileControls = useMobileControls;
        ApplyMobileUIVisibility();
    }

    private void OnDestroy()
    {
        UnhookMobileButton();
    }

    private void OnDisable()
    {
        ResetModuleInputState(true);
        ApplyMobileUIVisibility();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        EnsureMobileButtonHooked();
        ApplyMobileUIVisibility();
    }

    public void RefreshPerFrame()
    {
        EnsureMobileButtonHooked();

        if (prevUseMobileControls != useMobileControls)
        {
            prevUseMobileControls = useMobileControls;
            ApplyMobileUIVisibility();
        }
    }

    public DesktopInputSnapshot ReadDesktopInputFrame()
    {
        DesktopInputSnapshot snapshot = new DesktopInputSnapshot
        {
            MoveX = 0f,
            IsRebinding = false,
            ShortJumpDown = false,
            ChargeDownSource = HoldSource.None
        };

        var rebind = GetLegacyRebind();

        if (rebind != null && rebind.IsRebinding)
        {
            snapshot.IsRebinding = true;
            return snapshot;
        }

        int dir = 0;

        if (rebind != null)
        {
            dir = rebind.GetKeyboardMoveDir();
        }
        else
        {
            if (Input.GetKey(leftKey)) dir -= 1;
            if (Input.GetKey(rightKey)) dir += 1;
        }

        float axis = 0f;
        if (dir == 0 && useInputManagerAxisFallback)
        {
            if (!(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
                  Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)))
            {
                axis = Input.GetAxisRaw("Horizontal");
            }
        }

        snapshot.MoveX = (dir != 0) ? dir : axis;

        bool keyboardChargeDown = GetKeyboardChargeJumpDown();
        bool gamepadChargeDown = GetGamepadChargeJumpDown();

        if (keyboardChargeDown || gamepadChargeDown)
        {
            snapshot.ChargeDownSource =
                (gamepadChargeDown && !keyboardChargeDown)
                ? HoldSource.GamepadCharge
                : HoldSource.Keyboard;
        }

        // Если в ребинде кто-то повесил short и charge на одну и ту же кнопку,
        // приоритет отдаём зарядному прыжку, чтобы Space не вызывал слабый прыжок.
        snapshot.ShortJumpDown = !keyboardChargeDown && !gamepadChargeDown && GetDesktopShortJumpDown();

        return snapshot;
    }

    public MobileInputSnapshot ReadMobileInputFrame()
    {
        MobileInputSnapshot snapshot = new MobileInputSnapshot
        {
            MoveX = mobileJoystick != null ? mobileJoystick.Horizontal : 0f,
            JumpHeld = mobileJumpHeld
        };

        return snapshot;
    }

    public bool IsHoldInputStillHeld(HoldSource source)
    {
        switch (source)
        {
            case HoldSource.GamepadCharge:
                return GetGamepadChargeJumpHeld();

            case HoldSource.Mobile:
                return mobileJumpHeld;

            case HoldSource.Keyboard:
            case HoldSource.None:
            default:
                return GetKeyboardChargeJumpHeld();
        }
    }

    public bool IsHoldInputReleased(HoldSource source)
    {
        switch (source)
        {
            case HoldSource.GamepadCharge:
                return GetGamepadChargeJumpUp();

            case HoldSource.Mobile:
                return !mobileJumpHeld;

            case HoldSource.Keyboard:
            case HoldSource.None:
            default:
                return GetKeyboardChargeJumpUp();
        }
    }

    public void SetInputEnabled(bool enabled)
    {
        gameplayInputEnabled = enabled;

        if (!enabled)
        {
            waitForGameplayInputRelease = false;
            gameplayInputUnlockAtUnscaled = -1f;
            return;
        }

        if (waitReleaseAfterInputEnable)
        {
            waitForGameplayInputRelease = true;
            gameplayInputUnlockAtUnscaled = Time.unscaledTime + Mathf.Max(0f, postMenuInputUnlockDelay);
        }
        else
        {
            waitForGameplayInputRelease = false;
            gameplayInputUnlockAtUnscaled = Time.unscaledTime + Mathf.Max(0f, postMenuInputUnlockDelay);
        }
    }

    public bool IsGameplayInputAllowedThisFrame()
    {
        if (!gameplayInputEnabled)
            return false;

        if (waitForGameplayInputRelease)
        {
            if (AreAnyGameplayInputsStillHeld())
                return false;

            waitForGameplayInputRelease = false;
            gameplayInputUnlockAtUnscaled = Time.unscaledTime + Mathf.Max(0f, postMenuInputUnlockDelay);
            return false;
        }

        if (Time.unscaledTime < gameplayInputUnlockAtUnscaled)
            return false;

        return true;
    }

    public void ResetModuleInputState(bool clearMobileHold)
    {
        if (clearMobileHold)
            mobileJumpHeld = false;
    }

    private LegacyKeycodeRebind GetLegacyRebind()
    {
        if (!useLegacyKeycodeRebind)
            return null;

        return LegacyKeycodeRebind.I != null ? LegacyKeycodeRebind.I : null;
    }

    private bool GetKeyboardChargeJumpDown()
    {
        var rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetDown(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKeyDown(jumpKey);
    }

    private bool GetKeyboardChargeJumpHeld()
    {
        var rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKey(jumpKey);
    }

    private bool GetKeyboardChargeJumpUp()
    {
        var rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetUp(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKeyUp(jumpKey);
    }

    private bool GetGamepadChargeJumpDown()
    {
        if (!useGamepadJump)
            return false;

        var rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetDown(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKeyDown(gamepadChargeJumpKey);
    }

    private bool GetGamepadChargeJumpHeld()
    {
        if (!useGamepadJump)
            return false;

        var rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetHeld(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKey(gamepadChargeJumpKey);
    }

    private bool GetGamepadChargeJumpUp()
    {
        if (!useGamepadJump)
            return false;

        var rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetUp(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKeyUp(gamepadChargeJumpKey);
    }

    private bool GetDesktopShortJumpDown()
    {
        var rebind = GetLegacyRebind();

        if (rebind != null)
        {
            bool kb = rebind.GetDown(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpShort);
            bool gp = useGamepadJump && rebind.GetDown(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpShort);
            return kb || gp;
        }

        bool keyboardShort = Input.GetKeyDown(shortJumpKey) ||
                             (alternateShortJumpKey != KeyCode.None && Input.GetKeyDown(alternateShortJumpKey));

        bool gamepadShort = useGamepadJump && Input.GetKeyDown(gamepadShortJumpKey);
        return keyboardShort || gamepadShort;
    }

    private bool AreAnyGameplayInputsStillHeld()
    {
        if (useMobileControls)
        {
            if (mobileJumpHeld)
                return true;

            if (mobileJoystick != null && Mathf.Abs(mobileJoystick.Horizontal) > inputReleaseAxisDeadZone)
                return true;

            return false;
        }

        var rebind = GetLegacyRebind();

        if (rebind != null)
        {
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveLeft)) return true;
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveRight)) return true;
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold)) return true;
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpShort)) return true;

            if (useGamepadJump)
            {
                if (rebind.GetHeld(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold)) return true;
                if (rebind.GetHeld(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpShort)) return true;
            }
        }
        else
        {
            bool keyboardShortHeld = Input.GetKey(shortJumpKey) ||
                                     (alternateShortJumpKey != KeyCode.None && Input.GetKey(alternateShortJumpKey));

            if (Input.GetKey(leftKey) || Input.GetKey(rightKey) || Input.GetKey(jumpKey) || keyboardShortHeld)
                return true;

            if (useGamepadJump && (Input.GetKey(gamepadChargeJumpKey) || Input.GetKey(gamepadShortJumpKey)))
                return true;
        }

        if (useInputManagerAxisFallback && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > inputReleaseAxisDeadZone)
            return true;

        return false;
    }

    private void EnsureMobileButtonHooked()
    {
        if (hookedMobileJumpButton == mobileJumpButton && mobileJumpHoldHandler != null)
            return;

        UnhookMobileButton();

        hookedMobileJumpButton = mobileJumpButton;

        if (hookedMobileJumpButton == null)
            return;

        mobileJumpHoldHandler = hookedMobileJumpButton.GetComponent<PointerHoldHandler>();
        if (mobileJumpHoldHandler == null)
            mobileJumpHoldHandler = hookedMobileJumpButton.gameObject.AddComponent<PointerHoldHandler>();

        mobileJumpHoldHandler.OnDown += OnMobileJumpDown;
        mobileJumpHoldHandler.OnUp += OnMobileJumpUp;
    }

    private void UnhookMobileButton()
    {
        if (mobileJumpHoldHandler != null)
        {
            mobileJumpHoldHandler.OnDown -= OnMobileJumpDown;
            mobileJumpHoldHandler.OnUp -= OnMobileJumpUp;
        }

        mobileJumpHoldHandler = null;
        hookedMobileJumpButton = null;
    }

    private void ApplyMobileUIVisibility()
    {
        if (mobileJoystick != null)
            mobileJoystick.gameObject.SetActive(useMobileControls);

        if (mobileJumpButton != null)
            mobileJumpButton.gameObject.SetActive(useMobileControls);
    }

    private void OnMobileJumpDown()
    {
        mobileJumpHeld = true;
    }

    private void OnMobileJumpUp()
    {
        mobileJumpHeld = false;
    }
}