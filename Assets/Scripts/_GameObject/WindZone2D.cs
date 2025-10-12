using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WindZone2D : MonoBehaviour
{
    [Header("Кого зацеплять")]
    [SerializeField] private LayerMask affectedLayers; // обычно слой Player и т.п.

    [Header("Порывы")]
    [SerializeField, Tooltip("Сколько длится один порыв, сек")]
    private float gustDuration = 1.0f;

    [SerializeField, Tooltip("Пауза между порывами, сек")]
    private float gustCooldown = 2.0f;

    [SerializeField, Tooltip("Плавный разгон/спад порыва, сек (0 = мгновенно)")]
    private float rampTime = 0.15f;

    [Header("Сила ветра (ускорение)")]
    [SerializeField, Tooltip("Мин/макс ускорение (м/с²). Будет выбрано случайное значение на порыв")]
    private Vector2 windAccelRange = new Vector2(8f, 12f);

    [SerializeField, Tooltip("Использовать оси объекта (Right/Left относительно поворота зоны) вместо мировых X")]
    private bool useLocalAxes = false;

    [SerializeField, Tooltip("Одинаковое ускорение для всех масс (для AddForce)")]
    private bool massIndependent = true;

    // текущее состояние
    private readonly HashSet<Rigidbody2D> bodies = new HashSet<Rigidbody2D>();
    private float nextAllowedTime;
    private bool gustActive;
    private float gustStartTime;
    private int dirSign = +1;          // +1 = вправо, -1 = влево
    private float gustAccel;           // выбрано для текущего порыва
    private float currentAlpha;        // 0..1, для плавности

    private Collider2D col;

    private void Reset()
    {
        col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;
    }
    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col && !col.isTrigger) col.isTrigger = true;
        nextAllowedTime = Time.time; // первый порыв может начаться сразу
    }

    private void Update()
    {
        // управление таймингом порывов
        if (!gustActive && Time.time >= nextAllowedTime)
        {
            StartGust();
        }
        else if (gustActive)
        {
            float t = Time.time - gustStartTime;
            float end = gustDuration;

            // плавный вход/выход
            if (rampTime > 0f)
            {
                float inK = Mathf.Clamp01(t / Mathf.Max(0.0001f, rampTime));
                float outK = Mathf.Clamp01((end - t) / Mathf.Max(0.0001f, rampTime));
                currentAlpha = Mathf.Min(inK, outK); // треугольник
            }
            else currentAlpha = 1f;

            if (t >= end)
            {
                EndGust();
            }
        }
        else
        {
            currentAlpha = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (!gustActive || currentAlpha <= 0f || bodies.Count == 0) return;

        // направление (лево/право)
        Vector2 dir = useLocalAxes ? (Vector2)transform.right * dirSign
                                   : new Vector2(dirSign, 0f);

        foreach (var rb in bodies)
        {
            if (!rb) continue;

            // Если это игрок — даём приращение скорости Δv по X, чтобы контроллер не "съедал" AddForce
            var pc = rb.GetComponent<PlayerController>() ?? rb.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                // Δv = a * dt * знак(направления)
                float deltaVx = gustAccel * currentAlpha * Time.fixedDeltaTime * Mathf.Sign(dir.x);
                pc.AddExternalWindVX(deltaVx);
            }
            else
            {
                // Прочим телам — сила (ускорение). Масса учитывается опцией massIndependent
                float force = gustAccel * currentAlpha * (massIndependent ? rb.mass : 1f);
                rb.AddForce(dir * force, ForceMode2D.Force);
            }
        }

        // чистим умершие rigidbody из множества (редко, но полезно)
        bodies.RemoveWhere(rb => rb == null);
    }

    private void StartGust()
    {
        gustActive = true;
        gustStartTime = Time.time;
        dirSign = (Random.value < 0.5f) ? -1 : +1;
        gustAccel = Random.Range(Mathf.Min(windAccelRange.x, windAccelRange.y),
                                 Mathf.Max(windAccelRange.x, windAccelRange.y));
        currentAlpha = (rampTime > 0f) ? 0f : 1f;
    }

    private void EndGust()
    {
        gustActive = false;
        currentAlpha = 0f;
        nextAllowedTime = Time.time + gustCooldown;
    }

    private bool Affects(Collider2D other)
    {
        return (affectedLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Affects(other)) return;
        var rb = other.attachedRigidbody;
        if (rb) bodies.Add(rb);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var rb = other.attachedRigidbody;
        if (rb) bodies.Remove(rb);
    }

    // --------- Gizmos ----------
    private void OnDrawGizmos()
    {
        DrawGizmosImpl(selected: false);
    }
    private void OnDrawGizmosSelected()
    {
        DrawGizmosImpl(selected: true);
    }

    private void DrawGizmosImpl(bool selected)
    {
        Collider2D c = col ? col : GetComponent<Collider2D>();
        if (!c) return;

        // рамка зоны
        Gizmos.color = selected ? new Color(1f, 1f, 0f, 0.8f) : new Color(1f, 1f, 0f, 0.4f);
        var b = c.bounds;
        Gizmos.DrawWireCube(b.center, b.size);

        // стрелка направления текущего ветра
        Vector2 baseDir = useLocalAxes ? (Vector2)transform.right : Vector2.right;
        Vector2 showDir = baseDir * (Application.isPlaying ? dirSign : +1);

        float strength01 = Application.isPlaying ? Mathf.Clamp01(currentAlpha) : 0.6f;
        float len = Mathf.Max(b.extents.x, b.extents.y) * (0.6f + 0.6f * strength01);

        Vector3 start = b.center;
        Vector3 end = start + (Vector3)(showDir.normalized * len);

        Gizmos.color = (Application.isPlaying && gustActive)
            ? new Color(0.2f, 0.8f, 1f, 0.9f)
            : new Color(0.5f, 0.6f, 0.7f, 0.6f);

        Gizmos.DrawLine(start, end);
        // наконечник
        Vector3 side = Quaternion.Euler(0, 0, 25f) * (end - start);
        Gizmos.DrawLine(end, end - side * 0.25f);
        side = Quaternion.Euler(0, 0, -25f) * (end - start);
        Gizmos.DrawLine(end, end - side * 0.25f);
    }
}
