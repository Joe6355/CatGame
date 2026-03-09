using UnityEngine;

[DisallowMultipleComponent]
public class PlayerBounceModule : MonoBehaviour
{
    [Header("ќтскок от стен/потолка")]
    [SerializeField, Range(0f, 1f), Tooltip("ƒол€ силы последнего прыжка, превращаема€ в отскок по X от стены.\n0.33 = 33% от силы прыжка.\n–екоменд: 0.2Ц0.5 (часто 0.3Ц0.4).")]
    private float wallBounceFraction = 0.33f;

    [SerializeField, Tooltip("ƒемпфирование отскока (уменьшение силы), если прошло достаточно времени после прыжка.\n1 = без демпфа, 0.5 = в 2 раза слабее.\n–екоменд: 0.4Ц0.8 (часто 0.5Ц0.7).")]
    private float damping = 0.5f;

    [SerializeField, Tooltip("ќкно после прыжка, в которое демпфирование не примен€етс€, чтобы отскок сразу после прыжка был бодрее.\n–екоменд: 0.1Ц0.3 сек (часто 0.15Ц0.25).")]
    private float dampingExclusionTime = 0.2f;

    [SerializeField, Tooltip("ћинимальна€ |скорость по Y|, чтобы считать игрока в воздухе дл€ отскока от стены.\n≈сли меньше Ч отскок всЄ равно разрешаетс€ в течение wallBounceApexWindow после прыжка.\n–екоменд: 0.03Ц0.10 (часто 0.05).")]
    private float wallBounceMinAbsY = 0.05f;

    [SerializeField, Tooltip("ќкно после прыжка/пинка, когда отскок от стены разрешЄн даже если скорость по Y почти 0 (вершина дуги).\n–екоменд: 0.3Ц0.9 сек (часто 0.6).")]
    private float wallBounceApexWindow = 0.6f;

    [SerializeField, Tooltip("ѕорог 'боковости' стены по нормали (|normal.x|). Ќа углах нормаль бывает неидеальной.\nћеньше = чаще срабатывает отскок на углах.\n–екоменд: 0.40Ц0.60 (часто 0.45Ц0.55).")]
    private float wallNormalMinAbsX = 0.45f;

    [SerializeField, Tooltip("ћинимальна€ пауза между обработками отскока, чтобы не словить двойной отскок за один и тот же контакт.\n–екоменд: 0.01Ц0.05 сек (часто 0.02).")]
    private float bounceCooldown = 0.02f;

    private float lastBounceTime = -999f;

    /// <summary>
    /// —ообщить модулю, что только что был выполнен прыжок / резкий импульс.
    /// Ќужен, чтобы не получить мгновенный повторный bounce в тот же момент.
    /// </summary>
    public void NotifyJumpImpulse(float now)
    {
        lastBounceTime = now;
    }

    /// <summary>
    /// ќбработать столкновение и при необходимости выполнить bounce.
    /// </summary>
    public void HandleBounce(
        Collision2D collision,
        Rigidbody2D rb,
        PlayerJumpModule jumpModule,
        PlayerMovementModule movementModule,
        float externalWindVX,
        float now)
    {
        if (collision == null || rb == null)
            return;

        if (now - lastBounceTime < bounceCooldown)
            return;

        if (collision.contactCount <= 0)
            return;

        ContactPoint2D cp = collision.GetContact(0);
        Vector2 n = cp.normal;

        bool isWall = Mathf.Abs(n.x) >= wallNormalMinAbsX && n.y < 0.6f;
        bool isCeil = n.y <= -0.6f;

        if (!isWall && !isCeil)
            return;

        float absY = Mathf.Abs(rb.velocity.y);

        float lastJumpTime = jumpModule != null ? jumpModule.LastJumpTime : -999f;
        float lastJumpForce = jumpModule != null ? jumpModule.LastAppliedJumpForce : 0f;

        bool allowApex = (now - lastJumpTime) <= wallBounceApexWindow;

        if (!allowApex && absY < wallBounceMinAbsY)
            return;

        float bounce = lastJumpForce * wallBounceFraction;

        if ((now - lastJumpTime) > dampingExclusionTime)
            bounce *= damping;

        if (isWall)
        {
            float dir = Mathf.Sign(n.x);
            float bouncedVx = bounce * dir;

            rb.velocity = new Vector2(bouncedVx, rb.velocity.y);

            if (movementModule != null)
                movementModule.SetAirVx(bouncedVx);

            lastBounceTime = now;
        }
        else if (isCeil)
        {
            rb.velocity = new Vector2(rb.velocity.x, -Mathf.Abs(rb.velocity.y));

            if (movementModule != null)
                movementModule.SetAirVx(rb.velocity.x - externalWindVX);

            lastBounceTime = now;
        }
    }

    public void ResetBounceState()
    {
        lastBounceTime = -999f;
    }

    private void OnDisable()
    {
        ResetBounceState();
    }
}