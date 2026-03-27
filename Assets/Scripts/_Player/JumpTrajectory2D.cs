using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class JumpTrajectory2D : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField, Tooltip(
        "Ссылка на PlayerController.\n" +
        "Из него берём:\n" +
        "- отдельную старую траекторию зарядного прыжка,\n" +
        "- текущую обычную траекторию в apex-окне,\n" +
        "- новую предлагаемую apex-trajectory,\n" +
        "- gravityScale игрока.")]
    private PlayerController player;

    [SerializeField, Tooltip(
        "Collider2D игрока. Нужен, чтобы траектория не проходила сквозь объекты реальным размером коллайдера.")]
    private Collider2D playerCollider;

    [Header("Какие траектории показывать")]
    [SerializeField, Tooltip(
        "Если ВКЛ — до прыжка показывается старая зарядная траектория.\n" +
        "После выполнения сильного прыжка она пропадает, как и раньше.")]
    private bool showChargeTrajectory = true;

    [SerializeField, Tooltip(
        "Если ВКЛ — в окно apex throw рисуется текущая обычная траектория БЕЗ броска вниз.\n" +
        "Это и есть 'старая' траектория в воздухе.")]
    private bool showCurrentTrajectoryDuringApex = true;

    [SerializeField, Tooltip(
        "Если ВКЛ — в окно apex throw рисуется новая предлагаемая траектория С броском вниз.")]
    private bool showSuggestedApexTrajectory = true;

    [Header("Renderer для новой предлагаемой траектории")]
    [SerializeField, Tooltip(
        "Если ВКЛ и secondary renderer не назначен — скрипт сам создаст второй LineRenderer как дочерний объект.")]
    private bool autoCreateSuggestedRenderer = true;

    [SerializeField, Tooltip(
        "Отдельный LineRenderer для новой предлагаемой траектории.\n" +
        "Можно оставить пустым — при включённом autoCreateSuggestedRenderer он создастся сам.")]
    private LineRenderer suggestedLineRenderer;

    [SerializeField, Tooltip(
        "Если ВКЛ — второму renderer будут скопированы основные настройки первого.\n" +
        "Удобно, чтобы обе линии выглядели похоже и отличались только цветом/материалом.")]
    private bool copyPrimaryRendererSettingsToSuggested = true;

    [SerializeField, Min(0.05f), Tooltip("Множитель толщины новой предлагаемой траектории.")]
    private float suggestedWidthMultiplier = 1f;

    [SerializeField, Tooltip("Цвет новой предлагаемой траектории.")]
    private Color suggestedLineColor = new Color(0.55f, 1f, 0.55f, 1f);

    [Header("Точки траектории")]
    [SerializeField, Tooltip("Сколько точек рисовать. Больше = плавнее, но дороже.")]
    private int points = 35;

    [SerializeField, Tooltip("Шаг по времени между точками.")]
    private float step = 0.05f;

    [SerializeField, Tooltip("Смещение старта траектории относительно игрока.")]
    private Vector2 startOffset = new Vector2(0f, 0.8f);

    [Header("Столкновения")]
    [SerializeField, Tooltip("Если ВКЛ — траектория обрывается при первом столкновении.")]
    private bool stopOnHit = true;

    [SerializeField, Tooltip("Слои, с которыми траектория сталкивается.")]
    private LayerMask hitMask;

    [SerializeField, Tooltip("Игнорировать триггеры при проверке.")]
    private bool ignoreTriggers = true;

    [Header("Отрисовка основной линии")]
    [SerializeField, Tooltip("ВЫКЛ — сплошная линия. ВКЛ — пунктир.")]
    private bool useDottedLine = false;

    [SerializeField, Tooltip("Материал сплошной основной линии.")]
    private Material solidMaterial;

    [SerializeField, Tooltip("Материал пунктирной основной линии.")]
    private Material dottedMaterial;

    [SerializeField, Tooltip("Размер паттерна пунктира в мировых единицах.")]
    private float dottedWorldPatternSize = 0.25f;

    [SerializeField, Tooltip("Прокрутка пунктира по линии.")]
    private float dottedScrollSpeed = 0f;

    [Header("Отрисовка новой предлагаемой линии")]
    [SerializeField, Tooltip("Материал сплошной новой линии. Можно оставить пустым — тогда будет взят основной.")]
    private Material suggestedSolidMaterial;

    [SerializeField, Tooltip("Материал пунктирной новой линии. Можно оставить пустым — тогда будет взят основной.")]
    private Material suggestedDottedMaterial;

    private LineRenderer primaryLineRenderer;

    private readonly List<Vector3> primaryBuf = new List<Vector3>(128);
    private readonly List<Vector3> suggestedBuf = new List<Vector3>(128);
    private Vector3[] tmpPositions = new Vector3[128];

    private Material solidMatInst;
    private Material dottedMatInst;
    private Material suggestedSolidMatInst;
    private Material suggestedDottedMatInst;

    private readonly RaycastHit2D[] hitBuf = new RaycastHit2D[16];
    private ContactFilter2D filter;

    private void Awake()
    {
        primaryLineRenderer = GetComponent<LineRenderer>();

        ResolveReferences();
        ConfigureFilter();
        CreateMaterialInstances();
        EnsureSuggestedRenderer();
        ApplySuggestedRendererStaticStyle();
    }

    private void OnValidate()
    {
        points = Mathf.Max(2, points);
        step = Mathf.Max(0.001f, step);
        dottedWorldPatternSize = Mathf.Max(0.0001f, dottedWorldPatternSize);
        suggestedWidthMultiplier = Mathf.Max(0.05f, suggestedWidthMultiplier);

        ResolveReferences();

        if (!Application.isPlaying)
            return;

        ConfigureFilter();
        EnsureSuggestedRenderer();
        ApplySuggestedRendererStaticStyle();
    }

    private void OnDisable()
    {
        ClearAllLines();
    }

    private void ResolveReferences()
    {
        if (!player)
            player = GetComponent<PlayerController>();

        if (!playerCollider)
            playerCollider = GetComponent<Collider2D>();
    }

    private void ConfigureFilter()
    {
        filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = hitMask;
        filter.useTriggers = !ignoreTriggers;
    }

    private void CreateMaterialInstances()
    {
        if (solidMaterial != null)
            solidMatInst = new Material(solidMaterial);

        if (dottedMaterial != null)
            dottedMatInst = new Material(dottedMaterial);

        if (suggestedSolidMaterial != null)
            suggestedSolidMatInst = new Material(suggestedSolidMaterial);

        if (suggestedDottedMaterial != null)
            suggestedDottedMatInst = new Material(suggestedDottedMaterial);
    }

    private void EnsureSuggestedRenderer()
    {
        if (suggestedLineRenderer != null)
            return;

        if (!autoCreateSuggestedRenderer || primaryLineRenderer == null)
            return;

        GameObject child = new GameObject("SuggestedTrajectoryLine");
        child.transform.SetParent(transform, false);

        suggestedLineRenderer = child.AddComponent<LineRenderer>();
        CopyRendererSettings(primaryLineRenderer, suggestedLineRenderer);
        suggestedLineRenderer.positionCount = 0;
    }

    private void CopyRendererSettings(LineRenderer source, LineRenderer target)
    {
        if (source == null || target == null)
            return;

        target.useWorldSpace = true;
        target.loop = false;
        target.alignment = source.alignment;
        target.widthCurve = source.widthCurve;
        target.widthMultiplier = source.widthMultiplier;
        target.numCapVertices = source.numCapVertices;
        target.numCornerVertices = source.numCornerVertices;
        target.textureMode = source.textureMode;
        target.colorGradient = source.colorGradient;
        target.sharedMaterial = source.sharedMaterial;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
    }

    private void ApplySuggestedRendererStaticStyle()
    {
        if (suggestedLineRenderer == null)
            return;

        if (primaryLineRenderer != null && copyPrimaryRendererSettingsToSuggested)
            CopyRendererSettings(primaryLineRenderer, suggestedLineRenderer);

        suggestedLineRenderer.widthMultiplier *= suggestedWidthMultiplier;
        suggestedLineRenderer.startColor = suggestedLineColor;
        suggestedLineRenderer.endColor = suggestedLineColor;
    }

    private void Update()
    {
        if (player == null)
        {
            ClearAllLines();
            return;
        }

        if (showChargeTrajectory && player.IsChargeTrajectoryPreviewVisible)
        {
            Vector2 chargeVelocity = player.GetPredictedChargeTrajectoryVelocity();

            if (chargeVelocity.sqrMagnitude > 0.000001f)
            {
                DrawTrajectory(primaryLineRenderer, primaryBuf, chargeVelocity, false);
                ClearLine(suggestedLineRenderer);
                return;
            }
        }

        if (player.IsApexThrowTrajectoryPreviewVisible)
        {
            bool drewPrimary = false;

            if (showCurrentTrajectoryDuringApex)
            {
                Vector2 currentVelocity = player.GetCurrentWorldVelocity();

                if (currentVelocity.sqrMagnitude > 0.000001f)
                {
                    DrawTrajectory(primaryLineRenderer, primaryBuf, currentVelocity, false);
                    drewPrimary = true;
                }
                else
                {
                    ClearLine(primaryLineRenderer);
                }
            }
            else
            {
                ClearLine(primaryLineRenderer);
            }

            if (showSuggestedApexTrajectory)
            {
                Vector2 apexVelocity = player.GetPredictedApexThrowTrajectoryVelocity();

                if (apexVelocity.sqrMagnitude > 0.000001f)
                {
                    if (suggestedLineRenderer != null)
                    {
                        DrawTrajectory(suggestedLineRenderer, suggestedBuf, apexVelocity, true);
                    }
                    else if (!drewPrimary)
                    {
                        DrawTrajectory(primaryLineRenderer, primaryBuf, apexVelocity, true);
                    }
                }
                else
                {
                    ClearLine(suggestedLineRenderer);
                }
            }
            else
            {
                ClearLine(suggestedLineRenderer);
            }

            return;
        }

        ClearAllLines();
    }

    private void ClearAllLines()
    {
        ClearLine(primaryLineRenderer);
        ClearLine(suggestedLineRenderer);
    }

    private void ClearLine(LineRenderer target)
    {
        if (target != null && target.positionCount != 0)
            target.positionCount = 0;
    }

    private void DrawTrajectory(LineRenderer target, List<Vector3> buffer, Vector2 predictedVelocity, bool useSuggestedStyle)
    {
        if (target == null || player == null || points <= 1 || step <= 0f)
        {
            ClearLine(target);
            return;
        }

        Vector3 origin = (player != null ? player.transform.position : transform.position) + (Vector3)startOffset;
        Vector2 gravity = Physics2D.gravity * player.GetGravityScale();

        buffer.Clear();
        buffer.Add(origin);

        Vector3 prev = origin;
        float tPrev = 0f;

        for (int i = 1; i < points; i++)
        {
            float tCur = tPrev + step;

            Vector2 p = (Vector2)origin + predictedVelocity * tCur + 0.5f * gravity * (tCur * tCur);
            Vector3 cur = new Vector3(p.x, p.y, 0f);

            if (stopOnHit)
            {
                if (TryCastBetween(prev, cur, out float hitFrac))
                {
                    float tHit = tPrev + step * Mathf.Clamp01(hitFrac);
                    Vector2 pHit = (Vector2)origin + predictedVelocity * tHit + 0.5f * gravity * (tHit * tHit);
                    buffer.Add(new Vector3(pHit.x, pHit.y, 0f));
                    break;
                }
            }

            buffer.Add(cur);
            prev = cur;
            tPrev = tCur;
        }

        ApplyPositionsToLineRenderer(target, buffer);
        ApplyLineStyle(target, buffer, useSuggestedStyle);
    }

    private void ApplyPositionsToLineRenderer(LineRenderer target, List<Vector3> buffer)
    {
        int n = buffer.Count;
        if (n <= 0)
        {
            target.positionCount = 0;
            return;
        }

        if (tmpPositions == null || tmpPositions.Length < n)
            tmpPositions = new Vector3[Mathf.NextPowerOfTwo(n)];

        for (int i = 0; i < n; i++)
            tmpPositions[i] = buffer[i];

        target.useWorldSpace = true;
        target.positionCount = n;
        target.SetPositions(tmpPositions);
    }

    private void ApplyLineStyle(LineRenderer target, List<Vector3> buffer, bool useSuggestedStyle)
    {
        if (target == null)
            return;

        if (!useDottedLine)
        {
            if (useSuggestedStyle)
            {
                if (suggestedSolidMatInst != null) target.material = suggestedSolidMatInst;
                else if (suggestedSolidMaterial != null) target.material = suggestedSolidMaterial;
                else if (solidMatInst != null) target.material = solidMatInst;
                else if (solidMaterial != null) target.material = solidMaterial;
            }
            else
            {
                if (solidMatInst != null) target.material = solidMatInst;
                else if (solidMaterial != null) target.material = solidMaterial;
            }

            target.textureMode = LineTextureMode.Stretch;
        }
        else
        {
            if (useSuggestedStyle)
            {
                if (suggestedDottedMatInst != null) target.material = suggestedDottedMatInst;
                else if (suggestedDottedMaterial != null) target.material = suggestedDottedMaterial;
                else if (dottedMatInst != null) target.material = dottedMatInst;
                else if (dottedMaterial != null) target.material = dottedMaterial;
            }
            else
            {
                if (dottedMatInst != null) target.material = dottedMatInst;
                else if (dottedMaterial != null) target.material = dottedMaterial;
            }

            target.textureMode = LineTextureMode.Tile;

            float len = ComputePolylineLength(buffer);
            float pattern = Mathf.Max(0.0001f, dottedWorldPatternSize);
            float tiles = len / pattern;

            Material mat = target.material;
            if (mat != null)
            {
                mat.mainTextureScale = new Vector2(tiles, 1f);

                if (Mathf.Abs(dottedScrollSpeed) > 0.0001f)
                {
                    float off = Time.time * dottedScrollSpeed;
                    mat.mainTextureOffset = new Vector2(off, 0f);
                }
                else
                {
                    mat.mainTextureOffset = Vector2.zero;
                }
            }
        }

        if (useSuggestedStyle)
        {
            target.startColor = suggestedLineColor;
            target.endColor = suggestedLineColor;
        }
    }

    private float ComputePolylineLength(List<Vector3> buffer)
    {
        float len = 0f;

        for (int i = 1; i < buffer.Count; i++)
            len += Vector3.Distance(buffer[i - 1], buffer[i]);

        return len;
    }

    private bool TryCastBetween(Vector3 prev, Vector3 cur, out float hitFraction)
    {
        hitFraction = 1f;

        Vector2 dir = (Vector2)(cur - prev);
        float dist = dir.magnitude;
        if (dist < 0.00001f)
            return false;

        dir /= dist;

        filter.layerMask = hitMask;
        filter.useTriggers = !ignoreTriggers;

        if (playerCollider == null)
        {
            RaycastHit2D h = Physics2D.Raycast((Vector2)prev, dir, dist, hitMask);
            if (h.collider != null && !IsSelf(h.collider))
            {
                hitFraction = h.fraction;
                return true;
            }

            return false;
        }

        int hitCount = 0;

        if (playerCollider is CapsuleCollider2D cap)
        {
            hitCount = Physics2D.CapsuleCast((Vector2)prev, cap.size, cap.direction, 0f, dir, filter, hitBuf, dist);
        }
        else if (playerCollider is BoxCollider2D box)
        {
            hitCount = Physics2D.BoxCast((Vector2)prev, box.size, 0f, dir, filter, hitBuf, dist);
        }
        else if (playerCollider is CircleCollider2D cc)
        {
            hitCount = Physics2D.CircleCast((Vector2)prev, cc.radius, dir, filter, hitBuf, dist);
        }
        else
        {
            RaycastHit2D h = Physics2D.Raycast((Vector2)prev, dir, dist, hitMask);
            if (h.collider != null && !IsSelf(h.collider))
            {
                hitFraction = h.fraction;
                return true;
            }

            return false;
        }

        if (hitCount <= 0)
            return false;

        float best = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D h = hitBuf[i];
            if (h.collider == null) continue;
            if (IsSelf(h.collider)) continue;
            if (h.fraction < best) best = h.fraction;
        }

        if (best < float.MaxValue)
        {
            hitFraction = best;
            return true;
        }

        return false;
    }

    private bool IsSelf(Collider2D col)
    {
        if (col == null)
            return false;

        if (playerCollider != null)
        {
            if (col == playerCollider)
                return true;

            if (col.transform.IsChildOf(playerCollider.transform))
                return true;
        }

        Transform root = player != null ? player.transform : transform;
        return col.transform.IsChildOf(root);
    }
}