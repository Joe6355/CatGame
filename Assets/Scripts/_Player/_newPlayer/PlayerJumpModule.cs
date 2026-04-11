using UnityEngine;

[DisallowMultipleComponent]
public class PlayerJumpModule : MonoBehaviour
{
    [Header("Основной прыжок")]
    [SerializeField, Tooltip("Начальная вертикальная скорость прыжка.")]
    private float jumpForce = 12f;

    [Header("Контролируемая высота прыжка")]
    [SerializeField, Range(0f, 1f), Tooltip("Во сколько раз режется текущая скорость вверх, если кнопку отпустили рано.\nНиже = короче тап-прыжок.")]
    private float jumpReleaseVerticalMultiplier = 0.5f;

    [SerializeField, Min(0f), Tooltip("Максимальное время, сколько удержание может продлевать подъём.")]
    private float maxJumpHoldTime = 0.16f;

    [SerializeField, Min(0f), Tooltip("Насколько активно во время удержания поддерживается вертикальная скорость вверх.")]
    private float jumpHoldAcceleration = 110f;

    [SerializeField, Min(0f), Tooltip("К какой доле стартовой скорости тянем прыжок во время удержания.")]
    private float jumpHoldTargetVelocityMultiplier = 1.08f;

    [SerializeField, Min(0f), Tooltip("Если скорость вверх стала меньше этого порога, фаза удержания завершается.")]
    private float minUpwardSpeedForHeldJump = 0.15f;

    [SerializeField, Min(0f), Tooltip("Сколько секунд после старта прыжка игнорируем устаревшее grounded-состояние.\nНужно, чтобы контролируемая высота не умирала в первый же момент после отрыва.")]
    private float jumpHoldGroundedIgnoreTime = 0.1f;

    [Header("Задержка между прыжками")]
    [SerializeField, Range(0f, 5f), Tooltip("Минимальная задержка между стартами двух прыжков.")]
    private float jumpRepeatCooldown = 0.08f;

    [Header("Спринт -> усиленный прыжок")]
    [SerializeField, Tooltip("Если ВКЛ — во время спринтового движения основной прыжок усиливается.")]
    private bool boostJumpDuringSprint = true;

    [SerializeField, Min(1f), Tooltip("Множитель силы прыжка во время спринта.")]
    private float sprintJumpMultiplier = 1.2f;

    [Header("Койот-тайм и буфер")]
    [SerializeField, Tooltip("Сколько секунд после схода с платформы ещё можно нажать прыжок.")]
    private float coyoteTime = 0.12f;

    [SerializeField, Tooltip("Если кнопку прыжка нажали чуть раньше приземления, прыжок сработает автоматически.")]
    private float jumpBufferTime = 0.1f;

    [Header("Orb: рефреш обычного прыжка")]
    [SerializeField, Tooltip("Если ВКЛ — специальные орбы могут в воздухе выдавать ещё один обычный прыжок.")]
    private bool enableOrbJumpRefresh = true;

    [SerializeField, Min(1), Tooltip("Максимум зарядов orb-рефреша, которые можно держать одновременно.")]
    private int maxOrbJumpRefreshCharges = 1;

    [Header("Бросок после вершины прыжка")]
    [SerializeField, Tooltip("Если ВКЛ — после выхода в вершину прыжка можно выполнить одноразовый бросок вниз.")]
    private bool enableApexThrowAfterJump = true;

    [SerializeField, Min(0f), Tooltip("Минимальная сила последнего прыжка, чтобы разрешить apex throw.")]
    private float apexThrowMinJumpForce = 0.1f;

    [SerializeField, Min(0f), Tooltip("Минимальная задержка после прыжка, прежде чем можно искать вершину.")]
    private float apexThrowMinTimeAfterJump = 0f;

    [SerializeField, Min(0f), Tooltip("Когда вертикальная скорость станет меньше или равна этому порогу, считаем, что достигнута вершина/начался спад.")]
    private float apexThrowEnterMaxUpwardSpeed = 5f;

    [SerializeField, Min(0f), Tooltip("Сколько секунд после входа в вершину разрешено нажать бросок вниз.")]
    private float apexThrowAvailableDuration = 2f;

    [SerializeField, Min(0f), Tooltip("Вертикальная скорость броска вниз.")]
    private float apexThrowDownwardSpeed = 20f;

    [SerializeField, Tooltip("Если ВКЛ — JumpTrajectory2D сможет показывать новую траекторию apex throw.")]
    private bool showApexThrowTrajectoryPreview = true;

    public struct JumpContext
    {
        public float Now;
        public bool IsGrounded;
        public float LastGroundedTime;
        public bool IsFacingRight;
        public float MoveSpeed;
        public float PlatformVX;
        public float SnowMoveMul;
        public float SnowJumpMul;
        public float ExternalWindVX;
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
    private bool hasBufferedJump = false;
    private PlayerInputModule.HoldSource bufferedHoldSource = PlayerInputModule.HoldSource.None;

    private float lastJumpTime = -999f;
    private float lastAppliedJumpForce = 0f;

    private bool controlledJumpActive = false;
    private PlayerInputModule.HoldSource activeJumpHoldSource = PlayerInputModule.HoldSource.None;
    private float controlledJumpElapsed = 0f;
    private float controlledJumpMaxDuration = 0f;
    private float controlledJumpTargetUpSpeed = 0f;
    private float controlledJumpAcceleration = 0f;
    private bool controlledJumpCutConsumed = false;

    private int orbJumpRefreshCharges = 0;
    private float orbRefreshGrantedAt = -999f;

    private bool apexThrowArmed = false;
    private bool apexThrowAvailable = false;
    private bool apexThrowUsed = false;
    private float apexThrowAvailableUntil = -999f;
    private float apexThrowPreviewAimX = 0f;
    private Vector2 apexThrowPreviewVelocity = Vector2.zero;
    private float apexThrowLockedHorizontalDir = 0f;

    private const float ApexThrowGroundedResetGrace = 0.12f;
    private const float ApexThrowRecentJumpUpVelocityEpsilon = 0.01f;

    public float CoyoteTime => coyoteTime;
    public bool IsJumpHoldActive => controlledJumpActive;
    public bool IsChargingJump => false;
    public bool IsChargingJumpPublic => false;
    public bool IsChargeTrajectoryPreviewVisible => false;
    public bool IsApexThrowTrajectoryPreviewVisible => IsApexThrowTrajectoryPreviewActive();
    public float CurrentBarNormalized => 0f;
    public float LastJumpTime => lastJumpTime;
    public float LastAppliedJumpForce => lastAppliedJumpForce;
    public PlayerInputModule.HoldSource CurrentHoldSource => activeJumpHoldSource;
    public bool AllowInstantMaxChargeFromSprint => false;
    public bool IsJumpHoldActiveForPresentation => controlledJumpActive;
    public bool IsChargeVisualActive => false;
    public float ChargeBarNormalizedForPresentation => 0f;
    public bool IsApexThrowAvailable => CanUseApexThrowNow(Time.time);

    public int OrbJumpRefreshCharges => orbJumpRefreshCharges;
    public bool HasOrbJumpRefresh => enableOrbJumpRefresh && orbJumpRefreshCharges > 0;
    public bool CanReceiveOrbJumpRefresh =>
        enableOrbJumpRefresh &&
        orbJumpRefreshCharges < Mathf.Max(1, maxOrbJumpRefreshCharges);

    private void OnValidate()
    {
        jumpForce = Mathf.Max(0f, jumpForce);

        jumpReleaseVerticalMultiplier = Mathf.Clamp01(jumpReleaseVerticalMultiplier);
        maxJumpHoldTime = Mathf.Max(0f, maxJumpHoldTime);
        jumpHoldAcceleration = Mathf.Max(0f, jumpHoldAcceleration);
        jumpHoldTargetVelocityMultiplier = Mathf.Max(1f, jumpHoldTargetVelocityMultiplier);
        minUpwardSpeedForHeldJump = Mathf.Max(0f, minUpwardSpeedForHeldJump);
        jumpHoldGroundedIgnoreTime = Mathf.Max(0f, jumpHoldGroundedIgnoreTime);

        jumpRepeatCooldown = Mathf.Max(0f, jumpRepeatCooldown);
        sprintJumpMultiplier = Mathf.Max(1f, sprintJumpMultiplier);

        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);

        maxOrbJumpRefreshCharges = Mathf.Max(1, maxOrbJumpRefreshCharges);

        apexThrowMinJumpForce = Mathf.Max(0f, apexThrowMinJumpForce);
        apexThrowMinTimeAfterJump = Mathf.Max(0f, apexThrowMinTimeAfterJump);
        apexThrowEnterMaxUpwardSpeed = Mathf.Max(0f, apexThrowEnterMaxUpwardSpeed);
        apexThrowAvailableDuration = Mathf.Max(0f, apexThrowAvailableDuration);
        apexThrowDownwardSpeed = Mathf.Max(0f, apexThrowDownwardSpeed);
    }

    public bool IsWithinGroundedJumpWindow(JumpContext ctx)
    {
        return ctx.IsGrounded || (ctx.Now - ctx.LastGroundedTime) <= coyoteTime;
    }

    public bool GrantOrbJumpRefresh(float now, int amount = 1)
    {
        if (!enableOrbJumpRefresh)
            return false;

        int maxCharges = Mathf.Max(1, maxOrbJumpRefreshCharges);
        if (orbJumpRefreshCharges >= maxCharges)
            return false;

        amount = Mathf.Max(1, amount);
        orbJumpRefreshCharges = Mathf.Clamp(orbJumpRefreshCharges + amount, 0, maxCharges);
        orbRefreshGrantedAt = now;
        return true;
    }

    public void MarkJumpPressed(PlayerInputModule.HoldSource source, float now)
    {
        lastJumpPressedTime = now;
        hasBufferedJump = true;
        bufferedHoldSource = source == PlayerInputModule.HoldSource.None
            ? PlayerInputModule.HoldSource.Keyboard
            : source;
    }

    public JumpActionResult TryConsumeJumpBuffer(JumpContext ctx)
    {
        TryClearOrbJumpRefreshOnGround(ctx);

        if (!hasBufferedJump)
            return default;

        if (ctx.Now - lastJumpPressedTime > jumpBufferTime)
        {
            hasBufferedJump = false;
            bufferedHoldSource = PlayerInputModule.HoldSource.None;
            return default;
        }

        if (!CanStartMainJump(ctx))
            return default;

        bool consumeOrbRefresh = ShouldConsumeOrbRefreshForThisJump(ctx);

        PlayerInputModule.HoldSource source = bufferedHoldSource;
        hasBufferedJump = false;
        bufferedHoldSource = PlayerInputModule.HoldSource.None;

        return PerformMainJumpByContext(ctx, source, consumeOrbRefresh);
    }

    public void UpdateJumpHold(JumpContext ctx, bool isButtonStillHeld, float deltaTime)
    {
        TryClearOrbJumpRefreshOnGround(ctx);

        if (!controlledJumpActive)
            return;

        Rigidbody2D rb = ctx.Rigidbody;
        if (rb == null)
        {
            StopControlledJump();
            return;
        }

        float dt = Mathf.Max(0f, deltaTime);
        float timeSinceJump = ctx.Now - lastJumpTime;
        bool ignoreGroundedBecauseJustJumped = timeSinceJump <= jumpHoldGroundedIgnoreTime;

        if (ctx.IsGrounded && !ignoreGroundedBecauseJustJumped)
        {
            StopControlledJump();
            return;
        }

        float currentVy = rb.velocity.y;
        if (currentVy <= minUpwardSpeedForHeldJump)
        {
            StopControlledJump();
            return;
        }

        controlledJumpElapsed += dt;
        bool withinHoldWindow = controlledJumpElapsed <= controlledJumpMaxDuration;

        if (!isButtonStillHeld)
        {
            TryCutControlledJump(rb, currentVy);
            StopControlledJump();
            return;
        }

        if (!withinHoldWindow)
        {
            StopControlledJump();
            return;
        }

        SustainControlledJump(rb, currentVy, dt);
    }

    public void UpdateApexThrowState(JumpContext ctx, float aimX)
    {
        TryClearOrbJumpRefreshOnGround(ctx);

        if (!enableApexThrowAfterJump)
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

        StopControlledJump();

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

    public void CancelJumpCharge()
    {
        // Заряда больше нет.
    }

    public void ResetJumpInputState()
    {
        lastJumpPressedTime = -999f;
        hasBufferedJump = false;
        bufferedHoldSource = PlayerInputModule.HoldSource.None;

        StopControlledJump();
        ClearApexThrowState();
    }

    public Vector2 GetPredictedJumpVelocity(JumpContext ctx)
    {
        if (IsApexThrowTrajectoryPreviewActive())
            return GetPredictedApexThrowTrajectoryVelocity(ctx);

        return Vector2.zero;
    }

    public Vector2 GetPredictedChargeTrajectoryVelocity(JumpContext ctx)
    {
        return Vector2.zero;
    }

    public Vector2 GetPredictedApexThrowTrajectoryVelocity(JumpContext ctx)
    {
        if (!IsApexThrowTrajectoryPreviewActive())
            return Vector2.zero;

        if (apexThrowPreviewVelocity.sqrMagnitude > 0.000001f)
            return apexThrowPreviewVelocity;

        return CalculateApexThrowVelocity(ctx, apexThrowPreviewAimX);
    }

    private bool CanStartMainJump(JumpContext ctx)
    {
        if ((ctx.Now - lastJumpTime) < jumpRepeatCooldown)
            return false;

        if (IsWithinGroundedJumpWindow(ctx))
            return true;

        if (CanUseOrbRefreshWithCurrentPress(ctx))
            return true;

        return false;
    }

    private bool CanUseOrbRefreshWithCurrentPress(JumpContext ctx)
    {
        if (!enableOrbJumpRefresh)
            return false;

        if (orbJumpRefreshCharges <= 0)
            return false;

        if (IsWithinGroundedJumpWindow(ctx))
            return false;

        // Важно: орб не должен срабатывать от прыжка, который был нажат ДО касания орба.
        if (lastJumpPressedTime < orbRefreshGrantedAt)
            return false;

        return true;
    }

    private bool ShouldConsumeOrbRefreshForThisJump(JumpContext ctx)
    {
        return !IsWithinGroundedJumpWindow(ctx) && CanUseOrbRefreshWithCurrentPress(ctx);
    }

    private JumpActionResult PerformMainJumpByContext(
        JumpContext ctx,
        PlayerInputModule.HoldSource source,
        bool consumeOrbRefresh)
    {
        if (ctx.Rigidbody == null)
            return default;

        float currentWorldVx = ctx.Rigidbody.velocity.x;
        float takeoffVx = currentWorldVx - ctx.ExternalWindVX;

        float sprintMul = boostJumpDuringSprint && ctx.IsSprintMovementActive
            ? sprintJumpMultiplier
            : 1f;

        float verticalForce = jumpForce * ctx.SnowJumpMul * sprintMul;

        if (consumeOrbRefresh)
            ConsumeOneOrbRefresh();

        PerformJump(ctx.Rigidbody, ctx.Now, currentWorldVx, verticalForce);
        StartControlledJump(source, verticalForce);
        ArmApexThrowState(currentWorldVx, ctx.IsFacingRight);

        return new JumpActionResult
        {
            DidJump = true,
            TakeoffVx = takeoffVx,
            WasChargedJump = false
        };
    }

    private void ConsumeOneOrbRefresh()
    {
        if (orbJumpRefreshCharges <= 0)
            return;

        orbJumpRefreshCharges = Mathf.Max(0, orbJumpRefreshCharges - 1);
        if (orbJumpRefreshCharges == 0)
            orbRefreshGrantedAt = -999f;
    }

    private void ClearOrbRefresh()
    {
        orbJumpRefreshCharges = 0;
        orbRefreshGrantedAt = -999f;
    }

    private void TryClearOrbJumpRefreshOnGround(JumpContext ctx)
    {
        if (!enableOrbJumpRefresh)
            return;

        float timeSinceJump = ctx.Now - lastJumpTime;
        bool ignoreGroundedBecauseJustJumped = timeSinceJump <= jumpHoldGroundedIgnoreTime;

        if (ctx.IsGrounded && !ignoreGroundedBecauseJustJumped)
            ClearOrbRefresh();
    }

    private void StartControlledJump(PlayerInputModule.HoldSource source, float takeoffUpSpeed)
    {
        controlledJumpActive = true;
        activeJumpHoldSource = source == PlayerInputModule.HoldSource.None
            ? PlayerInputModule.HoldSource.Keyboard
            : source;

        controlledJumpElapsed = 0f;
        controlledJumpMaxDuration = Mathf.Max(0f, maxJumpHoldTime);
        controlledJumpTargetUpSpeed = Mathf.Max(takeoffUpSpeed, takeoffUpSpeed * jumpHoldTargetVelocityMultiplier);
        controlledJumpAcceleration = Mathf.Max(0f, jumpHoldAcceleration);
        controlledJumpCutConsumed = false;
    }

    private void SustainControlledJump(Rigidbody2D rb, float currentVy, float deltaTime)
    {
        float targetVy = Mathf.Max(0f, controlledJumpTargetUpSpeed);
        float accel = Mathf.Max(0f, controlledJumpAcceleration);

        if (targetVy <= 0f || accel <= 0f || deltaTime <= 0f)
            return;

        if (currentVy >= targetVy)
            return;

        float newVy = Mathf.Min(targetVy, currentVy + accel * deltaTime);
        rb.velocity = new Vector2(rb.velocity.x, newVy);
        lastAppliedJumpForce = Mathf.Max(lastAppliedJumpForce, newVy);
    }

    private void TryCutControlledJump(Rigidbody2D rb, float currentVy)
    {
        if (rb == null)
            return;

        if (controlledJumpCutConsumed)
            return;

        if (currentVy <= 0f)
            return;

        float cutMul = Mathf.Clamp01(jumpReleaseVerticalMultiplier);
        float newVy = currentVy * cutMul;

        rb.velocity = new Vector2(rb.velocity.x, newVy);
        controlledJumpCutConsumed = true;
    }

    private void StopControlledJump()
    {
        controlledJumpActive = false;
        activeJumpHoldSource = PlayerInputModule.HoldSource.None;
        controlledJumpElapsed = 0f;
        controlledJumpMaxDuration = 0f;
        controlledJumpTargetUpSpeed = 0f;
        controlledJumpAcceleration = 0f;
        controlledJumpCutConsumed = false;
    }

    private void ArmApexThrowState(float takeoffWorldVx, bool isFacingRight)
    {
        if (!enableApexThrowAfterJump)
        {
            ClearApexThrowState();
            return;
        }

        apexThrowArmed = true;
        apexThrowAvailable = false;
        apexThrowUsed = false;
        apexThrowAvailableUntil = -999f;

        float lockedDir = Mathf.Abs(takeoffWorldVx) > 0.001f
            ? Mathf.Sign(takeoffWorldVx)
            : (isFacingRight ? 1f : -1f);

        apexThrowLockedHorizontalDir = lockedDir;
        apexThrowPreviewAimX = lockedDir;
        apexThrowPreviewVelocity = Vector2.zero;
    }

    private bool CanUseApexThrowNow(float now)
    {
        return enableApexThrowAfterJump &&
               apexThrowArmed &&
               apexThrowAvailable &&
               !apexThrowUsed &&
               now <= apexThrowAvailableUntil;
    }

    private Vector2 CalculateApexThrowVelocity(JumpContext ctx, float aimX)
    {
        float finalVx = ctx.ExternalWindVX;
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
        apexThrowLockedHorizontalDir = 0f;
    }

    private void PerformJump(Rigidbody2D rb, float now, float worldVx, float vy)
    {
        lastJumpTime = now;
        lastAppliedJumpForce = vy;

        if (rb != null)
            rb.velocity = new Vector2(worldVx, vy);
    }

    private void OnDisable()
    {
        ResetJumpInputState();
        lastJumpTime = -999f;
        lastAppliedJumpForce = 0f;
        ClearOrbRefresh();
        StopControlledJump();
        ClearApexThrowState();
    }
}