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

    [Header("Fallback Keyboard - работает только если Use Legacy Keycode Rebind выключен")]
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode downActionKey = KeyCode.S;
    [SerializeField] private KeyCode alternateDownActionKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode upActionKey = KeyCode.W;
    [SerializeField] private KeyCode alternateUpActionKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("Fallback Gamepad - работает только если Use Legacy Keycode Rebind выключен")]
    [SerializeField] private bool useGamepadJump = true;
    [SerializeField] private KeyCode gamepadJumpKey = KeyCode.JoystickButton0;
    [SerializeField] private KeyCode gamepadInteractKey = KeyCode.JoystickButton2;

    [Header("Fallback Axes - работает только если Use Legacy Keycode Rebind выключен")]
    [SerializeField] private string horizontalAxisName = "Horizontal";
    [SerializeField] private string verticalAxisName = "Vertical";
    [SerializeField] private bool useInputManagerAxisFallback = true;
    [SerializeField] private bool useVerticalAxisForApexThrowFallback = true;
    [SerializeField, Range(-1f, 0f)] private float apexThrowDownAxisThreshold = -0.65f;
    [SerializeField] private bool useVerticalAxisForLedgeClimbFallback = true;
    [SerializeField, Range(0f, 1f)] private float ledgeClimbUpAxisThreshold = 0.65f;
    [SerializeField] private bool useVerticalAxisForFenceClimbFallback = true;
    [SerializeField, Range(0f, 1f)] private float fenceClimbAxisThreshold = 0.35f;

    [Header("Legacy Rebind")]
    [SerializeField] private bool useLegacyKeycodeRebind = true;

    [Header("Mobile Controls")]
    [SerializeField] private bool useMobileControls = false;
    [SerializeField] private Joystick mobileJoystick;
    [SerializeField] private Button mobileJumpButton;

    [Header("Input Safety After Menu")]
    [SerializeField] private bool waitReleaseAfterInputEnable = true;
    [SerializeField] private float postMenuInputUnlockDelay = 0.06f;
    [SerializeField] private float inputReleaseAxisDeadZone = 0.2f;

    private bool prevUseMobileControls = false;
    private bool mobileJumpHeld = false;
    private bool prevMobileJumpHeld = false;

    private bool prevDesktopDownActionHeld = false;
    private bool prevMobileDownActionHeld = false;
    private bool prevDesktopUpActionHeld = false;
    private bool prevMobileUpActionHeld = false;

    private bool gameplayInputEnabled = true;
    private bool waitForGameplayInputRelease = false;
    private float gameplayInputUnlockAtUnscaled = -1f;

    private Button hookedMobileJumpButton = null;
    private PointerHoldHandler mobileJumpHoldHandler = null;

    public bool UseMobileControls => useMobileControls;
    public bool UseGamepadJump => useGamepadJump;
    public bool MobileJumpHeld => mobileJumpHeld;

    private bool RebindActive
    {
        get
        {
            if (!useLegacyKeycodeRebind)
                return false;

            return LegacyKeycodeRebind.RuntimeReady;
        }
    }

    private void Awake()
    {
        if (useLegacyKeycodeRebind)
        {
            bool _ = LegacyKeycodeRebind.RuntimeReady;
        }

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

        if (useLegacyKeycodeRebind && LegacyKeycodeRebind.IsAnyRebinding)
        {
            snapshot.IsRebinding = true;
            return snapshot;
        }

        bool useRebind = RebindActive;

        snapshot.MoveX = useRebind ? GetRebindMoveX() : GetFallbackMoveX();

        bool keyboardJumpDown = useRebind ? GetRebindKeyboardJumpDown() : Input.GetKeyDown(jumpKey);
        bool gamepadJumpDown = useRebind ? GetRebindGamepadJumpDown() : GetFallbackGamepadJumpDown();

        if (keyboardJumpDown || gamepadJumpDown)
        {
            snapshot.JumpDownSource = (gamepadJumpDown && !keyboardJumpDown)
                ? HoldSource.GamepadCharge
                : HoldSource.Keyboard;
        }

        snapshot.ClimbY = useRebind ? GetRebindClimbVerticalValue() : GetFallbackClimbVerticalValue();
        snapshot.ApexThrowDownPressed = GetDesktopDownActionPressed(useRebind);
        snapshot.LedgeUpPressed = GetDesktopUpActionPressed(useRebind);
        snapshot.FenceTogglePressed = useRebind ? GetRebindInteractPressed() : GetFallbackInteractPressed();

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
            ApexThrowDownPressed = GetMobileDownActionPressed(),
            LedgeUpPressed = GetMobileUpActionPressed(),
            FenceTogglePressed = false
        };

        prevMobileJumpHeld = currentHeld;
        return snapshot;
    }

    public bool IsHoldInputStillHeld(HoldSource source)
    {
        bool useRebind = RebindActive;

        switch (source)
        {
            case HoldSource.GamepadCharge:
                return useRebind ? GetRebindGamepadJumpHeld() : GetFallbackGamepadJumpHeld();

            case HoldSource.Mobile:
                return mobileJumpHeld;

            case HoldSource.Keyboard:
            case HoldSource.None:
            default:
                return useRebind ? GetRebindKeyboardJumpHeld() : Input.GetKey(jumpKey);
        }
    }

    public bool IsHoldInputReleased(HoldSource source)
    {
        bool useRebind = RebindActive;

        switch (source)
        {
            case HoldSource.GamepadCharge:
                return useRebind ? GetRebindGamepadJumpUp() : GetFallbackGamepadJumpUp();

            case HoldSource.Mobile:
                return !mobileJumpHeld;

            case HoldSource.Keyboard:
            case HoldSource.None:
            default:
                return useRebind ? GetRebindKeyboardJumpUp() : Input.GetKeyUp(jumpKey);
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
        prevDesktopDownActionHeld = false;
        prevMobileDownActionHeld = false;
        prevDesktopUpActionHeld = false;
        prevMobileUpActionHeld = false;
    }

    private float GetRebindMoveX()
    {
        int dir = 0;

        if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveLeft) ||
            LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.MoveLeft))
        {
            dir -= 1;
        }

        if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveRight) ||
            LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.MoveRight))
        {
            dir += 1;
        }

        return Mathf.Clamp(dir, -1f, 1f);
    }

    private float GetFallbackMoveX()
    {
        int dir = 0;

        if (Input.GetKey(leftKey))
            dir -= 1;

        if (Input.GetKey(rightKey))
            dir += 1;

        if (dir != 0)
            return Mathf.Clamp(dir, -1f, 1f);

        if (useInputManagerAxisFallback)
            return Mathf.Clamp(SafeGetAxisRaw(horizontalAxisName), -1f, 1f);

        return 0f;
    }

    private bool GetRebindKeyboardJumpDown()
    {
        return LegacyKeycodeRebind.GetDownStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.Jump);
    }

    private bool GetRebindKeyboardJumpHeld()
    {
        return LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.Jump);
    }

    private bool GetRebindKeyboardJumpUp()
    {
        return LegacyKeycodeRebind.GetUpStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.Jump);
    }

    private bool GetRebindGamepadJumpDown()
    {
        return LegacyKeycodeRebind.GetDownStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.Jump);
    }

    private bool GetRebindGamepadJumpHeld()
    {
        return LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.Jump);
    }

    private bool GetRebindGamepadJumpUp()
    {
        return LegacyKeycodeRebind.GetUpStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.Jump);
    }

    private bool GetFallbackGamepadJumpDown()
    {
        if (!useGamepadJump)
            return false;

        return gamepadJumpKey != KeyCode.None && Input.GetKeyDown(gamepadJumpKey);
    }

    private bool GetFallbackGamepadJumpHeld()
    {
        if (!useGamepadJump)
            return false;

        return gamepadJumpKey != KeyCode.None && Input.GetKey(gamepadJumpKey);
    }

    private bool GetFallbackGamepadJumpUp()
    {
        if (!useGamepadJump)
            return false;

        return gamepadJumpKey != KeyCode.None && Input.GetKeyUp(gamepadJumpKey);
    }

    private bool GetDesktopDownActionPressed(bool useRebind)
    {
        bool currentHeld = useRebind ? GetRebindDownActionHeld() : GetFallbackDownActionHeld();
        bool pressed = currentHeld && !prevDesktopDownActionHeld;

        prevDesktopDownActionHeld = currentHeld;
        return pressed;
    }

    private bool GetRebindDownActionHeld()
    {
        return LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.DownAction) ||
               LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.DownAction);
    }

    private bool GetFallbackDownActionHeld()
    {
        bool keyboard =
            Input.GetKey(downActionKey) ||
            (alternateDownActionKey != KeyCode.None && Input.GetKey(alternateDownActionKey));

        bool axis = useVerticalAxisForApexThrowFallback &&
                    SafeGetAxisRaw(verticalAxisName) <= apexThrowDownAxisThreshold;

        return keyboard || axis;
    }

    private bool GetMobileDownActionPressed()
    {
        bool currentHeld = false;

        if (mobileJoystick != null)
            currentHeld = mobileJoystick.Vertical <= apexThrowDownAxisThreshold;

        bool pressed = currentHeld && !prevMobileDownActionHeld;

        prevMobileDownActionHeld = currentHeld;
        return pressed;
    }

    private bool GetDesktopUpActionPressed(bool useRebind)
    {
        bool currentHeld = useRebind ? GetRebindUpActionHeld() : GetFallbackUpActionHeld();
        bool pressed = currentHeld && !prevDesktopUpActionHeld;

        prevDesktopUpActionHeld = currentHeld;
        return pressed;
    }

    private bool GetRebindUpActionHeld()
    {
        return LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.UpAction) ||
               LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.UpAction);
    }

    private bool GetFallbackUpActionHeld()
    {
        bool keyboard =
            Input.GetKey(upActionKey) ||
            (alternateUpActionKey != KeyCode.None && Input.GetKey(alternateUpActionKey));

        bool axis = useVerticalAxisForLedgeClimbFallback &&
                    SafeGetAxisRaw(verticalAxisName) >= ledgeClimbUpAxisThreshold;

        return keyboard || axis;
    }

    private bool GetMobileUpActionPressed()
    {
        bool currentHeld = false;

        if (mobileJoystick != null)
            currentHeld = mobileJoystick.Vertical >= ledgeClimbUpAxisThreshold;

        bool pressed = currentHeld && !prevMobileUpActionHeld;

        prevMobileUpActionHeld = currentHeld;
        return pressed;
    }

    private bool GetRebindInteractPressed()
    {
        return LegacyKeycodeRebind.GetDownStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.Interact) ||
               LegacyKeycodeRebind.GetDownStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.Interact);
    }

    private bool GetFallbackInteractPressed()
    {
        bool keyboard = interactKey != KeyCode.None && Input.GetKeyDown(interactKey);
        bool gamepad = gamepadInteractKey != KeyCode.None && Input.GetKeyDown(gamepadInteractKey);

        return keyboard || gamepad;
    }

    private float GetRebindClimbVerticalValue()
    {
        float value = 0f;

        if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.UpAction) ||
            LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.UpAction))
        {
            value += 1f;
        }

        if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.DownAction) ||
            LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.DownAction))
        {
            value -= 1f;
        }

        return Mathf.Clamp(value, -1f, 1f);
    }

    private float GetFallbackClimbVerticalValue()
    {
        float value = 0f;

        if (Input.GetKey(upActionKey) || (alternateUpActionKey != KeyCode.None && Input.GetKey(alternateUpActionKey)))
            value += 1f;

        if (Input.GetKey(downActionKey) || (alternateDownActionKey != KeyCode.None && Input.GetKey(alternateDownActionKey)))
            value -= 1f;

        if (Mathf.Abs(value) > 0.001f)
            return Mathf.Clamp(value, -1f, 1f);

        if (!useVerticalAxisForFenceClimbFallback)
            return 0f;

        float axis = SafeGetAxisRaw(verticalAxisName);

        if (Mathf.Abs(axis) < fenceClimbAxisThreshold)
            return 0f;

        return Mathf.Clamp(axis, -1f, 1f);
    }

    private float GetMobileClimbVerticalValue()
    {
        if (mobileJoystick == null)
            return 0f;

        float axis = mobileJoystick.Vertical;

        if (Mathf.Abs(axis) < fenceClimbAxisThreshold)
            return 0f;

        return Mathf.Clamp(axis, -1f, 1f);
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
            }

            return false;
        }

        if (RebindActive)
        {
            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveLeft)) return true;
            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveRight)) return true;
            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.Jump)) return true;
            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.UpAction)) return true;
            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.DownAction)) return true;
            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.Interact)) return true;

            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.MoveLeft)) return true;
            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.MoveRight)) return true;
            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.Jump)) return true;
            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.UpAction)) return true;
            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.DownAction)) return true;
            if (LegacyKeycodeRebind.GetHeldStatic(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.Interact)) return true;

            return false;
        }

        if (Input.GetKey(leftKey) ||
            Input.GetKey(rightKey) ||
            Input.GetKey(jumpKey) ||
            Input.GetKey(upActionKey) ||
            Input.GetKey(alternateUpActionKey) ||
            Input.GetKey(downActionKey) ||
            Input.GetKey(alternateDownActionKey) ||
            Input.GetKey(interactKey))
        {
            return true;
        }

        if (useGamepadJump && gamepadJumpKey != KeyCode.None && Input.GetKey(gamepadJumpKey))
            return true;

        if (gamepadInteractKey != KeyCode.None && Input.GetKey(gamepadInteractKey))
            return true;

        if (useInputManagerAxisFallback && Mathf.Abs(SafeGetAxisRaw(horizontalAxisName)) > inputReleaseAxisDeadZone)
            return true;

        if (Mathf.Abs(SafeGetAxisRaw(verticalAxisName)) > inputReleaseAxisDeadZone)
            return true;

        return false;
    }

    private float SafeGetAxisRaw(string axisName)
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