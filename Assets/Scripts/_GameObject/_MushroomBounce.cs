using System.Collections;
using UnityEngine;

public class _MushroomBounce : MonoBehaviour
{
    [Header("���������� �����")]
    [SerializeField] private Collider2D capCollider;   // ����� (IsTrigger=OFF)
    [SerializeField] private Collider2D stemCollider;  // ����� (��� ������)

    [Header("��� �����")]
    [Tooltip("�������� ��� '�����' �����. ���� ����� � ���� capCollider.transform.up")]
    [SerializeField] private Transform capUpReference;
    [Tooltip("������������� ��� '�����', ���� ����� ������� ������/����.")]
    [SerializeField] private bool invertUpAxis = false;

    [Header("���-�������� �����")]
    [SerializeField] private PhysicsMaterial2D baseCapMaterial;
    [SerializeField, Range(0f, 1f)] private float minBounciness = 0.15f;
    [SerializeField, Range(0f, 1f)] private float maxBounciness = 0.95f;
    [SerializeField] private AnimationCurve bouncinessCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("��������� �����")]
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private float minImpactSpeed = 0.01f; // ����� >0 ���� ���������
    [SerializeField] private float maxImpactSpeed = 7.0f;  // ������� ��������� �� Strongly

    [Header("�������� (����� ������� �� Base Layer)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string stateIdle = "MushroomIde";
    [SerializeField] private string stateSlight = "MushroomBentSlightly";
    [SerializeField] private string stateStrong = "MushroomBentStrongly";

    [Tooltip("��������������� ������: >= slight -> Slightly, >= strong -> Strongly")]
    [SerializeField, Range(0f, 1f)] private float slightThreshold01 = 0.001f; // ����������� ����� ����
    [SerializeField, Range(0f, 1f)] private float strongThreshold01 = 0.45f;  // �������� ��� ���� ����������

    [Header("������������ ������ (���)")]
    [SerializeField] private float slightClipDuration = 0.25f;
    [SerializeField] private float strongClipDuration = 0.35f;

    [Header("������� � Idle")]
    [SerializeField] private float idleExtraDelay = 0.05f;

    [Header("Air control ������ ����� �������")]
    [SerializeField] private float airControlTime = 0.35f;

    [Header("�������")]
    [SerializeField] private bool debugLog = false;

    // runtime
    private PhysicsMaterial2D runtimeCapMat;

    private const int BaseLayer = 0;
    private int idleHash, slightHash, strongHash;

    // ������ �� ������������
    private float animLockUntil = 0f; // ���� ������� � �� ������������� ��� ��/������ �����
    private int currentHash = 0;      // ����� ����� ������ ������ (hash); 0 = ����������/idle
    private Coroutine returnCo;

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

        Rehash();
    }

    private void OnValidate() => Rehash();

    private void Rehash()
    {
        idleHash = Animator.StringToHash(stateIdle);
        slightHash = Animator.StringToHash(stateSlight);
        strongHash = Animator.StringToHash(stateStrong);
    }

    // === �������� ����� ===
    public void OnCapEnter(Collision2D c) => EvaluateHit(c);
    public void OnCapStay(Collision2D c) => EvaluateHit(c); // ������� ��� ������/���������� �������

    private void EvaluateHit(Collision2D c)
    {
        var otherRb = c.otherRigidbody ? c.otherRigidbody : c.collider.attachedRigidbody;
        if (!otherRb) return;
        if ((playerMask.value & (1 << otherRb.gameObject.layer)) == 0) return;

        // ��� "�����" �����
        Vector2 up = capUpReference ? (Vector2)capUpReference.up : (Vector2)capCollider.transform.up;
        if (invertUpAxis) up = -up;

        // ���� ����� � �������� �� �������� (����� ����� ������������� �������)
        float impactA = Vector2.Dot(-c.relativeVelocity, up); // �������� ���������
        float impactB = Vector2.Dot(otherRb.velocity, up);  // �������� ������ � �����
        float impact = Mathf.Max(impactA, impactB);

        if (impact <= 0f) return;

        float t01 = Mathf.Clamp01(Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, impact));

        // �������� bounciness �� ������
        float b = Mathf.Lerp(minBounciness, maxBounciness, bouncinessCurve.Evaluate(t01));
        runtimeCapMat.bounciness = b;

        // �������� �����
        bool wantStrong = (t01 >= strongThreshold01);
        bool wantSlight = (t01 >= slightThreshold01);

        // ��� �������� ���
        // �����: �� ������������ � Idle �� EvaluateHit. ������ ������ Slight/Strong ��� ����������.
        if (Time.time < animLockUntil && currentHash != 0 && currentHash != idleHash)
        {
            // ����� �������: ��������� ������ ��������� Slight -> Strong
            if (wantStrong && currentHash != strongHash)
                PlayState(strongHash, strongClipDuration);
        }
        else
        {
            if (wantStrong) PlayState(strongHash, strongClipDuration);
            else if (wantSlight) PlayState(slightHash, slightClipDuration);
            // else: ���� ���� � ����� ������� ���� ��������/��������� idle
        }

        // ���� ���������� � ������� ������
        var pc = otherRb.GetComponent<PlayerController>() ?? otherRb.GetComponentInParent<PlayerController>();
        if (pc != null) pc.AllowAirControlFor(airControlTime);

        if (debugLog) Debug.Log($"[Mushroom] impact={impact:F3} t01={t01:F3}  b={b:F2}  anim={(wantStrong ? "STRONG" : wantSlight ? "SLIGHT" : "�")}");
    }

    private void PlayState(int hash, float clipDuration)
    {
        if (!EnsureAnimator(hash)) return;

        // ���� ��� ���� �� ����� � ����� ������� � �� �������������
        if (Time.time < animLockUntil && hash == currentHash) return;

        animator.CrossFade(hash, 0.05f, BaseLayer, 0f);
        currentHash = hash;

        animLockUntil = Time.time + Mathf.Max(0.01f, clipDuration);

        // ������������� ������� � idle
        if (returnCo != null) StopCoroutine(returnCo);
        returnCo = StartCoroutine(ReturnToIdleAfter(clipDuration + idleExtraDelay));
    }

    private IEnumerator ReturnToIdleAfter(float t)
    {
        yield return new WaitForSeconds(t);

        // ���� �� ����� �������� ��������� ��������� � �� ������
        if (Time.time < animLockUntil) yield break;

        if (!EnsureAnimator(idleHash)) yield break;

        animator.CrossFade(idleHash, 0.05f, BaseLayer, 0f);
        currentHash = idleHash;
        animLockUntil = 0f;
        returnCo = null;
    }

    private bool EnsureAnimator(int targetHash)
    {
        if (!animator)
        {
            Debug.LogWarning("[Mushroom] Animator �� ��������.");
            return false;
        }
        var ctrl = animator.runtimeAnimatorController;
        if (ctrl == null)
        {
            Debug.LogWarning("[Mushroom] � Animator �� �������� Controller.");
            return false;
        }
        if (!animator.HasState(BaseLayer, targetHash))
        {
            Debug.LogWarning($"[Mushroom] ��� ������ '{NameByHash(targetHash)}' �� Base Layer.");
            return false;
        }
        return true;
    }

    private string NameByHash(int hash)
    {
        if (hash == idleHash) return stateIdle;
        else if (hash == slightHash) return stateSlight;
        else if (hash == strongHash) return stateStrong;
        return $"hash:{hash}";
    }
}

// ��������� �������� � �������-�����
public class _MushroomCapForwarder : MonoBehaviour
{
    [HideInInspector] public _MushroomBounce owner;
    private void OnCollisionEnter2D(Collision2D c) => owner?.OnCapEnter(c);
    private void OnCollisionStay2D(Collision2D c) => owner?.OnCapStay(c);
}
