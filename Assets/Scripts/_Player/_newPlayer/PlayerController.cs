using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerEnvironmentModule))]
[RequireComponent(typeof(PlayerGroundModule))]
[RequireComponent(typeof(PlayerInputModule))]
[RequireComponent(typeof(PlayerJumpModule))]
[RequireComponent(typeof(PlayerMovementModule))]
[RequireComponent(typeof(PlayerBounceModule))]
[RequireComponent(typeof(PlayerPresentationModule))]
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

    [Header("Камера: отдаление при спринте")]
    [SerializeField, Tooltip("Если ВКЛ — PlayerController будет передавать в CamController текущий уровень спринта,\nчтобы камера могла плавно отдаляться на разгоне и возвращаться обратно после спринта.")]
    private bool enableSprintCameraFeedback = true;

    [Header("Камера: тряска при жёстком приземлении")]
    [SerializeField, Tooltip("Если ВКЛ — после сильного падения при приземлении будет вызываться лёгкая тряска камеры.")]
    private bool enableLandingCameraShake = true;

    [SerializeField, Min(0f), Tooltip("Минимальная скорость падения вниз по Y, после которой приземление уже считается достаточно жёстким для тряски камеры.\nНапример 10 = тряска начнётся примерно от скорости -10 и ниже.")]
    private float landingShakeMinFallSpeed = 10f;

    [SerializeField, Min(0f), Tooltip("Скорость падения вниз, на которой сила тряски достигает максимума.")]
    private float landingShakeMaxFallSpeed = 20f;

    [SerializeField, Min(0f), Tooltip("Минимальная сила лёгкой тряски при самом слабом подходящем жёстком приземлении.")]
    private float landingShakeMinStrength = 0.35f;

    [SerializeField, Min(0f), Tooltip("Максимальная сила тряски при очень жёстком приземлении.")]
    private float landingShakeMaxStrength = 0.8f;

    [SerializeField, Min(0f), Tooltip("Сколько секунд удерживать максимальную силу тряски перед затуханием.")]
    private float landingShakeHoldTime = 0.02f;

    [SerializeField, Min(0f), Tooltip("Сколько секунд плавно затухает тряска после жёсткого приземления.")]
    private float landingShakeFadeTime = 0.12f;

    private Rigidbody2D rb;
    private float inputX = 0f;

    private float trackedMinAirborneY = 0f;
    private bool hasAirborneFallData = false;

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

            PlayerJumpModule.JumpActionResult bufferResult =
                jumpModule.TryConsumeJumpBuffer(
                    BuildJumpContext(),
                    source => inputModule.IsHoldInputStillHeld(source));

            if (bufferResult.DidJump)
                OnJumpPerformed(bufferResult.TakeoffVx, bufferResult.WasChargedJump);
        }
        else
        {
            ResetGameplayInputState(false);
        }

        PushSprintCameraFeedback();
        RefreshPresentation();
    }

    private void FixedUpdate()
    {
        groundModule.EvaluateGround(rb, Time.time, jumpModule.CoyoteTime);

        if (groundModule.JustLanded)
        {
            TryPlayLandingCameraShake();

            PlayerJumpModule.JumpActionResult bufferResult =
                jumpModule.TryConsumeJumpBuffer(
                    BuildJumpContext(),
                    source => inputModule.IsHoldInputStillHeld(source));

            if (bufferResult.DidJump)
                OnJumpPerformed(bufferResult.TakeoffVx, bufferResult.WasChargedJump);
        }

        movementModule.ApplyMovement(BuildMovementContext());
        TrackAirborneLandingData();
        environmentModule.ClearFrameWind();
    }

    private void OnDisable()
    {
        CamController.ChangeSprintZoomBlendEvent?.Invoke(0f);
        ResetLandingTracking();
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
            FatigueSpeedMultiplier = movementModule.FatigueSpeedMultiplier,
            PlatformVX = PlatformVXNow,
            SnowMoveMul = SnowMoveMul,
            SnowJumpMul = SnowJumpMul,
            ExternalWindVX = ExternalWindVX,
            SprintChargedJumpReady = movementModule.IsSprintReady,
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
            IsFatigued = jumpModule.IsFatigued(Time.time),
            IsJumpCharging = jumpModule.IsChargingJump
        };
    }

    private void HandleDesktopInput(PlayerInputModule.DesktopInputSnapshot snapshot)
    {
        if (snapshot.IsRebinding)
        {
            inputX = 0f;
            movementModule.RefreshImmediateSprintBlocker(false, 0f);
            return;
        }

        inputX = snapshot.MoveX;
        movementModule.RefreshImmediateSprintBlocker(IsGroundedNow, inputX);
        movementModule.TryFaceByInput(inputX, IsGroundMovementAllowed());

        PlayerJumpModule.JumpContext apexCtx = BuildJumpContext();
        jumpModule.UpdateApexThrowState(apexCtx, inputX);

        if (snapshot.ApexThrowDownPressed)
        {
            PlayerJumpModule.ApexThrowResult apexThrowResult =
                jumpModule.TryPerformApexThrow(apexCtx, inputX);

            if (apexThrowResult.DidThrow)
            {
                movementModule.SetAirVx(apexThrowResult.AirVx);
                movementModule.ResetSprint();
                bounceModule.NotifyJumpImpulse(Time.time);
                return;
            }
        }

        if (snapshot.ShortJumpDown && !jumpModule.IsJumpHoldActive)
        {
            PlayerJumpModule.JumpContext jumpCtx = BuildJumpContext();

            if (jumpModule.CanAcceptDedicatedShortJumpInput(jumpCtx))
            {
                jumpModule.MarkShortJumpPressed(Time.time);

                PlayerJumpModule.JumpActionResult shortResult =
                    jumpModule.TryPerformDedicatedShortJump(jumpCtx);

                if (shortResult.DidJump)
                    OnJumpPerformed(shortResult.TakeoffVx, shortResult.WasChargedJump);
            }
        }

        if (snapshot.ChargeDownSource != PlayerInputModule.HoldSource.None)
        {
            jumpModule.MarkHoldJumpPressed(snapshot.ChargeDownSource, Time.time);

            PlayerJumpModule.JumpContext jumpCtx = BuildJumpContext();

            if (jumpModule.AllowInstantMaxChargeFromSprint && jumpCtx.SprintChargedJumpReady)
            {
                PlayerJumpModule.JumpActionResult instantResult =
                    jumpModule.TryPerformInstantMaxChargedJump(jumpCtx);

                if (instantResult.DidJump)
                {
                    OnJumpPerformed(instantResult.TakeoffVx, instantResult.WasChargedJump);
                }
                else if (jumpModule.CanStartJumpCharge(jumpCtx))
                {
                    jumpModule.BeginJumpHold(snapshot.ChargeDownSource, Time.time, false);
                }
            }
            else if (jumpModule.CanStartJumpCharge(jumpCtx))
            {
                jumpModule.BeginJumpHold(snapshot.ChargeDownSource, Time.time, false);
            }
        }

        if (jumpModule.IsJumpHoldActive)
        {
            bool held = inputModule.IsHoldInputStillHeld(jumpModule.CurrentHoldSource);
            bool released = inputModule.IsHoldInputReleased(jumpModule.CurrentHoldSource);

            if (held)
            {
                jumpModule.UpdateJumpHold(
                    Time.time,
                    jumpModule.IsWithinGroundedJumpWindow(BuildJumpContext()));
            }

            if (released)
            {
                PlayerJumpModule.JumpActionResult releaseResult =
                    jumpModule.ReleaseJumpHoldAndMaybeJump(BuildJumpContext());

                if (releaseResult.DidJump)
                    OnJumpPerformed(releaseResult.TakeoffVx, releaseResult.WasChargedJump);
            }
        }
    }

    private void HandleMobileInput(PlayerInputModule.MobileInputSnapshot snapshot)
    {
        inputX = snapshot.MoveX;
        movementModule.RefreshImmediateSprintBlocker(IsGroundedNow, inputX);
        movementModule.TryFaceByInput(inputX, IsGroundMovementAllowed());

        PlayerJumpModule.JumpContext apexCtx = BuildJumpContext();
        jumpModule.UpdateApexThrowState(apexCtx, inputX);

        if (snapshot.ApexThrowDownPressed)
        {
            PlayerJumpModule.ApexThrowResult apexThrowResult =
                jumpModule.TryPerformApexThrow(apexCtx, inputX);

            if (apexThrowResult.DidThrow)
            {
                movementModule.SetAirVx(apexThrowResult.AirVx);
                movementModule.ResetSprint();
                bounceModule.NotifyJumpImpulse(Time.time);
                return;
            }
        }

        if (snapshot.JumpHeld)
        {
            jumpModule.MarkHoldJumpPressed(PlayerInputModule.HoldSource.Mobile, Time.time);
        }

        if (snapshot.JumpHeld)
        {
            PlayerJumpModule.JumpContext jumpCtx = BuildJumpContext();

            if (!jumpModule.IsJumpHoldActive &&
                jumpModule.AllowInstantMaxChargeFromSprint &&
                jumpCtx.SprintChargedJumpReady)
            {
                PlayerJumpModule.JumpActionResult instantResult =
                    jumpModule.TryPerformInstantMaxChargedJump(jumpCtx);

                if (instantResult.DidJump)
                {
                    OnJumpPerformed(instantResult.TakeoffVx, instantResult.WasChargedJump);
                    return;
                }
            }

            if (!jumpModule.IsJumpHoldActive && jumpModule.CanStartJumpCharge(jumpCtx))
                jumpModule.BeginJumpHold(PlayerInputModule.HoldSource.Mobile, Time.time, false);

            if (jumpModule.IsJumpHoldActive)
            {
                jumpModule.UpdateJumpHold(
                    Time.time,
                    jumpModule.IsWithinGroundedJumpWindow(BuildJumpContext()));
            }
        }
        else
        {
            if (jumpModule.IsJumpHoldActive)
            {
                PlayerJumpModule.JumpActionResult releaseResult =
                    jumpModule.ReleaseJumpHoldAndMaybeJump(BuildJumpContext());

                if (releaseResult.DidJump)
                    OnJumpPerformed(releaseResult.TakeoffVx, releaseResult.WasChargedJump);
            }
        }
    }

    private void OnJumpPerformed(float takeoffVx, bool wasChargedJump)
    {
        movementModule.OnJumpPerformed(takeoffVx);

        if (wasChargedJump)
            movementModule.ResetSprint();

        bounceModule.NotifyJumpImpulse(Time.time);
    }

    private bool IsGroundMovementAllowed()
    {
        return !jumpModule.IsJumpHoldActive && !jumpModule.IsChargingJump;
    }

    private void RefreshPresentation()
    {
        presentationModule.RefreshPresentation(
            jumpModule.IsFatigued(Time.time),
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

            return;
        }

        if (!groundModule.JustLanded)
            ResetLandingTracking();
    }

    private void TryPlayLandingCameraShake()
    {
        if (!enableLandingCameraShake)
        {
            ResetLandingTracking();
            return;
        }

        if (!hasAirborneFallData)
            return;

        float fallSpeed = Mathf.Abs(Mathf.Min(trackedMinAirborneY, 0f));
        ResetLandingTracking();

        if (fallSpeed < landingShakeMinFallSpeed)
            return;

        float maxSpeed = Mathf.Max(landingShakeMinFallSpeed + 0.01f, landingShakeMaxFallSpeed);
        float t = Mathf.InverseLerp(landingShakeMinFallSpeed, maxSpeed, fallSpeed);
        float strength = Mathf.Lerp(landingShakeMinStrength, landingShakeMaxStrength, t);

        CamController.CameraShake?.Invoke(strength, landingShakeHoldTime, landingShakeFadeTime);
    }

    private void ResetLandingTracking()
    {
        trackedMinAirborneY = 0f;
        hasAirborneFallData = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        bounceModule.HandleBounce(collision, rb, jumpModule, movementModule, ExternalWindVX, Time.time);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        bounceModule.HandleBounce(collision, rb, jumpModule, movementModule, ExternalWindVX, Time.time);
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
        inputModule.ResetModuleInputState(clearMobileHold);
        jumpModule.ResetJumpInputState();
        movementModule.ResetSprint();
        movementModule.RefreshImmediateSprintBlocker(false, 0f);
        PushSprintCameraFeedback();
    }

    public bool IsChargingJumpPublic
    {
        get { return jumpModule.IsChargingJumpPublic; }
    }

    public Vector2 GetPredictedJumpVelocity()
    {
        return jumpModule.GetPredictedJumpVelocity(BuildJumpContext());
    }

    public float GetGravityScale()
    {
        return rb != null ? rb.gravityScale : 1f;
    }
}
