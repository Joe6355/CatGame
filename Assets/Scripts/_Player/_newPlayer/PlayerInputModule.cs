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
        public bool ApexThrowDownPressed;
        public HoldSource JumpDownSource;
    }

    public struct MobileInputSnapshot
    {
        public float MoveX;
        public bool JumpDown;
        public bool JumpHeld;
        public bool JumpReleased;
        public bool ApexThrowDownPressed;
    }

    [Header("Клавиатура и мышь (PC)")]
    [SerializeField, Tooltip("Клавиша движения влево (PC).")]
    private KeyCode leftKey = KeyCode.A;

    [SerializeField, Tooltip("Клавиша движения вправо (PC).")]
    private KeyCode rightKey = KeyCode.D;

    [SerializeField, Tooltip("Главная клавиша прыжка (PC). Прыжок срабатывает сразу по нажатию.")]
    private KeyCode jumpKey = KeyCode.Space;

    [SerializeField, Tooltip("Клавиша вниз для броска после вершины прыжка.")]
    private KeyCode apexThrowDownKey = KeyCode.S;

    [SerializeField, Tooltip("Дополнительная клавиша вниз для броска после вершины прыжка.")]
    private KeyCode alternateApexThrowDownKey = KeyCode.DownArrow;

    [Header("Геймпад (desktop)")]
    [SerializeField, Tooltip("Если ВКЛ — в desktop-режиме дополнительно читается геймпад.")]
    private bool useGamepadJump = true;

    [SerializeField, Tooltip("Кнопка геймпада для обычного прыжка. Обычно A / Cross (JoystickButton0).")]
    private KeyCode gamepadJumpKey = KeyCode.JoystickButton0;

    [SerializeField, Tooltip("Если ВКЛ — для броска после вершины дополнительно читается ось Vertical.")]
    private bool useVerticalAxisForApexThrow = true;

    [SerializeField, Tooltip("Имя вертикальной оси в Input Manager. Обычно Vertical.")]
    private string verticalAxisName = "Vertical";

    [SerializeField, Range(-1f, 0f), Tooltip("Порог нажатия вниз по оси Vertical для команды apex throw.")]
    private float apexThrowDownAxisThreshold = -0.65f;

    [Header("Legacy Rebind (KeyCode)")]
    [SerializeField, Tooltip("Если ВКЛ и LegacyKeycodeRebind существует, бинды берутся из него.")]
    private bool useLegacyKeycodeRebind = true;

    [SerializeField, Tooltip("Если ВКЛ и цифровые клавиши движения не нажаты, читается ось Horizontal из Input Manager.")]
    private bool useInputManagerAxisFallback = true;

    [Header("Мобильное управление")]
    [SerializeField, Tooltip("Если ВКЛ — используются мобильные элементы управления (джойстик + кнопка прыжка).")]
    private bool useMobileControls = false;

    [SerializeField, Tooltip("Ссылка на UI-джойстик для мобильного движения.")]
    private Joystick mobileJoystick;

    [SerializeField, Tooltip("UI-кнопка прыжка для мобилки. Удержание этой кнопки продлевает подъём прыжка.")]
    private Button mobileJumpButton;

    [Header("Безопасное включение ввода (после меню)")]
    [SerializeField, Tooltip("Если ВКЛ — после возврата из меню/паузы ввод не включится, пока игрок не отпустит зажатые кнопки.")]
    private bool waitReleaseAfterInputEnable = true;

    [SerializeField, Tooltip("Небольшая задержка после включения ввода, чтобы не словить случайный клик из UI.")]
    private float postMenuInputUnlockDelay = 0.06f;

    [SerializeField, Tooltip("Порог абсолютного значения для оси Horizontal, выше которого считаем, что ось ещё удерживается.")]
    private float inputReleaseAxisDeadZone = 0.2f;

    private bool prevUseMobileControls = false;
    private bool mobileJumpHeld = false;
    private bool prevMobileJumpHeld = false;
    private bool prevDesktopApexThrowDownHeld = false;
    private bool prevMobileApexThrowDownHeld = false;

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
            ApexThrowDownPressed = false,
            JumpDownSource = HoldSource.None
        };

        LegacyKeycodeRebind rebind = GetLegacyRebind();

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

        snapshot.MoveX = dir != 0 ? dir : axis;

        bool keyboardJumpDown = GetKeyboardJumpDown();
        bool gamepadJumpDown = GetGamepadJumpDown();

        if (keyboardJumpDown || gamepadJumpDown)
        {
            snapshot.JumpDownSource = (gamepadJumpDown && !keyboardJumpDown)
                ? HoldSource.GamepadCharge
                : HoldSource.Keyboard;
        }

        snapshot.ApexThrowDownPressed = GetDesktopApexThrowDownPressed();
        return snapshot;
    }

    public MobileInputSnapshot ReadMobileInputFrame()
    {
        bool currentHeld = mobileJumpHeld;

        MobileInputSnapshot snapshot = new MobileInputSnapshot
        {
            MoveX = mobileJoystick != null ? mobileJoystick.Horizontal : 0f,
            JumpDown = currentHeld && !prevMobileJumpHeld,
            JumpHeld = currentHeld,
            JumpReleased = !currentHeld && prevMobileJumpHeld,
            ApexThrowDownPressed = GetMobileApexThrowDownPressed()
        };

        prevMobileJumpHeld = currentHeld;
        return snapshot;
    }

    public bool IsHoldInputStillHeld(HoldSource source)
    {
        switch (source)
        {
            case HoldSource.GamepadCharge:
                return GetGamepadJumpHeld();

            case HoldSource.Mobile:
                return mobileJumpHeld;

            case HoldSource.Keyboard:
            case HoldSource.None:
            default:
                return GetKeyboardJumpHeld();
        }
    }

    public bool IsHoldInputReleased(HoldSource source)
    {
        switch (source)
        {
            case HoldSource.GamepadCharge:
                return GetGamepadJumpUp();

            case HoldSource.Mobile:
                return !mobileJumpHeld;

            case HoldSource.Keyboard:
            case HoldSource.None:
            default:
                return GetKeyboardJumpUp();
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

        prevMobileJumpHeld = false;
        prevDesktopApexThrowDownHeld = false;
        prevMobileApexThrowDownHeld = false;
    }

    private LegacyKeycodeRebind GetLegacyRebind()
    {
        if (!useLegacyKeycodeRebind)
            return null;

        return LegacyKeycodeRebind.I != null ? LegacyKeycodeRebind.I : null;
    }

    private bool GetKeyboardJumpDown()
    {
        LegacyKeycodeRebind rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetDown(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKeyDown(jumpKey);
    }

    private bool GetKeyboardJumpHeld()
    {
        LegacyKeycodeRebind rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKey(jumpKey);
    }

    private bool GetKeyboardJumpUp()
    {
        LegacyKeycodeRebind rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetUp(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKeyUp(jumpKey);
    }

    private bool GetGamepadJumpDown()
    {
        if (!useGamepadJump)
            return false;

        LegacyKeycodeRebind rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetDown(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKeyDown(gamepadJumpKey);
    }

    private bool GetGamepadJumpHeld()
    {
        if (!useGamepadJump)
            return false;

        LegacyKeycodeRebind rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetHeld(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKey(gamepadJumpKey);
    }

    private bool GetGamepadJumpUp()
    {
        if (!useGamepadJump)
            return false;

        LegacyKeycodeRebind rebind = GetLegacyRebind();
        if (rebind != null)
            return rebind.GetUp(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKeyUp(gamepadJumpKey);
    }

    private bool AreAnyGameplayInputsStillHeld()
    {
        if (useMobileControls)
        {
            if (mobileJumpHeld)
                return true;

            if (mobileJoystick != null)
            {
                if (Mathf.Abs(mobileJoystick.Horizontal) > inputReleaseAxisDeadZone)
                    return true;

                if (useVerticalAxisForApexThrow && mobileJoystick.Vertical <= apexThrowDownAxisThreshold)
                    return true;
            }

            return false;
        }

        LegacyKeycodeRebind rebind = GetLegacyRebind();

        if (rebind != null)
        {
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveLeft)) return true;
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveRight)) return true;
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold)) return true;

            if (useGamepadJump && rebind.GetHeld(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold))
                return true;
        }
        else
        {
            bool apexThrowDownHeld =
                Input.GetKey(apexThrowDownKey) ||
                (alternateApexThrowDownKey != KeyCode.None && Input.GetKey(alternateApexThrowDownKey));

            if (Input.GetKey(leftKey) || Input.GetKey(rightKey) || Input.GetKey(jumpKey) || apexThrowDownHeld)
                return true;

            if (useGamepadJump && Input.GetKey(gamepadJumpKey))
                return true;
        }

        if (useInputManagerAxisFallback && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > inputReleaseAxisDeadZone)
            return true;

        if (useVerticalAxisForApexThrow && GetVerticalAxisValue() <= apexThrowDownAxisThreshold)
            return true;

        return false;
    }

    private bool GetDesktopApexThrowDownPressed()
    {
        bool currentHeld = GetKeyboardApexThrowDownHeld() || GetAxisApexThrowDownHeld();
        bool pressed = currentHeld && !prevDesktopApexThrowDownHeld;
        prevDesktopApexThrowDownHeld = currentHeld;
        return pressed;
    }

    private bool GetMobileApexThrowDownPressed()
    {
        bool currentHeld = false;

        if (mobileJoystick != null)
            currentHeld = useVerticalAxisForApexThrow && mobileJoystick.Vertical <= apexThrowDownAxisThreshold;

        bool pressed = currentHeld && !prevMobileApexThrowDownHeld;
        prevMobileApexThrowDownHeld = currentHeld;
        return pressed;
    }

    private bool GetKeyboardApexThrowDownHeld()
    {
        return Input.GetKey(apexThrowDownKey) ||
               (alternateApexThrowDownKey != KeyCode.None && Input.GetKey(alternateApexThrowDownKey));
    }

    private bool GetAxisApexThrowDownHeld()
    {
        if (!useVerticalAxisForApexThrow)
            return false;

        return GetVerticalAxisValue() <= apexThrowDownAxisThreshold;
    }

    private float GetVerticalAxisValue()
    {
        if (!useVerticalAxisForApexThrow || string.IsNullOrEmpty(verticalAxisName))
            return 0f;

        return Input.GetAxisRaw(verticalAxisName);
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