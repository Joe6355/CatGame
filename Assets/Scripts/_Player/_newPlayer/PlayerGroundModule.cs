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

    [SerializeField, Tooltip("Edge Assist включается только когда игрок уже реально падает вниз.\nЕсли скорость по Y выше -этого значения, снап-лучи не сработают.\nРекоменд: 0.02–0.12 (часто 0.04–0.08).")]
    private float snapOnlyWhenFallingY = 0.02f;

    [SerializeField, Range(0f, 1f), Tooltip("Минимальная вертикальная компонента нормали (normal.y), чтобы считать поверхность землёй.\nЧем больше — тем меньше шанс 'прилипнуть' к стенам.\nРекоменд: 0.3–0.6 (часто 0.35–0.45).")]
    private float snapMinNormalY = 0.35f;

    private const float EdgeAssistSkin = 0.005f;

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

        Vector2 checkCenter = GetGroundCheckCenter();

        bool grounded = false;
        Collider2D col = null;

        if (useBoxGroundCheck)
        {
            col = Physics2D.OverlapBox(checkCenter, groundBoxSize, 0f, groundMask);
            grounded = col != null;
        }
        else
        {
            col = Physics2D.OverlapCircle(checkCenter, groundCheckRadius, groundMask);
            grounded = col != null;
        }

        if (!grounded && useEdgeAssist)
        {
            float fallThreshold = Mathf.Max(0f, snapOnlyWhenFallingY);

            // Важно: edge assist теперь работает только когда игрок реально уже снижается.
            if (rb.velocity.y <= -fallThreshold)
            {
                float half = Mathf.Max(0.05f, edgeProbeHalfWidth);
                float probeHeight = Mathf.Max(0.01f, edgeProbeHeight);
                float probeWidth = Mathf.Max(0.05f, half * 0.9f);

                float feetBottomY = GetGroundCheckBottomY(checkCenter);
                float probeCenterY = feetBottomY - (probeHeight * 0.5f) - EdgeAssistSkin;

                Vector2 probeSize = new Vector2(probeWidth, probeHeight);

                Collider2D leftProbe = Physics2D.OverlapBox(
                    new Vector2(checkCenter.x - half, probeCenterY),
                    probeSize,
                    0f,
                    groundMask);

                Collider2D rightProbe = Physics2D.OverlapBox(
                    new Vector2(checkCenter.x + half, probeCenterY),
                    probeSize,
                    0f,
                    groundMask);

                if (leftProbe != null || rightProbe != null)
                {
                    float dist = Mathf.Min(snapProbeDistance, snapProbeDistanceMax);
                    float rayStartY = feetBottomY + EdgeAssistSkin;

                    RaycastHit2D hit = Physics2D.Raycast(
                        new Vector2(checkCenter.x, rayStartY),
                        Vector2.down,
                        dist + EdgeAssistSkin,
                        groundMask);

                    if (IsValidSnapHit(hit))
                    {
                        grounded = true;
                        col = hit.collider;
                    }
                    else
                    {
                        RaycastHit2D hitL = Physics2D.Raycast(
                            new Vector2(checkCenter.x - half, rayStartY),
                            Vector2.down,
                            dist + EdgeAssistSkin,
                            groundMask);

                        if (IsValidSnapHit(hitL))
                        {
                            grounded = true;
                            col = hitL.collider;
                        }

                        if (!grounded)
                        {
                            RaycastHit2D hitR = Physics2D.Raycast(
                                new Vector2(checkCenter.x + half, rayStartY),
                                Vector2.down,
                                dist + EdgeAssistSkin,
                                groundMask);

                            if (IsValidSnapHit(hitR))
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

    private Vector2 GetGroundCheckCenter()
    {
        if (useBoxGroundCheck)
            return (Vector2)transform.position + groundBoxOffset;

        return groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position + groundBoxOffset;
    }

    private float GetGroundCheckBottomY(Vector2 checkCenter)
    {
        if (useBoxGroundCheck)
            return checkCenter.y - (groundBoxSize.y * 0.5f);

        return checkCenter.y - groundCheckRadius;
    }

    private bool IsValidSnapHit(RaycastHit2D hit)
    {
        return hit.collider != null && hit.normal.y >= snapMinNormalY;
    }

    private void OnDisable()
    {
        ResetGroundState();
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 checkCenter = GetGroundCheckCenter();

        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.6f);

        if (useBoxGroundCheck)
        {
            Gizmos.DrawWireCube(checkCenter, groundBoxSize);
        }
        else
        {
            Gizmos.DrawWireSphere(checkCenter, groundCheckRadius);
        }

        if (useEdgeAssist)
        {
            float half = Mathf.Max(0.05f, edgeProbeHalfWidth);
            float probeHeight = Mathf.Max(0.01f, edgeProbeHeight);
            float probeWidth = Mathf.Max(0.05f, half * 0.9f);
            float feetBottomY = GetGroundCheckBottomY(checkCenter);
            float probeCenterY = feetBottomY - (probeHeight * 0.5f) - EdgeAssistSkin;

            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(
                new Vector3(checkCenter.x - half, probeCenterY, 0f),
                new Vector3(probeWidth, probeHeight, 0f));
            Gizmos.DrawWireCube(
                new Vector3(checkCenter.x + half, probeCenterY, 0f),
                new Vector3(probeWidth, probeHeight, 0f));

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            float gizSnap = Mathf.Min(snapProbeDistance, snapProbeDistanceMax);
            float rayStartY = feetBottomY + EdgeAssistSkin;

            Gizmos.DrawLine(
                new Vector3(checkCenter.x, rayStartY, 0f),
                new Vector3(checkCenter.x, rayStartY - gizSnap, 0f));

            Gizmos.DrawLine(
                new Vector3(checkCenter.x - half, rayStartY, 0f),
                new Vector3(checkCenter.x - half, rayStartY - gizSnap, 0f));

            Gizmos.DrawLine(
                new Vector3(checkCenter.x + half, rayStartY, 0f),
                new Vector3(checkCenter.x + half, rayStartY - gizSnap, 0f));
        }
    }
}