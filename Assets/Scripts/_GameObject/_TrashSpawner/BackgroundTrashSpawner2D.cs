using System;
using UnityEngine;

public class BackgroundTrashSpawner2D : MonoBehaviour
{
    [Serializable]
    public class TrashPrefabEntry
    {
        [Tooltip("Префаб мусора. На нем должен быть BackgroundTrashItem2D.")]
        public GameObject prefab;

        [Tooltip("Вес появления. Чем больше число, тем чаще объект появляется.")]
        [Min(0f)] public float weight = 1f;
    }

    [Header("Spawn Zone")]
    [Tooltip("Размер зоны, внутри которой сверху появляется мусор.")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(12f, 1.5f);

    [Tooltip("Смещение зоны относительно объекта со спавнером.")]
    [SerializeField] private Vector2 spawnAreaOffset = Vector2.zero;

    [Tooltip("Z-позиция мусора. Для 2D обычно можно оставить 0, а порядок задавать Sorting Layer.")]
    [SerializeField] private float spawnZ = 0f;

    [Header("Prefabs")]
    [Tooltip("Список префабов мусора.")]
    [SerializeField] private TrashPrefabEntry[] trashPrefabs;

    [Tooltip("Куда складывать созданные объекты. Если пусто, будут создаваться без родителя.")]
    [SerializeField] private Transform spawnedParent;

    [Header("Spawn Timing")]
    [Tooltip("Сколько мусора создать при старте сцены.")]
    [SerializeField] private int spawnOnStart = 5;

    [Tooltip("Минимальная пауза между появлениями мусора.")]
    [SerializeField] private float minSpawnInterval = 0.15f;

    [Tooltip("Максимальная пауза между появлениями мусора.")]
    [SerializeField] private float maxSpawnInterval = 0.7f;

    [Tooltip("Максимум живых объектов мусора. Защита от перегруза.")]
    [SerializeField] private int maxAliveObjects = 60;

    [Header("Movement Random")]
    [Tooltip("Скорость падения мусора.")]
    [SerializeField] private Vector2 fallSpeedRange = new Vector2(0.8f, 2.4f);

    [Tooltip("Постоянный снос по X. Например, как легкий поток воздуха.")]
    [SerializeField] private Vector2 horizontalSpeedRange = new Vector2(-0.25f, 0.25f);

    [Tooltip("Амплитуда плавного покачивания по X.")]
    [SerializeField] private Vector2 driftAmplitudeRange = new Vector2(0.05f, 0.35f);

    [Tooltip("Частота покачивания по X.")]
    [SerializeField] private Vector2 driftFrequencyRange = new Vector2(0.5f, 2.2f);

    [Header("Rotation Random")]
    [Tooltip("Скорость вращения в градусах в секунду.")]
    [SerializeField] private Vector2 rotationSpeedRange = new Vector2(-120f, 120f);

    [Tooltip("Давать случайный стартовый поворот.")]
    [SerializeField] private bool randomStartRotation = true;

    [Header("Visual Random")]
    [Tooltip("Случайный масштаб мусора.")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.75f, 1.25f);

    [Tooltip("Случайный альфа-канал. Полезно, чтобы задний фон не спорил с игроком.")]
    [SerializeField] private Vector2 alphaRange = new Vector2(0.45f, 0.85f);

    [Tooltip("Случайно зеркалить по X.")]
    [SerializeField] private bool randomFlipX = true;

    [Tooltip("Случайно зеркалить по Y.")]
    [SerializeField] private bool randomFlipY = false;

    [Tooltip("Случайный Sorting Order для глубины.")]
    [SerializeField] private Vector2Int sortingOrderRange = new Vector2Int(-20, -5);

    [Header("Lifetime")]
    [Tooltip("Время жизни каждого мусора.")]
    [SerializeField] private Vector2 lifetimeRange = new Vector2(5f, 10f);

    [Tooltip("За сколько секунд до смерти объект начнет исчезать.")]
    [SerializeField] private Vector2 fadeDurationRange = new Vector2(0.5f, 1.5f);

    [Header("Debug")]
    [SerializeField] private bool drawSpawnZone = true;

    private float timer;
    private float nextSpawnDelay;
    private int aliveObjects;

    private void Awake()
    {
        ScheduleNextSpawn();
    }

    private void Start()
    {
        for (int i = 0; i < spawnOnStart; i++)
        {
            SpawnOne();
        }
    }

    private void Update()
    {
        if (trashPrefabs == null || trashPrefabs.Length == 0)
            return;

        if (aliveObjects >= maxAliveObjects)
            return;

        timer += Time.deltaTime;

        if (timer >= nextSpawnDelay)
        {
            timer = 0f;
            SpawnOne();
            ScheduleNextSpawn();
        }
    }

    private void ScheduleNextSpawn()
    {
        float min = Mathf.Min(minSpawnInterval, maxSpawnInterval);
        float max = Mathf.Max(minSpawnInterval, maxSpawnInterval);
        nextSpawnDelay = UnityEngine.Random.Range(min, max);
    }

    private void SpawnOne()
    {
        GameObject prefab = PickRandomPrefab();

        if (prefab == null)
            return;

        Vector3 spawnPos = GetRandomSpawnPosition();
        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity, spawnedParent);

        BackgroundTrashItem2D trashItem = obj.GetComponent<BackgroundTrashItem2D>();

        if (trashItem == null)
        {
            Debug.LogWarning($"На префабе мусора '{prefab.name}' нет BackgroundTrashItem2D. Объект будет удален.");
            Destroy(obj);
            return;
        }

        float fallSpeed = RandomRangeSafe(fallSpeedRange);
        float horizontalSpeed = RandomRangeSafe(horizontalSpeedRange);
        float driftAmplitude = RandomRangeSafe(driftAmplitudeRange);
        float driftFrequency = RandomRangeSafe(driftFrequencyRange);
        float rotationSpeed = RandomRangeSafe(rotationSpeedRange);
        float lifetime = RandomRangeSafe(lifetimeRange);
        float fadeDuration = RandomRangeSafe(fadeDurationRange);
        float alpha = Mathf.Clamp01(RandomRangeSafe(alphaRange));
        float scale = RandomRangeSafe(scaleRange);
        int sortingOrder = UnityEngine.Random.Range(sortingOrderRange.x, sortingOrderRange.y + 1);

        bool flipX = randomFlipX && UnityEngine.Random.value > 0.5f;
        bool flipY = randomFlipY && UnityEngine.Random.value > 0.5f;

        float startRotationZ = randomStartRotation ? UnityEngine.Random.Range(0f, 360f) : 0f;

        obj.transform.localScale = Vector3.one * scale;
        obj.transform.rotation = Quaternion.Euler(0f, 0f, startRotationZ);

        aliveObjects++;

        trashItem.Init(
            spawner: this,
            fallSpeed: fallSpeed,
            horizontalSpeed: horizontalSpeed,
            driftAmplitude: driftAmplitude,
            driftFrequency: driftFrequency,
            rotationSpeed: rotationSpeed,
            lifetime: lifetime,
            fadeDuration: fadeDuration,
            alpha: alpha,
            flipX: flipX,
            flipY: flipY,
            sortingOrder: sortingOrder
        );
    }

    private GameObject PickRandomPrefab()
    {
        float totalWeight = 0f;

        for (int i = 0; i < trashPrefabs.Length; i++)
        {
            if (trashPrefabs[i] == null || trashPrefabs[i].prefab == null)
                continue;

            totalWeight += Mathf.Max(0f, trashPrefabs[i].weight);
        }

        if (totalWeight <= 0f)
            return null;

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float current = 0f;

        for (int i = 0; i < trashPrefabs.Length; i++)
        {
            if (trashPrefabs[i] == null || trashPrefabs[i].prefab == null)
                continue;

            current += Mathf.Max(0f, trashPrefabs[i].weight);

            if (randomValue <= current)
                return trashPrefabs[i].prefab;
        }

        return trashPrefabs[trashPrefabs.Length - 1].prefab;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Vector2 center = (Vector2)transform.position + spawnAreaOffset;

        float x = UnityEngine.Random.Range(
            center.x - spawnAreaSize.x * 0.5f,
            center.x + spawnAreaSize.x * 0.5f
        );

        float y = UnityEngine.Random.Range(
            center.y - spawnAreaSize.y * 0.5f,
            center.y + spawnAreaSize.y * 0.5f
        );

        return new Vector3(x, y, spawnZ);
    }

    private float RandomRangeSafe(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        return UnityEngine.Random.Range(min, max);
    }

    public void NotifyTrashDestroyed()
    {
        aliveObjects = Mathf.Max(0, aliveObjects - 1);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSpawnZone)
            return;

        Vector3 center = transform.position + new Vector3(spawnAreaOffset.x, spawnAreaOffset.y, 0f);

        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.25f);
        Gizmos.DrawCube(center, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0.1f));

        Gizmos.color = new Color(0.4f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(center, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0.1f));
    }
}