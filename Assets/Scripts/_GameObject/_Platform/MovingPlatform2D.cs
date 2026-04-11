using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform2D : MonoBehaviour
{
    [Header("Пути")]
    [Tooltip("Точки пути в мировых координатах (платформа ходит 1→2→…→N→…→2→1).")]
    public Transform[] waypoints;

    [Header("Движение")]
    [Tooltip("Скорость платформы (юн/с).")]
    public float speed = 2f;
    [Tooltip("Пауза на каждой точке (сек).")]
    public float dwellTime = 0.5f;
    [Tooltip("Насколько близко к точке считаем, что пришли.")]
    public float arriveDistance = 0.02f;
    [Tooltip("Ставить платформу в стартовую точку при запуске.")]
    public bool snapToFirstOnStart = true;

    [Header("Перевозка игрока")]
    [Tooltip("Галочка: если включено — стоящий сверху объект будет ехать вместе с платформой.")]
    public bool parentRider = true;

    // === доступно игроку ===
    public Vector2 FrameDelta { get; private set; }
    public Vector2 FrameVelocity => (Time.fixedDeltaTime > 1e-6f)
        ? FrameDelta / Time.fixedDeltaTime
        : Vector2.zero;

    private Rigidbody2D rb;
    private int idx = 0;     // текущая цель в массиве waypoints
    private int dir = +1;    // направление обхода (+1 → вперёд, -1 → назад)
    private bool waiting = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.isKinematic = true; // управляем вручную
    }

    void Start()
    {
        if (waypoints == null || waypoints.Length < 2)
        {
            Debug.LogWarning("[MovingPlatform2D] Нужны минимум 2 точки.");
            enabled = false;
            return;
        }

        if (snapToFirstOnStart)
        {
            rb.position = waypoints[0].position;
            idx = 1;
            dir = +1;
        }
        else
        {
            // найти ближайшую точку и направить к следующей
            int closest = 0; float best = float.MaxValue;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (!waypoints[i]) continue;
                float d = Vector2.SqrMagnitude((Vector2)waypoints[i].position - rb.position);
                if (d < best) { best = d; closest = i; }
            }
            idx = Mathf.Clamp(closest + 1, 1, waypoints.Length - 1);
            dir = +1;
        }
    }

    void FixedUpdate()
    {
        if (waiting)
        {
            FrameDelta = Vector2.zero;
            return;
        }

        Vector2 cur = rb.position;
        Vector2 target = waypoints[idx].position;

        Vector2 next = Vector2.MoveTowards(cur, target, speed * Time.fixedDeltaTime);

        // считаем дельту за этот физический шаг
        rb.MovePosition(next);
        FrameDelta = next - cur;

        // прибытие в точку
        if ((next - target).sqrMagnitude <= arriveDistance * arriveDistance)
            StartCoroutine(ArrivedAtPoint());
    }

    IEnumerator ArrivedAtPoint()
    {
        waiting = true;
        if (dwellTime > 0f) yield return new WaitForSeconds(dwellTime);

        if (idx == waypoints.Length - 1) dir = -1;
        else if (idx == 0) dir = +1;

        idx += dir;
        waiting = false;
    }

    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (!waypoints[i]) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.06f);
            if (i < waypoints.Length - 1 && waypoints[i + 1])
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }
    }
}
