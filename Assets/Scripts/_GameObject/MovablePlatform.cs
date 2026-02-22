using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AutoTriggerMovablePlatform : MonoBehaviour
{
    [Header("Настройки платформы")]
    [SerializeField, Tooltip(
        "На сколько платформа опускается вниз, когда игрок стоит на ней.\n" +
        "Пример: 0.2 = платформа просядет на 0.2 юнита.")]
    private float dropDistance = 0.2f;

    [SerializeField, Tooltip(
        "Скорость, с которой платформа движется к целевой позиции (вниз/вверх).\n" +
        "Чем больше — тем быстрее реакция.\n" +
        "Важно: это не 'скорость в юнитах/сек', а коэффициент для сглаживания (Lerp).")]
    private float dropSpeed = 8f;

    [SerializeField, Tooltip(
        "Задержка перед возвратом платформы в исходную позицию после ухода игрока.\n" +
        "Полезно, чтобы платформа не дергалась при микрошаге на границе.")]
    private float returnDelay = 0.15f;


    [Header("Проверка 'игрок сверху'")]
    [SerializeField, Tooltip(
        "Допуск по высоте для проверки 'игрок сверху'.\n" +
        "Мы сравниваем низ коллайдера игрока и верх платформы.\n" +
        "Если поставить слишком мало — может не срабатывать из-за погрешностей физики.\n" +
        "Если слишком много — будет срабатывать даже когда игрок почти сбоку.")]
    private float verticalTolerance = 0.08f;

    [SerializeField, Tooltip(
        "Допуск по X (по краям платформы) для проверки перекрытия.\n" +
        "Это помогает, чтобы на самом краю срабатывало стабильно,\n" +
        "и чтобы мелкие касания боком не считались 'стоящим сверху'.")]
    private float horizontalTolerance = 0.02f;

    [SerializeField, Tooltip(
        "Если включить — платформа будет проседать только когда игрок РЕАЛЬНО касается\n" +
        "твердого коллайдера (solidCollider), а не просто находится в зоне триггера.\n" +
        "Может потребовать корректные Rigidbody2D/Collider2D у игрока.")]
    private bool requireTouchingSolid = false;


    [Header("Триггерный коллайдер (зона над платформой)")]
    [SerializeField, Tooltip(
        "Высота триггерной зоны над платформой.\n" +
        "Игрок должен попасть в эту зону, чтобы скрипт начал проверять 'игрок сверху'.\n" +
        "Слишком маленькая — иногда не ловит; слишком большая — ловит лишнее.")]
    private float triggerHeight = 0.3f;

    [SerializeField, Tooltip(
        "Смещение триггера вверх относительно верхней границы платформы.\n" +
        "Нужно, чтобы триггер был именно над платформой, а не пересекался с ней.\n" +
        "Если триггер пересечётся с платформой, срабатывания могут быть странными.")]
    private float triggerOffsetY = 0.15f;

    [SerializeField, Tooltip(
        "Множитель ширины триггера относительно физического (твердого) коллайдера.\n" +
        "Пример: 1.1 = триггер чуть шире платформы.\n" +
        "Если поставить слишком большое — игрок будет входить в триггер, даже стоя рядом.")]
    private float triggerWidthMultiplier = 1.1f;


    // --- Ниже служебные переменные (обычно не нужно показывать в инспекторе) ---

    private Vector2 originalPosition;   // исходная позиция платформы (куда она возвращается)
    private Vector2 targetPosition;     // целевая позиция (вниз или обратно вверх)

    private bool playerOnPlatform;      // флаг: считаем ли мы, что игрок стоит сверху прямо сейчас
    private float timeSincePlayerLeft;  // таймер с момента ухода игрока (для returnDelay)
    private Transform playerTransform;  // ссылка на текущего игрока (кто активировал платформу)

    private Collider2D solidCollider;   // основной "твердый" коллайдер платформы (НЕ trigger)
    private Collider2D triggerCollider; // созданный триггерный коллайдер над платформой
    private Rigidbody2D rb;             // Rigidbody2D платформы (Kinematic), двигаем через MovePosition

    private bool isInitialized;         // чтобы инициализация не выполнялась повторно


private void Start()
    {
        InitializePlatform();
    }

    private void InitializePlatform()
    {
        if (isInitialized) return;

        originalPosition = rb ? rb.position : (Vector2)transform.position;
        targetPosition = originalPosition;

        // Находим "твердый" коллайдер: берем первый НЕ-триггер (если есть)
        var all = GetComponents<Collider2D>();
        solidCollider = null;
        foreach (var c in all)
        {
            if (!c.isTrigger)
            {
                solidCollider = c;
                break;
            }
        }
        if (solidCollider == null)
        {
            // Если все были триггеры (редко, но бывает) — берем первый и делаем твердым
            solidCollider = GetComponent<Collider2D>();
            solidCollider.isTrigger = false;
        }

        // Удаляем старые триггеры на этом объекте (кроме solidCollider)
        RemoveOldTriggerCollider();

        // Создаем новый триггерный BoxCollider2D
        CreateTriggerCollider();

        // Rigidbody2D
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // если transform уже где-то смещен — фиксируем корректно
        rb.position = (Vector2)transform.position;
        originalPosition = rb.position;
        targetPosition = originalPosition;

        isInitialized = true;
    }

    private void RemoveOldTriggerCollider()
    {
        var allColliders = GetComponents<Collider2D>();
        foreach (var c in allColliders)
        {
            if (c != solidCollider && c.isTrigger)
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
        }
    }

    private void CreateTriggerCollider()
    {
        Vector2 physicalSize = GetPhysicalColliderSize();
        Vector2 physicalCenter = GetPhysicalColliderCenter();

        var triggerBox = gameObject.AddComponent<BoxCollider2D>();

        triggerBox.size = new Vector2(
            physicalSize.x * triggerWidthMultiplier,
            triggerHeight
        );

        triggerBox.offset = new Vector2(
            physicalCenter.x,
            physicalCenter.y + physicalSize.y * 0.5f + triggerOffsetY
        );

        triggerBox.isTrigger = true;
        triggerCollider = triggerBox;
    }

    private Vector2 GetPhysicalColliderSize()
    {
        if (solidCollider is BoxCollider2D box) return box.size;

        if (solidCollider is CircleCollider2D circle)
        {
            float d = circle.radius * 2f;
            return new Vector2(d, d);
        }

        if (solidCollider is CapsuleCollider2D capsule) return capsule.size;

        if (solidCollider is PolygonCollider2D)
        {
            // bounds в world, переводим в local (предполагаем без поворота)
            Bounds b = solidCollider.bounds;
            Vector3 localSize = transform.InverseTransformVector(b.size);
            return new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
        }

        Debug.LogWarning($"Тип коллайдера {solidCollider.GetType()} не поддерживается, используется размер 1x1");
        return Vector2.one;
    }

    private Vector2 GetPhysicalColliderCenter()
    {
        if (solidCollider is BoxCollider2D box) return box.offset;
        if (solidCollider is CircleCollider2D circle) return circle.offset;
        if (solidCollider is CapsuleCollider2D capsule) return capsule.offset;

        if (solidCollider is PolygonCollider2D)
        {
            Bounds b = solidCollider.bounds;
            Vector3 localCenter = transform.InverseTransformPoint(b.center);
            return new Vector2(localCenter.x, localCenter.y);
        }

        return Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (!isInitialized) return;

        // Если игрок "отмечен как стоящий", но на самом деле уже не сверху — снимаем
        if (playerOnPlatform && playerTransform != null)
        {
            if (!IsPlayerAbove(playerTransform))
            {
                PlayerLeft();
            }
        }

        // Цель по состоянию
        if (playerOnPlatform)
        {
            timeSincePlayerLeft = 0f;
            targetPosition = originalPosition + Vector2.down * dropDistance;
        }
        else
        {
            // ждём и возвращаемся
            if (Vector2.Distance(rb.position, originalPosition) > 0.0005f)
            {
                timeSincePlayerLeft += Time.fixedDeltaTime;
                if (timeSincePlayerLeft >= returnDelay)
                    targetPosition = originalPosition;
            }
            else
            {
                timeSincePlayerLeft = 0f;
                targetPosition = originalPosition;
            }
        }

        // Плавное движение (кинематик) — через MovePosition
        Vector2 newPos = Vector2.Lerp(rb.position, targetPosition, dropSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isInitialized) return;
        TryLand(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isInitialized) return;
        TryLand(other);

        // если это текущий игрок и он уже не сверху — уходим
        if (playerTransform != null && other.transform == playerTransform && playerOnPlatform)
        {
            if (!IsPlayerAbove(playerTransform))
                PlayerLeft();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!isInitialized) return;

        if (other.CompareTag("Player") && playerTransform == other.transform)
        {
            PlayerLeft();
        }
    }

    private void TryLand(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // если уже стоит — ничего
        if (playerOnPlatform && playerTransform == other.transform) return;

        if (IsPlayerAbove(other.transform))
        {
            PlayerLanded(other.transform);
        }
    }

    private void PlayerLanded(Transform player)
    {
        playerOnPlatform = true;
        playerTransform = player;
        timeSincePlayerLeft = 0f;
    }

    private void PlayerLeft()
    {
        playerOnPlatform = false;
        playerTransform = null;
        timeSincePlayerLeft = 0f;
    }

    private bool IsPlayerAbove(Transform player)
    {
        if (player == null) return false;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider == null) return false;

        Bounds pb = playerCollider.bounds;
        Bounds plat = solidCollider.bounds;

        // 1) По высоте: низ игрока около верха платформы
        bool isVerticallyAbove = pb.min.y >= plat.max.y - verticalTolerance;

        // 2) По X: НЕ "центр внутри", а перекрытие по ширине
        bool hasHorizontalOverlap =
            pb.max.x > plat.min.x + horizontalTolerance &&
            pb.min.x < plat.max.x - horizontalTolerance;

        if (!isVerticallyAbove || !hasHorizontalOverlap)
            return false;

        // 3) По желанию: реальный контакт с solid
        if (requireTouchingSolid)
        {
            // иногда требует корректные Rigidbody2D/Collider2D на игроке
            if (!solidCollider.IsTouching(playerCollider))
                return false;
        }

        return true;
    }

    #region Debug / Gizmos

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            originalPosition = transform.position;
        }

        dropDistance = Mathf.Max(0f, dropDistance);
        dropSpeed = Mathf.Max(0.1f, dropSpeed);
        returnDelay = Mathf.Max(0f, returnDelay);

        triggerHeight = Mathf.Max(0.01f, triggerHeight);
        triggerWidthMultiplier = Mathf.Max(0.1f, triggerWidthMultiplier);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 orig = Application.isPlaying ? (Vector3)originalPosition : transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(orig, Vector3.one * 0.5f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(orig + Vector3.down * dropDistance, Vector3.one * 0.5f);

        // Триггер
        var sc = solidCollider != null ? solidCollider : GetComponent<Collider2D>();
        if (sc == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);

        Bounds b = sc.bounds;
        Vector3 triggerCenter = b.center + new Vector3(0, b.extents.y + triggerOffsetY, 0);
        Vector3 triggerSize = new Vector3(b.size.x * triggerWidthMultiplier, triggerHeight, 0.1f);

        Gizmos.DrawCube(triggerCenter, triggerSize);
        Gizmos.DrawWireCube(triggerCenter, triggerSize);
    }

    #endregion

    #region Public helpers

    [ContextMenu("Обновить триггерный коллайдер")]
    public void UpdateTriggerCollider()
    {
        // в редакторе пересоздаем
        if (!Application.isPlaying)
        {
            solidCollider = GetComponent<Collider2D>();
            if (solidCollider != null) solidCollider.isTrigger = false;

            RemoveOldTriggerCollider();
            CreateTriggerCollider();
        }
        else
        {
            // в игре просто гарантируем init
            InitializePlatform();
        }
    }

    public void ResetToOriginalPosition()
    {
        if (!isInitialized) InitializePlatform();

        rb.position = originalPosition;
        targetPosition = originalPosition;

        playerOnPlatform = false;
        playerTransform = null;
        timeSincePlayerLeft = 0f;
    }

    #endregion
}
