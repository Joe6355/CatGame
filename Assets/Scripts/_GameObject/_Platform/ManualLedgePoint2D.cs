using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ManualLedgePoint2D : MonoBehaviour
{
    [Header("Ручной ledge")]
    [SerializeField, Tooltip("Точка, в которую снапается корень игрока во время висения.")]
    private Transform hangPoint;

    [SerializeField, Tooltip("Точка, в которую должен приехать корень игрока после подтягивания.")]
    private Transform standPoint;

    [SerializeField, Tooltip("Если ВКЛ — пока висим на этом ledge, игрок смотрит вправо.\nЕсли ВЫКЛ — влево.")]
    private bool playerFacesRightWhileHanging = true;

    [SerializeField, Tooltip("Если ВКЛ — зацеп разрешён только при движении в сторону ledge.\nДля ручных краёв это обычно нужно оставлять включённым.")]
    private bool requireMatchingApproachDirection = true;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    public Vector2 HangPointPosition => hangPoint != null ? (Vector2)hangPoint.position : (Vector2)transform.position;
    public Vector2 StandPointPosition => standPoint != null ? (Vector2)standPoint.position : HangPointPosition;
    public bool PlayerFacesRightWhileHanging => playerFacesRightWhileHanging;
    public float RequiredApproachDirectionX => requireMatchingApproachDirection ? (playerFacesRightWhileHanging ? 1f : -1f) : 0f;

    private void Reset()
    {
        EnsureTrigger();
    }

    private void OnValidate()
    {
        EnsureTrigger();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        NotifyStay(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        NotifyStay(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerLedgeModule ledgeModule = other.GetComponentInParent<PlayerLedgeModule>();
        if (ledgeModule != null)
            ledgeModule.ReportCandidateExit(this);
    }

    private void NotifyStay(Collider2D other)
    {
        PlayerLedgeModule ledgeModule = other.GetComponentInParent<PlayerLedgeModule>();
        if (ledgeModule != null)
            ledgeModule.ReportCandidateStay(this);
    }

    private void EnsureTrigger()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        Vector3 hang = hangPoint != null ? hangPoint.position : transform.position;
        Vector3 stand = standPoint != null ? standPoint.position : hang;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.95f);
        Gizmos.DrawSphere(hang, 0.05f);

        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.95f);
        Gizmos.DrawSphere(stand, 0.05f);
        Gizmos.DrawLine(hang, stand);

        float dir = playerFacesRightWhileHanging ? 1f : -1f;
        Vector3 arrowTip = hang + Vector3.right * dir * 0.18f;

        Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.95f);
        Gizmos.DrawLine(hang, arrowTip);
        Gizmos.DrawLine(arrowTip, arrowTip + new Vector3(-dir * 0.06f, 0.04f, 0f));
        Gizmos.DrawLine(arrowTip, arrowTip + new Vector3(-dir * 0.06f, -0.04f, 0f));
    }
}