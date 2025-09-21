using UnityEngine;

public class _MushroomBounce : MonoBehaviour
{
    public enum AnimMode { AutoByImpact, ForceSlight, ForceStrong }
    public enum BounceTrend { None, Increase, Decrease }

    [Header("��������� �����")]
    [SerializeField] private Collider2D capCollider;   // IsTrigger = OFF

    [Header("��� �����")]
    [SerializeField] private Transform capUpReference; // ���� ����� � cap.transform.up
    [SerializeField] private bool invertUpAxis = false;

    [Header("���-�������� ����� (������������ ������)")]
    [SerializeField] private PhysicsMaterial2D baseCapMaterial;
    [SerializeField, Range(0f, 1f)] private float minBounciness = 0.15f;
    [SerializeField, Range(0f, 1f)] private float maxBounciness = 0.95f;
    [SerializeField] private AnimationCurve bouncinessCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("����-��������� ��� Decrease")]
    [SerializeField, Range(0f, 1f)] private float minBouncinessDecrease = 0f;
    [SerializeField] private bool absorbAtMin = true;
    [SerializeField, Range(0f, 1f)] private float absorbFactor = 0f; // 0 = ��������� ��������
    [SerializeField, Range(0f, 0.2f)] private float idleThreshold01 = 0.03f;

    [Tooltip("�������/�������� ������ ��� ������ '����'. �������� ����������, ����� �� �������.")]
    [SerializeField, Range(0.5f, 1f)] private float standEnterDotUp = 0.92f;
    [SerializeField, Range(0.5f, 1f)] private float standExitDotUp = 0.86f;
    [SerializeField, Range(0f, 0.2f)] private float standEnterSpeed = 0.04f;
    [SerializeField, Range(0f, 0.3f)] private float standExitSpeed = 0.07f;

    [Tooltip("��������� �������������� ���������� ���������� ����� ��� �����, ���� �����.")]
    [SerializeField] private bool cancelGravityWhileStanding = true;

    [Header("��������� �����")]
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private float minImpactSpeed = 0.05f;
    [SerializeField] private float maxImpactSpeed = 7.0f;

    [Header("����� ��������")]
    [SerializeField] private AnimMode animationMode = AnimMode.AutoByImpact;
    [SerializeField, Range(0f, 1f)] private float strongThreshold01 = 0.45f;

    [Header("Animator (�������� � Animator)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string paramImpact01 = "Impact01";     // Float (0..1)
    [SerializeField] private string paramPlaySlight = "PlaySlight"; // Trigger
    [SerializeField] private string paramPlayStrong = "PlayStrong"; // Trigger
    [SerializeField] private string paramPlayIdle = "PlayIdle";   // Trigger (�����������)

    [Header("����-���� ���������")]
    [SerializeField] private float triggerCooldown = 0.06f;

    [Header("Air-control ������ (���)")]
    [SerializeField] private float airControlTime = 0.35f;

    [Header("����� ���� ����� ������ �������")]
    [SerializeField] private BounceTrend bounceTrend = BounceTrend.None;
    [SerializeField, Range(0f, 1f)] private float trendStep01 = 0.12f;
    [SerializeField, Min(0)] private int maxTrendStacks = 4;
    [SerializeField, Min(0f)] private float trendResetDelay = 1.0f;

    [Header("�������")]
    [SerializeField] private bool debugLog = false;

    // ---- runtime ----
    private PhysicsMaterial2D runtimeCapMat;
    private int hashImpact01, hashPlaySlight, hashPlayStrong, hashPlayIdle;
    private float nextTriggerTime = 0f;

    // �����
    private int trendStacks = 0;
    private float lastHitTime = -999f;

    // standing-lock
    private bool standingLock = false;
    private Rigidbody2D standingRb = null;
    private Vector2 lastUp = Vector2.up;

    private void Awake()
    {
        if (!capCollider)
        {
            Debug.LogError("[_MushroomBounce] �� �������� capCollider (�����).");
            enabled = false; return;
        }

        runtimeCapMat = new PhysicsMaterial2D((baseCapMaterial ? baseCapMaterial.name : "CapMat") + "_Runtime");
        if (baseCapMaterial)
        {
            runtimeCapMat.friction = baseCapMaterial.friction;
            runtimeCapMat.bounciness = baseCapMaterial.bounciness;
        }
        capCollider.sharedMaterial = runtimeCapMat;

        var fwd = capCollider.GetComponent<_MushroomCapForwarder>();
        if (!fwd) fwd = capCollider.gameObject.AddComponent<_MushroomCapForwarder>();
        fwd.owner = this;

        CacheParamHashes();
    }

    private void OnValidate() => CacheParamHashes();

    private void CacheParamHashes()
    {
        hashImpact01 = Animator.StringToHash(paramImpact01);
        hashPlaySlight = Animator.StringToHash(paramPlaySlight);
        hashPlayStrong = Animator.StringToHash(paramPlayStrong);
        hashPlayIdle = Animator.StringToHash(paramPlayIdle);
    }

    // ======== ����� �� ����� ========
    public void OnCapEnter(Collision2D c) => EvaluateHit(c);
    public void OnCapStay(Collision2D c) => EvaluateHit(c);
    public void OnCapExit(Collision2D c)
    {
        // ������ ������� � ������� standing-lock, ���� ��� ��
        if (standingLock && standingRb && (c.otherRigidbody == standingRb || c.collider.attachedRigidbody == standingRb))
        {
            standingLock = false;
            standingRb = null;
        }
    }

    private void EvaluateHit(Collision2D c)
    {
        var rb = c.otherRigidbody ? c.otherRigidbody : c.collider.attachedRigidbody;
        if (!rb) return;
        if ((playerMask.value & (1 << rb.gameObject.layer)) == 0) return;

        Vector2 up = capUpReference ? (Vector2)capUpReference.up : (Vector2)capCollider.transform.up;
        if (invertUpAxis) up = -up;
        lastUp = up;

        if (Time.time - lastHitTime > trendResetDelay) trendStacks = 0;

        // �������� ���������
        float impactA = Vector2.Dot(-c.relativeVelocity, up);
        float impactB = Vector2.Dot(rb.velocity, up);
        float impact = Mathf.Max(impactA, impactB);
        if (impact <= minImpactSpeed) return;

        float t01 = Mathf.Clamp01(Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, impact));

        // ---- ����� Decrease: �������� ������ � ������������ ----
        if (bounceTrend == BounceTrend.Decrease)
        {
            var n = c.GetContact(0).normal;
            float dot = Vector2.Dot(n, up);
            float relUpSpeed = Mathf.Abs(Vector2.Dot(c.relativeVelocity, up));

            if (!standingLock)
            {
                // ������� �����
                bool enter = (dot >= standEnterDotUp && relUpSpeed <= standEnterSpeed) || (t01 <= idleThreshold01);
                if (enter)
                {
                    standingLock = true;
                    standingRb = rb;

                    runtimeCapMat.bounciness = 0f; // ��� �������
                    TryPlayIdleOnce();

                    if (debugLog) Debug.Log("[Mushroom] Standing ENTER");
                    // �������: ������ ������ �� ������ � ���� ����
                    lastHitTime = Time.time;
                    return;
                }
            }
            else if (standingRb == rb)
            {
                // ������� ������ (���������� � ������ ���������)
                bool exit = (dot < standExitDotUp || relUpSpeed > standExitSpeed) && (t01 > idleThreshold01);
                if (!exit)
                {
                    // ���� ��������� �������: ����� ����� �������� ����� up + ������������ ���������� �� up
                    runtimeCapMat.bounciness = 0f;

                    float vAlong = Vector2.Dot(rb.velocity, up);
                    rb.velocity = rb.velocity - vAlong * up;

                    if (cancelGravityWhileStanding)
                    {
                        // ������� �������� ���������� ����� up: F = -m*g_proj
                        Vector2 g = Physics2D.gravity;
                        float gAlong = Vector2.Dot(g, up);
                        if (Mathf.Abs(gAlong) > 1e-4f)
                            rb.AddForce(-up * (gAlong * rb.mass), ForceMode2D.Force);
                    }

                    // ������ Idle, �������� �� ���, ����� �� ����������
                    lastHitTime = Time.time;
                    return;
                }
                else
                {
                    // ������� �� ������� � ����� ��������� ������ �������
                    standingLock = false;
                    standingRb = null;
                    if (debugLog) Debug.Log("[Mushroom] Standing EXIT");
                }
            }
        }
        // ---------------------------------------------------------

        // ������ ������� ������ �����/�������

        // �����
        float trendSigned = 0f;
        if (bounceTrend == BounceTrend.Increase) trendSigned = +trendStacks * trendStep01;
        else if (bounceTrend == BounceTrend.Decrease) trendSigned = -trendStacks * trendStep01;

        float effective01 = Mathf.Clamp01(t01 + trendSigned);

        // bounciness
        float minB = (bounceTrend == BounceTrend.Decrease) ? minBouncinessDecrease : minBounciness;
        float b = Mathf.Lerp(minB, maxBounciness, bouncinessCurve.Evaluate(effective01));
        runtimeCapMat.bounciness = b;

        // ��� ���������� ������� ��� Decrease
        if (bounceTrend == BounceTrend.Decrease)
        {
            float att = Mathf.Clamp01(1f - trendStacks * trendStep01);
            float outAlong = Vector2.Dot(rb.velocity, up);
            if (outAlong > 0f)
            {
                Vector2 v = rb.velocity;
                Vector2 vOrtho = v - outAlong * up;
                rb.velocity = vOrtho + up * (outAlong * att);
            }
        }

        // ���. ���������� �� ��������
        if (absorbAtMin && effective01 <= 0.0001f)
        {
            float vAlong = Vector2.Dot(rb.velocity, up);
            if (vAlong > 0f)
            {
                Vector2 v = rb.velocity;
                Vector2 vOrtho = v - vAlong * up;
                rb.velocity = vOrtho + up * (vAlong * absorbFactor);
            }
        }

        // Animator ���������/��������
        if (animator && AnimatorHasFloat(animator, hashImpact01))
            animator.SetFloat(hashImpact01, effective01);

        if (Time.time >= nextTriggerTime)
        {
            nextTriggerTime = Time.time + triggerCooldown;

            bool strong = animationMode switch
            {
                AnimMode.ForceSlight => false,
                AnimMode.ForceStrong => true,
                _ => (effective01 >= strongThreshold01)
            };

            if (animator)
            {
                if (strong)
                {
                    if (AnimatorHasTrigger(animator, hashPlayStrong))
                        animator.SetTrigger(hashPlayStrong);
                }
                else
                {
                    if (AnimatorHasTrigger(animator, hashPlaySlight))
                        animator.SetTrigger(hashPlaySlight);
                }
            }
        }

        // ��� air-control ������ ���� ��� ��� ������ ������
        var pc = rb.GetComponent<PlayerController>() ?? rb.GetComponentInParent<PlayerController>();
        if (pc != null)
        {
            pc.CancelJumpCharge();
            pc.AllowAirControlFor(airControlTime);
        }

        if (Time.time - lastHitTime > trendResetDelay) trendStacks = 0; // (������.)
        if (bounceTrend != BounceTrend.None && trendStacks < maxTrendStacks) trendStacks++;
        lastHitTime = Time.time;

        if (debugLog) Debug.Log($"[Mushroom] raw={t01:F2} eff={effective01:F2} b={b:F2} stacks={trendStacks}");
    }

    private void TryPlayIdleOnce()
    {
        if (animator && !string.IsNullOrEmpty(paramPlayIdle) && AnimatorHasTrigger(animator, hashPlayIdle))
        {
            if (Time.time >= nextTriggerTime)
            {
                animator.SetTrigger(hashPlayIdle);
                nextTriggerTime = Time.time + triggerCooldown;
            }
        }
    }

    private bool AnimatorHasFloat(Animator a, int hash)
    {
        if (!a || a.runtimeAnimatorController == null) return false;
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Float && p.nameHash == hash) return true;
        return false;
    }

    private bool AnimatorHasTrigger(Animator a, int hash)
    {
        if (!a || a.runtimeAnimatorController == null) return false;
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.nameHash == hash) return true;
        return false;
    }

    // ====== ��������������� �����: ��� up ======
    private void OnDrawGizmosSelected()
    {
        Vector2 up = capUpReference ? (Vector2)capUpReference.up : (Vector2)transform.up;
        if (invertUpAxis) up = -up;
        Gizmos.color = Color.cyan;
        Vector3 p = capCollider ? capCollider.bounds.center : transform.position;
        Gizmos.DrawLine(p, p + (Vector3)up * 0.8f);
    }
}

// ��������� �������� � �������-�����
public class _MushroomCapForwarder : MonoBehaviour
{
    [HideInInspector] public _MushroomBounce owner;
    private void OnCollisionEnter2D(Collision2D c) => owner?.OnCapEnter(c);
    private void OnCollisionStay2D(Collision2D c) => owner?.OnCapStay(c);
    private void OnCollisionExit2D(Collision2D c) => owner?.OnCapExit(c);
}
