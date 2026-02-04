using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class JumpTrajectory2D : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField, Tooltip(
        "PlayerController, у которого берём:\n" +
        "- IsChargingJumpPublic (когда рисовать)\n" +
        "- GetPredictedJumpVelocity() (стартовая скорость прыжка)\n" +
        "- GetGravityScale() (масштаб гравитации)\n" +
        "Рекоменд: перетащить PlayerController игрока (или оставить пустым — найдёт GetComponent).")]
    private PlayerController player;

    [SerializeField, Tooltip(
        "Rigidbody2D игрока (просто проверка, что физика есть).\n" +
        "Рекоменд: Rigidbody2D игрока (или оставить пустым — найдёт GetComponent).")]
    private Rigidbody2D rb;

    [SerializeField, Tooltip(
        "Коллайдер игрока (Capsule/Box). Нужен, чтобы траектория НЕ проходила сквозь объекты,\n" +
        "а учитывала реальные размеры игрока.\n" +
        "Рекоменд: назначить Collider2D игрока (или оставить пустым — найдёт GetComponent).")]
    private Collider2D playerCollider;

    [Header("Отрисовка")]
    [SerializeField, Tooltip(
        "Сколько точек рисовать.\nБольше = плавнее, но дороже.\nРекоменд: 25–60 (часто 30–45).")]
    private int points = 35;

    [SerializeField, Tooltip(
        "Шаг времени между точками (сек).\nМеньше = точнее, но дороже.\nРекоменд: 0.03–0.07 (часто 0.05).")]
    private float step = 0.05f;

    [SerializeField, Tooltip(
        "Смещение старта линии относительно игрока.\n" +
        "Рекоменд: Y 0.5–1.2 (чтобы линия начиналась не из земли).")]
    private Vector2 startOffset = new Vector2(0f, 0.8f);

    [Header("Столкновения (важно)")]
    [SerializeField, Tooltip(
        "Если ВКЛ — линия обрывается в точке первого столкновения.\nРекоменд: ВКЛ (true).")]
    private bool stopOnHit = true;

    [SerializeField, Tooltip(
        "Слои, с которыми траектория сталкивается (Ground/Wall/Platform).\n" +
        "ВАЖНО: если платформа на другом слое — линия будет проходить сквозь.\n" +
        "Рекоменд: те же слои, что и у groundMask в PlayerController.")]
    private LayerMask hitMask;

    [SerializeField, Tooltip(
        "Игнорировать триггеры при проверке.\n" +
        "Рекоменд: ВКЛ (true), если у тебя есть триггер-зоны, чтобы траектория не обрубалась об них.")]
    private bool ignoreTriggers = true;

    [Header("One-way платформы (PlatformEffector2D)")]
    [SerializeField, Tooltip(
        "Если ВКЛ — учитываем one-way платформы (PlatformEffector2D) по направлению.\n" +
        "Типично: вверх можно пройти, вниз — блокирует.\n" +
        "Рекоменд: ВКЛ (true), если у тебя есть one-way платформы.")]
    private bool respectOneWayPlatforms = true;

    [SerializeField, Tooltip(
        "Минимальная 'вниз' скорость, чтобы считать, что мы падаем и one-way платформа должна блокировать.\n" +
        "Рекоменд: 0.01–0.2 (часто 0.05).")]
    private float oneWayDownThreshold = 0.05f;

    [Header("Gizmos (опционально)")]
    [SerializeField, Tooltip(
        "Рисовать гизмосы траектории/кастов при выделении объекта.\n" +
        "Рекоменд: ВКЛ на этапе отладки.")]
    private bool drawGizmos = false;

    private LineRenderer lr;
    private readonly List<Vector3> buf = new List<Vector3>(128);

    private readonly RaycastHit2D[] hitBuf = new RaycastHit2D[16];
    private ContactFilter2D filter;

    private void Reset()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 0;
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.06f;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
    }

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        if (!player) player = GetComponent<PlayerController>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!playerCollider) playerCollider = GetComponent<Collider2D>();

        filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = hitMask;
        filter.useTriggers = !ignoreTriggers;
    }

    private void Update()
    {
        if (player != null && player.IsChargingJumpPublic)
            DrawTrajectory();
        else if (lr.positionCount != 0)
            lr.positionCount = 0;
    }

    private void DrawTrajectory()
    {
        if (rb == null || player == null || points <= 1 || step <= 0f)
        {
            lr.positionCount = 0;
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
                    // ВАЖНО: заканчиваем на ПАРАБОЛЕ в момент удара, а не на hit.point
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

        lr.positionCount = buf.Count;
        lr.SetPositions(buf.ToArray());
    }


    private bool TryCastBetween(Vector3 prev, Vector3 cur, out float hitFraction)
    {
        hitFraction = 1f;

        Vector2 dir = (Vector2)(cur - prev);
        float dist = dir.magnitude;
        if (dist < 0.00001f) return false;

        dir /= dist;

        // fallback: Raycast
        if (playerCollider == null)
        {
            RaycastHit2D h = Physics2D.Raycast((Vector2)prev, dir, dist, hitMask);
            if (h.collider != null && !IsSelf(h.collider) && PassOneWayRule(h, dir))
            {
                hitFraction = h.fraction; // 0..1
                return true;
            }
            return false;
        }

        // обновим фильтр
        filter.layerMask = hitMask;
        filter.useTriggers = !ignoreTriggers;

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
            if (h.collider != null && !IsSelf(h.collider) && PassOneWayRule(h, dir))
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
            if (!PassOneWayRule(h, dir)) continue;

            if (h.fraction < best)
                best = h.fraction;
        }

        if (best < float.MaxValue)
        {
            hitFraction = best; // 0..1
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

    private bool PassOneWayRule(RaycastHit2D hit, Vector2 moveDir)
    {
        if (!respectOneWayPlatforms) return true;

        var eff = hit.collider.GetComponent<PlatformEffector2D>() ?? hit.collider.GetComponentInParent<PlatformEffector2D>();
        if (eff == null) return true;

        bool movingDown = moveDir.y < -Mathf.Max(0.0001f, oneWayDownThreshold);
        return movingDown;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.3f);
        Gizmos.DrawSphere(transform.position + (Vector3)startOffset, 0.06f);
    }
#endif
}
