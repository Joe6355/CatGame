using UnityEngine;

[DisallowMultipleComponent]
public class PlayerGroundModule : MonoBehaviour
{
    [Header("Ground Check")]
    [SerializeField, Tooltip("Слои, которые считаются землёй (Ground/Platform и т.п.).\nРекоменд: выделить отдельный слой Ground и выбрать его здесь.")]
    private LayerMask groundMask;

    [SerializeField, Tooltip("Если ВКЛ — проверяем землю OverlapBox под ногами.\nЕсли ВЫКЛ — проверяем OverlapCircle (groundCheck + radius).\nРекоменд: ВКЛ (true) для платформера.")]
    private bool useBoxGroundCheck = true;

    [SerializeField, Tooltip("Размер бокса проверки земли (ширина/высота).\nШирина обычно чуть меньше ширины персонажа, высота — тонкая.\nРекоменд: X 0.4–0.9, Y 0.06–0.15.")]
    private Vector2 groundBoxSize = new Vector2(0.6f, 0.12f);

    [SerializeField, Tooltip("Смещение бокса проверки земли от центра персонажа (локально).\nОбычно немного вниз.\nРекоменд: Y -0.15..-0.30, X чаще 0.")]
    private Vector2 groundBoxOffset = new Vector2(0f, -0.2f);

    [SerializeField, Tooltip("Точка для OverlapCircle, если useBoxGroundCheck выключен.\nРекоменд: пустышка GroundCheck под ногами.")]
    private Transform groundCheck;

    [SerializeField, Tooltip("Радиус круга проверки земли, если useBoxGroundCheck выключен.\nРекоменд: 0.08–0.18 (часто 0.10–0.14).")]
    private float groundCheckRadius = 0.12f;

    [Header("Ground Edge Assist (борьба с краями)")]
    [SerializeField, Tooltip("Если ВКЛ — включается помощь на краях платформ: доп. зонды и снап-лучи вниз.\nРекоменд: ВКЛ (true) — сильно улучшает контроль на краях.")]
    private bool useEdgeAssist = true;

    [SerializeField, Tooltip("Половина ширины 'стоп' для боковых зондов (влево/вправо от центра).\nБольше = легче цепляться за край, но может цепляться 'слишком'.\nРекоменд: 0.12–0.30 (часто 0.18–0.24).")]
    private float edgeProbeHalfWidth = 0.22f;

    [SerializeField, Tooltip("Высота тонких боксов под краями стоп.\nОчень тонкая, чтобы не ловить стены.\nРекоменд: 0.03–0.08 (часто 0.05–0.07).")]
    private float edgeProbeHeight = 0.06f;

    [SerializeField, Tooltip("Дистанция снап-лучей вниз: насколько далеко 'нащупываем' землю.\nБольше = легче приземлиться на край.\nРекоменд: 0.06–0.18 (часто 0.10–0.14).")]
    private float snapProbeDistance = 0.12f;

    [SerializeField, Tooltip("Максимальная эффективная дистанция снап-лучей. Даже если snapProbeDistance больше, будет использовано не больше этого значения.\nНужен, чтобы Edge Assist не считал игрока 'на земле' слишком высоко и не ломал отскоки.\nРекоменд: 0.12–0.22 (часто 0.18).")]
    private float snapProbeDistanceMax = 0.18f;

    [SerializeField, Tooltip("Снап-лучи вниз срабатывают только когда игрок НЕ летит вверх.\nЕсли rb.velocity.y выше этого порога — снап-лучи игнорируются, чтобы не ломать прыжки/отскоки.\nРекоменд: 0.0–0.05 (часто 0.02).")]
    private float snapOnlyWhenFallingY = 0.02f;

    [SerializeField, Range(0f, 1f), Tooltip("Минимальная вертикальная компонента нормали (normal.y), чтобы считать поверхность землёй.\nЧем больше — тем меньше шанс 'прилипнуть' к стенам.\nРекоменд: 0.3–0.6 (часто 0.35–0.45).")]
    private float snapMinNormalY = 0.35f;

    // ===== Runtime state =====
    private bool isGrounded = false;
    private bool wasGroundedLastFrame = false;
    private bool justLanded = false;

    private float lastGroundedTime = -999f;
    private Collider2D lastGroundCol = null;

    private bool isOnIce = false;
    private float platformVX = 0f;

    public bool IsGrounded => isGrounded;
    public bool JustLanded => justLanded;
    public float LastGroundedTime => lastGroundedTime;
    public Collider2D LastGroundCollider => lastGroundCol;
    public bool IsOnIce => isOnIce;
    public float PlatformVX => platformVX;

    /// <summary>
    /// Полный пересчёт состояния земли.
    /// Вызывать из PlayerController в FixedUpdate ДО ApplyMovement().
    /// </summary>
    public void EvaluateGround(Rigidbody2D rb, float currentTime, float coyoteTime)
    {
        if (rb == null)
            return;

        wasGroundedLastFrame = isGrounded;
        justLanded = false;

        Vector2 origin = (Vector2)transform.position + groundBoxOffset;

        bool grounded = false;
        Collider2D col = null;

        if (useBoxGroundCheck)
        {
            col = Physics2D.OverlapBox(origin, groundBoxSize, 0f, groundMask);
            grounded = col != null;
        }
        else
        {
            Vector2 p = groundCheck ? (Vector2)groundCheck.position : origin;
            col = Physics2D.OverlapCircle(p, groundCheckRadius, groundMask);
            grounded = col != null;
        }

        if (!grounded && useEdgeAssist)
        {
            if (rb.velocity.y <= snapOnlyWhenFallingY)
            {
                float half = Mathf.Max(0.05f, edgeProbeHalfWidth);
                float y = origin.y - 0.001f;
                Vector2 size = new Vector2(half * 0.9f, edgeProbeHeight);

                Collider2D leftProbe = Physics2D.OverlapBox(new Vector2(origin.x - half, y), size, 0f, groundMask);
                Collider2D rightProbe = Physics2D.OverlapBox(new Vector2(origin.x + half, y), size, 0f, groundMask);

                if (leftProbe != null || rightProbe != null)
                {
                    float dist = Mathf.Min(snapProbeDistance, snapProbeDistanceMax);

                    RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, dist, groundMask);
                    if (hit.collider != null && hit.normal.y >= snapMinNormalY)
                    {
                        grounded = true;
                        col = hit.collider;
                    }
                    else
                    {
                        RaycastHit2D hitL = Physics2D.Raycast(
                            new Vector2(origin.x - half, transform.position.y),
                            Vector2.down,
                            dist,
                            groundMask);

                        if (hitL.collider != null && hitL.normal.y >= snapMinNormalY)
                        {
                            grounded = true;
                            col = hitL.collider;
                        }

                        if (!grounded)
                        {
                            RaycastHit2D hitR = Physics2D.Raycast(
                                new Vector2(origin.x + half, transform.position.y),
                                Vector2.down,
                                dist,
                                groundMask);

                            if (hitR.collider != null && hitR.normal.y >= snapMinNormalY)
                            {
                                grounded = true;
                                col = hitR.collider;
                            }
                        }
                    }
                }
            }
        }

        isGrounded = grounded;

        if (isGrounded)
        {
            lastGroundedTime = currentTime;
            lastGroundCol = col;
        }
        else if ((currentTime - lastGroundedTime) > coyoteTime)
        {
            lastGroundCol = null;
        }

        justLanded = !wasGroundedLastFrame && isGrounded;

        if (lastGroundCol != null)
        {
            isOnIce = lastGroundCol.CompareTag("Ice");

            SurfaceEffector2D eff = lastGroundCol.GetComponent<SurfaceEffector2D>();
            if (eff != null)
                platformVX = eff.speed;
            else
                platformVX = 0f;
        }
        else
        {
            isOnIce = false;
            platformVX = 0f;
        }
    }

    public void ResetGroundState()
    {
        isGrounded = false;
        wasGroundedLastFrame = false;
        justLanded = false;
        lastGroundedTime = -999f;
        lastGroundCol = null;
        isOnIce = false;
        platformVX = 0f;
    }

    private void OnDisable()
    {
        ResetGroundState();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.6f);

        if (useBoxGroundCheck)
        {
            Vector2 origin = (Vector2)transform.position + groundBoxOffset;
            Gizmos.DrawWireCube(origin, groundBoxSize);
        }
        else
        {
            Vector2 p = groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position;
            Gizmos.DrawWireSphere(p, groundCheckRadius);
        }

        if (useEdgeAssist)
        {
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.6f);

            Vector2 feetCenter = useBoxGroundCheck
                ? (Vector2)transform.position + groundBoxOffset
                : (groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position);

            float half = Mathf.Max(0.05f, edgeProbeHalfWidth);
            float y = feetCenter.y - 0.001f;
            Vector2 probeSize = new Vector2(half * 0.9f, edgeProbeHeight);

            Gizmos.DrawWireCube(new Vector3(feetCenter.x - half, y, 0f), new Vector3(probeSize.x, probeSize.y, 0f));
            Gizmos.DrawWireCube(new Vector3(feetCenter.x + half, y, 0f), new Vector3(probeSize.x, probeSize.y, 0f));

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            float gizSnap = Mathf.Min(snapProbeDistance, snapProbeDistanceMax);

            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * gizSnap);
            Gizmos.DrawLine(
                new Vector3(feetCenter.x - half, transform.position.y, 0f),
                new Vector3(feetCenter.x - half, transform.position.y - gizSnap, 0f));

            Gizmos.DrawLine(
                new Vector3(feetCenter.x + half, transform.position.y, 0f),
                new Vector3(feetCenter.x + half, transform.position.y - gizSnap, 0f));
        }
    }
}