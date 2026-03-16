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
        public bool IsFatigued;
        public bool IsJumpCharging;
    }

    [Header("Движение")]
    [SerializeField, Tooltip("Базовая скорость бега по земле без учёта спринта, льда, снега и усталости.")]
    private float moveSpeed = 5f;

    [SerializeField, Tooltip("Множитель скорости при усталости.")]
    private float fatigueSpeedMultiplier = 0.6f;

    [Header("Спринт")]
    [SerializeField, Min(0f), Tooltip("Задержка перед началом спринта.\nПока это время не прошло, персонаж бежит с обычной скоростью.")]
    private float sprintStartDelay = 0.35f;

    [SerializeField, Min(0f), Tooltip("Сколько секунд занимает плавный разгон от обычной скорости до пика спринта после sprintStartDelay.\n0 = мгновенный выход на пик после задержки.")]
    private float sprintRampDuration = 0.35f;

    [SerializeField, Min(1f), Tooltip("Пиковый множитель скорости спринта относительно moveSpeed.\n1.5 = на 50% быстрее обычного бега.")]
    private float sprintSpeedMultiplier = 1.5f;

    [SerializeField, Range(0f, 1f), Tooltip("Минимальная сила горизонтального ввода, чтобы считать, что направление реально удерживается для спринта.")]
    private float sprintInputDeadZone = 0.2f;

    [Header("Air Control")]
    [SerializeField, Tooltip("Скорость управления в воздухе, когда air-control разрешён.")]
    private float airControlSpeed = 5f;

    [SerializeField, Tooltip("Если ВКЛ — управление в воздухе доступно всегда.")]
    private bool enableAirControlInAir = false;

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

    private bool isFacingRight = true;
    private float airVx = 0f;
    private float airControlUnlockUntil = 0f;

    private float sprintHeldDirection = 0f;
    private float sprintHeldTime = 0f;
    private float sprintBlend = 0f;

    public float MoveSpeed => moveSpeed;
    public float CurrentSprintMultiplier => Mathf.Lerp(1f, sprintSpeedMultiplier, sprintBlend);
    public float CurrentMoveSpeed => moveSpeed * CurrentSprintMultiplier;
    public float FatigueSpeedMultiplier => fatigueSpeedMultiplier;
    public bool IsFacingRight => isFacingRight;
    public float AirVx => airVx;
    public bool IsSprintReady => sprintBlend >= 0.999f;

    public void AllowAirControlFor(float duration)
    {
        airControlUnlockUntil = Mathf.Max(
            airControlUnlockUntil,
            Time.time + Mathf.Max(0f, duration));
    }

    public void OnJumpPerformed(float takeoffVx)
    {
        airVx = takeoffVx;
    }

    public void SetAirVx(float vx)
    {
        airVx = vx;
    }

    public void ResetSprint()
    {
        sprintHeldDirection = 0f;
        sprintHeldTime = 0f;
        sprintBlend = 0f;
    }

    public void TryFaceByInput(float inputX, bool allowFlip)
    {
        if (!allowFlip)
            return;

        if (Mathf.Abs(inputX) <= flipDeadZone)
            return;

        bool faceRight = inputX > 0f;
        if (faceRight != isFacingRight)
            Flip();
    }

    public void ApplyMovement(MovementContext ctx)
    {
        if (ctx.Rigidbody == null)
            return;

        UpdateSprintState(ctx);

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
        float absInput = Mathf.Abs(ctx.InputX);
        bool hasDirectionalInput = absInput > sprintInputDeadZone;
        float inputDir = hasDirectionalInput ? Mathf.Sign(ctx.InputX) : 0f;

        // Отпустили кнопку направления -> спринт сразу сбрасывается.
        if (!hasDirectionalInput)
        {
            ResetSprint();
            return;
        }

        // Сменили направление -> спринт сразу сбрасывается и начинается заново.
        if (Mathf.Abs(sprintHeldDirection) > 0.001f && inputDir != sprintHeldDirection)
        {
            ResetSprint();
            sprintHeldDirection = inputDir;
            return;
        }

        if (Mathf.Abs(sprintHeldDirection) < 0.001f)
            sprintHeldDirection = inputDir;

        // В воздухе не накапливаем спринт, но и не меняем его, если направление то же самое.
        if (!ctx.IsGrounded)
            return;

        sprintHeldTime += Mathf.Max(0f, ctx.FixedDeltaTime);

        if (sprintHeldTime < sprintStartDelay)
        {
            sprintBlend = 0f;
            return;
        }

        if (sprintRampDuration <= 0f)
        {
            sprintBlend = 1f;
            return;
        }

        float t = (sprintHeldTime - sprintStartDelay) / sprintRampDuration;
        sprintBlend = Mathf.Clamp01(t);
    }

    private void ApplyAirMovement(MovementContext ctx)
    {
        if (enableAirControlInAir || ctx.Now < airControlUnlockUntil)
        {
            float speedMul = (ctx.IsFatigued ? fatigueSpeedMultiplier : 1f) * ctx.SnowMoveMul;
            float vx = ctx.InputX * airControlSpeed * speedMul + ctx.ExternalWindVX;

            ctx.Rigidbody.velocity = new Vector2(vx, ctx.Rigidbody.velocity.y);

            if (Mathf.Abs(vx) > flipDeadZone)
            {
                bool faceRight = vx > 0f;
                if (faceRight != isFacingRight)
                    Flip();
            }
        }
        else
        {
            ctx.Rigidbody.velocity = new Vector2(airVx + ctx.ExternalWindVX, ctx.Rigidbody.velocity.y);

            float vx = ctx.Rigidbody.velocity.x;
            if (Mathf.Abs(vx) > flipDeadZone)
            {
                bool faceRight = vx > 0f;
                if (faceRight != isFacingRight)
                    Flip();
            }
        }
    }

    private void ApplyGroundMovement(MovementContext ctx)
    {
        float speedMul = (ctx.IsFatigued ? fatigueSpeedMultiplier : 1f) * ctx.SnowMoveMul;
        float sprintMul = CurrentSprintMultiplier;
        float target = ctx.InputX * moveSpeed * sprintMul * speedMul;

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
        airVx = 0f;
        airControlUnlockUntil = 0f;
    }
}