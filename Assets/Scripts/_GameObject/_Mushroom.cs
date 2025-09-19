using System.Collections.Generic;
using UnityEngine;

public class _Mushroom : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Transform platform;  // Движущаяся часть (Collider2D, IsTrigger=OFF)
    [SerializeField] private Transform top;       // Верхняя точка покоя
    [SerializeField] private Transform down;      // Нижняя точка проседания

    [Header("Игрок")]
    [SerializeField] private LayerMask playerMask = 0;

    [Header("Датчик «стоит сверху»")]
    [SerializeField] private float sensorThickness = 0.16f;                 // толщина вдоль оси гриба
    [SerializeField, Range(0.5f, 2f)] private float sensorWidthScale = 1.15f; // ширина = ширина верхней грани * коэф.
    [SerializeField] private float sensorGap = 0.02f;                       // зазор от поверхности

    [Header("Динамика проседания")]
    [SerializeField] private float minImpactSpeed = 0.3f;   // с этой скорости начинается проседание
    [SerializeField] private float maxImpactSpeed = 12f;    // на этой — уходит до down
    [SerializeField] private float compressLerpSpeed = 10f; // скорость утапливания к target01
    [SerializeField] private float recoverDelay = 0.25f;    // задержка перед подъёмом
    [SerializeField] private float recoverLerpSpeed = 2.5f; // скорость подъёма к top

    [Header("Гладкость (без прилипания)")]
    [SerializeField] private float riderStickTime = 0.08f;  // сколько держим «факт присутствия» после краткой потери контакта

    [Header("Отладка")]
    [SerializeField] private bool debugGizmos = false;

    private float current01 = 0f;     // текущая глубина [0..1]
    private float target01 = 0f;     // целевая глубина [0..1]
    private float recoverAtTime = 0f; // когда можно начинать подъём

    // только учёт присутствия игроков (без перемещения их вместе с платформой)
    private readonly HashSet<Rigidbody2D> riders = new HashSet<Rigidbody2D>();
    private readonly Dictionary<Rigidbody2D, float> riderLastSeen = new Dictionary<Rigidbody2D, float>();

    private Collider2D platformCol;
    private Rigidbody2D platformRb;
    private Vector3 prevPlatformPos;

    private void Awake()
    {
        if (!platform) { Debug.LogError("[_Mushroom] Не назначен Platform."); return; }

        platformCol = platform.GetComponent<Collider2D>();
        if (!platformCol || platformCol.isTrigger)
            Debug.LogError("[_Mushroom] На Platform нужен Collider2D c IsTrigger=OFF.");

        platformRb = platform.GetComponent<Rigidbody2D>(); // не обязателен
    }

    private void Start()
    {
        if (platform && top) platform.position = top.position;
        prevPlatformPos = platform ? platform.position : transform.position;
        current01 = target01 = 0f;
    }

    private void FixedUpdate()
    {
        // --- 1) Скан игроков в датчике над верхней гранью ---
        Vector2 up, center, size; float angleDeg;
        ComputeSensor(out up, out center, out size, out angleDeg);

        var cols = Physics2D.OverlapBoxAll(center, size, angleDeg, playerMask);
        // скорость платформы (для относительной скорости удара)
        Vector2 platVel = (Vector2)((platform.position - prevPlatformPos) / Mathf.Max(Time.fixedDeltaTime, 1e-6f));

        float bestTarget = target01;
        bool hadPressure = false;

        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                var rb = cols[i].attachedRigidbody;
                if (!rb) continue;

                riders.Add(rb);
                riderLastSeen[rb] = Time.time;

                // относительная скорость игрока к платформе вдоль -up
                float vRel = Vector2.Dot(rb.velocity - platVel, -up); // >0 — давит платформу
                if (vRel > 0f)
                {
                    hadPressure = true;
                    float hit01 = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, vRel);
                    if (hit01 > bestTarget) bestTarget = hit01;
                }
            }
        }

        // давление есть — утапливаем и откладываем подъём,
        // давления нет — разрешаем подниматься (даже если игрок стоит)
        if (hadPressure)
        {
            target01 = Mathf.Clamp01(bestTarget);
            recoverAtTime = Time.time + recoverDelay;
        }
        else
        {
            PruneRiders();
            if (Time.time >= recoverAtTime)
                target01 = Mathf.MoveTowards(target01, 0f, Time.fixedDeltaTime * recoverLerpSpeed);
        }

        // --- 2) Плавное движение к цели ---
        current01 = Mathf.Lerp(current01, target01, 1f - Mathf.Exp(-compressLerpSpeed * Time.fixedDeltaTime));

        // --- 3) Перемещаем платформу (НЕ трогаем игрока) ---
        if (platform && top && down)
        {
            Vector3 targetPos = Vector3.Lerp(top.position, down.position, Mathf.Clamp01(current01));

            if (platformRb) platformRb.MovePosition(targetPos);
            else platform.position = targetPos;

            prevPlatformPos = targetPos;
        }
    }

    // ---------- Датчик над верхней гранью ----------
    private void ComputeSensor(out Vector2 up, out Vector2 center, out Vector2 size, out float angleDeg)
    {
        up = UpAxis();

        Bounds b = platformCol ? platformCol.bounds : new Bounds(platform.position, Vector3.one * 0.5f);
        Vector2 ext = b.extents;

        Vector2 right = new Vector2(up.y, -up.x);
        float halfUp = Mathf.Abs(Vector2.Dot(up, new Vector2(ext.x, 0))) + Mathf.Abs(Vector2.Dot(up, new Vector2(0, ext.y)));
        float halfRt = Mathf.Abs(Vector2.Dot(right, new Vector2(ext.x, 0))) + Mathf.Abs(Vector2.Dot(right, new Vector2(0, ext.y)));

        Vector2 topFaceCenter = (Vector2)b.center + up * halfUp;

        center = topFaceCenter + up * (sensorGap + sensorThickness * 0.5f);
        size = new Vector2(halfRt * 2f * sensorWidthScale, sensorThickness);
        angleDeg = Vector2.SignedAngle(Vector2.up, up);
    }

    private Vector2 UpAxis()
    {
        if (top && down)
        {
            Vector2 a = (top.position - down.position);
            if (a.sqrMagnitude > 1e-6f) return a.normalized;
        }
        return Vector2.up;
    }

    private void PruneRiders()
    {
        if (riders.Count == 0) return;
        var toRemove = new List<Rigidbody2D>();
        foreach (var rb in riders)
        {
            if (!rb) { toRemove.Add(rb); continue; }
            float last = riderLastSeen.TryGetValue(rb, out var t) ? t : -999f;
            if (Time.time - last > riderStickTime) toRemove.Add(rb);
        }
        foreach (var rb in toRemove)
        {
            riders.Remove(rb);
            riderLastSeen.Remove(rb);
        }
    }

    // ---------- Гизмосы ----------
    private void OnDrawGizmosSelected()
    {
        if (!platform) return;

        Vector2 up = UpAxis();
        Collider2D col = platformCol ? platformCol : platform.GetComponent<Collider2D>();
        Bounds b = col ? col.bounds : new Bounds(platform.position, Vector3.one * 0.5f);
        Vector2 ext = b.extents;
        Vector2 right = new Vector2(up.y, -up.x);

        float halfUp = Mathf.Abs(Vector2.Dot(up, new Vector2(ext.x, 0))) + Mathf.Abs(Vector2.Dot(up, new Vector2(0, ext.y)));
        float halfRt = Mathf.Abs(Vector2.Dot(right, new Vector2(ext.x, 0))) + Mathf.Abs(Vector2.Dot(right, new Vector2(0, ext.y)));

        Vector2 topFaceCenter = (Vector2)b.center + up * halfUp;
        Vector2 center = topFaceCenter + up * (sensorGap + sensorThickness * 0.5f);
        Vector2 size = new Vector2(halfRt * 2f * sensorWidthScale, sensorThickness);
        float angleDeg = Vector2.SignedAngle(Vector2.up, up);

        if (debugGizmos)
        {
            Gizmos.color = Color.yellow;
            var m = Matrix4x4.TRS(center, Quaternion.Euler(0, 0, angleDeg), Vector3.one);
            Gizmos.matrix = m;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 1f));
            Gizmos.matrix = Matrix4x4.identity;

            if (top && down)
            {
                Gizmos.color = Color.green; Gizmos.DrawSphere(top.position, 0.035f);
                Gizmos.color = Color.red; Gizmos.DrawSphere(down.position, 0.035f);
                Gizmos.color = Color.cyan; Gizmos.DrawLine(down.position, top.position);
            }
        }
    }
}
