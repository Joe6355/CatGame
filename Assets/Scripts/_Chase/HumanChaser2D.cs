using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
public class HumanChaser2D : MonoBehaviour
{
    public enum EndBehavior
    {
        Stop,           // ������������ � ������
        Despawn,        // ���������� ������
        DisableScript,  // ��������� ���� ������
        InvokeEventOnly // ������ ������� �������
    }

    [Header("������")]
    [SerializeField] private Transform player;
    [Tooltip("��� ��������� ������ � ��� � ���������.")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private Transform spriteRoot;

    [Header("��������")]
    [SerializeField] private float runSpeed = 4.5f;
    [SerializeField] private float stopAtEndDistance = 0.25f; // ������ ������

    [Header("������ ����� �����������")]
    [SerializeField] private float jumpForce = 9f;
    [SerializeField] private float jumpCooldown = 0.25f;

    [Header("������-���")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Vector2 groundBoxSize = new(0.5f, 0.12f);
    [SerializeField] private Vector2 groundBoxOffset = new(0f, -0.3f);

    [Header("����� ����� (Raycast)")]
    [Tooltip("��� ������ ������� ����� �� �����������.")]
    [SerializeField] private float lookAhead = 0.6f;
    [Tooltip("������ ���� �� ������ ��� ����� �������.")]
    [SerializeField] private float headClearance = 0.5f;
    [SerializeField] private LayerMask obstacleMask; // ������ Ground | Pallet

    [Header("�������")]
    [Tooltip("������� ������ ������ �������.")]
    [SerializeField] private float palletBreakTime = 1.2f;
    [SerializeField] private string palletTag = "Pallet";

    [Header("�����/�����")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool flipSpriteByDir = true;

    [Header("�����")]
    [SerializeField] private EndBehavior endBehavior = EndBehavior.Stop;
    [Tooltip("�������� ����� ��������� �� ������ (���).")]
    [SerializeField] private float endDelay = 0f;
    [SerializeField] private UnityEvent onReachedEnd;

    private Rigidbody2D rb;
    private bool isGrounded;
    private int facing = +1;
    private float lastJumpTime = -999f;

    private bool breaking = false;
    private bool finished = false;

    // ������ ���� (�������) � �����
    private Vector2 pathDir = Vector2.right;
    private float pathLen = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (!startPoint || !endPoint)
        {
            Debug.LogError("[HumanChaser2D] ��������� startPoint � endPoint.");
            enabled = false; return;
        }

        // ����� � ����� � ��������� �����
        rb.position = startPoint.position;

        // ���������� ����������� ����
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

        // ��������� �����: �������� (��� ����������) �������� ����� ����� ����
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

        // �� ������� ������-������ ����� � ������� ������, �� ����������� ������������ ����� ����.
        // ���������� ������� ��������� ������ �� ��� ���� � ���������� ���� �� ������.
        float selfT = ScalarOnPath(rb.position);
        float playerT = ScalarOnPath(player.position);

        float dirOnPath = Mathf.Sign(playerT - selfT);
        if (Mathf.Abs(playerT - selfT) < 0.02f) dirOnPath = 0f; // ����� �������

        Vector2 desiredV = pathDir * (runSpeed * dirOnPath);
        rb.velocity = new Vector2(desiredV.x, rb.velocity.y);

        // ���� �� ����
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

        // ����� ������� ����� ����
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

    // === ��������� ���� ===

    // ������ ���������� ����� �� ��� ���� (0 � �����, pathLen � �����)
    private float ScalarOnPath(Vector2 worldPos)
    {
        Vector2 a = startPoint.position;
        return Vector2.Dot(worldPos - a, pathDir);
    }

    private bool ReachedOrPassedEnd()
    {
        float t = ScalarOnPath(rb.position);
        // �������� ������� ������ ��� ������ ������ ����� ����������� ����
        return (t >= pathLen - stopAtEndDistance) || (t > pathLen);
    }

    // === �������� � ������� ===

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
        // TODO: ��� ����� ��������� �������� �������
        yield return new WaitForSeconds(palletBreakTime);
        if (p) p.BreakNow();
        breaking = false;
    }

    // === ����� � ����� ===

    private void UpdateGrounded()
    {
        Vector2 center = (Vector2)transform.TransformPoint(groundBoxOffset);
        isGrounded = Physics2D.OverlapBox(center, groundBoxSize, 0f, groundMask);
    }

    private bool CheckObstacleAhead(out RaycastHit2D hit)
    {
        // ��� �� ������ ������
        Vector2 originHead = (Vector2)transform.position + Vector2.up * headClearance * 0.5f;
        Vector2 dir = new Vector2(pathDir.x, 0f).normalized;
        if (dir.sqrMagnitude < 1e-6f) dir = (facing >= 0) ? Vector2.right : Vector2.left;

        hit = Physics2D.Raycast(originHead, dir, lookAhead, obstacleMask);
        if (hit.collider != null) return true;

        // ��� �� ������ ���
        Vector2 originFeet = (Vector2)transform.position + groundBoxOffset + new Vector2(0f, groundBoxSize.y * 0.5f);
        hit = Physics2D.Raycast(originFeet, dir, lookAhead, obstacleMask);
        return hit.collider != null;
    }

    // === ����� ������ ===
    private void OnCollisionEnter2D(Collision2D c)
    {
        if (player && c.collider.CompareTag(playerTag))
        {
            Destroy(c.collider.gameObject); // ������� ������
        }
    }

    // === ���������� ===
    private IEnumerator HandleEndBehavior()
    {
        if (endDelay > 0f) yield return new WaitForSeconds(endDelay);

        onReachedEnd?.Invoke();

        switch (endBehavior)
        {
            case EndBehavior.Stop:
                // ��� ������������
                break;
            case EndBehavior.Despawn:
                Destroy(gameObject);
                break;
            case EndBehavior.DisableScript:
                enabled = false;
                break;
            case EndBehavior.InvokeEventOnly:
                // ������
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

        // ���� ������
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
