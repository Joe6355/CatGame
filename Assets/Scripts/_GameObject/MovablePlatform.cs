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

    // Приватные переменные
    private Vector2 originalPosition;
    private Vector2 targetPosition;
    private bool playerOnPlatform;
    private float timeSincePlayerLeft;
    private Transform playerTransform;
    private Collider2D solidCollider;
    private Collider2D triggerCollider;
    private Rigidbody2D rb;
    private bool isInitialized;

    // Для PolygonCollider2D - кэшируем верхнюю границу
    private float cachedPlatformTopY;

    private void Start()
    {
        InitializePlatform();
    }

    private void InitializePlatform()
    {
        if (isInitialized) return;

        // Находим "твердый" коллайдер
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
            solidCollider = GetComponent<Collider2D>();
            if (solidCollider != null)
                solidCollider.isTrigger = false;
        }

        if (solidCollider == null)
        {
            Debug.LogError("AutoTriggerMovablePlatform: не найден Collider2D!");
            return;
        }

        // Кэшируем верхнюю границу платформы
        UpdatePlatformTopY();

        // Удаляем старые триггеры
        RemoveOldTriggerColliders();

        // Создаем новый триггер
        CreateTriggerCollider();

        // Настройка Rigidbody2D
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // Сохраняем позицию
        originalPosition = rb.position;
        targetPosition = originalPosition;

        isInitialized = true;
    }

    private void UpdatePlatformTopY()
    {
        if (solidCollider is PolygonCollider2D polygon)
        {
            // Для PolygonCollider2D находим самую верхнюю точку в локальных координатах
            float maxY = float.MinValue;
            for (int i = 0; i < polygon.points.Length; i++)
            {
                Vector2 worldPoint = transform.TransformPoint(polygon.points[i] + polygon.offset);
                if (worldPoint.y > maxY)
                    maxY = worldPoint.y;
            }
            cachedPlatformTopY = maxY;
        }
    }

    private void RemoveOldTriggerColliders()
    {
        var allColliders = GetComponents<Collider2D>();
        foreach (var c in allColliders)
        {
            if (c != solidCollider && c.isTrigger && c != triggerCollider)
            {
                if (Application.isPlaying)
                    Destroy(c);
                else
                    DestroyImmediate(c);
            }
        }
    }

    private void CreateTriggerCollider()
    {
        if (solidCollider == null) return;

        // Получаем границы для триггера
        Bounds bounds = GetColliderBounds(solidCollider);

        // Создаем BoxCollider2D для триггера (всегда используем Box для простоты)
        var triggerBox = gameObject.AddComponent<BoxCollider2D>();

        // Рассчитываем размеры и позицию триггера
        float triggerWidth = bounds.size.x * triggerWidthMultiplier;
        Vector2 triggerSize = new Vector2(triggerWidth, triggerHeight);

        // Центр триггера - над верхней частью платформы
        Vector2 triggerCenter = new Vector2(
            bounds.center.x,
            bounds.max.y + triggerOffsetY + triggerHeight * 0.5f
        );

        triggerBox.size = triggerSize;
        triggerBox.offset = transform.InverseTransformPoint(triggerCenter);
        triggerBox.isTrigger = true;

        triggerCollider = triggerBox;
    }

    private Bounds GetColliderBounds(Collider2D collider)
    {
        if (collider is PolygonCollider2D polygon)
        {
            // Для PolygonCollider2D создаем bounds на основе всех точек
            Bounds bounds = new Bounds();
            bool hasBounds = false;

            for (int i = 0; i < polygon.points.Length; i++)
            {
                Vector2 worldPoint = transform.TransformPoint(polygon.points[i] + polygon.offset);
                if (!hasBounds)
                {
                    bounds = new Bounds(worldPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(worldPoint);
                }
            }
            return bounds;
        }

        // Для остальных коллайдеров используем стандартные bounds
        return collider.bounds;
    }

    private void FixedUpdate()
    {
        if (!isInitialized || solidCollider == null) return;

        // Обновляем кэшированную верхнюю границу для PolygonCollider2D
        if (solidCollider is PolygonCollider2D)
        {
            UpdatePlatformTopY();
        }

        // Проверяем, все ли еще игрок на платформе
        if (playerOnPlatform && playerTransform != null)
        {
            if (!IsPlayerAbove(playerTransform))
            {
                PlayerLeft();
            }
        }

        // Управление целевой позицией
        if (playerOnPlatform)
        {
            timeSincePlayerLeft = 0f;
            targetPosition = originalPosition + Vector2.down * dropDistance;
        }
        else
        {
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

        // Плавное движение
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
        if (player == null || solidCollider == null) return false;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider == null) return false;

        // Получаем границы для платформы
        Bounds platformBounds;
        float platformTopY;

        if (solidCollider is PolygonCollider2D)
        {
            // Используем кэшированную верхнюю границу
            platformTopY = cachedPlatformTopY;

            // Для X границ используем общие bounds
            platformBounds = GetColliderBounds(solidCollider);
        }
        else
        {
            platformBounds = solidCollider.bounds;
            platformTopY = platformBounds.max.y;
        }

        Bounds playerBounds = playerCollider.bounds;

        // Проверка по высоте
        bool isVerticallyAbove = playerBounds.min.y >= platformTopY - verticalTolerance;

        // Проверка по X
        bool hasHorizontalOverlap =
            playerBounds.max.x > platformBounds.min.x + horizontalTolerance &&
            playerBounds.min.x < platformBounds.max.x - horizontalTolerance;

        if (!isVerticallyAbove || !hasHorizontalOverlap)
            return false;

        // Проверка касания (опционально)
        if (requireTouchingSolid)
        {
            if (!solidCollider.IsTouching(playerCollider))
                return false;
        }

        return true;
    }

    #region Gizmos

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
        if (!isInitialized && solidCollider == null)
        {
            solidCollider = GetComponent<Collider2D>();
            if (solidCollider != null && solidCollider.isTrigger)
            {
                // Ищем не-триггер коллайдер
                var all = GetComponents<Collider2D>();
                foreach (var c in all)
                {
                    if (!c.isTrigger)
                    {
                        solidCollider = c;
                        break;
                    }
                }
            }
        }

        if (solidCollider == null) return;

        Vector3 orig = Application.isPlaying ? (Vector3)originalPosition : transform.position;

        // Исходная позиция
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(orig, Vector3.one * 0.5f);

        // Позиция при проседании
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(orig + Vector3.down * dropDistance, Vector3.one * 0.5f);

        // Триггер
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);

        Bounds bounds = GetColliderBounds(solidCollider);
        Vector3 triggerCenter = new Vector3(
            bounds.center.x,
            bounds.max.y + triggerOffsetY + triggerHeight * 0.5f,
            0
        );
        Vector3 triggerSize = new Vector3(bounds.size.x * triggerWidthMultiplier, triggerHeight, 0.1f);

        Gizmos.DrawCube(triggerCenter, triggerSize);
        Gizmos.DrawWireCube(triggerCenter, triggerSize);
    }

    #endregion

    #region Public methods

    [ContextMenu("Обновить триггерный коллайдер")]
    public void UpdateTriggerCollider()
    {
        if (!Application.isPlaying)
        {
            solidCollider = GetComponent<Collider2D>();
            if (solidCollider != null && solidCollider.isTrigger)
            {
                var all = GetComponents<Collider2D>();
                foreach (var c in all)
                {
                    if (!c.isTrigger)
                    {
                        solidCollider = c;
                        break;
                    }
                }
            }

            if (solidCollider != null)
                solidCollider.isTrigger = false;

            RemoveOldTriggerColliders();
            CreateTriggerCollider();
        }
        else
        {
            InitializePlatform();
        }
    }

    public void ResetToOriginalPosition()
    {
        if (!isInitialized) InitializePlatform();
        if (rb != null)
        {
            rb.position = originalPosition;
            targetPosition = originalPosition;
        }

        playerOnPlatform = false;
        playerTransform = null;
        timeSincePlayerLeft = 0f;
    }

    #endregion
}