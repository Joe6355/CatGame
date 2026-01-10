using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AutoTriggerMovablePlatform : MonoBehaviour
{
    [Header("Настройки платформы")]
    [SerializeField] private float dropDistance = 0.2f;
    [SerializeField] private float dropSpeed = 8f;
    [SerializeField] private float returnDelay = 0.15f;

    [Header("Триггерный коллайдер")]
    [SerializeField] private float triggerHeight = 0.3f; // Высота триггерной зоны
    [SerializeField] private float triggerOffsetY = 0.15f; // Смещение вверх
    [SerializeField] private float triggerWidthMultiplier = 1.1f; // Множитель ширины относительно физического коллайдера

    private Vector3 originalPosition;
    private Vector3 targetPosition;
    private bool playerOnPlatform = false;
    private float timeSincePlayerLeft = 0f;
    private Transform playerTransform;
    private Collider2D solidCollider;
    private Collider2D triggerCollider;
    private bool isInitialized = false;

    void Start()
    {
        InitializePlatform();
    }

    void InitializePlatform()
    {
        if (isInitialized) return;

        originalPosition = transform.position;
        targetPosition = originalPosition;

        // Получаем основной коллайдер (должен быть на объекте из-за RequireComponent)
        solidCollider = GetComponent<Collider2D>();
        solidCollider.isTrigger = false; // Убеждаемся, что он НЕ триггер

        // Удаляем старый триггерный коллайдер, если есть
        RemoveOldTriggerCollider();

        // Создаем триггерный коллайдер
        CreateTriggerCollider();

        // Добавляем Rigidbody2D если нет
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.freezeRotation = true;
        }

        isInitialized = true;
    }

    void RemoveOldTriggerCollider()
    {
        // Находим и удаляем все триггерные коллайдеры, созданные нами ранее
        Collider2D[] allColliders = GetComponents<Collider2D>();
        foreach (var collider in allColliders)
        {
            if (collider != solidCollider && collider.isTrigger)
            {
                if (Application.isPlaying)
                    Destroy(collider);
                else
                    DestroyImmediate(collider);
            }
        }
    }

    void CreateTriggerCollider()
    {
        // Получаем границы физического коллайдера в локальных координатах
        Vector2 physicalSize = GetPhysicalColliderSize();
        Vector2 physicalCenter = GetPhysicalColliderCenter();

        // Создаем BoxCollider2D для триггера
        BoxCollider2D triggerBox = gameObject.AddComponent<BoxCollider2D>();

        // Устанавливаем размеры на основе физического коллайдера
        triggerBox.size = new Vector2(
            physicalSize.x * triggerWidthMultiplier, // Ширина = ширина физического коллайдера × множитель
            triggerHeight // Высота задается параметром
        );

        // Позиционируем триггер над физическим коллайдером
        triggerBox.offset = new Vector2(
            physicalCenter.x,
            physicalCenter.y + physicalSize.y / 2 + triggerOffsetY
        );

        triggerBox.isTrigger = true;
        triggerCollider = triggerBox;
    }

    Vector2 GetPhysicalColliderSize()
    {
        if (solidCollider is BoxCollider2D)
        {
            BoxCollider2D box = solidCollider as BoxCollider2D;
            return box.size;
        }
        else if (solidCollider is CircleCollider2D)
        {
            CircleCollider2D circle = solidCollider as CircleCollider2D;
            float diameter = circle.radius * 2;
            return new Vector2(diameter, diameter);
        }
        else if (solidCollider is CapsuleCollider2D)
        {
            CapsuleCollider2D capsule = solidCollider as CapsuleCollider2D;
            return capsule.size;
        }
        else if (solidCollider is PolygonCollider2D)
        {
            // Для PolygonCollider получаем границы
            Bounds bounds = solidCollider.bounds;
            Vector3 localSize = transform.InverseTransformVector(bounds.size);
            return new Vector2(localSize.x, localSize.y);
        }
        else
        {
            // По умолчанию используем размер 1x1
            Debug.LogWarning($"Тип коллайдера {solidCollider.GetType()} не поддерживается, используется размер 1x1");
            return Vector2.one;
        }
    }

    Vector2 GetPhysicalColliderCenter()
    {
        if (solidCollider is BoxCollider2D)
        {
            BoxCollider2D box = solidCollider as BoxCollider2D;
            return box.offset;
        }
        else if (solidCollider is CircleCollider2D)
        {
            CircleCollider2D circle = solidCollider as CircleCollider2D;
            return circle.offset;
        }
        else if (solidCollider is CapsuleCollider2D)
        {
            CapsuleCollider2D capsule = solidCollider as CapsuleCollider2D;
            return capsule.offset;
        }
        else if (solidCollider is PolygonCollider2D)
        {
            // Для PolygonCollider вычисляем центр из границ
            Bounds bounds = solidCollider.bounds;
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            return new Vector2(localCenter.x, localCenter.y);
        }
        else
        {
            // По умолчанию центр в (0,0)
            return Vector2.zero;
        }
    }

    void Update()
    {
        if (!isInitialized) return;

        // Плавное перемещение платформы
        transform.position = Vector3.Lerp(transform.position, targetPosition,
                                         dropSpeed * Time.deltaTime);

        // Проверяем, ушел ли игрок с платформы
        if (playerOnPlatform && playerTransform != null)
        {
            if (!IsPlayerAbove(playerTransform))
            {
                PlayerLeft();
            }
            else
            {
                // Игрок все еще на платформе - сбрасываем таймер
                timeSincePlayerLeft = 0f;
                targetPosition = originalPosition - new Vector3(0, dropDistance, 0);
            }
        }

        // Возвращаем платформу если игрок ушел
        if (!playerOnPlatform && transform.position != originalPosition)
        {
            timeSincePlayerLeft += Time.deltaTime;
            if (timeSincePlayerLeft >= returnDelay)
            {
                targetPosition = originalPosition;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isInitialized) return;

        if (other.CompareTag("Player"))
        {
            if (IsPlayerAbove(other.transform))
            {
                PlayerLanded(other.transform);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!isInitialized) return;

        if (other.CompareTag("Player") && playerTransform == other.transform)
        {
            PlayerLeft();
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!isInitialized) return;

        if (other.CompareTag("Player"))
        {
            if (IsPlayerAbove(other.transform))
            {
                if (!playerOnPlatform)
                {
                    PlayerLanded(other.transform);
                }
            }
            else if (playerTransform == other.transform)
            {
                PlayerLeft();
            }
        }
    }

    void PlayerLanded(Transform player)
    {
        playerOnPlatform = true;
        playerTransform = player;
        timeSincePlayerLeft = 0f;

        // Опускаем платформу
        targetPosition = originalPosition - new Vector3(0, dropDistance, 0);
    }

    void PlayerLeft()
    {
        playerOnPlatform = false;
        playerTransform = null;
        timeSincePlayerLeft = 0f;
    }

    bool IsPlayerAbove(Transform player)
    {
        if (player == null) return false;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider == null) return false;

        // Получаем нижнюю точку игрока
        float playerBottom = playerCollider.bounds.min.y;

        // Получаем верхнюю точку платформы (используем solid коллайдер)
        float platformTop = solidCollider.bounds.max.y;

        // Также проверяем, что игрок находится в пределах ширины платформы
        float platformLeft = solidCollider.bounds.min.x;
        float platformRight = solidCollider.bounds.max.x;
        float playerCenterX = playerCollider.bounds.center.x;

        // Игрок считается сверху если:
        // 1. Его нижняя часть чуть выше верха платформы
        // 2. Он находится по центру платформы по X
        bool isVerticallyAbove = playerBottom > platformTop - 0.05f;
        bool isHorizontallyAligned = playerCenterX > platformLeft && playerCenterX < platformRight;

        return isVerticallyAbove && isHorizontallyAligned;
    }

    #region Редактор и отладка

    void OnValidate()
    {
        // В редакторе показываем предварительный просмотр
        if (!Application.isPlaying)
        {
            originalPosition = transform.position;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            originalPosition = transform.position;
        }

        // Рисуем оригинальную позицию
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(originalPosition, Vector3.one * 0.5f);

        // Рисуем опущенную позицию
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(originalPosition - new Vector3(0, dropDistance, 0), Vector3.one * 0.5f);

        // Рисуем триггерную зону
        if (solidCollider != null && triggerCollider != null)
        {
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f); // Оранжевый прозрачный

            // Рисуем триггерный коллайдер
            if (triggerCollider is BoxCollider2D)
            {
                BoxCollider2D triggerBox = triggerCollider as BoxCollider2D;
                Vector3 worldCenter = transform.TransformPoint(triggerBox.offset);
                Vector3 worldSize = transform.TransformVector(triggerBox.size);

                Gizmos.DrawCube(worldCenter, worldSize);
                Gizmos.DrawWireCube(worldCenter, worldSize);
            }
        }
        else if (solidCollider != null)
        {
            // Если триггер еще не создан, показываем где он будет
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
            Bounds bounds = solidCollider.bounds;

            Vector3 triggerCenter = bounds.center + new Vector3(0, bounds.extents.y + triggerOffsetY, 0);
            Vector3 triggerSize = new Vector3(bounds.size.x * triggerWidthMultiplier, triggerHeight, 0.1f);

            Gizmos.DrawCube(triggerCenter, triggerSize);
            Gizmos.DrawWireCube(triggerCenter, triggerSize);
        }
    }

    #endregion

    #region Публичные методы для настройки

    // Метод для обновления триггера при изменении физического коллайдера
    [ContextMenu("Обновить триггерный коллайдер")]
    public void UpdateTriggerCollider()
    {
        if (!Application.isPlaying)
        {
            // В редакторе
            RemoveOldTriggerCollider();
            solidCollider = GetComponent<Collider2D>();
            CreateTriggerCollider();
        }
        else
        {
            // В игре
            InitializePlatform();
        }
    }

    public void SetDropDistance(float distance)
    {
        dropDistance = Mathf.Max(0, distance);
    }

    public void SetDropSpeed(float speed)
    {
        dropSpeed = Mathf.Max(0.1f, speed);
    }

    public void SetReturnDelay(float delay)
    {
        returnDelay = Mathf.Max(0, delay);
    }

    public void ResetToOriginalPosition()
    {
        transform.position = originalPosition;
        targetPosition = originalPosition;
        playerOnPlatform = false;
        playerTransform = null;
        timeSincePlayerLeft = 0f;
    }

    #endregion
}