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

    [SerializeField, Tooltip("Если ВКЛ — отдельный слабый прыжок разрешён во время спринтового движения.\nСпринтовым движением считаем активный спринт, его остаточную инерцию и skid-занос.\nЕсли ВЫКЛ — слабый прыжок в этом состоянии блокируется.")]
    private bool allowDedicatedShortJumpDuringSprint = false;

    [Header("Койот-тайм и буфер")]
    [SerializeField, Tooltip("Сколько секунд после схода с платформы ещё можно начать прыжок.")]
    private float coyoteTime = 0.05f;

    [SerializeField, Tooltip("Буфер прыжка: если нажать кнопку до приземления, прыжок сработает автоматически.")]
    private float jumpBufferTime = 0.1f;

    [Header("Усталость")]
    [SerializeField, Tooltip("Сколько длится усталость после прыжка.")]
    private float fatigueDuration = 0.8f;

    [Header("Бросок после вершины сильного прыжка")]
    [SerializeField, Tooltip("Если ВКЛ — после выхода сильного прыжка в вершину можно выполнить одноразовый бросок вниз.")]
    private bool enableApexThrowAfterChargedJump = true;

    [SerializeField, Tooltip("Если ВКЛ — бросок вниз доступен только после усиленного прыжка, а не после отдельного слабого.")]
    private bool apexThrowOnlyAfterChargedJump = true;

    [SerializeField, Min(0f), Tooltip("Минимальная сила последнего прыжка, чтобы вообще разрешить механику броска после вершины.")]
    private float apexThrowMinJumpForce = 6f;

    [SerializeField, Min(0f), Tooltip("Минимальная задержка после прыжка, прежде чем можно считать вершину достигнутой.")]
    private float apexThrowMinTimeAfterJump = 0.08f;

    [SerializeField, Min(0f), Tooltip("Когда вертикальная скорость станет меньше или равна этому порогу, считаем, что игрок дошёл до вершины/перешёл в спад.")]
    private float apexThrowEnterMaxUpwardSpeed = 0.2f;

    [SerializeField, Min(0f), Tooltip("Сколько секунд после входа в вершину разрешено нажать бросок вниз.")]
    private float apexThrowAvailableDuration = 0.9f;

    [SerializeField, Min(0f), Tooltip("Вертикальная скорость броска вниз.")]
    private float apexThrowDownwardSpeed = 16f;

    [SerializeField, Min(0f), Tooltip("Базовая горизонтальная скорость, с которой бросок уводит игрока в сторону.")]
    private float apexThrowHorizontalSpeed = 8f;

    [SerializeField, Range(0f, 1f), Tooltip("Если при броске нет горизонтального ввода, скорость в сторону лица будет умножена на этот коэффициент.")]
    private float apexThrowNeutralHorizontalMultiplier = 0.35f;

    [SerializeField, Range(0f, 1f), Tooltip("Минимальная сила горизонтального ввода, чтобы считать, что игрок реально выбирает траекторию броска.")]
    private float apexThrowHorizontalInputDeadZone = 0.2f;

    [SerializeField, Tooltip("Если ВКЛ — при отсутствии горизонтального ввода бросок пойдёт в сторону взгляда персонажа.")]
    private bool apexThrowUseFacingWhenNoInput = true;

    [SerializeField, Tooltip("Если ВКЛ — существующий скрипт траектории сможет показывать траекторию броска после вершины, используя тот же публичный интерфейс, что и у зарядного прыжка.")]
    private bool showApexThrowTrajectoryPreview = true;

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
        public bool IsSprintMovementActive;
        public Rigidbody2D Rigidbody;
    }

    public struct JumpActionResult
    {
        public bool DidJump;
        public float TakeoffVx;
        public bool WasChargedJump;
    }

    public struct ApexThrowResult
    {
        public bool DidThrow;
        public float AirVx;
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

    private bool apexThrowArmed = false;
    private bool apexThrowAvailable = false;
    private bool apexThrowUsed = false;
    private float apexThrowAvailableUntil = -999f;
    private float apexThrowPreviewAimX = 0f;
    private Vector2 apexThrowPreviewVelocity = Vector2.zero;

    public float CoyoteTime => coyoteTime;
    public bool IsJumpHoldActive => isJumpHoldActive;
    public bool IsChargingJump => isChargingJump;
    public bool IsChargingJumpPublic => isChargingJump || IsInstantSprintPreviewActive() || IsApexThrowTrajectoryPreviewActive();
    public bool IsChargeTrajectoryPreviewVisible => isChargingJump || IsInstantSprintPreviewActive();
    public bool IsApexThrowTrajectoryPreviewVisible => IsApexThrowTrajectoryPreviewActive();
    public float CurrentBarNormalized => currentBarNormalized;
    public float LastJumpTime => lastJumpTime;
    public float LastAppliedJumpForce => lastAppliedJumpForce;
    public PlayerInputModule.HoldSource CurrentHoldSource => currentHoldJumpSource;
    public bool AllowInstantMaxChargeFromSprint => allowInstantMaxChargeFromSprint;
    public bool IsJumpHoldActiveForPresentation => isJumpHoldActive || IsInstantSprintPreviewActive();
    public bool IsChargeVisualActive => isChargingJump || IsInstantSprintPreviewActive();
    public float ChargeBarNormalizedForPresentation => IsInstantSprintPreviewActive() ? 1f : currentBarNormalized;
    public bool IsApexThrowAvailable => CanUseApexThrowNow(Time.time);

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

    public bool CanAcceptDedicatedShortJumpInput(JumpContext ctx)
    {
        return allowDedicatedShortJumpDuringSprint || !ctx.IsSprintMovementActive;
    }

    private const float ApexThrowGroundedResetGrace = 0.12f;
    private const float ApexThrowRecentJumpUpVelocityEpsilon = 0.01f;

    public void UpdateApexThrowState(JumpContext ctx, float aimX)
    {
        if (!enableApexThrowAfterChargedJump)
        {
            ClearApexThrowState();
            return;
        }

        if (ctx.Rigidbody == null)
            return;

        bool isRecentlyAfterJump = (ctx.Now - lastJumpTime) <= ApexThrowGroundedResetGrace;
        bool isStillGoingUp = ctx.Rigidbody.velocity.y > ApexThrowRecentJumpUpVelocityEpsilon;

        bool ignoreStaleGroundedRightAfterJump =
            apexThrowArmed &&
            !apexThrowUsed &&
            isRecentlyAfterJump &&
            isStillGoingUp;

        if (ctx.IsGrounded && !ignoreStaleGroundedRightAfterJump)
        {
            ClearApexThrowState();
            return;
        }

        if (!apexThrowArmed || apexThrowUsed)
            return;

        if (!apexThrowAvailable)
        {
            if (ctx.Now - lastJumpTime < apexThrowMinTimeAfterJump)
                return;

            if (Mathf.Abs(lastAppliedJumpForce) < apexThrowMinJumpForce)
            {
                ClearApexThrowState();
                return;
            }

            if (ctx.Rigidbody.velocity.y > apexThrowEnterMaxUpwardSpeed)
                return;

            apexThrowAvailable = true;
            apexThrowAvailableUntil = ctx.Now + Mathf.Max(0f, apexThrowAvailableDuration);
        }

        if (!CanUseApexThrowNow(ctx.Now))
        {
            ClearApexThrowState();
            return;
        }

        apexThrowPreviewAimX = aimX;
        apexThrowPreviewVelocity = CalculateApexThrowVelocity(ctx, aimX);
    }

    public ApexThrowResult TryPerformApexThrow(JumpContext ctx, float aimX)
    {
        UpdateApexThrowState(ctx, aimX);

        if (!CanUseApexThrowNow(ctx.Now))
            return default;

        Vector2 velocity = CalculateApexThrowVelocity(ctx, aimX);

        if (ctx.Rigidbody != null)
            ctx.Rigidbody.velocity = velocity;

        lastJumpTime = ctx.Now;
        lastAppliedJumpForce = Mathf.Abs(velocity.y);

        apexThrowUsed = true;
        apexThrowArmed = false;
        apexThrowAvailable = false;
        apexThrowAvailableUntil = -999f;
        apexThrowPreviewVelocity = Vector2.zero;

        return new ApexThrowResult
        {
            DidThrow = true,
            AirVx = velocity.x - ctx.ExternalWindVX
        };
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
        if (!CanAcceptDedicatedShortJumpInput(ctx))
            return default;

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
        ClearApexThrowState();
    }

    public Vector2 GetPredictedJumpVelocity(JumpContext ctx)
    {
        if (IsApexThrowTrajectoryPreviewActive())
            return GetPredictedApexThrowTrajectoryVelocity(ctx);

        return GetPredictedChargeTrajectoryVelocity(ctx);
    }

    public Vector2 GetPredictedChargeTrajectoryVelocity(JumpContext ctx)
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

    public Vector2 GetPredictedApexThrowTrajectoryVelocity(JumpContext ctx)
    {
        if (!IsApexThrowTrajectoryPreviewActive())
            return Vector2.zero;

        if (apexThrowPreviewVelocity.sqrMagnitude > 0.000001f)
            return apexThrowPreviewVelocity;

        return CalculateApexThrowVelocity(ctx, apexThrowPreviewAimX);
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
        ArmApexThrowState(wasCharged, ctx.IsFacingRight);

        return new JumpActionResult
        {
            DidJump = true,
            TakeoffVx = takeoffVx,
            WasChargedJump = wasCharged
        };
    }

    private void ArmApexThrowState(bool wasChargedJump, bool isFacingRight)
    {
        bool canArm =
            enableApexThrowAfterChargedJump &&
            (!apexThrowOnlyAfterChargedJump || wasChargedJump);

        if (!canArm)
        {
            ClearApexThrowState();
            return;
        }

        apexThrowArmed = true;
        apexThrowAvailable = false;
        apexThrowUsed = false;
        apexThrowAvailableUntil = -999f;
        apexThrowPreviewAimX = isFacingRight ? 1f : -1f;
        apexThrowPreviewVelocity = Vector2.zero;
    }

    private bool CanUseApexThrowNow(float now)
    {
        return enableApexThrowAfterChargedJump &&
               apexThrowArmed &&
               apexThrowAvailable &&
               !apexThrowUsed &&
               now <= apexThrowAvailableUntil;
    }

    private Vector2 CalculateApexThrowVelocity(JumpContext ctx, float aimX)
    {
        float aimDir = 0f;

        if (Mathf.Abs(aimX) > apexThrowHorizontalInputDeadZone)
        {
            aimDir = Mathf.Sign(aimX);
        }
        else if (apexThrowUseFacingWhenNoInput)
        {
            aimDir = ctx.IsFacingRight ? 1f : -1f;
        }

        float horizontalSpeed = apexThrowHorizontalSpeed;

        if (Mathf.Abs(aimX) <= apexThrowHorizontalInputDeadZone)
            horizontalSpeed *= apexThrowNeutralHorizontalMultiplier;

        float finalVx = ctx.PlatformVX + aimDir * horizontalSpeed + ctx.ExternalWindVX;
        float finalVy = -Mathf.Abs(apexThrowDownwardSpeed);

        return new Vector2(finalVx, finalVy);
    }

    private bool IsApexThrowTrajectoryPreviewActive()
    {
        return showApexThrowTrajectoryPreview && CanUseApexThrowNow(Time.time);
    }

    private void ClearApexThrowState()
    {
        apexThrowArmed = false;
        apexThrowAvailable = false;
        apexThrowUsed = false;
        apexThrowAvailableUntil = -999f;
        apexThrowPreviewAimX = 0f;
        apexThrowPreviewVelocity = Vector2.zero;
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
        ClearApexThrowState();
    }
}