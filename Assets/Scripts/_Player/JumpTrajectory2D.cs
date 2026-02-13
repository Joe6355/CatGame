using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class JumpTrajectory2D : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField, Tooltip(
        "Ссылка на PlayerController, у которого берём:\n" +
        "- IsChargingJumpPublic (когда показывать)\n" +
        "- GetPredictedJumpVelocity() (стартовая скорость)\n" +
        "- GetGravityScale() (масштаб гравитации)\n" +
        "Рекоменд: перетащить PlayerController игрока сюда (или оставить пустым — попробует GetComponent).")]
    private PlayerController player;

    [SerializeField, Tooltip(
        "Collider2D игрока. Нужен, чтобы траектория не проходила сквозь объекты (каст реальным размером).\n" +
        "Рекоменд: назначить Collider2D игрока (или оставить пустым — найдёт GetComponent).")]
    private Collider2D playerCollider;

    [Header("Точки траектории")]
    [SerializeField, Tooltip("Сколько точек рисовать. Больше = плавнее, но дороже.\nРекоменд: 25–60 (часто 35–45).")]
    private int points = 35;

    [SerializeField, Tooltip("Шаг по времени между точками (сек). Меньше = точнее, но дороже.\nРекоменд: 0.03–0.07 (часто 0.05).")]
    private float step = 0.05f;

    [SerializeField, Tooltip("Смещение старта траектории относительно игрока.\nРекоменд: Y 0.5–1.2.")]
    private Vector2 startOffset = new Vector2(0f, 0.8f);

    [Header("Столкновения")]
    [SerializeField, Tooltip("Если ВКЛ — траектория обрывается при первом столкновении.\nРекоменд: ВКЛ (true).")]
    private bool stopOnHit = true;

    [SerializeField, Tooltip(
        "Слои, с которыми траектория сталкивается (Ground/Wall/Platform).\n" +
        "ВАЖНО: если слой не выбран — линия будет проходить сквозь него.")]
    private LayerMask hitMask;

    [SerializeField, Tooltip("Игнорировать триггеры при проверке.\nРекоменд: ВКЛ (true).")]
    private bool ignoreTriggers = true;

    [Header("Отрисовка LineRenderer")]
    [SerializeField, Tooltip(
        "Галочка:\n" +
        "ВЫКЛ — обычная (сплошная) линия как раньше.\n" +
        "ВКЛ — пунктир через LineRenderer (Tile + dash-текстура).")]
    private bool useDottedLine = false;

    [SerializeField, Tooltip(
        "Материал сплошной линии (можно оставить пустым — тогда используется текущий material у LineRenderer).")]
    private Material solidMaterial;

    [SerializeField, Tooltip(
        "Материал пунктирной линии (обязательно с dash-текстурой и Wrap=Repeat).")]
    private Material dottedMaterial;

    [SerializeField, Tooltip(
        "Длина одного 'паттерна' пунктира в мировых единицах (dash+gap).\n" +
        "Меньше = чаще штрихи, больше = реже.\n" +
        "Рекоменд: 0.15–0.45 (под пиксели подбирай).")]
    private float dottedWorldPatternSize = 0.25f;

    [SerializeField, Tooltip(
        "Прокрутка пунктира по линии (0 = без анимации).\n" +
        "Рекоменд: 0..2.")]
    private float dottedScrollSpeed = 0f;

    private LineRenderer lr;
    private readonly List<Vector3> buf = new List<Vector3>(128);
    private Vector3[] tmpPositions = new Vector3[128];

    // чтобы не портить sharedMaterial
    private Material solidMatInst;
    private Material dottedMatInst;

    private readonly RaycastHit2D[] hitBuf = new RaycastHit2D[16];
    private ContactFilter2D filter;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        if (!player) player = GetComponent<PlayerController>();
        if (!playerCollider) playerCollider = GetComponent<Collider2D>();

        filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = hitMask;
        filter.useTriggers = !ignoreTriggers;

        // инстансы материалов, чтобы не менять ассет
        if (solidMaterial != null) solidMatInst = new Material(solidMaterial);
        if (dottedMaterial != null) dottedMatInst = new Material(dottedMaterial);
    }

    private void Update()
    {
        if (player != null && player.IsChargingJumpPublic)
        {
            DrawTrajectory();
        }
        else
        {
            ClearLine();
        }
    }

    private void ClearLine()
    {
        if (lr != null && lr.positionCount != 0)
            lr.positionCount = 0;
    }

    private void DrawTrajectory()
    {
        if (lr == null || player == null || points <= 1 || step <= 0f)
        {
            ClearLine();
            return;
        }

        Vector3 p0 = transform.position + (Vector3)startOffset;
        Vector2 v0 = player.GetPredictedJumpVelocity();
        Vector2 g = Physics2D.gravity * player.GetGravityScale();

        buf.Clear();
        buf.Add(p0);

        Vector3 prev = p0;
        float tPrev = 0f;

        for (int i = 1; i < points; i++)
        {
            float tCur = tPrev + step;

            Vector2 p = (Vector2)p0 + v0 * tCur + 0.5f * g * (tCur * tCur);
            Vector3 cur = new Vector3(p.x, p.y, 0f);

            if (stopOnHit)
            {
                if (TryCastBetween(prev, cur, out float hitFrac))
                {
                    float tHit = tPrev + step * Mathf.Clamp01(hitFrac);
                    Vector2 pHit = (Vector2)p0 + v0 * tHit + 0.5f * g * (tHit * tHit);
                    buf.Add(new Vector3(pHit.x, pHit.y, 0f));
                    break;
                }
            }

            buf.Add(cur);
            prev = cur;
            tPrev = tCur;
        }

        ApplyPositionsToLineRenderer();
        ApplyLineStyle();
    }

    private void ApplyPositionsToLineRenderer()
    {
        int n = buf.Count;
        if (n <= 0)
        {
            lr.positionCount = 0;
            return;
        }

        if (tmpPositions == null || tmpPositions.Length < n)
            tmpPositions = new Vector3[Mathf.NextPowerOfTwo(n)];

        for (int i = 0; i < n; i++)
            tmpPositions[i] = buf[i];

        lr.useWorldSpace = true;
        lr.positionCount = n;
        lr.SetPositions(tmpPositions);
    }

    private void ApplyLineStyle()
    {
        if (!useDottedLine)
        {
            // --- СПЛОШНАЯ ---
            if (solidMatInst != null) lr.material = solidMatInst;
            else if (solidMaterial != null) lr.material = solidMaterial; // на всякий
            // в сплошном режиме чаще лучше Stretch
            lr.textureMode = LineTextureMode.Stretch;
            return;
        }

        // --- ПУНКТИР ---
        if (dottedMatInst != null) lr.material = dottedMatInst;
        else if (dottedMaterial != null) lr.material = dottedMaterial;

        // важно: Tile, иначе текстура растянется на всю линию
        lr.textureMode = LineTextureMode.Tile;

        // Подгоняем тайлинг по длине линии, чтобы "dash+gap" был одинакового размера
        float len = ComputePolylineLength();
        float pattern = Mathf.Max(0.0001f, dottedWorldPatternSize);
        float tiles = len / pattern;

        // mainTextureScale.x = сколько раз повторить текстуру на длину линии
        // (требуется Wrap=Repeat на текстуре)
        var mat = lr.material; // инстанс на рендерере
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

    private float ComputePolylineLength()
    {
        float len = 0f;
        for (int i = 1; i < buf.Count; i++)
            len += Vector3.Distance(buf[i - 1], buf[i]);
        return len;
    }

    private bool TryCastBetween(Vector3 prev, Vector3 cur, out float hitFraction)
    {
        hitFraction = 1f;

        Vector2 dir = (Vector2)(cur - prev);
        float dist = dir.magnitude;
        if (dist < 0.00001f) return false;
        dir /= dist;

        filter.layerMask = hitMask;
        filter.useTriggers = !ignoreTriggers;

        // Если нет коллайдера — обычный Raycast
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

        if (hitCount <= 0) return false;

        float best = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            var h = hitBuf[i];
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
        if (playerCollider != null)
        {
            if (col == playerCollider) return true;
            if (col.transform.IsChildOf(playerCollider.transform)) return true;
        }
        return col.transform.IsChildOf(transform);
    }
}
