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
    [SerializeField, Tooltip("Если ВКЛ — включается помощь на краях платформ: доп. зонды и снап-лучи вниз.\nРекоменд: ВКЛ (true), но с аккуратными настройками.")]
    private bool useEdgeAssist = true;

    [SerializeField, Tooltip("Насколько далеко влево/вправо от центра стопы ставим edge-зонды.\nБольше = легче поймать край, но выше шанс липнуть к углам.\nРекоменд: 0.12–0.18.")]
    private float edgeProbeHalfWidth = 0.14f;

    [SerializeField, Tooltip("Реальная ширина каждого бокового edge-зонда.\nДержим маленькой, чтобы не ловить стену как землю.\nРекоменд: 0.03–0.08.")]
    private float edgeProbeWidth = 0.05f;

    [SerializeField, Tooltip("Высота тонких боксов под краями стоп.\nОчень тонкая, чтобы не ловить стены.\nРекоменд: 0.03–0.08 (часто 0.05–0.07).")]
    private float edgeProbeHeight = 0.05f;

    [SerializeField, Tooltip("Дистанция снап-лучей вниз: насколько далеко 'нащупываем' землю.\nБольше = легче приземлиться на край, но выше шанс ложного grounded.\nРекоменд: 0.06–0.10.")]
    private float snapProbeDistance = 0.08f;

    [SerializeField, Tooltip("Максимальная эффективная дистанция снап-лучей.\nДаже если snapProbeDistance больше, будет использовано не больше этого значения.\nРекоменд: 0.08–0.12.")]
    private float snapProbeDistanceMax = 0.10f;

    [SerializeField, Tooltip("Снап-лучи вниз срабатывают только когда игрок НЕ летит вверх.\nЕсли rb.velocity.y выше этого порога — снап-лучи игнорируются, чтобы не ломать прыжки/отскоки.\nРекоменд: 0.0–0.05 (часто 0.02).")]
    private float snapOnlyWhenFallingY = 0.02f;

    [SerializeField, Range(0f, 1f), Tooltip("Минимальная вертикальная компонента нормали (normal.y), чтобы считать поверхность землёй.\nЧем больше — тем меньше шанс прилипнуть к стенам/углам.\nРекоменд: 0.55–0.75.")]
    private float snapMinNormalY = 0.6f;

    [SerializeField, Tooltip("Небольшой подъём точки старта снап-лучей относительно уровня стоп.\nНужен, чтобы лучи шли вниз от ног, а не из центра тела.\nРекоменд: 0.02–0.08.")]
    private float snapRayStartHeight = 0.04f;

    [Header("Анти-залипание в боковую грань")]
    [SerializeField, Tooltip("Если ВКЛ — edge assist не будет считать игрока на земле, когда он реально упёрся боком в вертикальную грань/угол.")]
    private bool rejectEdgeAssistWhenSideBlocked = true;

    [SerializeField, Tooltip("На сколько кастуем влево/вправо от коллайдера игрока, чтобы понять, что впереди именно боковая грань.\nРекоменд: 0.02–0.08.")]
    private float sideBlockCheckDistance = 0.04f;

    [SerializeField, Range(0f, 1f), Tooltip("Минимальная |normal.x| у бокового препятствия, чтобы считать его стеной.\nБольше = жёстче фильтр.\nРекоменд: 0.6–0.9.")]
    private float sideBlockMinNormalX = 0.7f;

    [SerializeField, Tooltip("Коллайдер игрока. Нужен для анти-залипания в боковую грань.\nЕсли не назначен — найдётся автоматически на этом же объекте.")]
    private Collider2D bodyCollider;

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

    private void Awake()
    {
        CacheComponents();
    }

    private void OnValidate()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// Полный пересчёт состояния земли.
    /// Вызывать из PlayerController в FixedUpdate ДО ApplyMovement().
    /// </summary>
    public void EvaluateGround(Rigidbody2D rb, float currentTime, float coyoteTime)
    {
        if (rb == null)
            return;

        CacheComponents();

        wasGroundedLastFrame = isGrounded;
        justLanded = false;

        Vector2 feetCenter = GetFeetCenter();

        bool grounded = TryMainGroundCheck(feetCenter, out Collider2D col);

        if (!grounded && useEdgeAssist && rb.velocity.y <= snapOnlyWhenFallingY)
        {
            grounded = TryEdgeAssistGround(rb, feetCenter, out col);
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

    private Vector2 GetFeetCenter()
    {
        if (useBoxGroundCheck)
            return (Vector2)transform.position + groundBoxOffset;

        return groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position + groundBoxOffset;
    }

    private bool TryMainGroundCheck(Vector2 feetCenter, out Collider2D col)
    {
        col = null;

        if (useBoxGroundCheck)
        {
            col = Physics2D.OverlapBox(feetCenter, groundBoxSize, 0f, groundMask);
            return col != null;
        }

        Vector2 p = groundCheck ? (Vector2)groundCheck.position : feetCenter;
        col = Physics2D.OverlapCircle(p, groundCheckRadius, groundMask);
        return col != null;
    }

    private bool TryEdgeAssistGround(Rigidbody2D rb, Vector2 feetCenter, out Collider2D col)
    {
        col = null;

        float half = Mathf.Max(0.03f, edgeProbeHalfWidth);
        float probeWidth = Mathf.Clamp(edgeProbeWidth, 0.02f, Mathf.Max(0.02f, half * 0.75f));
        float probeHeight = Mathf.Max(0.02f, edgeProbeHeight);

        float probeY = feetCenter.y - 0.001f;
        Vector2 probeSize = new Vector2(probeWidth, probeHeight);

        Vector2 leftProbeCenter = new Vector2(feetCenter.x - half, probeY);
        Vector2 rightProbeCenter = new Vector2(feetCenter.x + half, probeY);

        Collider2D leftProbe = Physics2D.OverlapBox(leftProbeCenter, probeSize, 0f, groundMask);
        Collider2D rightProbe = Physics2D.OverlapBox(rightProbeCenter, probeSize, 0f, groundMask);

        bool hasLeftProbe = leftProbe != null;
        bool hasRightProbe = rightProbe != null;

        if (!hasLeftProbe && !hasRightProbe)
            return false;

        if (rejectEdgeAssistWhenSideBlocked && Mathf.Abs(rb.velocity.x) > 0.01f)
        {
            if (rb.velocity.x > 0.01f && hasRightProbe && IsSideBlocked(Vector2.right))
                return false;

            if (rb.velocity.x < -0.01f && hasLeftProbe && IsSideBlocked(Vector2.left))
                return false;
        }

        float dist = Mathf.Min(snapProbeDistance, snapProbeDistanceMax);
        if (dist <= 0f)
            return false;

        float rayStartY = feetCenter.y + Mathf.Max(0.01f, snapRayStartHeight);

        bool hasBestHit = false;
        RaycastHit2D bestHit = default;

        // Луч из центра — только как дополнительная проверка, не основная.
        TryRegisterGroundHit(new Vector2(feetCenter.x, rayStartY), dist, ref hasBestHit, ref bestHit);

        if (hasLeftProbe)
            TryRegisterGroundHit(new Vector2(feetCenter.x - half, rayStartY), dist, ref hasBestHit, ref bestHit);

        if (hasRightProbe)
            TryRegisterGroundHit(new Vector2(feetCenter.x + half, rayStartY), dist, ref hasBestHit, ref bestHit);

        if (!hasBestHit)
            return false;

        col = bestHit.collider;
        return true;
    }

    private void TryRegisterGroundHit(Vector2 rayOrigin, float dist, ref bool hasBestHit, ref RaycastHit2D bestHit)
    {
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, dist, groundMask);
        if (!IsValidGroundHit(hit))
            return;

        if (!hasBestHit || hit.distance < bestHit.distance)
        {
            bestHit = hit;
            hasBestHit = true;
        }
    }

    private bool IsValidGroundHit(RaycastHit2D hit)
    {
        if (hit.collider == null)
            return false;

        if (hit.normal.y < snapMinNormalY)
            return false;

        return true;
    }

    private bool IsSideBlocked(Vector2 dir)
    {
        if (bodyCollider == null)
            return false;

        Bounds b = bodyCollider.bounds;
        Vector2 castSize = new Vector2(
            Mathf.Max(0.02f, b.size.x * 0.9f),
            Mathf.Max(0.02f, b.size.y * 0.9f));

        RaycastHit2D hit = Physics2D.BoxCast(
            b.center,
            castSize,
            0f,
            dir.normalized,
            Mathf.Max(0.01f, sideBlockCheckDistance),
            groundMask);

        if (hit.collider == null)
            return false;

        return Mathf.Abs(hit.normal.x) >= sideBlockMinNormalX && hit.normal.y < snapMinNormalY;
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

        Vector2 feetCenter = GetFeetCenter();

        if (useBoxGroundCheck)
        {
            Gizmos.DrawWireCube(feetCenter, groundBoxSize);
        }
        else
        {
            Vector2 p = groundCheck ? (Vector2)groundCheck.position : feetCenter;
            Gizmos.DrawWireSphere(p, groundCheckRadius);
        }

        if (useEdgeAssist)
        {
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.6f);

            float half = Mathf.Max(0.03f, edgeProbeHalfWidth);
            float probeWidth = Mathf.Clamp(edgeProbeWidth, 0.02f, Mathf.Max(0.02f, half * 0.75f));
            float probeHeight = Mathf.Max(0.02f, edgeProbeHeight);

            float probeY = feetCenter.y - 0.001f;
            Vector2 probeSize = new Vector2(probeWidth, probeHeight);

            Gizmos.DrawWireCube(
                new Vector3(feetCenter.x - half, probeY, 0f),
                new Vector3(probeSize.x, probeSize.y, 0f));

            Gizmos.DrawWireCube(
                new Vector3(feetCenter.x + half, probeY, 0f),
                new Vector3(probeSize.x, probeSize.y, 0f));

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            float gizSnap = Mathf.Min(snapProbeDistance, snapProbeDistanceMax);
            float rayStartY = feetCenter.y + Mathf.Max(0.01f, snapRayStartHeight);

            Gizmos.DrawLine(
                new Vector3(feetCenter.x, rayStartY, 0f),
                new Vector3(feetCenter.x, rayStartY - gizSnap, 0f));

            Gizmos.DrawLine(
                new Vector3(feetCenter.x - half, rayStartY, 0f),
                new Vector3(feetCenter.x - half, rayStartY - gizSnap, 0f));

            Gizmos.DrawLine(
                new Vector3(feetCenter.x + half, rayStartY, 0f),
                new Vector3(feetCenter.x + half, rayStartY - gizSnap, 0f));
        }

        if (rejectEdgeAssistWhenSideBlocked && bodyCollider != null)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);

            Bounds b = bodyCollider.bounds;
            Gizmos.DrawWireCube(b.center + Vector3.left * sideBlockCheckDistance * 0.5f, b.size);
            Gizmos.DrawWireCube(b.center + Vector3.right * sideBlockCheckDistance * 0.5f, b.size);
        }
    }
}