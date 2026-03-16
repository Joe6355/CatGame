using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerJumpModule : MonoBehaviour
{
    [Header("Прыжок (заряд)")]
    [SerializeField, Tooltip("Максимальная вертикальная скорость (сила) прыжка при полном заряде.")]
    private float maxJumpForce = 20f;

    [SerializeField, Tooltip("Время, за которое прыжок заряжается до maxJumpForce ПОСЛЕ начала накопления.")]
    private float jumpTimeLimit = 1f;

    [SerializeField, Tooltip("Сила отдельного слабого прыжка. Используется только для dedicated short jump, а не для отпускания кнопки заряда.")]
    private float shortJumpForce = 8f;

    [Header("Старт накопления заряда")]
    [SerializeField, Tooltip("Если ВКЛ — накопление начинается только после задержки ниже.")]
    private bool useChargeEnterDelay = true;

    [SerializeField, Min(0f), Tooltip("Через сколько секунд удержания начинается накопление усиленного прыжка.")]
    private float chargeEnterDelay = 0.3f;

    [Header("Спринт + мгновенный максимум")]
    [SerializeField, Tooltip("Если ВКЛ — при полном спринте нажатие зарядного прыжка сразу выполняет усиленный прыжок с максимальной силой.")]
    private bool allowInstantMaxChargeFromSprint = true;

    [SerializeField, Tooltip("Если ВКЛ — после мгновенного спринт-прыжка на короткое время показывается полная полоска и максимальная траектория.")]
    private bool showInstantSprintPreview = true;

    [SerializeField, Min(0f), Tooltip("Сколько секунд держать визуальный предпросмотр полного заряда после мгновенного спринт-прыжка.")]
    private float instantSprintPreviewDuration = 0.06f;

    [Header("Койот-тайм и буфер")]
    [SerializeField, Tooltip("Сколько секунд после схода с платформы ещё можно начать прыжок.")]
    private float coyoteTime = 0.05f;

    [SerializeField, Tooltip("Буфер прыжка: если нажать кнопку до приземления, прыжок сработает автоматически.")]
    private float jumpBufferTime = 0.1f;

    [Header("Усталость")]
    [SerializeField, Tooltip("Сколько длится усталость после прыжка.")]
    private float fatigueDuration = 0.8f;

    private enum BufferedJumpKind
    {
        None,
        Hold,
        Short
    }

    public struct JumpContext
    {
        public float Now;
        public bool IsGrounded;
        public float LastGroundedTime;
        public bool IsFacingRight;
        public float MoveSpeed;
        public float FatigueSpeedMultiplier;
        public float PlatformVX;
        public float SnowMoveMul;
        public float SnowJumpMul;
        public float ExternalWindVX;
        public bool SprintChargedJumpReady;
        public Rigidbody2D Rigidbody;
    }

    public struct JumpActionResult
    {
        public bool DidJump;
        public float TakeoffVx;
        public bool WasChargedJump;
    }

    private float lastJumpPressedTime = -999f;
    private BufferedJumpKind bufferedJumpKind = BufferedJumpKind.None;

    private PlayerInputModule.HoldSource currentHoldJumpSource = PlayerInputModule.HoldSource.None;
    private PlayerInputModule.HoldSource bufferedHoldSource = PlayerInputModule.HoldSource.None;

    private bool isJumpHoldActive = false;
    private bool isChargingJump = false;

    private float jumpButtonDownTime = 0f;
    private float jumpStartHoldTime = 0f;

    private float fatigueEndTime = -999f;

    private float lastJumpTime = -999f;
    private float lastAppliedJumpForce = 0f;

    private float currentBarNormalized = 0f;

    private float instantSprintPreviewUntilUnscaled = -999f;
    private Vector2 instantSprintPreviewVelocity = Vector2.zero;

    public float CoyoteTime => coyoteTime;
    public bool IsJumpHoldActive => isJumpHoldActive;
    public bool IsChargingJump => isChargingJump;
    public bool IsChargingJumpPublic => isChargingJump || IsInstantSprintPreviewActive();
    public float CurrentBarNormalized => currentBarNormalized;
    public float LastJumpTime => lastJumpTime;
    public float LastAppliedJumpForce => lastAppliedJumpForce;
    public PlayerInputModule.HoldSource CurrentHoldSource => currentHoldJumpSource;
    public bool AllowInstantMaxChargeFromSprint => allowInstantMaxChargeFromSprint;
    public bool IsJumpHoldActiveForPresentation => isJumpHoldActive || IsInstantSprintPreviewActive();
    public bool IsChargeVisualActive => isChargingJump || IsInstantSprintPreviewActive();
    public float ChargeBarNormalizedForPresentation => IsInstantSprintPreviewActive() ? 1f : currentBarNormalized;

    public bool IsFatigued(float now)
    {
        return now < fatigueEndTime;
    }

    public bool IsWithinGroundedJumpWindow(JumpContext ctx)
    {
        return ctx.IsGrounded || (ctx.Now - ctx.LastGroundedTime) <= coyoteTime;
    }

    public bool CanStartJumpCharge(JumpContext ctx)
    {
        return IsWithinGroundedJumpWindow(ctx) && !IsFatigued(ctx.Now);
    }

    public void MarkShortJumpPressed(float now)
    {
        lastJumpPressedTime = now;
        bufferedJumpKind = BufferedJumpKind.Short;
        bufferedHoldSource = PlayerInputModule.HoldSource.None;
    }

    public void MarkHoldJumpPressed(PlayerInputModule.HoldSource source, float now)
    {
        lastJumpPressedTime = now;
        bufferedJumpKind = BufferedJumpKind.Hold;
        bufferedHoldSource = source;
    }

    public void BeginJumpHold(PlayerInputModule.HoldSource source, float now, bool startChargingImmediately = false)
    {
        ClearInstantSprintPreview();

        isJumpHoldActive = true;
        isChargingJump = false;
        bufferedJumpKind = BufferedJumpKind.None;
        currentHoldJumpSource = source;
        bufferedHoldSource = PlayerInputModule.HoldSource.None;

        jumpButtonDownTime = now;
        jumpStartHoldTime = 0f;
        currentBarNormalized = 0f;

        if (startChargingImmediately || !useChargeEnterDelay || chargeEnterDelay <= 0f)
        {
            isChargingJump = true;
            jumpStartHoldTime = now;
            currentBarNormalized = 0f;
        }
    }

    public void UpdateJumpHold(float now, bool isStillWithinGroundedWindow)
    {
        if (!isStillWithinGroundedWindow)
        {
            CancelJumpCharge();
            return;
        }

        if (!isChargingJump)
        {
            float held = now - jumpButtonDownTime;

            if (useChargeEnterDelay && held < chargeEnterDelay)
            {
                currentBarNormalized = 0f;
                return;
            }

            isChargingJump = true;
            jumpStartHoldTime = now;
            currentBarNormalized = 0f;
            return;
        }

        currentBarNormalized = CalculateChargeNormalized(now);
    }

    public JumpActionResult ReleaseJumpHoldAndMaybeJump(JumpContext ctx)
    {
        if (!IsWithinGroundedJumpWindow(ctx))
        {
            ClearHoldState();
            bufferedJumpKind = BufferedJumpKind.None;
            bufferedHoldSource = PlayerInputModule.HoldSource.None;
            return default;
        }

        if (!isChargingJump)
        {
            // Слабый прыжок по отпусканию кнопки заряда отключён.
            ClearHoldState();
            bufferedJumpKind = BufferedJumpKind.None;
            bufferedHoldSource = PlayerInputModule.HoldSource.None;
            return default;
        }

        float verticalForce = CalculateChargeNormalized(ctx.Now) * maxJumpForce * ctx.SnowJumpMul;
        return PerformJumpByContext(ctx, verticalForce, true);
    }

    public JumpActionResult TryPerformDedicatedShortJump(JumpContext ctx)
    {
        if (!CanStartJumpCharge(ctx))
            return default;

        ClearInstantSprintPreview();

        float verticalForce = shortJumpForce * ctx.SnowJumpMul;
        return PerformJumpByContext(ctx, verticalForce, false);
    }

    public JumpActionResult TryPerformInstantMaxChargedJump(JumpContext ctx)
    {
        if (!allowInstantMaxChargeFromSprint)
            return default;

        if (!ctx.SprintChargedJumpReady)
            return default;

        if (!CanStartJumpCharge(ctx))
            return default;

        if (showInstantSprintPreview && instantSprintPreviewDuration > 0f)
            ActivateInstantSprintPreview(ctx);
        else
            ClearInstantSprintPreview();

        float verticalForce = maxJumpForce * ctx.SnowJumpMul;
        return PerformJumpByContext(ctx, verticalForce, true);
    }

    public JumpActionResult TryConsumeJumpBuffer(
        JumpContext ctx,
        Func<PlayerInputModule.HoldSource, bool> isHoldInputStillHeld)
    {
        if (bufferedJumpKind == BufferedJumpKind.None)
            return default;

        if (ctx.Now - lastJumpPressedTime > jumpBufferTime)
        {
            bufferedJumpKind = BufferedJumpKind.None;
            bufferedHoldSource = PlayerInputModule.HoldSource.None;
            return default;
        }

        if (!CanStartJumpCharge(ctx))
            return default;

        if (bufferedJumpKind == BufferedJumpKind.Short)
        {
            JumpActionResult shortResult = TryPerformDedicatedShortJump(ctx);
            bufferedJumpKind = BufferedJumpKind.None;
            bufferedHoldSource = PlayerInputModule.HoldSource.None;
            return shortResult;
        }

        if (bufferedJumpKind == BufferedJumpKind.Hold)
        {
            PlayerInputModule.HoldSource source = bufferedHoldSource;
            if (source == PlayerInputModule.HoldSource.None)
                source = PlayerInputModule.HoldSource.Keyboard;

            if (allowInstantMaxChargeFromSprint && ctx.SprintChargedJumpReady)
            {
                JumpActionResult instantResult = TryPerformInstantMaxChargedJump(ctx);
                bufferedJumpKind = BufferedJumpKind.None;
                bufferedHoldSource = PlayerInputModule.HoldSource.None;
                return instantResult;
            }

            if (isHoldInputStillHeld != null && isHoldInputStillHeld(source))
            {
                BeginJumpHold(source, ctx.Now, false);
            }
            else
            {
                // Тап по кнопке заряда без удержания больше не превращаем в слабый прыжок.
                bufferedJumpKind = BufferedJumpKind.None;
                bufferedHoldSource = PlayerInputModule.HoldSource.None;
                return default;
            }

            bufferedJumpKind = BufferedJumpKind.None;
            bufferedHoldSource = PlayerInputModule.HoldSource.None;
        }

        return default;
    }

    public void CancelJumpCharge()
    {
        if (!isJumpHoldActive && !isChargingJump)
            return;

        ClearHoldState();
    }

    public void ResetJumpInputState()
    {
        lastJumpPressedTime = -999f;
        bufferedJumpKind = BufferedJumpKind.None;
        bufferedHoldSource = PlayerInputModule.HoldSource.None;

        jumpButtonDownTime = 0f;
        jumpStartHoldTime = 0f;

        ClearHoldState();
        ClearInstantSprintPreview();
    }

    public Vector2 GetPredictedJumpVelocity(JumpContext ctx)
    {
        if (IsInstantSprintPreviewActive())
            return instantSprintPreviewVelocity;

        float speedMul = IsFatigued(ctx.Now) ? ctx.FatigueSpeedMultiplier : 1f;

        float predictedVx =
            ctx.PlatformVX +
            (ctx.IsFacingRight ? 1f : -1f) * ctx.MoveSpeed * speedMul * ctx.SnowMoveMul +
            ctx.ExternalWindVX;

        float predictedVy;

        if (!isChargingJump)
        {
            predictedVy = shortJumpForce * ctx.SnowJumpMul;
        }
        else
        {
            float normalized = CalculateChargeNormalized(ctx.Now);
            predictedVy = normalized * maxJumpForce * ctx.SnowJumpMul;
        }

        return new Vector2(predictedVx, predictedVy);
    }

    private float CalculateChargeNormalized(float now)
    {
        float safeTimeLimit = Mathf.Max(0.0001f, jumpTimeLimit);
        float hold = Mathf.Clamp(now - jumpStartHoldTime, 0f, safeTimeLimit);
        return Mathf.Clamp01(hold / safeTimeLimit);
    }

    private JumpActionResult PerformJumpByContext(JumpContext ctx, float verticalForce, bool wasCharged)
    {
        ClearHoldState();
        bufferedJumpKind = BufferedJumpKind.None;
        bufferedHoldSource = PlayerInputModule.HoldSource.None;

        float speedMul = IsFatigued(ctx.Now) ? ctx.FatigueSpeedMultiplier : 1f;
        float takeoffVx =
            ctx.PlatformVX +
            (ctx.IsFacingRight ? 1f : -1f) * ctx.MoveSpeed * speedMul * ctx.SnowMoveMul;

        PerformJump(ctx.Rigidbody, ctx.Now, takeoffVx, verticalForce, ctx.ExternalWindVX);
        StartFatigue(ctx.Now);

        return new JumpActionResult
        {
            DidJump = true,
            TakeoffVx = takeoffVx,
            WasChargedJump = wasCharged
        };
    }

    private void ClearHoldState()
    {
        isJumpHoldActive = false;
        isChargingJump = false;
        currentHoldJumpSource = PlayerInputModule.HoldSource.None;
        jumpStartHoldTime = 0f;
        currentBarNormalized = 0f;
    }

    private void PerformJump(Rigidbody2D rb, float now, float vx, float vy, float externalWindVX)
    {
        lastJumpTime = now;
        lastAppliedJumpForce = vy;

        if (rb != null)
            rb.velocity = new Vector2(vx + externalWindVX, vy);
    }

    private void StartFatigue(float now)
    {
        fatigueEndTime = now + fatigueDuration;
    }

    private void ActivateInstantSprintPreview(JumpContext ctx)
    {
        float speedMul = IsFatigued(ctx.Now) ? ctx.FatigueSpeedMultiplier : 1f;

        float predictedVx =
            ctx.PlatformVX +
            (ctx.IsFacingRight ? 1f : -1f) * ctx.MoveSpeed * speedMul * ctx.SnowMoveMul +
            ctx.ExternalWindVX;

        float predictedVy = maxJumpForce * ctx.SnowJumpMul;

        instantSprintPreviewVelocity = new Vector2(predictedVx, predictedVy);
        instantSprintPreviewUntilUnscaled = Time.unscaledTime + Mathf.Max(0f, instantSprintPreviewDuration);
    }

    private bool IsInstantSprintPreviewActive()
    {
        return Time.unscaledTime < instantSprintPreviewUntilUnscaled;
    }

    private void ClearInstantSprintPreview()
    {
        instantSprintPreviewUntilUnscaled = -999f;
        instantSprintPreviewVelocity = Vector2.zero;
    }

    private void OnDisable()
    {
        ResetJumpInputState();
        fatigueEndTime = -999f;
        lastJumpTime = -999f;
        lastAppliedJumpForce = 0f;
        currentBarNormalized = 0f;
    }
}