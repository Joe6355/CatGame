using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class JumpTrajectory2D : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private PlayerController player;   // перетащи сюда PlayerController
    [SerializeField] private Rigidbody2D rb;            // Rigidbody игрока (для gravityScale)

    [Header("Отрисовка")]
    [SerializeField] private int points = 30;           // сколько точек рисовать
    [SerializeField] private float step = 0.05f;        // шаг по времени между точками (сек)
    [SerializeField] private Vector2 startOffset = new Vector2(0f, 0.8f); // откуда рисовать (над центром)

    [Header("Столкновения (опционально)")]
    [SerializeField] private bool stopOnHit = true;     // обрубать линию по первому попаданию
    [SerializeField] private LayerMask hitMask;         // во что «врезаться» (Ground и т.д.)
    [SerializeField, Range(0.01f, 0.5f)] private float radius = 0.05f; // радиус «капли» при проверке

    private LineRenderer lr;
    private readonly List<Vector3> buf = new List<Vector3>(128);

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
    }

    private void Update()
    {
        // показываем только во время зарядки прыжка
        if (player != null && player.IsChargingJumpPublic)
        {
            DrawTrajectory();
        }
        else
        {
            if (lr.positionCount != 0) lr.positionCount = 0;
        }
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

        // g = Physics2D.gravity * gravityScale
        Vector2 g = Physics2D.gravity * player.GetGravityScale();

        buf.Clear();
        buf.Add(p0);

        Vector3 prev = p0;
        float t = 0f;

        for (int i = 1; i < points; i++)
        {
            t += step;
            Vector2 p = (Vector2)p0 + v0 * t + 0.5f * g * (t * t);
            Vector3 cur = new Vector3(p.x, p.y, 0f);

            if (stopOnHit)
            {
                // капсула как «след» между prev и cur, чтобы ловить тонкие поверхности
                RaycastHit2D hit = Physics2D.CircleCast((Vector2)prev, radius, (cur - prev).normalized,
                                                        Vector2.Distance(prev, cur), hitMask);
                if (hit.collider != null)
                {
                    buf.Add(hit.point);
                    break;
                }
            }

            buf.Add(cur);
            prev = cur;
        }

        lr.positionCount = buf.Count;
        lr.SetPositions(buf.ToArray());
    }
}
