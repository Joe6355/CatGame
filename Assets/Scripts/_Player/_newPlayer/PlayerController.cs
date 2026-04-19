using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerEnvironmentModule))]
[RequireComponent(typeof(PlayerGroundModule))]
[RequireComponent(typeof(PlayerInputModule))]
[RequireComponent(typeof(PlayerJumpModule))]
[RequireComponent(typeof(PlayerMovementModule))]
[RequireComponent(typeof(PlayerBounceModule))]
[RequireComponent(typeof(PlayerPresentationModule))]
[RequireComponent(typeof(PlayerLedgeModule))]
[RequireComponent(typeof(PlayerFenceClimbModule))]
public class PlayerController : MonoBehaviour
{
    [Header("Ссылки на модули")]
    [SerializeField] private PlayerEnvironmentModule environmentModule;
    [SerializeField] private PlayerGroundModule groundModule;
    [SerializeField] private PlayerInputModule inputModule;
    [SerializeField] private PlayerJumpModule jumpModule;
    [SerializeField] private PlayerMovementModule movementModule;
    [SerializeField] private PlayerBounceModule bounceModule;
    [SerializeField] private PlayerPresentationModule presentationModule;
    [SerializeField] private PlayerLedgeModule ledgeModule;
    [SerializeField] private PlayerFenceClimbModule fenceClimbModule;

    [Header("Камера: отдаление при спринте")]
    [SerializeField, Tooltip("Если ВКЛ — PlayerController будет передавать в CamController текущий уровень спринта, чтобы камера могла плавно отдаляться на разгоне и возвращаться обратно после спринта.")]
    private bool enableSprintCameraFeedback = true;

    [Header("Камера: тряска при жёстком приземлении")]
    [SerializeField, Tooltip("Если ВКЛ — после сильного падения при приземлении будет вызываться лёгкая тряска камеры.")]
    private bool enableLandingCameraShake = true;

    [SerializeField, Min(0f), Tooltip("Минимальная скорость падения вниз по Y, после которой приземление уже считается достаточно жёстким для тряски камеры и вибрации.")]
    private float landingShakeMinFallSpeed = 10f;

    [SerializeField, Min(0f), Tooltip("Скорость падения вниз, на которой сила тряски и вибрации достигает максимума.")]
    private float landingShakeMaxFallSpeed = 20f;

    [SerializeField, Min(0f), Tooltip("Минимальная сила лёгкой тряски при самом слабом подходящем жёстком приземлении.")]
    private float landingShakeMinStrength = 0.35f;

    [SerializeField, Min(0f), Tooltip("Максимальная сила тряски при очень жёстком приземлении.")]
    private float landingShakeMaxStrength = 0.8f;

    [SerializeField, Min(0f), Tooltip("Сколько секунд удерживать максимальную силу тряски перед затуханием.")]
    private float landingShakeHoldTime = 0.02f;

    [SerializeField, Min(0f), Tooltip("Сколько секунд плавно затухает тряска после жёсткого приземления.")]
    private float landingShakeFadeTime = 0.12f;

    [Header("Геймпад: вибрация при жёстком приземлении")]
    [SerializeField, Tooltip("Если ВКЛ — жёсткое приземление дополнительно запускает вибрацию на геймпаде.")]
    private bool enableLandingGamepadRumble = true;

    [SerializeField, Min(0f), Tooltip("Минимальная сила левого low-frequency мотора при слабом жёстком приземлении.")]
    private float landingRumbleMinLow = 0.20f;

    [SerializeField, Min(0f), Tooltip("Максимальная сила левого low-frequency мотора при очень жёстком приземлении.")]
    private float landingRumbleMaxLow = 0.85f;

    [SerializeField, Min(0f), Tooltip("Минимальная сила правого high-frequency мотора при слабом жёстком приземлении.")]
    private float landingRumbleMinHigh = 0.10f;

    [SerializeField, Min(0f), Tooltip("Максимальная сила правого high-frequency мотора при очень жёстком приземлении.")]
    private float landingRumbleMaxHigh = 0.45f;

    [SerializeField, Min(0f), Tooltip("Минимальная длительность вибрации.")]
    private float landingRumbleMinDuration = 0.06f;

    [SerializeField, Min(0f), Tooltip("Максимальная длительность вибрации.")]
    private float landingRumbleMaxDuration = 0.18f;

    private Rigidbody2D rb;
    private float inputX = 0f;
    private bool resetSprintAfterLanding = false;

    private float trackedMinAirborneY = 0f;
    private bool hasAirborneFallData = false;

#if ENABLE_INPUT_SYSTEM
    private float rumbleStopAtUnscaled = -1f;
    private bool rumbleActive = false;
#endif

    private float ExternalWindVX => environmentModule != null ? environmentModule.ExternalWindVX : 0f;
    private float SnowMoveMul => environmentModule != null ? environmentModule.SnowMoveMultiplier : 1f;
    private float SnowJumpMul => environmentModule != null ? environmentModule.SnowJumpMultiplier : 1f;

    private bool IsGroundedNow => groundModule != null && groundModule.IsGrounded;
    private float LastGroundedTimeNow => groundModule != null ? groundModule.LastGroundedTime : -999f;
    private Collider2D LastGroundColNow => groundModule != null ? groundModule.LastGroundCollider : null;
    private bool IsOnIceNow => groundModule != null && groundModule.IsOnIce;
    private float PlatformVXNow => groundModule != null ? groundModule.PlatformVX : 0f;

    private void Reset()
    {
        CacheComponents();
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnValidate()
    {
        CacheComponents();

        landingShakeMaxFallSpeed = Mathf.Max(landingShakeMinFallSpeed + 0.01f, landingShakeMaxFallSpeed);
        landingShakeMinStrength = Mathf.Max(0f, landingShakeMinStrength);
        landingShakeMaxStrength = Mathf.Max(landingShakeMinStrength, landingShakeMaxStrength);
        landingShakeHoldTime = Mathf.Max(0f, landingShakeHoldTime);
        landingShakeFadeTime = Mathf.Max(0f, landingShakeFadeTime);

        landingRumbleMinLow = Mathf.Max(0f, landingRumbleMinLow);
        landingRumbleMaxLow = Mathf.Max(landingRumbleMinLow, landingRumbleMaxLow);
        landingRumbleMinHigh = Mathf.Max(0f, landingRumbleMinHigh);
        landingRumbleMaxHigh = Mathf.Max(landingRumbleMinHigh, landingRumbleMaxHigh);
        landingRumbleMinDuration = Mathf.Max(0f, landingRumbleMinDuration);
        landingRumbleMaxDuration = Mathf.Max(landingRumbleMinDuration, landingRumbleMaxDuration);
    }

    private void Start()
    {
        RefreshPresentation();
        PushSprintCameraFeedback();
    }

    private void Update()
    {
        inputModule.RefreshPerFrame();

        if (inputModule.IsGameplayInputAllowedThisFrame())
        {
            if (!inputModule.UseMobileControls)
                HandleDesktopInput(inputModule.ReadDesktopInputFrame());
            else
                HandleMobileInput(inputModule.ReadMobileInputFrame());

            bool fenceActive = fenceClimbModule != null && fenceClimbModule.BlocksStandardController;
            bool ledgeActive = ledgeModule != null && ledgeModule.BlocksStandardController;

            if (!fenceActive && !ledgeActive)
            {
                UpdateControlledJumpHold();

                PlayerJumpModule.JumpActionResult bufferResult = jumpModule.TryConsumeJumpBuffer(BuildJumpContext());
                if (bufferResult.DidJump)
                    OnJumpPerformed(bufferResult.TakeoffVx, bufferResult.WasChargedJump);
            }
        }
        else
        {
            ResetGameplayInputState(false);
        }

        UpdateLandingGamepadRumble();
        PushSprintCameraFeedback();
        RefreshPresentation();
    }

    private void FixedUpdate()
    {
        bool fenceActive = fenceClimbModule != null && fenceClimbModule.BlocksStandardController;
        bool ledgeActive = ledgeModule != null && ledgeModule.BlocksStandardController;

        if (!fenceActive && !ledgeActive)
        {
            groundModule.EvaluateGround(rb, Time.time, jumpModule.CoyoteTime);

            if (groundModule.JustLanded)
            {
                if (resetSprintAfterLanding)
                {
                    movementModule.ResetSprint();
                    resetSprintAfterLanding = false;
                }

                TryPlayLandingFeedback();

                PlayerJumpModule.JumpActionResult bufferResult = jumpModule.TryConsumeJumpBuffer(BuildJumpContext());
                if (bufferResult.DidJump)
                    OnJumpPerformed(bufferResult.TakeoffVx, bufferResult.WasChargedJump);
            }
        }

        bool fenceControlled = fenceClimbModule != null && fenceClimbModule.ApplyFenceMotion(rb);
        if (fenceControlled)
        {
            environmentModule.ClearFrameWind();
            return;
        }

        bool ledgeControlled = ledgeModule != null && ledgeModule.ApplyLedgeMotion(rb, movementModule);
        if (ledgeControlled)
        {
            environmentModule.ClearFrameWind();
            return;
        }

        bounceModule?.RefreshWallState(inputX, IsGroundedNow, Time.time);
        movementModule.ApplyMovement(BuildMovementContext());
        bounceModule?.ApplyWallSlide(rb, inputX, IsGroundedNow, Time.time);

        TrackAirborneLandingData();
        environmentModule.ClearFrameWind();
    }

    private void OnDisable()
    {
        CamController.ChangeSprintZoomBlendEvent?.Invoke(0f);
        ResetLandingTracking();
        StopLandingGamepadRumble();
        resetSprintAfterLanding = false;
        fenceClimbModule?.ForceCancel(false);
        ledgeModule?.ForceCancel();
        bounceModule?.ResetWallState();
    }

    private void OnDestroy()
    {
        StopLandingGamepadRumble();
    }

    private void CacheComponents()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (environmentModule == null) environmentModule = GetComponent<PlayerEnvironmentModule>();
        if (groundModule == null) groundModule = GetComponent<PlayerGroundModule>();
        if (inputModule == null) inputModule = GetComponent<PlayerInputModule>();
        if (jumpModule == null) jumpModule = GetComponent<PlayerJumpModule>();
        if (movementModule == null) movementModule = GetComponent<PlayerMovementModule>();
        if (bounceModule == null) bounceModule = GetComponent<PlayerBounceModule>();
        if (presentationModule == null) presentationModule = GetComponent<PlayerPresentationModule>();
        if (ledgeModule == null) ledgeModule = GetComponent<PlayerLedgeModule>();
        if (fenceClimbModule == null) fenceClimbModule = GetComponent<PlayerFenceClimbModule>();
    }

    private PlayerJumpModule.JumpContext BuildJumpContext()
    {
        return new PlayerJumpModule.JumpContext
        {
            Now = Time.time,
            IsGrounded = IsGroundedNow,
            LastGroundedTime = LastGroundedTimeNow,
            IsFacingRight = movementModule.IsFacingRight,
            MoveSpeed = movementModule.CurrentMoveSpeed,
            PlatformVX = PlatformVXNow,
            SnowMoveMul = SnowMoveMul,
            SnowJumpMul = SnowJumpMul,
            ExternalWindVX = ExternalWindVX,
            IsSprintMovementActive = movementModule.IsSprintMovementActive,
            Rigidbody = rb
        };
    }

    private PlayerMovementModule.MovementContext BuildMovementContext()
    {
        return new PlayerMovementModule.MovementContext
        {
            Now = Time.time,
            FixedDeltaTime = Time.fixedDeltaTime,
            Rigidbody = rb,
            InputX = inputX,
            IsGrounded = IsGroundedNow,
            IsOnIce = IsOnIceNow,
            LastGroundCollider = LastGroundColNow,
            PlatformVX = PlatformVXNow,
            ExternalWindVX = ExternalWindVX,
            SnowMoveMul = SnowMoveMul,
            IsJumpCharging = jumpModule.IsChargingJump
        };
    }

    private void HandleDesktopInput(PlayerInputModule.DesktopInputSnapshot snapshot)
    {
        bool jumpPressed = snapshot.JumpDownSource != PlayerInputModule.HoldSource.None;

        if (snapshot.IsRebinding)
        {
            inputX = 0f;
            movementModule.RefreshImmediateSprintBlocker(false, 0f);
            fenceClimbModule?.TickFence(0f, 0f, false, false, movementModule, jumpModule);
            if (fenceClimbModule != null && fenceClimbModule.BlocksStandardController)
                return;

            ledgeModule?.TickLedge(0f, IsGroundedNow, snapshot.LedgeUpPressed, snapshot.ApexThrowDownPressed, movementModule, jumpModule);
            return;
        }

        if (fenceClimbModule != null && fenceClimbModule.BlocksStandardController)
        {
            inputX = 0f;
            movementModule.RefreshImmediateSprintBlocker(false, 0f);

            PlayerFenceClimbModule.FenceTickResult fenceResult =
                fenceClimbModule.TickFence(snapshot.MoveX, snapshot.ClimbY, snapshot.FenceTogglePressed, jumpPressed, movementModule, jumpModule);

            if (fenceResult.DidJumpOff)
                OnFenceJumpPerformed(fenceResult.TakeoffVx);

            return;
        }

        if (ledgeModule != null && ledgeModule.BlocksStandardController)
        {
            inputX = 0f;
            movementModule.RefreshImmediateSprintBlocker(false, 0f);
            ledgeModule.TickLedge(0f, IsGroundedNow, snapshot.LedgeUpPressed, snapshot.ApexThrowDownPressed, movementModule, jumpModule);
            return;
        }

        float rawInputX = snapshot.MoveX;
        inputX = rawInputX;

        PlayerFenceClimbModule.FenceTickResult preFenceResult =
            fenceClimbModule != null
                ? fenceClimbModule.TickFence(rawInputX, snapshot.ClimbY, snapshot.FenceTogglePressed, jumpPressed, movementModule, jumpModule)
                : default;

        if (preFenceResult.DidJumpOff)
            OnFenceJumpPerformed(preFenceResult.TakeoffVx);

        if (fenceClimbModule != null && fenceClimbModule.BlocksStandardController)
        {
            inputX = 0f;
            movementModule.RefreshImmediateSprintBlocker(false, 0f);
            return;
        }

        movementModule.RefreshImmediateSprintBlocker(IsGroundedNow, inputX);
        movementModule.TryFaceByInput(inputX, true, IsGroundedNow);

        ledgeModule?.TickLedge(inputX, IsGroundedNow, snapshot.LedgeUpPressed, snapshot.ApexThrowDownPressed, movementModule, jumpModule);
        if (ledgeModule != null && ledgeModule.BlocksStandardController)
        {
            inputX = 0f;
            movementModule.RefreshImmediateSprintBlocker(false, 0f);
            return;
        }

        bounceModule?.RefreshWallState(inputX, IsGroundedNow, Time.time);

        if (snapshot.ApexThrowDownPressed && bounceModule != null && bounceModule.TryStartWallSlideDrop(Time.time))
            return;

        PlayerJumpModule.JumpContext apexCtx = BuildJumpContext();
        jumpModule.UpdateApexThrowState(apexCtx, rawInputX);

        if (snapshot.ApexThrowDownPressed)
        {
            PlayerJumpModule.ApexThrowResult apexThrowResult = jumpModule.TryPerformApexThrow(apexCtx, rawInputX);
            if (apexThrowResult.DidThrow)
            {
                bounceModule.NotifyJumpImpulse(Time.time);
                return;
            }
        }

        if (jumpPressed)
        {
            PlayerBounceModule.WallJumpResult wallJumpResult = bounceModule != null
                ? bounceModule.TryPerformWallJump(inputX, IsGroundedNow, jumpModule, movementModule, ExternalWindVX, Time.time)
                : default;

            if (wallJumpResult.DidJump)
            {
                OnWallJumpPerformed(wallJumpResult.TakeoffVx);
                return;
            }

            jumpModule.MarkJumpPressed(snapshot.JumpDownSource, Time.time);
        }
    }

    private void HandleMobileInput(PlayerInputModule.MobileInputSnapshot snapshot)
    {
        if (fenceClimbModule != null && fenceClimbModule.BlocksStandardController)
        {
            inputX = 0f;
            movementModule.RefreshImmediateSprintBlocker(false, 0f);

            PlayerFenceClimbModule.FenceTickResult fenceResult =
                fenceClimbModule.TickFence(snapshot.MoveX, snapshot.ClimbY, snapshot.FenceTogglePressed, snapshot.JumpDown, movementModule, jumpModule);

            if (fenceResult.DidJumpOff)
                OnFenceJumpPerformed(fenceResult.TakeoffVx);

            return;
        }

        if (ledgeModule != null && ledgeModule.BlocksStandardController)
        {
            inputX = 0f;
            movementModule.RefreshImmediateSprintBlocker(false, 0f);
            ledgeModule.TickLedge(0f, IsGroundedNow, snapshot.LedgeUpPressed, snapshot.ApexThrowDownPressed, movementModule, jumpModule);
            return;
        }

        float rawInputX = snapshot.MoveX;
        inputX = rawInputX;

        PlayerFenceClimbModule.FenceTickResult preFenceResult =
            fenceClimbModule != null
                ? fenceClimbModule.TickFence(rawInputX, snapshot.ClimbY, snapshot.FenceTogglePressed, snapshot.JumpDown, movementModule, jumpModule)
                : default;

        if (preFenceResult.DidJumpOff)
            OnFenceJumpPerformed(preFenceResult.TakeoffVx);

        if (fenceClimbModule != null && fenceClimbModule.BlocksStandardController)
        {
            inputX = 0f;
            movementModule.RefreshImmediateSprintBlocker(false, 0f);
            return;
        }

        movementModule.RefreshImmediateSprintBlocker(IsGroundedNow, inputX);
        movementModule.TryFaceByInput(inputX, true, IsGroundedNow);

        ledgeModule?.TickLedge(inputX, IsGroundedNow, snapshot.LedgeUpPressed, snapshot.ApexThrowDownPressed, movementModule, jumpModule);
        if (ledgeModule != null && ledgeModule.BlocksStandardController)
        {
            inputX = 0f;
            movementModule.RefreshImmediateSprintBlocker(false, 0f);
            return;
        }

        bounceModule?.RefreshWallState(inputX, IsGroundedNow, Time.time);

        if (snapshot.ApexThrowDownPressed && bounceModule != null && bounceModule.TryStartWallSlideDrop(Time.time))
            return;

        PlayerJumpModule.JumpContext apexCtx = BuildJumpContext();
        jumpModule.UpdateApexThrowState(apexCtx, rawInputX);

        if (snapshot.ApexThrowDownPressed)
        {
            PlayerJumpModule.ApexThrowResult apexThrowResult = jumpModule.TryPerformApexThrow(apexCtx, rawInputX);
            if (apexThrowResult.DidThrow)
            {
                bounceModule.NotifyJumpImpulse(Time.time);
                return;
            }
        }

        if (snapshot.JumpDown)
        {
            PlayerBounceModule.WallJumpResult wallJumpResult = bounceModule != null
                ? bounceModule.TryPerformWallJump(inputX, IsGroundedNow, jumpModule, movementModule, ExternalWindVX, Time.time)
                : default;

            if (wallJumpResult.DidJump)
            {
                OnWallJumpPerformed(wallJumpResult.TakeoffVx);
                return;
            }

            jumpModule.MarkJumpPressed(PlayerInputModule.HoldSource.Mobile, Time.time);
        }
    }

    private void UpdateControlledJumpHold()
    {
        PlayerInputModule.HoldSource source = jumpModule.CurrentHoldSource;
        bool isHeld = source != PlayerInputModule.HoldSource.None && inputModule.IsHoldInputStillHeld(source);
        jumpModule.UpdateJumpHold(BuildJumpContext(), isHeld, Time.deltaTime);
    }

    private void OnJumpPerformed(float takeoffVx, bool wasChargedJump)
    {
        movementModule.OnJumpPerformed(takeoffVx);
        resetSprintAfterLanding = true;
        bounceModule.NotifyJumpImpulse(Time.time);
    }

    private void OnWallJumpPerformed(float takeoffVx)
    {
        movementModule.OnJumpPerformed(takeoffVx);
        movementModule.ResetSprint();
        resetSprintAfterLanding = true;
    }

    private void OnFenceJumpPerformed(float takeoffVx)
    {
        movementModule.OnJumpPerformed(takeoffVx);
        resetSprintAfterLanding = true;
        bounceModule.NotifyJumpImpulse(Time.time);
    }

    private void RefreshPresentation()
    {
        presentationModule.RefreshPresentation(
            jumpModule.IsJumpHoldActiveForPresentation,
            jumpModule.IsChargeVisualActive,
            jumpModule.ChargeBarNormalizedForPresentation,
            jumpModule.IsApexThrowAvailable);
    }

    private void PushSprintCameraFeedback()
    {
        float blend = 0f;

        if (enableSprintCameraFeedback && movementModule != null)
            blend = movementModule.SprintCameraBlend;

        CamController.ChangeSprintZoomBlendEvent?.Invoke(blend);
    }

    private void TrackAirborneLandingData()
    {
        if (rb == null)
            return;

        if (!IsGroundedNow)
        {
            if (!hasAirborneFallData)
            {
                trackedMinAirborneY = rb.velocity.y;
                hasAirborneFallData = true;
            }
            else
            {
                trackedMinAirborneY = Mathf.Min(trackedMinAirborneY, rb.velocity.y);
            }
        }
        else if (hasAirborneFallData)
        {
            trackedMinAirborneY = Mathf.Min(trackedMinAirborneY, rb.velocity.y);
        }
    }

    private void TryPlayLandingFeedback()
    {
        if (!hasAirborneFallData)
        {
            ResetLandingTracking();
            return;
        }

        float fallSpeed = Mathf.Abs(Mathf.Min(0f, trackedMinAirborneY));
        if (fallSpeed < landingShakeMinFallSpeed)
        {
            ResetLandingTracking();
            return;
        }

        float denom = Mathf.Max(0.0001f, landingShakeMaxFallSpeed - landingShakeMinFallSpeed);
        float t = Mathf.Clamp01((fallSpeed - landingShakeMinFallSpeed) / denom);

        if (enableLandingCameraShake)
        {
            float strength = Mathf.Lerp(landingShakeMinStrength, landingShakeMaxStrength, t);
            CamController.CameraShake?.Invoke(strength, landingShakeHoldTime, landingShakeFadeTime);
        }

        if (enableLandingGamepadRumble)
            PlayLandingGamepadRumble(t);

        ResetLandingTracking();
    }

    private void PlayLandingGamepadRumble(float normalizedStrength)
    {
#if ENABLE_INPUT_SYSTEM
        Gamepad pad = Gamepad.current;
        if (pad == null)
            return;

        float t = Mathf.Clamp01(normalizedStrength);

        float low = Mathf.Lerp(landingRumbleMinLow, landingRumbleMaxLow, t);
        float high = Mathf.Lerp(landingRumbleMinHigh, landingRumbleMaxHigh, t);
        float duration = Mathf.Lerp(landingRumbleMinDuration, landingRumbleMaxDuration, t);

        pad.SetMotorSpeeds(low, high);
        rumbleStopAtUnscaled = Time.unscaledTime + duration;
        rumbleActive = true;
#endif
    }

    private void UpdateLandingGamepadRumble()
    {
#if ENABLE_INPUT_SYSTEM
        if (!rumbleActive)
            return;

        if (Time.unscaledTime < rumbleStopAtUnscaled)
            return;

        StopLandingGamepadRumble();
#endif
    }

    private void StopLandingGamepadRumble()
    {
#if ENABLE_INPUT_SYSTEM
        Gamepad pad = Gamepad.current;
        if (pad != null)
            pad.ResetHaptics();

        rumbleActive = false;
        rumbleStopAtUnscaled = -1f;
#endif
    }

    private void ResetLandingTracking()
    {
        trackedMinAirborneY = 0f;
        hasAirborneFallData = false;
    }

    public void AllowAirControlFor(float duration)
    {
        movementModule.AllowAirControlFor(duration);
    }

    public void AddExternalWind(float vx)
    {
        environmentModule.AddExternalWind(vx);
    }

    public void CancelJumpCharge()
    {
        jumpModule.CancelJumpCharge();
    }

    public void RegisterSnow(SnowdriftArea2D area, float moveMul, float jumpMul)
    {
        environmentModule.RegisterSnow(area, moveMul, jumpMul);
    }

    public void UnregisterSnow(SnowdriftArea2D area)
    {
        environmentModule.UnregisterSnow(area);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputModule.SetInputEnabled(enabled);
        ResetGameplayInputState(true);
    }

    private void ResetGameplayInputState(bool clearMobileHold)
    {
        inputX = 0f;
        resetSprintAfterLanding = false;
        inputModule.ResetModuleInputState(clearMobileHold);
        jumpModule.ResetJumpInputState();
        movementModule.ResetSprint();
        movementModule.RefreshImmediateSprintBlocker(false, 0f);
        fenceClimbModule?.ClearMoveInput();
        bounceModule?.ResetWallState();
        StopLandingGamepadRumble();
        PushSprintCameraFeedback();
    }

    public bool IsChargingJumpPublic
    {
        get { return jumpModule != null && jumpModule.IsChargingJumpPublic; }
    }

    public bool IsChargeTrajectoryPreviewVisible
    {
        get { return jumpModule != null && jumpModule.IsChargeTrajectoryPreviewVisible; }
    }

    public bool IsApexThrowTrajectoryPreviewVisible
    {
        get { return jumpModule != null && jumpModule.IsApexThrowTrajectoryPreviewVisible; }
    }

    public Vector2 GetPredictedJumpVelocity()
    {
        return jumpModule != null
            ? jumpModule.GetPredictedJumpVelocity(BuildJumpContext())
            : Vector2.zero;
    }

    public Vector2 GetPredictedChargeTrajectoryVelocity()
    {
        return jumpModule != null
            ? jumpModule.GetPredictedChargeTrajectoryVelocity(BuildJumpContext())
            : Vector2.zero;
    }

    public Vector2 GetPredictedApexThrowTrajectoryVelocity()
    {
        return jumpModule != null
            ? jumpModule.GetPredictedApexThrowTrajectoryVelocity(BuildJumpContext())
            : Vector2.zero;
    }

    public Vector2 GetCurrentWorldVelocity()
    {
        return rb != null ? rb.velocity : Vector2.zero;
    }

    public float GetGravityScale()
    {
        return rb != null ? rb.gravityScale : 1f;
    }
}