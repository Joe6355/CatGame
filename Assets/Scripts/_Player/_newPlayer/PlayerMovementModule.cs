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

    [Header("ƒвижение")]
    [SerializeField, Tooltip("Ѕазова€ скорость бега по земле (без льда/снега/усталости).\n–екоменд: 3Ц8 (часто 4Ц6).")]
    private float moveSpeed = 5f;

    [SerializeField, Tooltip("ћножитель скорости при усталости (анти-спам прыжка).\n0.6 = скорость падает до 60%.\n–екоменд: 0.5Ц0.8 (часто 0.6Ц0.7).")]
    private float fatigueSpeedMultiplier = 0.6f;

    [Header("Air Control")]
    [SerializeField, Tooltip("—корость управлени€ в воздухе, когда air-control временно разрешЄн (AllowAirControlFor).\n–екоменд: 3Ц10 (часто 4Ц7).")]
    private float airControlSpeed = 5f;

    [SerializeField, Tooltip("≈сли ¬ Ћ Ч управление в воздухе доступно всегда. ≈сли ¬џ Ћ Ч работает текуща€ логика с временным разрешением через AllowAirControlFor().")]
    private bool enableAirControlInAir = false;

    [Header("ЋЄд (Tag = \"Ice\")")]
    [SerializeField, Tooltip("”скорение на льду (как быстро набираем скорость к target).\nћеньше = более скользко.\n–екоменд: 1Ц6 (часто 2Ц4).")]
    private float iceAccel = 2.5f;

    [SerializeField, Tooltip("“орможение на льду при смене направлени€.\nћеньше = более скользко.\n–екоменд: 0.5Ц4 (часто 1Ц2).")]
    private float iceBrake = 1.2f;

    [SerializeField, Tooltip("ћаксимальна€ скорость на льду как множитель от moveSpeed.\n1.15 = на льду можно чуть быстрее.\n–екоменд: 1.0Ц1.4 (часто 1.1Ц1.25).")]
    private float iceMaxSpeedMul = 1.15f;

    [SerializeField, Tooltip("”скорение на обычной земле. ќчень большое значение делает движение почти мгновенным.\n–екоменд: 20Ц200 (дл€ плавности) или 9999 (мгновенно).")]
    private float normalAccel = 9999f;

    [SerializeField, Tooltip("“орможение/смена направлени€ на обычной земле.\n–екоменд: 20Ц200 (плавно) или 9999 (мгновенно).")]
    private float normalBrake = 9999f;

    [Header("¬изуальный разворот")]
    [SerializeField, Tooltip("ћинимальна€ |скорость|/|вход| по X, после которой разрешЄн разворот персонажа. Ќужен, чтобы персонаж не дЄргалс€ на микроскопических значени€х осей.\n–екоменд: 0.01Ц0.10 (часто 0.05).")]
    private float flipDeadZone = 0.05f;

    private bool isFacingRight = true;
    private float airVx = 0f;
    private float airControlUnlockUntil = 0f;

    public float MoveSpeed => moveSpeed;
    public float FatigueSpeedMultiplier => fatigueSpeedMultiplier;
    public bool IsFacingRight => isFacingRight;
    public float AirVx => airVx;

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
        float target = ctx.InputX * moveSpeed * speedMul;

        float maxSpeed = moveSpeed * (ctx.IsOnIce ? iceMaxSpeedMul : 1f) * ctx.SnowMoveMul;
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

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }
}