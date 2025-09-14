using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Piston : MonoBehaviour
{
    public enum DetectSide { Top, Bottom, Right, Left }

    [Header("Логика")]
    [SerializeField] private float cooldown = 2f;

    [Header("Сила пинка")]
    [SerializeField] private float launchForce = 15f;              // для Top/Bottom (по вертикали)
    [SerializeField] private float horizontalLaunchForce = 15f;    // для Right/Left (по горизонтали)

    [SerializeField] private bool resetYBeforeLaunch = true; // сбрасываем скорость вдоль оси пинка перед ударом

    [Header("Кого подкидывать")]
    [SerializeField] private LayerMask playerMask;

    [Header("Расположение шапки")]
    [SerializeField] private DetectSide detectSide = DetectSide.Top;
    [SerializeField, Range(0.1f, 2f)] private float detectWidthScale = 1.0f;
    [SerializeField] private float detectThickness = 0.12f;
    [SerializeField] private float detectGap = 0.02f;

    [Header("Фильтр «реально стоит» (опц.)")]
    [SerializeField] private bool requireNearlyZeroVy = true; // проверяем скорость вдоль оси пинка
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

        Vector2 dir = GetLaunchDirection();
        float force = GetLaunchForceForSide(); // <-- берём вертикальную или горизонтальную силу

        foreach (var hit in hits)
        {
            var rb = hit.attachedRigidbody;
            if (!rb) continue;

            if (requireNearlyZeroVy)
            {
                float vAlong = Vector2.Dot(rb.velocity, dir);
                if (Mathf.Abs(vAlong) > vyThreshold) continue;
            }

            // отменяем заряд прыжка
            var pc = rb.GetComponent<PlayerController>() ?? rb.GetComponentInParent<PlayerController>();
            if (pc != null) pc.CancelJumpCharge();

            // корректный пинок через контроллер (фиксирует X в воздухе и т.п.)
            if (pc != null) pc.ExternalPistonLaunch(dir, force, resetYBeforeLaunch);
            else LaunchRaw(rb, dir, force);
        }

        nextReadyTime = Time.time + cooldown;
    }

    private float GetLaunchForceForSide()
    {
        switch (detectSide)
        {
            case DetectSide.Right:
            case DetectSide.Left:
                return horizontalLaunchForce;
            case DetectSide.Top:
            case DetectSide.Bottom:
            default:
                return launchForce;
        }
    }

    private void LaunchRaw(Rigidbody2D rb, Vector2 dir, float force)
    {
        Vector2 v = rb.velocity;
        float vAlong = Vector2.Dot(v, dir);
        Vector2 vOrtho = v - vAlong * dir;

        if (resetYBeforeLaunch) vAlong = 0f;

        rb.velocity = vOrtho + dir * force;
    }

    private Vector2 GetLaunchDirection()
    {
        switch (detectSide)
        {
            case DetectSide.Top: return transform.up.normalized;
            case DetectSide.Bottom: return (-transform.up).normalized;
            case DetectSide.Right: return transform.right.normalized;
            case DetectSide.Left: return (-transform.right).normalized;
            default: return Vector2.up;
        }
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

        // стрелка направления пинка
        Vector2 dir = Application.isPlaying ? GetLaunchDirection() : Vector2.up;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(center, (Vector3)dir * 0.6f);
    }
}
