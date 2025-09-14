using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class _SteamVent : MonoBehaviour
{
    public enum Side { Top, Bottom, Right, Left }

    [Header("Тайминги")]
    [SerializeField] private float cooldown = 2f;     // пауза между циклами
    [SerializeField] private float duration = 1.5f;   // сколько дует

    [Header("Сила")]
    [SerializeField] private float baseForce = 12f;   // базовая сила (Н) каждый FixedUpdate
    [SerializeField] private float peakMultiplier = 2.0f;  // усиление к концу (x)
    [SerializeField, Range(0.5f, 0.99f)] private float blastThreshold = 0.9f; // с какой доли времени дать импульс
    [SerializeField] private float blastImpulse = 10f;   // финальный рывок (импульс)

    [Header("Кривая роста силы (0..1 времени → множитель)")]
    [SerializeField]
    private AnimationCurve strengthCurve =
        AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f); // можно править в инспекторе

    [Header("Кого дуть")]
    [SerializeField] private LayerMask playerMask;

    [Header("Геометрия выдува (относительно BoxCollider2D)")]
    [SerializeField] private Side side = Side.Top;
    [SerializeField, Tooltip("Длина струи от трубы в метрах")]
    private float range = 2.0f;
    [SerializeField, Tooltip("Ширина струи поперёк направления (как доля ширины/высоты коллайдера)")]
    private float widthScale = 1.0f;
    [SerializeField, Tooltip("Зазор между трубой и началом струи")]
    private float gap = 0.02f;

    private BoxCollider2D col;
    private bool isBlowing = false;
    private float blowStartTime = 0f;
    private float nextReadyTime = 0f;
    private readonly HashSet<int> blastedThisCycle = new HashSet<int>(); // чтобы импульс один раз

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        if (!col) Debug.LogWarning("[SteamVent] Нужен BoxCollider2D на трубе.");
    }

    private void Start()
    {
        isBlowing = false;
        nextReadyTime = Time.time + cooldown;
    }

    private void FixedUpdate()
    {
        if (!isBlowing)
        {
            if (Time.time >= nextReadyTime) StartBlow();
            return;
        }

        float t = Mathf.InverseLerp(blowStartTime, blowStartTime + duration, Time.time);
        if (t >= 1f)
        {
            StopBlow();
            return;
        }

        // Текущее направление и область
        Vector2 dir = GetDir();
        GetBlowBox(out Vector2 center, out Vector2 size, out float angleDeg);

        // Сила на этом кадре (нарастает)
        float mul = strengthCurve.Evaluate(Mathf.Clamp01(t));
        float currForce = baseForce * Mathf.Lerp(1f, peakMultiplier, mul);

        // Кого задело — тянем
        var hits = Physics2D.OverlapBoxAll(center, size, angleDeg, playerMask);
        if (hits == null || hits.Length == 0) return;

        foreach (var h in hits)
        {
            var rb = h.attachedRigidbody;
            if (!rb) continue;

            // Постоянная тяга — можно против ветра, но будет «пихать»
            rb.AddForce(dir * currForce, ForceMode2D.Force);

            // Финальный откидывающий импульс (однократно на цикл)
            if (t >= blastThreshold && !blastedThisCycle.Contains(rb.GetInstanceID()))
            {
                rb.AddForce(dir * blastImpulse, ForceMode2D.Impulse);
                blastedThisCycle.Add(rb.GetInstanceID());
            }
        }
    }

    private void StartBlow()
    {
        isBlowing = true;
        blowStartTime = Time.time;
        blastedThisCycle.Clear();
    }

    private void StopBlow()
    {
        isBlowing = false;
        nextReadyTime = Time.time + cooldown;
        blastedThisCycle.Clear();
    }

    // ---- Геометрия и направление ----

    private Vector2 GetDir()
    {
        switch (side)
        {
            case Side.Top: return transform.up.normalized;
            case Side.Bottom: return (-transform.up).normalized;
            case Side.Right: return transform.right.normalized;
            case Side.Left: return (-transform.right).normalized;
            default: return Vector2.up;
        }
    }

    private void GetBlowBox(out Vector2 center, out Vector2 size, out float angleDeg)
    {
        // Базируемся на BoxCollider2D
        Vector2 worldCenter = transform.TransformPoint(col.offset);
        float sx = Mathf.Abs(transform.lossyScale.x);
        float sy = Mathf.Abs(transform.lossyScale.y);
        float halfW = col.size.x * 0.5f * sx;
        float halfH = col.size.y * 0.5f * sy;

        Vector2 right = transform.right;
        Vector2 up = transform.up;

        switch (side)
        {
            case Side.Top:
                center = worldCenter + up * (halfH + gap + range * 0.5f);
                size = new Vector2(col.size.x * sx * widthScale, range);
                break;
            case Side.Bottom:
                center = worldCenter - up * (halfH + gap + range * 0.5f);
                size = new Vector2(col.size.x * sx * widthScale, range);
                break;
            case Side.Right:
                center = worldCenter + right * (halfW + gap + range * 0.5f);
                size = new Vector2(range, col.size.y * sy * widthScale);
                break;
            case Side.Left:
                center = worldCenter - right * (halfW + gap + range * 0.5f);
                size = new Vector2(range, col.size.y * sy * widthScale);
                break;
            default:
                center = worldCenter; size = new Vector2(0.1f, 0.1f); break;
        }

        angleDeg = transform.eulerAngles.z;
    }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<BoxCollider2D>();
        if (!c) return;
        col = c;

        GetBlowBox(out Vector2 center, out Vector2 size, out float angleDeg);

        Gizmos.color = (isBlowing ? Color.cyan : new Color(1f, 1f, 0f, 0.9f));
        var m = Matrix4x4.TRS(center, Quaternion.Euler(0, 0, angleDeg), Vector3.one);
        Gizmos.matrix = m;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 1f));
        Gizmos.matrix = Matrix4x4.identity;

        // Стрелка направления
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(center, (Vector3)GetDir() * 0.8f);
    }
}
