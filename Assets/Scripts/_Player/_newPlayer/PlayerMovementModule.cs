using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMovementModule : MonoBehaviour
{
    public struct MovementContext
    {
        public float Now;
        public float FixedDeltaTime;
        public Rigidbody2D Rigidbody;
        public float InputX;
        public bool IsGrounded;
        public bool IsOnIce;
        public Collider2D LastGroundCollider;
        public float PlatformVX;
        public float ExternalWindVX;
        public float SnowMoveMul;

        // Оставлено только для совместимости со старыми вызовами.
        // Больше нигде не используется.
        public bool IsFatigued;

        public bool IsJumpCharging;
    }

    [Header("Движение")]
    [SerializeField, Tooltip("Базовая скорость бега по земле без учёта спринта, льда и снега.")]
    private float moveSpeed = 10f;

    [Header("Управление в воздухе")]
    [SerializeField, Tooltip("Если ВКЛ — в воздухе можно корректировать движение влево/вправо.")]
    private bool enableAirControl = true;

    [SerializeField, Range(0f, 1f), Tooltip("Максимальная скорость в воздухе как ползунок.\nЛевее = слабее, правее = быстрее.")]
    private float airSpeedSlider = 0.72f;

    [SerializeField, Range(0f, 1f), Tooltip("Насколько быстро персонаж добирает скорость в воздухе в ту же сторону.")]
    private float airSameDirectionResponse = 0.42f;

    [SerializeField, Range(0f, 1f), Tooltip("Насколько быстро разрешён разворот в другую сторону в воздухе.\nНиже = меньше резких разворотов.")]
    private float airReverseResponse = 0.20f;

    [SerializeField, Range(0f, 1f), Tooltip("Насколько быстро в воздухе гасится горизонтальная скорость, когда игрок отпустил A/D.")]
    private float airNoInputDamping = 0.16f;

    [SerializeField, Range(0f, 1f), Tooltip("Сколько собственной инерции сохраняется в воздухе.\nПравее = больше сохраняем старую скорость.")]
    private float airMomentumPreservation = 0.78f;

    [SerializeField, Range(0f, 1f), Tooltip("Мёртвая зона горизонтального ввода для воздуха.")]
    private float airControlInputDeadZone = 0.05f;

    [Header("Инерция сразу после отрыва")]
    [SerializeField, Min(0f), Tooltip("Сколько времени после старта прыжка управление в воздухе остаётся слегка приглушённым.")]
    private float jumpAirInertiaDuration = 0.10f;

    [SerializeField, Range(0f, 1f), Tooltip("Насколько сильно приглушать air control сразу после отрыва.\nНиже = больше инерции в начале прыжка.")]
    private float jumpAirInertiaControlMultiplier = 0.45f;

    [Header("Спринт")]
    [SerializeField, Min(0f), Tooltip("Задержка перед началом спринта.")]
    private float sprintStartDelay = 0.35f;

    [SerializeField, Min(0f), Tooltip("Сколько секунд занимает плавный разгон от обычной скорости до пика спринта после задержки.")]
    private float sprintRampDuration = 0.35f;

    [SerializeField, Min(1f), Tooltip("Пиковый множитель скорости спринта относительно moveSpeed.")]
    private float sprintSpeedMultiplier = 1.5f;

    [SerializeField, Range(0f, 1f), Tooltip("Минимальная сила горизонтального ввода, чтобы считать, что направление реально удерживается для спринта.")]
    private float sprintInputDeadZone = 0.2f;

    [Header("Анти-спринт в стену")]
    [SerializeField, Tooltip("Если ВКЛ — спринт не будет накапливаться, пока перед персонажем по ходу движения есть стена.")]
    private bool blockSprintIntoWall = true;

    [SerializeField, Min(0.01f), Tooltip("На сколько далеко вперёд от коллайдера проверять стену для блокировки спринта.")]
    private float sprintWallCheckDistance = 0.08f;

    [SerializeField, Range(0f, 1f), Tooltip("Насколько 'боковой' должна быть нормаль препятствия, чтобы считать его стеной для блокировки спринта.")]
    private float sprintWallNormalMinAbsX = 0.45f;

    [Header("Debug Gizmos")]
    [SerializeField, Tooltip("Если ВКЛ — в Scene view будет рисоваться зона проверки стены для анти-спринта.")]
    private bool drawSprintWallCheckGizmo = true;

    [SerializeField, Tooltip("Если ВКЛ — gizmo рисуется всегда. Если ВЫКЛ — только когда объект выделен.")]
    private bool drawSprintWallCheckAlways = false;

    [Header("Инерция спринта")]
    [SerializeField, Tooltip("Если ВКЛ — после спринта персонаж не останавливается мгновенно, а продолжает катиться по инерции.")]
    private bool enableSprintInertia = true;

    [SerializeField, Min(0f), Tooltip("Сколько секунд примерно длится затухание остаточного импульса спринта после отпускания кнопки направления.")]
    private float sprintReleaseInertiaDuration = 0.28f;

    [SerializeField, Min(0f), Tooltip("Сколько секунд примерно гасится старый импульс спринта, если во время спринта нажали противоположное направление.")]
    private float sprintReverseInertiaDuration = 0.18f;

    [SerializeField, Range(0f, 1f), Tooltip("Минимальный уровень набранного спринта, после которого вообще разрешено оставлять остаточную инерцию.")]
    private float sprintInertiaMinBlend = 0.2f;

    [Header("Skid / занос (заглушка)")]
    [SerializeField, Tooltip("Если ВКЛ — модуль будет вычислять состояние заноса/торможения со спринта.")]
    private bool enableSprintSkidPlaceholder = true;

    [SerializeField, Range(0f, 1f), Tooltip("Минимальный уровень спринта/инерции, с которого вообще разрешено входить в skid state.")]
    private float skidMinEnterBlend = 0.35f;

    [SerializeField, Min(0f), Tooltip("Минимальная доля от базовой скорости moveSpeed, чтобы считать торможение 'настоящим заносом'.")]
    private float skidMinSpeedRatio = 0.65f;

    [SerializeField, Min(0f), Tooltip("Минимальное время удержания skid state после входа.")]
    private float skidMinHoldTime = 0.08f;

    [SerializeField, Min(0f), Tooltip("При какой доле от moveSpeed можно окончательно выйти из skid state.")]
    private float skidExitSpeedRatio = 0.15f;

    [Header("Лёд (Tag = \"Ice\")")]
    [SerializeField, Tooltip("Ускорение на льду.")]
    private float iceAccel = 2.5f;

    [SerializeField, Tooltip("Торможение на льду при смене направления.")]
    private float iceBrake = 1.2f;

    [SerializeField, Tooltip("Максимальная скорость на льду как множитель от moveSpeed.")]
    private float iceMaxSpeedMul = 1.15f;

    [SerializeField, Tooltip("Ускорение на обычной земле.")]
    private float normalAccel = 9999f;

    [SerializeField, Tooltip("Торможение/разворот на обычной земле.")]
    private float normalBrake = 9999f;

    [Header("Визуальный разворот")]
    [SerializeField, Tooltip("Минимальная |скорость| или |ввод| по X, после которой разрешён разворот персонажа.")]
    private float flipDeadZone = 0.05f;

    [SerializeField, Tooltip("Если ВКЛ — персонаж может менять сторону взгляда в воздухе.")]
    private bool allowFlipInAir = true;

    private bool isFacingRight = true;

    private float sprintHeldDirection = 0f;
    private float sprintHeldTime = 0f;
    private float sprintBlend = 0f;

    private float sprintMomentumDirection = 0f;
    private float sprintMomentumBlend = 0f;

    private bool isSprintSkidActive = false;
    private float sprintSkidDirection = 0f;
    private float skidHoldUntil = -999f;

    private bool isForwardBlockedForSprint = false;

    private float jumpAirInertiaUntil = -999f;

    private Collider2D bodyCollider;
    private readonly RaycastHit2D[] sprintWallHits = new RaycastHit2D[8];
    private ContactFilter2D sprintWallFilter;

    public float MoveSpeed => moveSpeed;
    public float CurrentSprintMultiplier => Mathf.Lerp(1f, sprintSpeedMultiplier, GetEffectiveSprintBlend());
    public float CurrentMoveSpeed => moveSpeed * CurrentSprintMultiplier;

    // Совместимость со старым кодом/ссылками.
    public float FatigueSpeedMultiplier => 1f;

    public bool IsFacingRight => isFacingRight;
    public bool AllowFlipInAir => allowFlipInAir;

    public bool IsSprintReady => sprintBlend >= 0.999f && !isForwardBlockedForSprint;
    public bool IsSprintActive => sprintBlend > 0.0001f;
    public bool HasSprintMomentum => sprintMomentumBlend > 0.0001f;
    public bool IsSprintSkidActive => isSprintSkidActive;
    public float SprintSkidDirection => sprintSkidDirection;
    public float SprintCameraBlend => Mathf.Clamp01(GetEffectiveSprintBlend());
    public bool IsForwardBlockedForSprint => isForwardBlockedForSprint;
    public bool IsSprintMovementActive => GetEffectiveSprintBlend() > 0.0001f || isSprintSkidActive;

    private void Reset()
    {
        CacheComponents();
        ConfigureWallCastFilter();
    }

    private void Awake()
    {
        CacheComponents();
        ConfigureWallCastFilter();
    }

    private void OnValidate()
    {
        CacheComponents();
        ConfigureWallCastFilter();

        sprintWallCheckDistance = Mathf.Max(0.01f, sprintWallCheckDistance);
        sprintWallNormalMinAbsX = Mathf.Clamp01(sprintWallNormalMinAbsX);
        flipDeadZone = Mathf.Max(0f, flipDeadZone);

        airSpeedSlider = Mathf.Clamp01(airSpeedSlider);
        airSameDirectionResponse = Mathf.Clamp01(airSameDirectionResponse);
        airReverseResponse = Mathf.Clamp01(airReverseResponse);
        airNoInputDamping = Mathf.Clamp01(airNoInputDamping);
        airMomentumPreservation = Mathf.Clamp01(airMomentumPreservation);
        airControlInputDeadZone = Mathf.Clamp01(airControlInputDeadZone);

        jumpAirInertiaDuration = Mathf.Max(0f, jumpAirInertiaDuration);
        jumpAirInertiaControlMultiplier = Mathf.Clamp01(jumpAirInertiaControlMultiplier);
    }

    public void AllowAirControlFor(float duration)
    {
        // Совместимый stub оставлен намеренно.
    }

    public void OnJumpPerformed(float takeoffVx)
    {
        jumpAirInertiaUntil = Time.time + Mathf.Max(0f, jumpAirInertiaDuration);
        ClearSprintSkid();
    }

    public void SetAirVx(float vx)
    {
        // Совместимый stub оставлен намеренно.
    }

    public void ResetSprint()
    {
        ResetActiveSprintState();
        ClearSprintMomentum();
        ClearSprintSkid();
        isForwardBlockedForSprint = false;
        jumpAirInertiaUntil = -999f;
    }

    public void TryFaceByInput(float inputX, bool allowFlip, bool isGrounded)
    {
        if (!allowFlip)
            return;

        if (!isGrounded && !allowFlipInAir)
            return;

        if (Mathf.Abs(inputX) <= flipDeadZone)
            return;

        bool faceRight = inputX > 0f;
        if (faceRight != isFacingRight)
            Flip();
    }

    public void RefreshImmediateSprintBlocker(bool isGrounded, float inputX)
    {
        if (!blockSprintIntoWall || !isGrounded || Mathf.Abs(inputX) <= sprintInputDeadZone)
        {
            isForwardBlockedForSprint = false;
            return;
        }

        isForwardBlockedForSprint = IsForwardBlockedByWall(Mathf.Sign(inputX));
    }

    public void ApplyMovement(MovementContext ctx)
    {
        if (ctx.Rigidbody == null)
            return;

        UpdateSprintState(ctx);
        UpdateSprintSkidPlaceholder(ctx);

        if (ctx.IsJumpCharging)
        {
            bool onMovingGroundByEffector =
                ctx.IsGrounded &&
                ctx.LastGroundCollider != null &&
                ctx.LastGroundCollider.GetComponent<SurfaceEffector2D>() != null;

            bool carriedByPlatform = ctx.IsGrounded && Mathf.Abs(ctx.PlatformVX) > 0.0001f;

            if (ctx.IsGrounded && !ctx.IsOnIce && !onMovingGroundByEffector && !carriedByPlatform)
            {
                ctx.Rigidbody.velocity = new Vector2(ctx.ExternalWindVX, ctx.Rigidbody.velocity.y);
            }
            else if (carriedByPlatform)
            {
                ctx.Rigidbody.velocity = new Vector2(ctx.PlatformVX + ctx.ExternalWindVX, ctx.Rigidbody.velocity.y);
            }

            return;
        }

        if (!ctx.IsGrounded)
            ApplyAirMovement(ctx);
        else
            ApplyGroundMovement(ctx);
    }

    private void UpdateSprintState(MovementContext ctx)
    {
        float dt = Mathf.Max(0f, ctx.FixedDeltaTime);
        float absInput = Mathf.Abs(ctx.InputX);
        bool hasDirectionalInput = absInput > sprintInputDeadZone;
        float inputDir = hasDirectionalInput ? Mathf.Sign(ctx.InputX) : 0f;

        isForwardBlockedForSprint =
            blockSprintIntoWall &&
            ctx.IsGrounded &&
            hasDirectionalInput &&
            IsForwardBlockedByWall(inputDir);

        if (!hasDirectionalInput)
        {
            isForwardBlockedForSprint = false;
            TrySeedSprintMomentumFromActiveSprint();
            ResetActiveSprintState();
            UpdateSprintMomentum(dt, 0f, ctx.IsGrounded);
            return;
        }

        if (isForwardBlockedForSprint)
        {
            ResetActiveSprintState();
            ClearSprintMomentum();
            ClearSprintSkid();
            return;
        }

        if (Mathf.Abs(sprintHeldDirection) > 0.001f && inputDir != sprintHeldDirection)
        {
            TrySeedSprintMomentumFromActiveSprint();
            ResetActiveSprintState();
            sprintHeldDirection = inputDir;
            UpdateSprintMomentum(dt, inputDir, ctx.IsGrounded);
            return;
        }

        if (Mathf.Abs(sprintHeldDirection) < 0.001f)
            sprintHeldDirection = inputDir;

        if (!ctx.IsGrounded)
        {
            UpdateSprintMomentum(dt, inputDir, false);
            return;
        }

        sprintHeldTime += dt;

        if (sprintHeldTime < sprintStartDelay)
        {
            sprintBlend = 0f;
        }
        else if (sprintRampDuration <= 0f)
        {
            sprintBlend = 1f;
        }
        else
        {
            float t = (sprintHeldTime - sprintStartDelay) / sprintRampDuration;
            sprintBlend = Mathf.Clamp01(t);
        }

        UpdateSprintMomentum(dt, inputDir, true);
    }

    private void UpdateSprintMomentum(float dt, float inputDir, bool isGrounded)
    {
        if (!enableSprintInertia || sprintMomentumBlend <= 0.0001f)
        {
            if (!enableSprintInertia)
                ClearSprintMomentum();
            return;
        }

        if (!isGrounded)
            return;

        if (Mathf.Abs(inputDir) > 0.001f && Mathf.Sign(inputDir) == Mathf.Sign(sprintMomentumDirection))
        {
            if (sprintBlend >= sprintMomentumBlend - 0.0001f)
                ClearSprintMomentum();

            return;
        }

        float duration = Mathf.Abs(inputDir) > 0.001f
            ? sprintReverseInertiaDuration
            : sprintReleaseInertiaDuration;

        if (duration <= 0f)
        {
            ClearSprintMomentum();
            return;
        }

        sprintMomentumBlend = Mathf.MoveTowards(sprintMomentumBlend, 0f, dt / duration);

        if (sprintMomentumBlend <= 0.0001f)
            ClearSprintMomentum();
    }

    private void TrySeedSprintMomentumFromActiveSprint()
    {
        if (!enableSprintInertia)
            return;

        if (Mathf.Abs(sprintHeldDirection) <= 0.001f)
            return;

        if (sprintBlend < sprintInertiaMinBlend)
            return;

        if (Mathf.Abs(sprintMomentumDirection) > 0.001f &&
            Mathf.Sign(sprintMomentumDirection) != Mathf.Sign(sprintHeldDirection))
        {
            if (sprintBlend <= sprintMomentumBlend)
                return;
        }

        sprintMomentumDirection = Mathf.Sign(sprintHeldDirection);
        sprintMomentumBlend = Mathf.Max(sprintMomentumBlend, sprintBlend);
    }

    private float GetEffectiveSprintBlend()
    {
        return Mathf.Max(sprintBlend, sprintMomentumBlend);
    }

    private void ResetActiveSprintState()
    {
        sprintHeldDirection = 0f;
        sprintHeldTime = 0f;
        sprintBlend = 0f;
    }

    private void ClearSprintMomentum()
    {
        sprintMomentumDirection = 0f;
        sprintMomentumBlend = 0f;
    }

    private bool ShouldUseSprintMomentum(float inputX)
    {
        if (!enableSprintInertia)
            return false;

        if (sprintMomentumBlend <= 0.0001f || Mathf.Abs(sprintMomentumDirection) <= 0.001f)
            return false;

        if (Mathf.Abs(inputX) <= sprintInputDeadZone)
            return true;

        return Mathf.Sign(inputX) != Mathf.Sign(sprintMomentumDirection);
    }

    private float GetGroundTargetVelocity(float inputX, float speedMul)
    {
        if (ShouldUseSprintMomentum(inputX))
        {
            float inertiaStrength = Mathf.Clamp01(sprintMomentumBlend);
            float inertiaSprintMul = Mathf.Lerp(1f, sprintSpeedMultiplier, inertiaStrength);

            float forwardMomentumTarget =
                sprintMomentumDirection *
                moveSpeed *
                inertiaSprintMul *
                inertiaStrength *
                speedMul;

            bool isReverseInput =
                Mathf.Abs(inputX) > sprintInputDeadZone &&
                Mathf.Sign(inputX) != Mathf.Sign(sprintMomentumDirection);

            if (isReverseInput)
            {
                float reverseRamp = 1f - inertiaStrength;
                reverseRamp *= reverseRamp;

                float reverseTarget = Mathf.Sign(inputX) * moveSpeed * reverseRamp * speedMul;
                return forwardMomentumTarget + reverseTarget;
            }

            return forwardMomentumTarget;
        }

        return inputX * moveSpeed * CurrentSprintMultiplier * speedMul;
    }

    private void UpdateSprintSkidPlaceholder(MovementContext ctx)
    {
        if (!enableSprintSkidPlaceholder || ctx.Rigidbody == null)
        {
            ClearSprintSkid();
            return;
        }

        if (!ctx.IsGrounded)
        {
            ClearSprintSkid();
            return;
        }

        float localVx = ctx.Rigidbody.velocity.x - ctx.PlatformVX - ctx.ExternalWindVX;
        float absLocalVx = Mathf.Abs(localVx);

        float speedRatio = moveSpeed > 0.0001f
            ? absLocalVx / Mathf.Max(moveSpeed, 0.0001f)
            : 0f;

        bool noInput = Mathf.Abs(ctx.InputX) <= sprintInputDeadZone;
        bool reverseInput =
            Mathf.Abs(ctx.InputX) > sprintInputDeadZone &&
            absLocalVx > flipDeadZone &&
            Mathf.Sign(ctx.InputX) != Mathf.Sign(localVx);

        bool enoughSprint = GetEffectiveSprintBlend() >= skidMinEnterBlend;
        bool enoughSpeed = speedRatio >= skidMinSpeedRatio;

        bool shouldEnterSkid = enoughSprint && enoughSpeed && (noInput || reverseInput);

        if (shouldEnterSkid)
        {
            isSprintSkidActive = true;

            if (absLocalVx > flipDeadZone)
                sprintSkidDirection = Mathf.Sign(localVx);
            else if (Mathf.Abs(sprintMomentumDirection) > 0.001f)
                sprintSkidDirection = Mathf.Sign(sprintMomentumDirection);

            skidHoldUntil = Mathf.Max(skidHoldUntil, ctx.Now + Mathf.Max(0f, skidMinHoldTime));
            return;
        }

        if (!isSprintSkidActive)
            return;

        bool keepMinHold = ctx.Now < skidHoldUntil;
        bool keepByMomentum = HasSprintMomentum && absLocalVx > moveSpeed * skidExitSpeedRatio;
        bool keepBySpeed = absLocalVx > moveSpeed * skidExitSpeedRatio && reverseInput;

        if (keepMinHold || keepByMomentum || keepBySpeed)
        {
            if (absLocalVx > flipDeadZone)
                sprintSkidDirection = Mathf.Sign(localVx);

            return;
        }

        ClearSprintSkid();
    }

    private void ClearSprintSkid()
    {
        isSprintSkidActive = false;
        sprintSkidDirection = 0f;
        skidHoldUntil = -999f;
    }

    private void ApplyAirMovement(MovementContext ctx)
    {
        float dt = Mathf.Max(0f, ctx.FixedDeltaTime);
        float localVx = ctx.Rigidbody.velocity.x - ctx.ExternalWindVX;
        float inputX = Mathf.Clamp(ctx.InputX, -1f, 1f);
        float absInput = Mathf.Abs(inputX);

        if (!enableAirControl)
        {
            ctx.Rigidbody.velocity = new Vector2(localVx + ctx.ExternalWindVX, ctx.Rigidbody.velocity.y);
            return;
        }

        float targetSpeed = GetAirTargetSpeed() * ctx.SnowMoveMul;
        float sameDirRate = GetAirSameDirectionRate();
        float reverseRate = GetAirReverseRate();
        float noInputRate = GetAirNoInputDampingRate();

        float jumpInertiaMul = GetJumpAirInertiaMultiplier(ctx.Now);
        sameDirRate *= jumpInertiaMul;
        reverseRate *= jumpInertiaMul;
        noInputRate *= jumpInertiaMul;

        if (absInput > airControlInputDeadZone)
        {
            float desired = Mathf.Sign(inputX) * targetSpeed;
            bool sameDirection = Mathf.Abs(localVx) <= 0.0001f || Mathf.Sign(localVx) == Mathf.Sign(desired);

            if (sameDirection)
            {
                if (Mathf.Abs(localVx) > Mathf.Abs(desired))
                    desired = Mathf.Lerp(desired, localVx, airMomentumPreservation);

                localVx = Mathf.MoveTowards(localVx, desired, sameDirRate * dt);
            }
            else
            {
                localVx = Mathf.MoveTowards(localVx, desired, reverseRate * dt);
            }
        }
        else
        {
            float preservedNoInputRate = Mathf.Lerp(noInputRate, noInputRate * 0.18f, airMomentumPreservation);
            localVx = Mathf.MoveTowards(localVx, 0f, preservedNoInputRate * dt);
        }

        ctx.Rigidbody.velocity = new Vector2(localVx + ctx.ExternalWindVX, ctx.Rigidbody.velocity.y);
    }

    private void ApplyGroundMovement(MovementContext ctx)
    {
        float speedMul = ctx.SnowMoveMul;
        float sprintMul = CurrentSprintMultiplier;
        float target = GetGroundTargetVelocity(ctx.InputX, speedMul);

        float maxSpeed = moveSpeed * sprintMul * (ctx.IsOnIce ? iceMaxSpeedMul : 1f) * ctx.SnowMoveMul;
        float accel = ctx.IsOnIce ? iceAccel : normalAccel;
        float brake = ctx.IsOnIce ? iceBrake : normalBrake;

        float cur = ctx.Rigidbody.velocity.x - ctx.PlatformVX - ctx.ExternalWindVX;
        float rate = (Mathf.Sign(target) == Mathf.Sign(cur) || Mathf.Approximately(cur, 0f))
            ? accel
            : brake;

        float newVx = Mathf.MoveTowards(
            cur,
            Mathf.Clamp(target, -maxSpeed, +maxSpeed),
            rate * ctx.FixedDeltaTime);

        newVx += ctx.PlatformVX + ctx.ExternalWindVX;

        ctx.Rigidbody.velocity = new Vector2(newVx, ctx.Rigidbody.velocity.y);

        if (Mathf.Abs(newVx) > flipDeadZone)
        {
            bool faceRight = newVx > 0f;
            if (faceRight != isFacingRight)
                Flip();
        }
    }

    private float GetAirTargetSpeed()
    {
        return Mathf.Lerp(moveSpeed * 0.65f, moveSpeed * 1.30f, airSpeedSlider);
    }

    private float GetAirSameDirectionRate()
    {
        return Mathf.Lerp(8f, 85f, airSameDirectionResponse);
    }

    private float GetAirReverseRate()
    {
        return Mathf.Lerp(3f, 38f, airReverseResponse);
    }

    private float GetAirNoInputDampingRate()
    {
        return Mathf.Lerp(0.5f, 18f, airNoInputDamping);
    }

    private float GetJumpAirInertiaMultiplier(float now)
    {
        if (jumpAirInertiaDuration <= 0f || now >= jumpAirInertiaUntil)
            return 1f;

        float remaining = Mathf.Clamp01((jumpAirInertiaUntil - now) / jumpAirInertiaDuration);
        float t = 1f - remaining;
        return Mathf.Lerp(jumpAirInertiaControlMultiplier, 1f, t);
    }

    private bool IsForwardBlockedByWall(float inputDir)
    {
        if (!blockSprintIntoWall)
            return false;

        CacheComponents();
        if (bodyCollider == null)
            return false;

        if (Mathf.Abs(inputDir) <= 0.001f)
            return false;

        int hitCount = bodyCollider.Cast(
            new Vector2(Mathf.Sign(inputDir), 0f),
            sprintWallFilter,
            sprintWallHits,
            Mathf.Max(0.01f, sprintWallCheckDistance));

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = sprintWallHits[i];
            if (hit.collider == null)
                continue;

            Vector2 normal = hit.normal;

            if (Mathf.Abs(normal.x) < sprintWallNormalMinAbsX)
                continue;

            if (Mathf.Sign(inputDir) == -Mathf.Sign(normal.x))
                return true;
        }

        return false;
    }

    private void CacheComponents()
    {
        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();
    }

    private void ConfigureWallCastFilter()
    {
        sprintWallFilter.useTriggers = false;
        sprintWallFilter.useLayerMask = false;
        sprintWallFilter.useNormalAngle = false;
    }

    private void OnDrawGizmos()
    {
        if (!drawSprintWallCheckGizmo || !drawSprintWallCheckAlways)
            return;

        DrawSprintWallCheckGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSprintWallCheckGizmo || drawSprintWallCheckAlways)
            return;

        DrawSprintWallCheckGizmo();
    }

    private void DrawSprintWallCheckGizmo()
    {
        CacheComponents();
        if (bodyCollider == null)
            return;

        Bounds b = bodyCollider.bounds;

        float dir;
        if (Application.isPlaying)
        {
            if (Mathf.Abs(sprintHeldDirection) > 0.001f)
                dir = Mathf.Sign(sprintHeldDirection);
            else if (Mathf.Abs(sprintMomentumDirection) > 0.001f)
                dir = Mathf.Sign(sprintMomentumDirection);
            else
                dir = isFacingRight ? 1f : -1f;
        }
        else
        {
            dir = transform.lossyScale.x >= 0f ? 1f : -1f;
        }

        float checkDistance = Mathf.Max(0.01f, sprintWallCheckDistance);

        Vector3 center = b.center;
        Vector3 castBoxCenter = center + new Vector3(dir * checkDistance * 0.5f, 0f, 0f);
        Vector3 castBoxSize = new Vector3(checkDistance, b.size.y, 0.02f);

        bool blockedNow = Application.isPlaying && isForwardBlockedForSprint;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireCube(center, b.size);

        Gizmos.color = blockedNow
            ? new Color(1f, 0.2f, 0.2f, 0.95f)
            : new Color(1f, 0.85f, 0.15f, 0.95f);

        Gizmos.DrawWireCube(castBoxCenter, castBoxSize);
        Gizmos.DrawLine(center, center + new Vector3(dir * checkDistance, 0f, 0f));

        Vector3 arrowTip = center + new Vector3(dir * checkDistance, 0f, 0f);
        float arrowSize = Mathf.Min(0.12f, checkDistance * 0.5f);

        Gizmos.DrawLine(
            arrowTip,
            arrowTip + new Vector3(-dir * arrowSize, arrowSize * 0.5f, 0f));

        Gizmos.DrawLine(
            arrowTip,
            arrowTip + new Vector3(-dir * arrowSize, -arrowSize * 0.5f, 0f));
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }

    private void OnDisable()
    {
        ResetSprint();
    }
}