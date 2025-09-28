using System.Collections;
using UnityEngine;

/// <summary>
/// ВЕШАЕТСЯ НА ПУСТЫШКУ.
/// Спавнит префаб платформы в своей позиции. При наступании на платформу:
/// ждет timeBeforeBreak, затем выполняет "поломку" в течение breakDuration,
/// уничтожает инстанс и спустя respawnTime спавнит заново.
/// </summary>
[DisallowMultipleComponent]
public class BreakablePlatform2D : MonoBehaviour
{
    [Header("Префаб и спавн")]
    [Tooltip("Префаб платформы, которую нужно спавнить/уничтожать.")]
    [SerializeField] private GameObject platformPrefab;
    [Tooltip("Спавнить как дочерний объект пустышки.")]
    [SerializeField] private bool parentToThis = true;
    [Tooltip("Автоспавн при старте сцены.")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("Активация и тайминги")]
    [Tooltip("Кто может активировать платформу (по тегу). Пусто — любой объект.")]
    [SerializeField] private string[] activatorTags = new[] { "Player" };

    [Tooltip("Задержка с момента наступания до начала поломки.")]
    [SerializeField] private float timeBeforeBreak = 0.5f;

    [Tooltip("ВРЕМЯ ПОЛОМКИ: длительность стадии поломки до уничтожения.")]
    [SerializeField] private float breakDuration = 0.35f; // <-- добавлено

    [Tooltip("Сколько секунд ждать перед повторным появлением после уничтожения.")]
    [SerializeField] private float respawnTime = 2.0f;

    // Текущий инстанс платформы
    private GameObject currentInstance;
    private bool sequenceRunning;
    private Coroutine seqCo;

    private void Start()
    {
        if (spawnOnStart) Spawn();
    }

    /// <summary> Спавнит новый инстанс платформы. </summary>
    public void Spawn()
    {
        if (platformPrefab == null)
        {
            Debug.LogError("[BreakablePlatform2D] Не назначен platformPrefab.", this);
            return;
        }

        if (currentInstance != null) return;

        currentInstance = Instantiate(
            platformPrefab,
            transform.position,
            transform.rotation,
            parentToThis ? transform : null
        );

        //респавн  (заглушка: анимация/звук появления на новом инстансе)

        // Форвардер событий коллизий → в спавнер
        var fwd = currentInstance.GetComponent<BreakablePlatform2DInstance>();
        if (!fwd) fwd = currentInstance.AddComponent<BreakablePlatform2DInstance>();
        fwd.BindOwner(this);

        sequenceRunning = false;
    }

    /// <summary> Принудительно сломать текущую платформу (пропуская ожидание до поломки, но с фазой поломки). </summary>
    public void ForceBreakNow()
    {
        if (seqCo != null) StopCoroutine(seqCo);
        seqCo = StartCoroutine(BreakAndRespawnRoutine(0f)); // сразу поломка → breakDuration → destroy → респавн
    }

    /// <summary> Внутренний вызов от инстанса при наступании. </summary>
    internal void NotifyStepped(Collider2D by)
    {
        if (sequenceRunning || currentInstance == null) return;
        if (!IsActivator(by)) return;

        seqCo = StartCoroutine(BreakAndRespawnRoutine(timeBeforeBreak));
    }

    private bool IsActivator(Collider2D c)
    {
        if (activatorTags == null || activatorTags.Length == 0) return true;
        foreach (var tag in activatorTags)
            if (!string.IsNullOrEmpty(tag) && c.CompareTag(tag))
                return true;
        return false;
    }

    private IEnumerator BreakAndRespawnRoutine(float delayBeforeBreak)
    {
        sequenceRunning = true;

        // До начала поломки (платформа ещё держит игрока)
        if (delayBeforeBreak > 0f)
            yield return new WaitForSeconds(delayBeforeBreak);

        // === ПОЛОМКА ===
        //поломка  (заглушка: запустить анимацию трещины/крошения, звук и т.п. на currentInstance)
        // На стадии поломки выключим коллайдеры, чтобы игрок провалился
        SetChildCollidersEnabled(currentInstance, false);

        if (breakDuration > 0f)
            yield return new WaitForSeconds(breakDuration);

        // === УНИЧТОЖЕНИЕ ===
        //исчезновение  (заглушка: финальный эффект исчезновения/растворения)
        if (currentInstance != null)
        {
            Destroy(currentInstance); // уничтожаем инстанс платформы
            currentInstance = null;
        }

        // === ОЖИДАНИЕ РЕСПАВНА ===
        if (respawnTime > 0f)
            yield return new WaitForSeconds(respawnTime);

        // === РЕСПАВН ===
        //респавн  (заглушка: анимация появления)
        Spawn();

        sequenceRunning = false;
        seqCo = null;
    }

    private static void SetChildCollidersEnabled(GameObject go, bool enabled)
    {
        if (!go) return;
        var cols = go.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols) c.enabled = enabled;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 0.9f, 0.9f, 0.35f);
        Gizmos.DrawWireCube(transform.position, new Vector3(1.2f, 0.4f, 0f)); // условный габарит
    }
#endif
}

/// <summary>
/// КОМПОНЕНТ, КОТОРЫЙ ВЕШАЕТСЯ НА ИНСТАНС ПЛАТФОРМЫ (автоматически).
/// Он пересылает события столкновений/триггеров владельцу-спавнеру.
/// </summary>
[DisallowMultipleComponent]
public class BreakablePlatform2DInstance : MonoBehaviour
{
    private BreakablePlatform2D owner;

    public void BindOwner(BreakablePlatform2D o) => owner = o;

    private void OnCollisionEnter2D(Collision2D c)
    {
        owner?.NotifyStepped(c.collider);
    }

    private void OnCollisionStay2D(Collision2D c)
    {
        // На случай, если персонаж заспавнился уже стоя — подстраховка
        owner?.NotifyStepped(c.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        owner?.NotifyStepped(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        owner?.NotifyStepped(other);
    }
}
