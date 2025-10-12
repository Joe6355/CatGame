using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider2D))]
public class Piston : MonoBehaviour
{
    public enum DetectSide { Top, Bottom, Right, Left }

    [Header("Логика")]
    [SerializeField] private float cooldown = 2f;

    [Header("Сила пинка")]
    [SerializeField] private float launchForce = 15f;           // для Top/Bottom (по вертикали)
    [SerializeField] private float horizontalLaunchForce = 15f; // для Right/Left (по горизонтали)
    [SerializeField] private bool resetYBeforeLaunch = true;    // сбрасывать скорость вдоль оси пинка перед ударом

    [Header("Кого подкидывать")]
    [SerializeField] private LayerMask playerMask;

    [Header("Расположение шапки")]
    [SerializeField] private DetectSide detectSide = DetectSide.Top;
    [SerializeField, Range(0.1f, 2f)] private float detectWidthScale = 1.0f;
    [SerializeField] private float detectThickness = 0.12f;
    [SerializeField] private float detectGap = 0.02f;

    [Header("Фильтр «реально стоит» (опц.)")]
    [SerializeField] private bool requireNearlyZeroVy = true; // проверка скорости вдоль направления пинка
    [SerializeField] private float vyThreshold = 0.15f;

    [Header("Анимация")]
    [SerializeField] private Animator animator;
    [SerializeField] private string activateTrigger = "Activate";
    [SerializeField] private string returnTrigger = "Return";

    private BoxCollider2D col;
    private float nextReadyTime;
    private bool isActivating = false;

    // --- Новое: корректный отбор целей ---
    private ContactFilter2D contactFilter;                   // фильтр: наш слой + без триггеров
    private readonly Collider2D[] overlapBuf = new Collider2D[8];
    private readonly HashSet<Rigidbody2D> processedRBs = new HashSet<Rigidbody2D>();
    // --------------------------------------

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        if (!col) Debug.LogWarning("[Piston] Требуется BoxCollider2D на поршне.");

        if (!animator) animator = GetComponent<Animator>();
        if (!animator) Debug.LogWarning("[Piston] Требуется Animator для анимации поршня.");

        // Готовим фильтр: только слой игрока, без триггеров
        contactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        contactFilter.SetLayerMask(playerMask);
    }

    private void Start()
    {
        // Готов к срабатыванию сразу после старта сцены
        nextReadyTime = Time.time;
    }

    private void FixedUpdate()
    {
        if (col == null || Time.time < nextReadyTime) return;

        Vector2 center, size; float angle;
        GetDetectionBox(out center, out size, out angle);

        int count = Physics2D.OverlapBox(center, size, angle, contactFilter, overlapBuf);
        if (count <= 0) return;

        Vector2 dir = GetLaunchDirection();
        float force = GetLaunchForceForSide();
        Vector2 requiredSideNormal = GetOutwardNormalForSide();

        processedRBs.Clear();
        bool launchedThisFrame = false;

        for (int i = 0; i < count; i++)
        {
            var hit = overlapBuf[i];
            if (!hit || hit.isTrigger) continue;

            // Реальный контакт коллайдеров
            if (!col.IsTouching(hit)) continue;

            var rb = hit.attachedRigidbody;
            if (!rb) continue;

            // Один Rigidbody2D — один пинок за кадр
            if (!processedRBs.Add(rb)) continue;

            // Игрок должен быть с нужной стороны поршня (над ним для Top, и т.д.)
            Vector2 toOther = (Vector2)hit.bounds.center - (Vector2)col.bounds.center;
            if (Vector2.Dot(toOther.normalized, requiredSideNormal) < 0.6f) continue;

            // Фильтр скорости: отбрасываем только когда объект уже улетает ОТ поршня слишком быстро
            if (requireNearlyZeroVy)
            {
                float vAlong = Vector2.Dot(rb.velocity, dir); // скорость вдоль направления пинка
                if (vAlong > vyThreshold) continue; // приземление не режем (vAlong будет отрицательным)
            }

            // Отмена заряда прыжка, если есть свой контроллер
            var pc = rb.GetComponent<PlayerController>() ?? rb.GetComponentInParent<PlayerController>();
            if (pc != null) pc.CancelJumpCharge();

            // Пинок: через контроллер (если есть) или сырым методом
            if (pc != null) pc.ExternalPistonLaunch(dir, force, resetYBeforeLaunch);
            else LaunchRaw(rb, dir, force);

            launchedThisFrame = true;
        }

        // Анимация и кулдаун — только если действительно кого-то пнули
        if (launchedThisFrame)
        {
            if (animator && !isActivating)
            {
                animator.SetTrigger(activateTrigger);
                isActivating = true;
                Invoke(nameof(ReturnToIdle), cooldown * 0.5f);
            }

            nextReadyTime = Time.time + cooldown;
        }
    }

    private void ReturnToIdle()
    {
        if (animator) animator.SetTrigger(returnTrigger);
        isActivating = false;
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
        // Разлагаем скорость на вдоль-направления и ортогональную составляющие
        Vector2 v = rb.velocity;
        float vAlong = Vector2.Dot(v, dir);
        Vector2 vOrtho = v - vAlong * dir;

        if (resetYBeforeLaunch) vAlong = 0f; // обнуляем компонент вдоль направления пинка

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

    // Наружная нормаль поршня со стороны датчика (для проверки «с нужной стороны»)
    private Vector2 GetOutwardNormalForSide()
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
