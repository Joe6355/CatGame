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
        public float ClimbY;
        public bool IsRebinding;
        public bool ApexThrowDownPressed;
        public bool LedgeUpPressed;
        public bool FenceTogglePressed;
        public HoldSource JumpDownSource;
    }

    public struct MobileInputSnapshot
    {
        public float MoveX;
        public float ClimbY;
        public bool JumpDown;
        public bool JumpHeld;
        public bool JumpReleased;
        public bool ApexThrowDownPressed;
        public bool LedgeUpPressed;
        public bool FenceTogglePressed;
    }

    [Header("Клавиатура и мышь (PC)")]
    [SerializeField, Tooltip("Клавиша движения влево (PC).")]
    private KeyCode leftKey = KeyCode.A;

    [SerializeField, Tooltip("Клавиша движения вправо (PC).")]
    private KeyCode rightKey = KeyCode.D;

    [SerializeField, Tooltip("Главная клавиша прыжка (PC). Прыжок срабатывает сразу по нажатию.")]
    private KeyCode jumpKey = KeyCode.Space;

    [SerializeField, Tooltip("Клавиша вниз для броска после вершины прыжка и спрыга с ledge.")]
    private KeyCode apexThrowDownKey = KeyCode.S;

    [SerializeField, Tooltip("Дополнительная клавиша вниз для броска после вершины прыжка и спрыга с ledge.")]
    private KeyCode alternateApexThrowDownKey = KeyCode.DownArrow;

    [SerializeField, Tooltip("Клавиша вверх для подтягивания на платформу с ledge.")]
    private KeyCode ledgeClimbUpKey = KeyCode.W;

    [SerializeField, Tooltip("Дополнительная клавиша вверх для подтягивания на платформу с ledge.")]
    private KeyCode alternateLedgeClimbUpKey = KeyCode.UpArrow;

    [SerializeField, Tooltip("Клавиша взаимодействия с лестницей/забором.")]
    private KeyCode fenceToggleKey = KeyCode.F;

    [SerializeField, Tooltip("Клавиша вверх для движения по лестнице/забору.")]
    private KeyCode climbUpKey = KeyCode.W;

    [SerializeField, Tooltip("Клавиша вниз для движения по лестнице/забору.")]
    private KeyCode climbDownKey = KeyCode.S;

    [SerializeField, Tooltip("Дополнительная клавиша вверх для движения по лестнице/забору.")]
    private KeyCode alternateClimbUpKey = KeyCode.UpArrow;

    [SerializeField, Tooltip("Дополнительная клавиша вниз для движения по лестнице/забору.")]
    private KeyCode alternateClimbDownKey = KeyCode.DownArrow;

    [Header("Геймпад (desktop)")]
    [SerializeField, Tooltip("Если ВКЛ — в desktop-режиме дополнительно читается геймпад.")]
    private bool useGamepadJump = true;

    [SerializeField, Tooltip("Кнопка геймпада для обычного прыжка. Обычно A / Cross (JoystickButton0).")]
    private KeyCode gamepadJumpKey = KeyCode.JoystickButton0;

    [SerializeField, Tooltip("Кнопка геймпада для входа/выхода с лестницы/забора. По умолчанию X/Square (JoystickButton2).")]
    private KeyCode gamepadFenceToggleKey = KeyCode.JoystickButton2;

    [SerializeField, Tooltip("Если ВКЛ — для броска вниз и ledge-команд дополнительно читается ось Vertical.")]
    private bool useVerticalAxisForApexThrow = true;

    [SerializeField, Tooltip("Имя вертикальной оси в Input Manager. Обычно Vertical.")]
    private string verticalAxisName = "Vertical";

    [SerializeField, Range(-1f, 0f), Tooltip("Порог нажатия вниз по оси Vertical для команды apex throw / спрыга с ledge.")]
    private float apexThrowDownAxisThreshold = -0.65f;

    [SerializeField, Tooltip("Если ВКЛ — ledge climb также может срабатывать от вертикальной оси вверх.")]
    private bool useVerticalAxisForLedgeClimb = true;

    [SerializeField, Range(0f, 1f), Tooltip("Порог нажатия вверх по оси Vertical для подтягивания с ledge.")]
    private float ledgeClimbUpAxisThreshold = 0.65f;

    [SerializeField, Tooltip("Если ВКЛ — вертикальная ось также используется для движения по лестнице/забору.")]
    private bool useVerticalAxisForFenceClimb = true;

    [SerializeField, Range(0f, 1f), Tooltip("Порог вертикальной оси для движения по лестнице/забору.")]
    private float fenceClimbAxisThreshold = 0.35f;

    [Header("Legacy Rebind (KeyCode)")]
    [SerializeField, Tooltip("Если ВКЛ и LegacyKeycodeRebind существует, бинды прыжка/движения берутся из него.")]
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
    private bool prevDesktopLedgeUpHeld = false;
    private bool prevMobileLedgeUpHeld = false;

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
            ClimbY = 0f,
            IsRebinding = false,
            ApexThrowDownPressed = false,
            LedgeUpPressed = false,
            FenceTogglePressed = false,
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
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveLeft))
                dir -= 1;

            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveRight))
                dir += 1;
        }
        else
        {
            if (Input.GetKey(leftKey))
                dir -= 1;

            if (Input.GetKey(rightKey))
                dir += 1;
        }

        if (dir != 0)
        {
            snapshot.MoveX = Mathf.Clamp(dir, -1f, 1f);
        }
        else if (useInputManagerAxisFallback)
        {
            snapshot.MoveX = Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f);
        }

        bool keyboardJumpDown = GetKeyboardJumpDown();
        bool gamepadJumpDown = GetGamepadJumpDown();

        if (keyboardJumpDown || gamepadJumpDown)
        {
            snapshot.JumpDownSource = (gamepadJumpDown && !keyboardJumpDown)
                ? HoldSource.GamepadCharge
                : HoldSource.Keyboard;
        }

        snapshot.ClimbY = GetDesktopClimbVerticalValue();
        snapshot.ApexThrowDownPressed = GetDesktopApexThrowDownPressed();
        snapshot.LedgeUpPressed = GetDesktopLedgeUpPressed();
        snapshot.FenceTogglePressed = GetDesktopFenceTogglePressed();
        return snapshot;
    }

    public MobileInputSnapshot ReadMobileInputFrame()
    {
        bool currentHeld = mobileJumpHeld;

        MobileInputSnapshot snapshot = new MobileInputSnapshot
        {
            MoveX = mobileJoystick != null ? mobileJoystick.Horizontal : 0f,
            ClimbY = GetMobileClimbVerticalValue(),
            JumpDown = currentHeld && !prevMobileJumpHeld,
            JumpHeld = currentHeld,
            JumpReleased = !currentHeld && prevMobileJumpHeld,
            ApexThrowDownPressed = GetMobileApexThrowDownPressed(),
            LedgeUpPressed = GetMobileLedgeUpPressed(),
            FenceTogglePressed = false
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
        prevDesktopLedgeUpHeld = false;
        prevMobileLedgeUpHeld = false;
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

                if (Mathf.Abs(mobileJoystick.Vertical) > inputReleaseAxisDeadZone)
                    return true;

                if (useVerticalAxisForApexThrow && mobileJoystick.Vertical <= apexThrowDownAxisThreshold)
                    return true;

                if (useVerticalAxisForLedgeClimb && mobileJoystick.Vertical >= ledgeClimbUpAxisThreshold)
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
            bool apexThrowDownHeld = GetKeyboardApexThrowDownHeld();
            bool ledgeUpHeld = GetKeyboardLedgeUpHeld();

            if (Input.GetKey(leftKey) ||
                Input.GetKey(rightKey) ||
                Input.GetKey(jumpKey) ||
                Input.GetKey(fenceToggleKey) ||
                GetGamepadFenceToggleHeld() ||
                GetKeyboardClimbUpHeld() ||
                GetKeyboardClimbDownHeld() ||
                apexThrowDownHeld ||
                ledgeUpHeld)
            {
                return true;
            }

            if (useGamepadJump && Input.GetKey(gamepadJumpKey))
                return true;
        }

        if (useInputManagerAxisFallback && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > inputReleaseAxisDeadZone)
            return true;

        if (Mathf.Abs(GetVerticalAxisValue()) > inputReleaseAxisDeadZone)
            return true;

        if (useVerticalAxisForApexThrow && GetVerticalAxisValue() <= apexThrowDownAxisThreshold)
            return true;

        if (useVerticalAxisForLedgeClimb && GetVerticalAxisValue() >= ledgeClimbUpAxisThreshold)
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

    private bool GetDesktopLedgeUpPressed()
    {
        bool currentHeld = GetKeyboardLedgeUpHeld() || GetAxisLedgeUpHeld();
        bool pressed = currentHeld && !prevDesktopLedgeUpHeld;
        prevDesktopLedgeUpHeld = currentHeld;
        return pressed;
    }

    private bool GetMobileLedgeUpPressed()
    {
        bool currentHeld = false;

        if (mobileJoystick != null)
            currentHeld = useVerticalAxisForLedgeClimb && mobileJoystick.Vertical >= ledgeClimbUpAxisThreshold;

        bool pressed = currentHeld && !prevMobileLedgeUpHeld;
        prevMobileLedgeUpHeld = currentHeld;
        return pressed;
    }

    private bool GetDesktopFenceTogglePressed()
    {
        return Input.GetKeyDown(fenceToggleKey) || GetGamepadFenceTogglePressed();
    }

    private bool GetGamepadFenceTogglePressed()
    {
        return gamepadFenceToggleKey != KeyCode.None && Input.GetKeyDown(gamepadFenceToggleKey);
    }

    private bool GetGamepadFenceToggleHeld()
    {
        return gamepadFenceToggleKey != KeyCode.None && Input.GetKey(gamepadFenceToggleKey);
    }

    private float GetDesktopClimbVerticalValue()
    {
        float value = 0f;

        if (GetKeyboardClimbUpHeld())
            value += 1f;

        if (GetKeyboardClimbDownHeld())
            value -= 1f;

        if (Mathf.Abs(value) > 0.001f)
            return Mathf.Clamp(value, -1f, 1f);

        if (!useVerticalAxisForFenceClimb)
            return 0f;

        float axis = GetVerticalAxisValue();
        if (Mathf.Abs(axis) < fenceClimbAxisThreshold)
            return 0f;

        return Mathf.Clamp(axis, -1f, 1f);
    }

    private float GetMobileClimbVerticalValue()
    {
        if (mobileJoystick == null || !useVerticalAxisForFenceClimb)
            return 0f;

        float axis = mobileJoystick.Vertical;
        if (Mathf.Abs(axis) < fenceClimbAxisThreshold)
            return 0f;

        return Mathf.Clamp(axis, -1f, 1f);
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

    private bool GetKeyboardLedgeUpHeld()
    {
        return Input.GetKey(ledgeClimbUpKey) ||
               (alternateLedgeClimbUpKey != KeyCode.None && Input.GetKey(alternateLedgeClimbUpKey));
    }

    private bool GetAxisLedgeUpHeld()
    {
        if (!useVerticalAxisForLedgeClimb)
            return false;

        return GetVerticalAxisValue() >= ledgeClimbUpAxisThreshold;
    }

    private bool GetKeyboardClimbUpHeld()
    {
        return Input.GetKey(climbUpKey) ||
               (alternateClimbUpKey != KeyCode.None && Input.GetKey(alternateClimbUpKey));
    }

    private bool GetKeyboardClimbDownHeld()
    {
        return Input.GetKey(climbDownKey) ||
               (alternateClimbDownKey != KeyCode.None && Input.GetKey(alternateClimbDownKey));
    }

    private float GetVerticalAxisValue()
    {
        if (string.IsNullOrEmpty(verticalAxisName))
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