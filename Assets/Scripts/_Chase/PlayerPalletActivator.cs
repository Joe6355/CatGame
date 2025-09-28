using UnityEngine;

/// <summary>
/// �������� �� ������. ��������� ������������ (���������) ��������� ������� �� ������.
/// </summary>
public class PlayerPalletActivator : MonoBehaviour
{
    [Header("�������")]
    [SerializeField] private KeyCode activateKey = KeyCode.F; // ����� �������������

    [Header("����� �������")]
    [SerializeField] private float activateRadius = 1.2f;
    [SerializeField] private LayerMask palletMask;
    [SerializeField] private string palletTag = "Pallet";

    [Header("��������� (�����������)")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.2f);

    private void Update()
    {
        if (!Input.GetKeyDown(activateKey)) return;

        // ���� ��������� �������
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, activateRadius, palletMask);
        Pallet2D best = null; float bestDist = float.MaxValue;

        foreach (var h in hits)
        {
            if (!h || !h.gameObject.activeInHierarchy) continue;
            if (!h.CompareTag(palletTag)) continue;
            var p = h.GetComponent<Pallet2D>() ?? h.GetComponentInParent<Pallet2D>();
            if (p == null || p.IsActivated) continue;

            float d = Vector2.SqrMagnitude((Vector2)h.bounds.ClosestPoint(transform.position) - (Vector2)transform.position);
            if (d < bestDist) { bestDist = d; best = p; }
        }

        if (best != null) best.Activate();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, activateRadius);
    }
#endif
}
