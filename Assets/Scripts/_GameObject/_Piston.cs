using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Piston : MonoBehaviour
{
    public enum DetectSide { Top, Bottom, Right, Left }

    [Header("Логика")]
    [SerializeField] private float cooldown = 2f;
    [SerializeField] private float launchForce = 15f;
    [SerializeField] private bool resetYBeforeLaunch = true;

    [Header("Кого подкидывать")]
    [SerializeField] private LayerMask playerMask;

    [Header("Расположение шапки")]
    [SerializeField] private DetectSide detectSide = DetectSide.Top;
    [SerializeField, Range(0.1f, 2f)] private float detectWidthScale = 1.0f;
    [SerializeField] private float detectThickness = 0.12f;
    [SerializeField] private float detectGap = 0.02f;

    [Header("Фильтр «реально стоит» (опц.)")]
    [SerializeField] private bool requireNearlyZeroVy = true;
    [SerializeField] private float vyThreshold = 0.15f;

    private BoxCollider2D col;
    private float nextReadyTime;

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        if (!col) Debug.LogWarning("[Piston] Требуется BoxCollider2D на поршне.");
    }

    private void Start()
    {
        nextReadyTime = Time.time + cooldown; // если нужно сразу — Time.time
    }

    private void FixedUpdate()
    {
        if (Time.time < nextReadyTime || col == null) return;

        Vector2 center, size; float angle;
        GetDetectionBox(out center, out size, out angle);

        var hits = Physics2D.OverlapBoxAll(center, size, angle, playerMask);
        if (hits == null || hits.Length == 0) return;

        foreach (var hit in hits)
        {
            var rb = hit.attachedRigidbody;
            if (!rb) continue;

            if (requireNearlyZeroVy && Mathf.Abs(rb.velocity.y) > vyThreshold)
                continue;

            // 🔹 Перед пинком: отменяем заряд прыжка у игрока (если был)
            var pc = rb.GetComponent<PlayerController>() ?? rb.GetComponentInParent<PlayerController>();
            if (pc != null) pc.CancelJumpCharge();

            Launch(rb);
        }

        nextReadyTime = Time.time + cooldown;
    }

    private void Launch(Rigidbody2D rb)
    {
        if (resetYBeforeLaunch)
            rb.velocity = new Vector2(rb.velocity.x, 0f);

        rb.velocity = new Vector2(rb.velocity.x, launchForce);
        // Альтернатива: rb.AddForce(Vector2.up * launchForce, ForceMode2D.Impulse);
    }

    private void GetDetectionBox(out Vector2 center, out Vector2 size, out float angle)
    {
        Vector2 worldCenter = transform.TransformPoint(col.offset);
        float sx = Mathf.Abs(transform.lossyScale.x);
        float sy = Mathf.Abs(transform.lossyScale.y);
        float halfW = col.size.x * 0.5f * sx;
        float halfH = col.size.y * 0.5f * sy;

        Vector2 right = transform.right;
        Vector2 up = transform.up;

        switch (detectSide)
        {
            case DetectSide.Top:
                center = worldCenter + up * (halfH + detectGap + detectThickness * 0.5f);
                size = new Vector2(col.size.x * sx * detectWidthScale, detectThickness);
                break;
            case DetectSide.Bottom:
                center = worldCenter - up * (halfH + detectGap + detectThickness * 0.5f);
                size = new Vector2(col.size.x * sx * detectWidthScale, detectThickness);
                break;
            case DetectSide.Right:
                center = worldCenter + right * (halfW + detectGap + detectThickness * 0.5f);
                size = new Vector2(detectThickness, col.size.y * sy * detectWidthScale);
                break;
            case DetectSide.Left:
                center = worldCenter - right * (halfW + detectGap + detectThickness * 0.5f);
                size = new Vector2(detectThickness, col.size.y * sy * detectWidthScale);
                break;
            default:
                center = worldCenter; size = new Vector2(0.1f, 0.1f); break;
        }

        angle = transform.eulerAngles.z;
    }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<BoxCollider2D>();
        if (!c) return;

        col = c;
        Vector2 center, size; float angle;
        GetDetectionBox(out center, out size, out angle);

        Gizmos.color = (Application.isPlaying && Time.time >= nextReadyTime)
            ? Color.green : new Color(1f, 1f, 0f, 0.9f);

        var m = Matrix4x4.TRS(center, Quaternion.Euler(0, 0, angle), Vector3.one);
        Gizmos.matrix = m;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 1f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
