using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class JumpTrajectory2D : MonoBehaviour
{
    [Header("������")]
    [SerializeField] private PlayerController player;   // �������� ���� PlayerController
    [SerializeField] private Rigidbody2D rb;            // Rigidbody ������ (��� gravityScale)

    [Header("���������")]
    [SerializeField] private int points = 30;           // ������� ����� ��������
    [SerializeField] private float step = 0.05f;        // ��� �� ������� ����� ������� (���)
    [SerializeField] private Vector2 startOffset = new Vector2(0f, 0.8f); // ������ �������� (��� �������)

    [Header("������������ (�����������)")]
    [SerializeField] private bool stopOnHit = true;     // �������� ����� �� ������� ���������
    [SerializeField] private LayerMask hitMask;         // �� ��� ����������� (Ground � �.�.)
    [SerializeField, Range(0.01f, 0.5f)] private float radius = 0.05f; // ������ ������ ��� ��������

    private LineRenderer lr;
    private readonly List<Vector3> buf = new List<Vector3>(128);

    private void Reset()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 0;
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.06f;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
    }

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        if (!player) player = GetComponent<PlayerController>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // ���������� ������ �� ����� ������� ������
        if (player != null && player.IsChargingJumpPublic)
        {
            DrawTrajectory();
        }
        else
        {
            if (lr.positionCount != 0) lr.positionCount = 0;
        }
    }

    private void DrawTrajectory()
    {
        if (rb == null || player == null || points <= 1 || step <= 0f)
        {
            lr.positionCount = 0;
            return;
        }

        Vector3 p0 = transform.position + (Vector3)startOffset;
        Vector2 v0 = player.GetPredictedJumpVelocity();

        // g = Physics2D.gravity * gravityScale
        Vector2 g = Physics2D.gravity * player.GetGravityScale();

        buf.Clear();
        buf.Add(p0);

        Vector3 prev = p0;
        float t = 0f;

        for (int i = 1; i < points; i++)
        {
            t += step;
            Vector2 p = (Vector2)p0 + v0 * t + 0.5f * g * (t * t);
            Vector3 cur = new Vector3(p.x, p.y, 0f);

            if (stopOnHit)
            {
                // ������� ��� ����� ����� prev � cur, ����� ������ ������ �����������
                RaycastHit2D hit = Physics2D.CircleCast((Vector2)prev, radius, (cur - prev).normalized,
                                                        Vector2.Distance(prev, cur), hitMask);
                if (hit.collider != null)
                {
                    buf.Add(hit.point);
                    break;
                }
            }

            buf.Add(cur);
            prev = cur;
        }

        lr.positionCount = buf.Count;
        lr.SetPositions(buf.ToArray());
    }
}
