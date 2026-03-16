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
    [SerializeField, Tooltip("Базовая скорость обычного бега по земле, до разгона в спринт.\nРекоменд: 3–8 (часто 4–6).")]
    private float moveSpeed = 5f;

    [SerializeField, Tooltip("Множитель скорости при усталости (анти-спам прыжка).\n0.6 = скорость падает до 60%.\nРекоменд: 0.5–0.8 (часто 0.6–0.7).")]
    private float fatigueSpeedMultiplier = 0.6f;

    [Header("Спринт / плавный разгон")]
    [SerializeField, Tooltip("Если ВКЛ — при удержании направления на земле игрок постепенно разгоняется от moveSpeed до sprintMoveSpeed.")]
    private bool enableSprintAcceleration = true;

    [SerializeField, Tooltip("Пиковая скорость спринта.\nДолжна быть не меньше moveSpeed.\nРекоменд: 1.2x–2x от базовой скорости.")]
    private float sprintMoveSpeed = 8f;

    [SerializeField, Tooltip("За сколько секунд непрерывного удержания направления игрок дойдёт до полного спринта.\nРекоменд: 0.4–1.5 сек.")]
    private float sprintBuildTime = 0.8f;

    [SerializeField, Tooltip("Минимальная сила ввода по X, после которой считаем, что игрок реально держит направление для разгона.\nНужно, чтобы мелкий шум стика не накапливал спринт.\nРекоменд: 0.15–0.35.")]
    private float sprintInputDeadZone = 0.2f;

    [SerializeField, Tooltip("Если ВКЛ — при смене направления разгон в спринт сбрасывается и начинает копиться заново.")]
    private bool resetSprintOnDirectionChange = true;

    [Header("Air Control")]
    [SerializeField, Tooltip("Скорость управления в воздухе, когда air-control временно разрешён (AllowAirControlFor).\nРекоменд: 3–10 (часто 4–7).")]
    private float airControlSpeed = 5f;

    [SerializeField, Tooltip("Если ВКЛ — управление в воздухе доступно всегда. Если ВЫКЛ — работает текущая логика с временным разрешением через AllowAirControlFor().")]
    private bool enableAirControlInAir = false;

    [Header("Лёд (Tag = \"Ice\")")]
    [SerializeField, Tooltip("Ускорение на льду (как быстро набираем скорость к target).\nМеньше = более скользко.\nРекоменд: 1–6 (часто 2–4).")]
    private float iceAccel = 2.5f;

    [SerializeField, Tooltip("Торможение на льду при смене направления.\nМеньше = более скользко.\nРекоменд: 0.5–4 (часто 1–2).")]
    private float iceBrake = 1.2f;

    [SerializeField, Tooltip("Максимальная скорость на льду как множитель от текущей наземной скорости.\n1.15 = на льду можно чуть быстрее.\nРекоменд: 1.0–1.4 (часто 1.1–1.25).")]
    private float iceMaxSpeedMul = 1.15f;

    [SerializeField, Tooltip("Ускорение на обычной земле. Очень большое значение делает движение почти мгновенным.\nРекоменд: 20–200 (для плавности) или 9999 (мгновенно).")]
    private float normalAccel = 9999f;

    [SerializeField, Tooltip("Торможение/смена направления на обычной земле.\nРекоменд: 20–200 (плавно) или 9999 (мгновенно).")]
    private float normalBrake = 9999f;

    [Header("Визуальный разворот")]
    [SerializeField, Tooltip("Минимальная |скорость|/|вход| по X, после которой разрешён разворот персонажа.\nНужен, чтобы персонаж не дёргался на микроскопических значениях осей.\nРекоменд: 0.01–0.10 (часто 0.05).")]
    private float flipDeadZone = 0.05f;

    private bool isFacingRight = true;
    private float airVx = 0f;
    private float airControlUnlockUntil = 0f;

    private float sprintProgress = 0f;      // 0..1
    private float sprintDirectionSign = 0f; // -1 / +1 / 0

    public float MoveSpeed => moveSpeed;
    public float FatigueSpeedMultiplier => fatigueSpeedMultiplier;
    public bool IsFacingRight => isFacingRight;
    public float AirVx => airVx;

    public float SprintProgress => sprintProgress;
    public bool SprintChargedJumpReady => enableSprintAcceleration && sprintProgress >= 0.999f;

    /// <summary>
    /// Текущая наземная скорость, учитывающая накопленный разгон в спринт,
    /// но БЕЗ учёта усталости/снега. Нужна для расчёта takeoffVx в прыжке.
    /// </summary>
    public float CurrentMoveSpeedForJump => GetCurrentMoveSpeed();

    public void AllowAirControlFor(float duration)
    {
        airControlUnlockUntil = Mathf.Max(
            airControlUnlockUntil,
            Time.time + Mathf.Max(0f, duration));
    }

    public void OnJumpPerformed(float takeoffVx)
    {
        airVx = takeoffVx;

        // После реального прыжка начинаем копить разгон заново.
        sprintProgress = 0f;
        sprintDirectionSign = 0f;
    }

    public void SetAirVx(float vx)
    {
        airVx = vx;
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
        {
            ApplyAirMovement(ctx);
        }
        else
        {
            ApplyGroundMovement(ctx);
        }
    }

    private void UpdateSprintState(MovementContext ctx)
    {
        if (!enableSprintAcceleration)
        {
            sprintProgress = 0f;
            sprintDirectionSign = 0f;
            return;
        }

        // Спринт копим только на земле.
        if (!ctx.IsGrounded)
        {
            sprintProgress = 0f;
            sprintDirectionSign = 0f;
            return;
        }

        // Во время зарядки прыжка сохраняем уже набранный спринт,
        // чтобы release использовал ту скорость, с которой игрок "подбежал".
        if (ctx.IsJumpCharging)
            return;

        float absInput = Mathf.Abs(ctx.InputX);
        if (absInput < sprintInputDeadZone)
        {
            sprintProgress = 0f;
            sprintDirectionSign = 0f;
            return;
        }

        float sign = Mathf.Sign(ctx.InputX);

        if (resetSprintOnDirectionChange && sprintDirectionSign != 0f && sign != sprintDirectionSign)
        {
            sprintProgress = 0f;
        }

        sprintDirectionSign = sign;

        float safeBuildTime = Mathf.Max(0.0001f, sprintBuildTime);
        sprintProgress = Mathf.Clamp01(sprintProgress + ctx.FixedDeltaTime / safeBuildTime);
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
        float sprintAwareMoveSpeed = GetCurrentMoveSpeed();
        float speedMul = (ctx.IsFatigued ? fatigueSpeedMultiplier : 1f) * ctx.SnowMoveMul;

        float target = ctx.InputX * sprintAwareMoveSpeed * speedMul;

        float maxSpeed = sprintAwareMoveSpeed * (ctx.IsOnIce ? iceMaxSpeedMul : 1f) * ctx.SnowMoveMul;
        float accel = ctx.IsOnIce ? iceAccel : normalAccel;
        float brake = ctx.IsOnIce ? iceBrake : normalBrake;

        float cur = ctx.Rigidbody.velocity.x;
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

    private float GetCurrentMoveSpeed()
    {
        if (!enableSprintAcceleration)
            return moveSpeed;

        float clampedSprintSpeed = Mathf.Max(moveSpeed, sprintMoveSpeed);
        return Mathf.Lerp(moveSpeed, clampedSprintSpeed, sprintProgress);
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }
}