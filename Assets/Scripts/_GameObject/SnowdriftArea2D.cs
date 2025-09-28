using UnityEngine;

/// <summary>
/// ������: ���� ����� �������� ���� � ��� ������ ���������, � ������ ��������.
/// �������� � ��� Trigger, � ��� ������� Collider2D (����� ��������� ��������).
/// ������������� ������� IsTrigger = ON ��� ������� ���������.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SnowdriftArea2D : MonoBehaviour
{
    [Header("������ �������")]
    [Tooltip("�� ������� ��� ��������� �������� ���� (1 = ��� ���������, 0.5 = ����� ���������).")]
    [Range(0.1f, 1f)] public float moveSpeedMultiplier = 0.6f;

    [Tooltip("�� ������� ��� ��������� ���� ������ (1 = ��� ���������, 0.5 = ����� ������).")]
    [Range(0.1f, 1f)] public float jumpForceMultiplier = 0.7f;

    [Header("������ ������")]
    [Tooltip("������ ������ � ����� ������ PlayerController � �������������� �������.")]
    public string requiredTag = "Player"; // ����� �������� ������, ���� ��� �� ������������

    private Collider2D col;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (!col) Debug.LogWarning("[SnowdriftArea2D] ����� Collider2D.");
    }

    // ------- TRIGGER -------
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!col.isTrigger) return;
        TryRegister(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!col.isTrigger) return;
        TryUnregister(other);
    }

    // ------- COLLISION -------
    void OnCollisionEnter2D(Collision2D c)
    {
        if (col.isTrigger) return;
        TryRegister(c.collider);
    }

    void OnCollisionExit2D(Collision2D c)
    {
        if (col.isTrigger) return;
        TryUnregister(c.collider);
    }

    private void TryRegister(Collider2D other)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
        {
            // ���� � ������ ��� ���� Player � ����� ������ ����� ������ (�������� ������)
            // ���� �������� ��� Player �� ������ ������
            return;
        }

        var pc = other.GetComponentInParent<PlayerController>();
        if (!pc) return;

        pc.RegisterSnow(this, moveSpeedMultiplier, jumpForceMultiplier);
    }

    private void TryUnregister(Collider2D other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        if (!pc) return;

        pc.UnregisterSnow(this);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider2D>();
        if (!c) return;
        Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (c is BoxCollider2D b)
        {
            Gizmos.DrawCube(b.offset, b.size);
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.9f);
            Gizmos.DrawWireCube(b.offset, b.size);
        }
        else if (c is CircleCollider2D s)
        {
            Gizmos.DrawSphere(s.offset, s.radius);
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.9f);
            Gizmos.DrawWireSphere(s.offset, s.radius);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
