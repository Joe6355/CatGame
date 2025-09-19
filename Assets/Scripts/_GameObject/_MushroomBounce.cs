using System.Collections;
using UnityEngine;

public class _MushroomBounce : MonoBehaviour
{
    [Header("Коллайдеры гриба")]
    [SerializeField] private Collider2D capCollider;   // шапка (IsTrigger=OFF)
    [SerializeField] private Collider2D stemCollider;  // ножка (для физики)

    [Header("Ось шапки")]
    [Tooltip("Источник оси 'вверх' шапки. Если пусто — берём capCollider.transform.up")]
    [SerializeField] private Transform capUpReference;
    [Tooltip("Инвертировать ось 'вверх', если шапка смотрит внутрь/вниз.")]
    [SerializeField] private bool invertUpAxis = false;

    [Header("Физ-материал шапки")]
    [SerializeField] private PhysicsMaterial2D baseCapMaterial;
    [SerializeField, Range(0f, 1f)] private float minBounciness = 0.15f;
    [SerializeField, Range(0f, 1f)] private float maxBounciness = 0.95f;
    [SerializeField] private AnimationCurve bouncinessCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Измерение удара")]
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private float minImpactSpeed = 0.01f; // любой >0 удар считается
    [SerializeField] private float maxImpactSpeed = 7.0f;  // быстрее добраться до Strongly

    [Header("Анимация (имена стейтов на Base Layer)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string stateIdle = "MushroomIde";
    [SerializeField] private string stateSlight = "MushroomBentSlightly";
    [SerializeField] private string stateStrong = "MushroomBentStrongly";

    [Tooltip("Нормализованные пороги: >= slight -> Slightly, >= strong -> Strongly")]
    [SerializeField, Range(0f, 1f)] private float slightThreshold01 = 0.001f; // практически любой удар
    [SerializeField, Range(0f, 1f)] private float strongThreshold01 = 0.45f;  // подстрой под свой контроллер

    [Header("Длительность клипов (сек)")]
    [SerializeField] private float slightClipDuration = 0.25f;
    [SerializeField] private float strongClipDuration = 0.35f;

    [Header("Возврат в Idle")]
    [SerializeField] private float idleExtraDelay = 0.05f;

    [Header("Air control игроку после отскока")]
    [SerializeField] private float airControlTime = 0.35f;

    [Header("Отладка")]
    [SerializeField] private bool debugLog = false;

    // runtime
    private PhysicsMaterial2D runtimeCapMat;

    private const int BaseLayer = 0;
    private int idleHash, slightHash, strongHash;

    // защита от перезапусков
    private float animLockUntil = 0f; // пока активно — не перезапускаем тот же/слабее стейт
    private int currentHash = 0;      // какой стейт сейчас играет (hash); 0 = неизвестно/idle
    private Coroutine returnCo;

    private void Awake()
    {
        if (!capCollider)
        {
            Debug.LogError("[_MushroomBounce] Не назначен capCollider (шапка).");
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

    // === коллизии шапки ===
    public void OnCapEnter(Collision2D c) => EvaluateHit(c);
    public void OnCapStay(Collision2D c) => EvaluateHit(c); // полезно для слабых/скользящих касаний

    private void EvaluateHit(Collision2D c)
    {
        var otherRb = c.otherRigidbody ? c.otherRigidbody : c.collider.attachedRigidbody;
        if (!otherRb) return;
        if ((playerMask.value & (1 << otherRb.gameObject.layer)) == 0) return;

        // ось "вверх" шапки
        Vector2 up = capUpReference ? (Vector2)capUpReference.up : (Vector2)capCollider.transform.up;
        if (invertUpAxis) up = -up;

        // сила удара — максимум из проекций (чтобы точно зафиксировать касание)
        float impactA = Vector2.Dot(-c.relativeVelocity, up); // скорость схождения
        float impactB = Vector2.Dot(otherRb.velocity, up);  // скорость игрока к шапке
        float impact = Mathf.Max(impactA, impactB);

        if (impact <= 0f) return;

        float t01 = Mathf.Clamp01(Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, impact));

        // применим bounciness по кривой
        float b = Mathf.Lerp(minBounciness, maxBounciness, bouncinessCurve.Evaluate(t01));
        runtimeCapMat.bounciness = b;

        // желаемый стейт
        bool wantStrong = (t01 >= strongThreshold01);
        bool wantSlight = (t01 >= slightThreshold01);

        // ——— анимации ———
        // Важно: не возвращаемся в Idle из EvaluateHit. Только играем Slight/Strong или эскалируем.
        if (Time.time < animLockUntil && currentHash != 0 && currentHash != idleHash)
        {
            // замок активен: разрешаем только эскалацию Slight -> Strong
            if (wantStrong && currentHash != strongHash)
                PlayState(strongHash, strongClipDuration);
        }
        else
        {
            if (wantStrong) PlayState(strongHash, strongClipDuration);
            else if (wantSlight) PlayState(slightHash, slightClipDuration);
            // else: тише воды — пусть текущий клип доиграет/останется idle
        }

        // дать управление в воздухе игроку
        var pc = otherRb.GetComponent<PlayerController>() ?? otherRb.GetComponentInParent<PlayerController>();
        if (pc != null) pc.AllowAirControlFor(airControlTime);

        if (debugLog) Debug.Log($"[Mushroom] impact={impact:F3} t01={t01:F3}  b={b:F2}  anim={(wantStrong ? "STRONG" : wantSlight ? "SLIGHT" : "—")}");
    }

    private void PlayState(int hash, float clipDuration)
    {
        if (!EnsureAnimator(hash)) return;

        // если уже этот же стейт и замок активен — не перезапускаем
        if (Time.time < animLockUntil && hash == currentHash) return;

        animator.CrossFade(hash, 0.05f, BaseLayer, 0f);
        currentHash = hash;

        animLockUntil = Time.time + Mathf.Max(0.01f, clipDuration);

        // перезапускаем возврат в idle
        if (returnCo != null) StopCoroutine(returnCo);
        returnCo = StartCoroutine(ReturnToIdleAfter(clipDuration + idleExtraDelay));
    }

    private IEnumerator ReturnToIdleAfter(float t)
    {
        yield return new WaitForSeconds(t);

        // если во время ожидания случилась эскалация — не ломаем
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
            Debug.LogWarning("[Mushroom] Animator не назначен.");
            return false;
        }
        var ctrl = animator.runtimeAnimatorController;
        if (ctrl == null)
        {
            Debug.LogWarning("[Mushroom] У Animator не назначен Controller.");
            return false;
        }
        if (!animator.HasState(BaseLayer, targetHash))
        {
            Debug.LogWarning($"[Mushroom] Нет стейта '{NameByHash(targetHash)}' на Base Layer.");
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

// Форвардер коллизий с объекта-шапки
public class _MushroomCapForwarder : MonoBehaviour
{
    [HideInInspector] public _MushroomBounce owner;
    private void OnCollisionEnter2D(Collision2D c) => owner?.OnCapEnter(c);
    private void OnCollisionStay2D(Collision2D c) => owner?.OnCapStay(c);
}
