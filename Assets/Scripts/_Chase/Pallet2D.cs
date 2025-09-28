using UnityEngine;

/// <summary>
/// �������: ����� ����� � "�������" (������������) �������.
/// � �������� ��������� ������� ������ � ���������� ������������ (IsBlocking=true).
/// ��������������, ��������, ������ ������ � ����� ������� ������� ���������.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Pallet2D : MonoBehaviour
{
    [Header("��������� �� ���������")]
    [Tooltip("��������� �� ��������� (������ IsTrigger=ON ��� Disabled), ����� �������� ��� ��.")]
    [SerializeField] private Collider2D preCollider;
    [Tooltip("RigidBody �� ��������� (������ isKinematic=true/Gravity=0).")]
    [SerializeField] private Rigidbody2D preBody;

    [Header("��������� ����� ��������� (�������)")]
    [Tooltip("�������� ����������, ������� ��������� ������ � ������ ������.")]
    [SerializeField] private float postGravityScale = 2.5f;
    [SerializeField] private bool postColliderIsTrigger = false;

    [Header("�����")]
    [SerializeField] private bool startInactive = true;
    [SerializeField] private string palletTag = "Pallet";

    public bool IsBlocking { get; private set; } = false;
    public bool IsActivated { get; private set; } = false;

    private Collider2D ownCol;
    private Rigidbody2D body;

    private void Awake()
    {
        ownCol = GetComponent<Collider2D>();
        body = GetComponent<Rigidbody2D>();
        gameObject.tag = palletTag;

        if (preCollider == null) preCollider = ownCol;
        if (preBody == null) preBody = body;

        if (startInactive)
            ApplyPreState();
        else
            ApplyPostState();
    }

    private void ApplyPreState()
    {
        IsActivated = false;
        IsBlocking = false;

        if (preBody)
        {
            preBody.isKinematic = true;
            preBody.gravityScale = 0f;
            preBody.velocity = Vector2.zero;
            preBody.angularVelocity = 0f;
        }
        if (preCollider)
        {
            preCollider.enabled = true;
            preCollider.isTrigger = true; // �� ��������� � �� ���������
        }
    }

    private void ApplyPostState()
    {
        IsActivated = true;
        IsBlocking = true;

        if (body)
        {
            body.isKinematic = false;
            body.gravityScale = postGravityScale;
        }
        if (ownCol)
        {
            ownCol.enabled = true;
            ownCol.isTrigger = postColliderIsTrigger; // ������ false => ������� �����������
        }
    }

    /// <summary>���������� ������� ��� ������� ������ ����� � ��������.</summary>
    public void Activate()
    {
        if (IsActivated) return;
        ApplyPostState();
    }

    /// <summary>���������� ��������������� ����� "�������".</summary>
    public void BreakNow()
    {
        Destroy(gameObject);
    }
}
