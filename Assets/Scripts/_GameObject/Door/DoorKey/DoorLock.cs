using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DoorLock : MonoBehaviour
{
    [Header("ID �����")]
    [SerializeField] private int doorId = 1;

    [Header("���� ��� �����")]
    [Tooltip("�����, ���� ������ ��������� ���� ����� ���������.")]
    [SerializeField] private Transform keySlotPoint;

    [Header("��� �������")]
    [Tooltip("��� ���������, ����� ����� ��������� (���� �����/������/����������).")]
    [SerializeField] private GameObject doorRootToDisable;

    [Header("���� ������")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("������� ����� ����� ������ ����� �� �������� (��� ��������).")]
    [SerializeField] private float openDelay = 0.1f;

    [Header("��������� �������������")]
    [SerializeField] private bool oneShot = true; // ������� ���� ��� � ������ �� ���������

    [Header("��������")]
    [Tooltip("Animator ��� ������������ �������� �����.")]
    [SerializeField] private Animator doorAnimator;

    private bool opened = false;
    Collider2D triggerCol;

    void Awake()
    {
        triggerCol = GetComponent<Collider2D>();
        if (triggerCol) triggerCol.isTrigger = true;
        if (!doorRootToDisable) doorRootToDisable = gameObject; // �� ��������� ��������� ��� ������ �����

        // ���� �������� �� ��������, ��������� ����� ��� �� ���� �������
        if (!doorAnimator) doorAnimator = GetComponent<Animator>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (opened && oneShot) return;
        if (!other.CompareTag(playerTag)) return;

        var ring = other.GetComponent<PlayerKeyRing>() ?? other.GetComponentInParent<PlayerKeyRing>();
        if (!ring) return;

        if (!ring.HasKey(doorId)) return;

        // ���������� ���� � ����; �� ������ � ��������� �����
        if (!keySlotPoint) { OpenDoorInstant(); ring.GiveKeyToDoor(doorId, other.transform, () => { }); return; }

        ring.GiveKeyToDoor(doorId, keySlotPoint, () => { StartCoroutine(OpenAfterDelay()); });
    }

    System.Collections.IEnumerator OpenAfterDelay()
    {
        // 1) ��������� �������� ��������
        if (doorAnimator)
        {
            doorAnimator.SetTrigger("OpenDoor");
        }

        // 2) ���� ��������� �������� ����� ������ ���������
        if (openDelay > 0f) yield return new WaitForSeconds(openDelay);

        // 3) ��������� ��������
        OpenDoorInstant();
    }

    void OpenDoorInstant()
    {
        if (opened && oneShot) return;
        opened = true;

        // 1) ��������� ����/������� (��������)
        // PlaySound(); 

        // 2) �������� �������� ��� �������� � OpenAfterDelay

        // 3) �� ��������� � ��������� �����
        if (doorRootToDisable) doorRootToDisable.SetActive(false);

        // ���� ����� ����������� � ����� ��������� �������
        if (oneShot && triggerCol) triggerCol.enabled = false;
    }
}