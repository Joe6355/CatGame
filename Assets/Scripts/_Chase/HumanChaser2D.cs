using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
public class HumanChaser2D : MonoBehaviour
{
    public enum EndBehavior
    {
        Stop,           // остановиться и стоять
        Despawn,        // уничтожить объект
        DisableScript,  // выключить этот скрипт
        InvokeEventOnly // только вызвать событие
    }

    [Header("Ссылки")]
    [SerializeField] private Transform player;
    [Tooltip("Где запускать погоню и где её закончить.")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private Transform spriteRoot;

    [Header("Движение")]
    [SerializeField] private float runSpeed = 4.5f;
    [SerializeField] private float stopAtEndDistance = 0.25f; // радиус финиша

    [Header("Прыжок через препятствия")]
    [SerializeField] private float jumpForce = 9f;
    [SerializeField] private float jumpCooldown = 0.25f;

    [Header("Граунд-чек")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Vector2 groundBoxSize = new(0.5f, 0.12f);
    [SerializeField] private Vector2 groundBoxOffset = new(0f, -0.3f);

    [Header("Обзор вперёд (Raycast)")]
    [Tooltip("Как далеко смотрим вперёд по горизонтали.")]
    [SerializeField] private float lookAhead = 0.6f;
    [Tooltip("Высота луча от центра для «луча головы».")]
    [SerializeField] private float headClearance = 0.5f;
    [SerializeField] private LayerMask obstacleMask; // обычно Ground | Pallet

    [Header("Палетки")]
    [Tooltip("Сколько секунд ломаем палетку.")]
    [SerializeField] private float palletBreakTime = 1.2f;
    [SerializeField] private string palletTag = "Pallet";

    [Header("Игрок/флипы")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool flipSpriteByDir = true;

    [Header("Финиш")]
    [SerializeField] private EndBehavior endBehavior = EndBehavior.Stop;
    [Tooltip("Задержка перед действием на финише (сек).")]
    [SerializeField] private float endDelay = 0f;
    [SerializeField] private UnityEvent onReachedEnd;

    private Rigidbody2D rb;
    private bool isGrounded;
    private int facing = +1;
    private float lastJumpTime = -999f;

    private bool breaking = false;
    private bool finished = false;

    // вектор пути (нормаль) и длина
    private Vector2 pathDir = Vector2.right;
    private float pathLen = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (!startPoint || !endPoint)
        {
            Debug.LogError("[HumanChaser2D] Назначьте startPoint и endPoint.");
            enabled = false; return;
        }

        // Старт — точно в начальную точку
        rb.position = startPoint.position;

        // Предрасчёт направления пути
        Vector2 a = startPoint.position;
        Vector2 b = endPoint.position;
        Vector2 ab = b - a;
        pathLen = ab.magnitude;
        pathDir = (pathLen > 1e-5f) ? ab / pathLen : Vector2.right;
    }

    private void FixedUpdate()
    {
        if (finished || breaking)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
            return;
        }

        UpdateGrounded();

        // Проверяем финиш: достигли (или перелетели) конечную точку вдоль пути
        if (ReachedOrPassedEnd())
        {
            finished = true;
            rb.velocity = new Vector2(0f, rb.velocity.y);
            StartCoroutine(HandleEndBehavior());
            return;
        }

        if (!player)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
            return;
        }

        // На отрезке «старт-финиш» бежим в сторону игрока, но направление ограничиваем вдоль пути.
        // Проецируем текущее положение игрока на ось пути и используем знак до игрока.
        float selfT = ScalarOnPath(rb.position);
        float playerT = ScalarOnPath(player.position);

        float dirOnPath = Mathf.Sign(playerT - selfT);
        if (Mathf.Abs(playerT - selfT) < 0.02f) dirOnPath = 0f; // почти совпали

        Vector2 desiredV = pathDir * (runSpeed * dirOnPath);
        rb.velocity = new Vector2(desiredV.x, rb.velocity.y);

        // флип по ходу
        if (flipSpriteByDir && Mathf.Abs(dirOnPath) > 0.01f)
        {
            facing = dirOnPath >= 0 ? +1 : -1;
            if (spriteRoot)
            {
                var s = spriteRoot.localScale;
                s.x = Mathf.Abs(s.x) * facing;
                spriteRoot.localScale = s;
            }
        }

        // Обзор впереди вдоль пути
        if (CheckObstacleAhead(out RaycastHit2D hit))
        {
            if (hit.collider != null && hit.collider.CompareTag(palletTag))
            {
                var pallet = hit.collider.GetComponent<Pallet2D>() ?? hit.collider.GetComponentInParent<Pallet2D>();
                if (pallet && pallet.IsBlocking)
                {
                    StartCoroutine(BreakPalletRoutine(pallet));
                    return;
                }
            }

            if (isGrounded) TryJump();
        }
    }

    // === Геометрия пути ===

    // Скаляр «проекция» точки на ось пути (0 — старт, pathLen — финиш)
    private float ScalarOnPath(Vector2 worldPos)
    {
        Vector2 a = startPoint.position;
        return Vector2.Dot(worldPos - a, pathDir);
    }

    private bool ReachedOrPassedEnd()
    {
        float t = ScalarOnPath(rb.position);
        // достигли радиуса финиша ИЛИ прошли дальше вдоль направления пути
        return (t >= pathLen - stopAtEndDistance) || (t > pathLen);
    }

    // === Перепрыг и палетки ===

    private void TryJump()
    {
        if (Time.time - lastJumpTime < jumpCooldown) return;
        lastJumpTime = Time.time;

        rb.velocity = new Vector2(rb.velocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    private IEnumerator BreakPalletRoutine(Pallet2D p)
    {
        breaking = true;
        rb.velocity = new Vector2(0f, rb.velocity.y);
        // TODO: тут можно запустить анимацию «ломаю»
        yield return new WaitForSeconds(palletBreakTime);
        if (p) p.BreakNow();
        breaking = false;
    }

    // === Обзор и земля ===

    private void UpdateGrounded()
    {
        Vector2 center = (Vector2)transform.TransformPoint(groundBoxOffset);
        isGrounded = Physics2D.OverlapBox(center, groundBoxSize, 0f, groundMask);
    }

    private bool CheckObstacleAhead(out RaycastHit2D hit)
    {
        // Луч на уровне головы
        Vector2 originHead = (Vector2)transform.position + Vector2.up * headClearance * 0.5f;
        Vector2 dir = new Vector2(pathDir.x, 0f).normalized;
        if (dir.sqrMagnitude < 1e-6f) dir = (facing >= 0) ? Vector2.right : Vector2.left;

        hit = Physics2D.Raycast(originHead, dir, lookAhead, obstacleMask);
        if (hit.collider != null) return true;

        // Луч от уровня ног
        Vector2 originFeet = (Vector2)transform.position + groundBoxOffset + new Vector2(0f, groundBoxSize.y * 0.5f);
        hit = Physics2D.Raycast(originFeet, dir, lookAhead, obstacleMask);
        return hit.collider != null;
    }

    // === Игрок пойман ===
    private void OnCollisionEnter2D(Collision2D c)
    {
        if (player && c.collider.CompareTag(playerTag))
        {
            Destroy(c.collider.gameObject); // удалить игрока
        }
    }

    // === Завершение ===
    private IEnumerator HandleEndBehavior()
    {
        if (endDelay > 0f) yield return new WaitForSeconds(endDelay);

        onReachedEnd?.Invoke();

        switch (endBehavior)
        {
            case EndBehavior.Stop:
                // уже остановились
                break;
            case EndBehavior.Despawn:
                Destroy(gameObject);
                break;
            case EndBehavior.DisableScript:
                enabled = false;
                break;
            case EndBehavior.InvokeEventOnly:
                // ничего
                break;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!startPoint || !endPoint) return;
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(startPoint.position, 0.06f);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(endPoint.position, 0.06f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(startPoint.position, endPoint.position);

        // зона финиша
        Gizmos.color = new Color(0, 1, 0, 0.25f);
        Gizmos.DrawWireSphere(endPoint.position, stopAtEndDistance);
    }

    private void OnDrawGizmosSelected()
    {
        // ground box
        Gizmos.color = Color.yellow;
        Vector2 center = Application.isPlaying ? (Vector2)transform.TransformPoint(groundBoxOffset)
                                               : (Vector2)(transform.position + (Vector3)groundBoxOffset);
        Gizmos.DrawWireCube(center, groundBoxSize);

        // rays
        Gizmos.color = Color.magenta;
        Vector2 dir = (Application.isPlaying ? new Vector2(pathDir.x, 0f).normalized : Vector2.right);
        Vector2 head = (Vector2)transform.position + Vector2.up * headClearance * 0.5f;
        Vector2 feet = (Vector2)transform.position + groundBoxOffset + new Vector2(0f, groundBoxSize.y * 0.5f);
        Gizmos.DrawLine(head, head + dir * lookAhead);
        Gizmos.DrawLine(feet, feet + dir * lookAhead);
    }
#endif
}
