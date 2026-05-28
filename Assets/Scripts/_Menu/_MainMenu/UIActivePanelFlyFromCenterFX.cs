using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIActivePanelFlyFromCenterFX : MonoBehaviour
{
    [Header("Что анимировать")]
    [Tooltip("Если список пустой, скрипт автоматически возьмёт прямых детей этого объекта.")]
    [SerializeField] private RectTransform[] customItems;

    [Tooltip("Если Custom Items пустой, брать прямых детей панели автоматически.")]
    [SerializeField] private bool autoCollectDirectChildren = true;

    [Tooltip("Анимировать неактивных детей. Обычно лучше выключить.")]
    [SerializeField] private bool includeInactiveChildren = false;

    [Header("Точка появления")]
    [Tooltip("Точка, откуда вылетают элементы. Если пусто, используется центр этой панели.")]
    [SerializeField] private RectTransform spawnCenter;

    [Tooltip("Небольшой разброс от центра, чтобы элементы не появлялись строго из одной точки.")]
    [SerializeField] private float centerScatter = 4f;

    [Header("Основная анимация")]
    [Tooltip("Запускать эффект каждый раз при SetActive(true).")]
    [SerializeField] private bool playOnEnable = true;

    [Tooltip("Длительность вылета одного элемента.")]
    [SerializeField] private float duration = 0.22f;

    [Tooltip("Задержка между появлением соседних элементов.")]
    [SerializeField] private float itemStagger = 0.03f;

    [Tooltip("Начальный масштаб элемента в центре.")]
    [SerializeField] private float startScale = 0.72f;

    [Tooltip("Прозрачность при появлении.")]
    [SerializeField] private bool useFade = true;

    [Tooltip("Анимация масштаба при появлении.")]
    [SerializeField] private bool useScale = true;

    [Tooltip("Лёгкий перелёт за финальную позицию с возвратом назад.")]
    [SerializeField] private bool usePositionOvershoot = true;

    [Tooltip("Блокировать клики по элементам, пока идёт анимация.")]
    [SerializeField] private bool blockInputDuringAnimation = true;

    [Tooltip("Использовать unscaled time. Нужно для меню и паузы.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("VHS / ЭЛТ: дрожание")]
    [Tooltip("Добавить дрожание во время вылета.")]
    [SerializeField] private bool enableJitter = true;

    [Tooltip("Сила дрожания в UI-пикселях.")]
    [SerializeField] private float jitterPower = 2.4f;

    [Tooltip("Во сколько раз горизонтальное дрожание сильнее вертикального.")]
    [SerializeField] private float horizontalJitterMultiplier = 1.8f;

    [Tooltip("С какого момента анимации начинается дрожание.")]
    [Range(0f, 1f)]
    [SerializeField] private float jitterStart = 0.08f;

    [Tooltip("На каком моменте анимации дрожание заканчивается.")]
    [Range(0f, 1f)]
    [SerializeField] private float jitterEnd = 0.82f;

    [Header("VHS / ЭЛТ: горизонтальный срыв")]
    [Tooltip("Иногда резко смещать элемент по горизонтали на 1 кадр.")]
    [SerializeField] private bool enableHorizontalTear = true;

    [Tooltip("Шанс горизонтального срыва на кадр. Не ставь слишком много.")]
    [Range(0f, 1f)]
    [SerializeField] private float horizontalTearChance = 0.08f;

    [Tooltip("Сила горизонтального срыва в UI-пикселях.")]
    [SerializeField] private float horizontalTearOffset = 9f;

    [Header("VHS / ЭЛТ: мерцание")]
    [Tooltip("Мерцать всей панелью во время перехода.")]
    [SerializeField] private bool enableGlobalFlicker = true;

    [Tooltip("Сила мерцания всей панели.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float globalFlickerStrength = 0.08f;

    [Tooltip("Мерцать отдельными элементами.")]
    [SerializeField] private bool enableItemAlphaNoise = true;

    [Tooltip("Сила мерцания отдельных элементов.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float itemAlphaNoiseStrength = 0.10f;

    [Header("VHS / ЭЛТ: сканирующая полоса")]
    [Tooltip("Создавать тонкую светящуюся полосу, которая проходит по панели.")]
    [SerializeField] private bool enableScanSweep = true;

    [Tooltip("Цвет сканирующей полосы.")]
    [SerializeField] private Color scanSweepColor = new Color(1f, 0.12f, 0f, 0.28f);

    [Tooltip("Высота сканирующей полосы.")]
    [SerializeField] private float scanSweepHeight = 8f;

    [Tooltip("Длительность прохода сканирующей полосы.")]
    [SerializeField] private float scanSweepDuration = 0.32f;

    [Tooltip("Полоса идёт сверху вниз. Если выключить — снизу вверх.")]
    [SerializeField] private bool scanSweepTopToBottom = true;

    [Header("Шлейф")]
    [Tooltip("Включить копии-шлейфы за элементами.")]
    [SerializeField] private bool enableTrail = true;

    [Tooltip("Сколько копий шлейфа максимум создать для одного элемента.")]
    [SerializeField] private int maxTrailCopiesPerItem = 3;

    [Tooltip("Как часто создавать копию шлейфа.")]
    [SerializeField] private float trailSpawnEvery = 0.025f;

    [Tooltip("Сколько живёт одна копия шлейфа.")]
    [SerializeField] private float trailLife = 0.10f;

    [Tooltip("Общая прозрачность шлейфа.")]
    [Range(0f, 1f)]
    [SerializeField] private float trailAlpha = 0.18f;

    [Tooltip("Удалять у копий лишние скрипты, чтобы они были только визуальными.")]
    [SerializeField] private bool stripGhostScripts = true;

    [Header("Форма шлейфа")]
    [Tooltip("Растягивать шлейф по X, как размаз старого монитора.")]
    [SerializeField] private bool stretchTrail = true;

    [Tooltip("Множитель растяжения шлейфа по X.")]
    [SerializeField] private float trailStretchX = 1.22f;

    [Tooltip("Множитель сжатия шлейфа по Y.")]
    [SerializeField] private float trailStretchY = 0.94f;

    [Header("Цвет основного шлейфа")]
    [Tooltip("Перекрашивать основной шлейф в отдельный цвет.")]
    [SerializeField] private bool overrideTrailColor = true;

    [Tooltip("Цвет основных шлейфных копий.")]
    [SerializeField] private Color trailColor = new Color(1f, 0.18f, 0f, 1f);

    [Tooltip("Красить все Graphic-компоненты внутри копии: Image, Text, TMP_Text, Slider Fill и т.д.")]
    [SerializeField] private bool colorAllChildGraphics = true;

    [Tooltip("Сохранять исходную альфу отдельных Image/Text. Общая прозрачность всё равно задаётся Trail Alpha.")]
    [SerializeField] private bool preserveOriginalGraphicAlpha = true;

    [Header("Хроматический VHS-след")]
    [Tooltip("Добавить красно-голубые копии шлейфа со смещением.")]
    [SerializeField] private bool enableChromaticGhosts = true;

    [Tooltip("Красный VHS-след.")]
    [SerializeField] private Color chromaticRedColor = new Color(1f, 0.02f, 0f, 1f);

    [Tooltip("Голубой VHS-след.")]
    [SerializeField] private Color chromaticCyanColor = new Color(0f, 0.75f, 1f, 1f);

    [Tooltip("Смещение хроматических копий в UI-пикселях.")]
    [SerializeField] private float chromaticOffset = 3.5f;

    [Tooltip("Прозрачность хроматических копий относительно Trail Alpha.")]
    [Range(0f, 1f)]
    [SerializeField] private float chromaticAlphaMultiplier = 0.65f;

    private RectTransform root;
    private CanvasGroup rootCanvasGroup;
    private Coroutine routine;
    private Coroutine scanSweepRoutine;
    private GameObject scanSweepObject;

    private readonly List<ItemState> lastStates = new List<ItemState>();
    private readonly List<GameObject> spawnedGhosts = new List<GameObject>();

    private class ItemState
    {
        public RectTransform rect;
        public CanvasGroup group;

        public Vector3 targetWorldPosition;
        public Vector3 targetLocalScale;
        public float targetAlpha;

        public bool targetInteractable;
        public bool targetBlocksRaycasts;

        public Vector3 startWorldPosition;

        public float trailTimer;
        public int trailCopies;
    }

    private void Awake()
    {
        root = GetComponent<RectTransform>();
        rootCanvasGroup = GetOrAddCanvasGroup(gameObject);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (!playOnEnable)
            return;

        Play();
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (scanSweepRoutine != null)
        {
            StopCoroutine(scanSweepRoutine);
            scanSweepRoutine = null;
        }

        RestoreLastStates();
        ClearGhosts();
        ClearScanSweep();
    }

    public void Play()
    {
        if (!isActiveAndEnabled)
            return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        ClearGhosts();
        ClearScanSweep();

        Canvas.ForceUpdateCanvases();

        if (rootCanvasGroup == null)
            rootCanvasGroup = GetOrAddCanvasGroup(gameObject);

        float rootOriginalAlpha = rootCanvasGroup.alpha;
        bool rootOriginalInteractable = rootCanvasGroup.interactable;
        bool rootOriginalBlocksRaycasts = rootCanvasGroup.blocksRaycasts;

        if (blockInputDuringAnimation)
        {
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }

        List<ItemState> states = BuildStates();

        lastStates.Clear();
        lastStates.AddRange(states);

        if (states.Count == 0)
        {
            RestoreRootCanvasGroup(rootOriginalAlpha, rootOriginalInteractable, rootOriginalBlocksRaycasts);
            routine = null;
            yield break;
        }

        if (enableScanSweep)
            scanSweepRoutine = StartCoroutine(ScanSweepRoutine());

        Vector3 spawnWorld = GetSpawnWorldPoint();

        for (int i = 0; i < states.Count; i++)
        {
            ItemState state = states[i];

            Vector3 scatterOffset = Vector3.zero;

            if (centerScatter > 0f)
            {
                Vector2 random = Random.insideUnitCircle * centerScatter;
                scatterOffset = root.TransformVector(new Vector3(random.x, random.y, 0f));
            }

            state.startWorldPosition = spawnWorld + scatterOffset;
            state.rect.position = state.startWorldPosition;

            state.rect.localScale = useScale
                ? state.targetLocalScale * startScale
                : state.targetLocalScale;

            state.group.alpha = useFade ? 0f : state.targetAlpha;

            if (blockInputDuringAnimation)
            {
                state.group.interactable = false;
                state.group.blocksRaycasts = false;
            }
        }

        float totalTime = duration + itemStagger * Mathf.Max(0, states.Count - 1);
        float time = 0f;

        while (time < totalTime)
        {
            float dt = DeltaTime();
            time += dt;

            if (enableGlobalFlicker)
            {
                float flicker = Random.Range(1f - globalFlickerStrength, 1f);
                rootCanvasGroup.alpha = rootOriginalAlpha * flicker;
            }

            for (int i = 0; i < states.Count; i++)
            {
                ItemState state = states[i];

                float localTime = time - itemStagger * i;

                if (localTime < 0f)
                    continue;

                float t = Mathf.Clamp01(localTime / duration);

                float positionT = usePositionOvershoot ? EaseOutBack(t) : EaseOutCubic(t);
                float alphaT = EaseOutCubic(t);
                float scaleT = EaseOutBack(t);

                Vector3 pos = Vector3.LerpUnclamped(
                    state.startWorldPosition,
                    state.targetWorldPosition,
                    positionT
                );

                if (enableJitter && t >= jitterStart && t <= jitterEnd)
                {
                    float normalized = Mathf.InverseLerp(jitterStart, jitterEnd, t);
                    float fade = Mathf.Sin(normalized * Mathf.PI);

                    float jitterX = Random.Range(-jitterPower, jitterPower) * horizontalJitterMultiplier * fade;
                    float jitterY = Random.Range(-jitterPower, jitterPower) * fade;

                    pos += root.TransformVector(new Vector3(jitterX, jitterY, 0f));
                }

                if (enableHorizontalTear && Random.value < horizontalTearChance)
                {
                    float tear = Random.Range(-horizontalTearOffset, horizontalTearOffset);
                    pos += root.TransformVector(new Vector3(tear, 0f, 0f));
                }

                state.rect.position = pos;

                if (useScale)
                {
                    Vector3 start = state.targetLocalScale * startScale;
                    Vector3 end = state.targetLocalScale;
                    state.rect.localScale = Vector3.LerpUnclamped(start, end, scaleT);
                }

                if (useFade)
                {
                    float finalAlpha = Mathf.Lerp(0f, state.targetAlpha, alphaT);

                    if (enableItemAlphaNoise)
                    {
                        float noise = Random.Range(1f - itemAlphaNoiseStrength, 1f);
                        finalAlpha *= noise;
                    }

                    state.group.alpha = Mathf.Clamp01(finalAlpha);
                }

                if (enableTrail && t > 0.05f && t < 0.92f)
                {
                    state.trailTimer += dt;

                    if (state.trailTimer >= trailSpawnEvery && state.trailCopies < maxTrailCopiesPerItem)
                    {
                        state.trailTimer = 0f;
                        state.trailCopies++;
                        SpawnTrailSet(state);
                    }
                }
            }

            yield return null;
        }

        for (int i = 0; i < states.Count; i++)
            RestoreState(states[i]);

        RestoreRootCanvasGroup(rootOriginalAlpha, rootOriginalInteractable, rootOriginalBlocksRaycasts);

        routine = null;
    }

    private List<ItemState> BuildStates()
    {
        List<ItemState> states = new List<ItemState>();
        HashSet<RectTransform> used = new HashSet<RectTransform>();

        if (customItems != null && customItems.Length > 0)
        {
            for (int i = 0; i < customItems.Length; i++)
                TryAddItem(customItems[i], states, used);

            return states;
        }

        if (!autoCollectDirectChildren)
            return states;

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform child = root.GetChild(i) as RectTransform;
            TryAddItem(child, states, used);
        }

        return states;
    }

    private void TryAddItem(RectTransform item, List<ItemState> states, HashSet<RectTransform> used)
    {
        if (item == null)
            return;

        if (item == spawnCenter)
            return;

        if (scanSweepObject != null && item.gameObject == scanSweepObject)
            return;

        if (used.Contains(item))
            return;

        if (!includeInactiveChildren && !item.gameObject.activeSelf)
            return;

        CanvasGroup group = GetOrAddCanvasGroup(item.gameObject);

        ItemState state = new ItemState
        {
            rect = item,
            group = group,

            targetWorldPosition = item.position,
            targetLocalScale = item.localScale,
            targetAlpha = group.alpha,

            targetInteractable = group.interactable,
            targetBlocksRaycasts = group.blocksRaycasts,

            trailTimer = 0f,
            trailCopies = 0
        };

        states.Add(state);
        used.Add(item);
    }

    private Vector3 GetSpawnWorldPoint()
    {
        if (spawnCenter != null)
            return spawnCenter.position;

        return root.TransformPoint(root.rect.center);
    }

    private void SpawnTrailSet(ItemState state)
    {
        SpawnTrailGhost(
            state,
            Vector3.zero,
            overrideTrailColor ? trailColor : Color.white,
            trailAlpha,
            "_Trail"
        );

        if (!enableChromaticGhosts)
            return;

        Vector3 redOffset = root.TransformVector(new Vector3(-chromaticOffset, 0f, 0f));
        Vector3 cyanOffset = root.TransformVector(new Vector3(chromaticOffset, 0f, 0f));

        SpawnTrailGhost(
            state,
            redOffset,
            chromaticRedColor,
            trailAlpha * chromaticAlphaMultiplier,
            "_RedGhost"
        );

        SpawnTrailGhost(
            state,
            cyanOffset,
            chromaticCyanColor,
            trailAlpha * chromaticAlphaMultiplier,
            "_CyanGhost"
        );
    }

    private void SpawnTrailGhost(ItemState state, Vector3 worldOffset, Color color, float alpha, string suffix)
    {
        if (state == null || state.rect == null)
            return;

        GameObject ghost = Instantiate(state.rect.gameObject, state.rect.parent);
        ghost.name = state.rect.name + suffix;

        RectTransform ghostRect = ghost.GetComponent<RectTransform>();
        ghostRect.position = state.rect.position + worldOffset;
        ghostRect.rotation = state.rect.rotation;
        ghostRect.localScale = state.rect.localScale;

        if (stretchTrail)
        {
            Vector3 s = ghostRect.localScale;
            s.x *= trailStretchX;
            s.y *= trailStretchY;
            ghostRect.localScale = s;
        }

        ghostRect.SetSiblingIndex(state.rect.GetSiblingIndex());

        if (stripGhostScripts)
            StripGhostScripts(ghost);

        if (overrideTrailColor || enableChromaticGhosts)
            ApplyTrailColor(ghost, color);

        CanvasGroup ghostGroup = GetOrAddCanvasGroup(ghost);
        ghostGroup.alpha = alpha;
        ghostGroup.interactable = false;
        ghostGroup.blocksRaycasts = false;

        CanvasGroup[] allGroups = ghost.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < allGroups.Length; i++)
        {
            allGroups[i].interactable = false;
            allGroups[i].blocksRaycasts = false;
        }

        Graphic[] allGraphics = ghost.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < allGraphics.Length; i++)
        {
            if (allGraphics[i] != null)
                allGraphics[i].raycastTarget = false;
        }

        spawnedGhosts.Add(ghost);

        StartCoroutine(FadeAndDestroyGhost(ghost, ghostGroup, trailLife));
    }

    private void ApplyTrailColor(GameObject ghost, Color color)
    {
        if (ghost == null)
            return;

        Graphic[] graphics = colorAllChildGraphics
            ? ghost.GetComponentsInChildren<Graphic>(true)
            : ghost.GetComponents<Graphic>();

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null)
                continue;

            Color finalColor = color;

            if (preserveOriginalGraphicAlpha)
                finalColor.a = graphic.color.a;
            else
                finalColor.a = color.a;

            graphic.color = finalColor;
        }
    }

    private void StripGhostScripts(GameObject ghost)
    {
        MonoBehaviour[] behaviours = ghost.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];

            if (mb == null)
                continue;

            if (mb is Graphic)
                continue;

            if (mb is BaseMeshEffect)
                continue;

            if (mb is LayoutGroup)
                continue;

            if (mb is LayoutElement)
                continue;

            if (mb is ContentSizeFitter)
                continue;

            if (mb is Mask)
                continue;

            if (mb is RectMask2D)
                continue;

            Destroy(mb);
        }
    }

    private IEnumerator FadeAndDestroyGhost(GameObject ghost, CanvasGroup ghostGroup, float life)
    {
        float time = 0f;
        float startAlpha = ghostGroup != null ? ghostGroup.alpha : trailAlpha;

        while (time < life)
        {
            if (ghost == null || ghostGroup == null)
                yield break;

            time += DeltaTime();

            float t = Mathf.Clamp01(time / life);
            ghostGroup.alpha = Mathf.Lerp(startAlpha, 0f, EaseOutCubic(t));

            yield return null;
        }

        if (ghost != null)
        {
            spawnedGhosts.Remove(ghost);
            Destroy(ghost);
        }
    }

    private IEnumerator ScanSweepRoutine()
    {
        ClearScanSweep();

        scanSweepObject = new GameObject("VHS_CRT_ScanSweep", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        scanSweepObject.transform.SetParent(root, false);
        scanSweepObject.transform.SetAsLastSibling();

        RectTransform scanRect = scanSweepObject.GetComponent<RectTransform>();
        CanvasGroup scanGroup = scanSweepObject.GetComponent<CanvasGroup>();
        Image scanImage = scanSweepObject.GetComponent<Image>();

        scanImage.color = scanSweepColor;
        scanImage.raycastTarget = false;

        scanGroup.alpha = 0f;
        scanGroup.interactable = false;
        scanGroup.blocksRaycasts = false;

        scanRect.anchorMin = new Vector2(0f, 0.5f);
        scanRect.anchorMax = new Vector2(1f, 0.5f);
        scanRect.pivot = new Vector2(0.5f, 0.5f);
        scanRect.sizeDelta = new Vector2(0f, scanSweepHeight);

        float height = Mathf.Max(1f, root.rect.height);
        float startY = scanSweepTopToBottom ? height * 0.5f : -height * 0.5f;
        float endY = scanSweepTopToBottom ? -height * 0.5f : height * 0.5f;

        float time = 0f;
        float life = Mathf.Max(0.01f, scanSweepDuration);

        while (time < life)
        {
            if (scanSweepObject == null)
                yield break;

            time += DeltaTime();

            float t = Mathf.Clamp01(time / life);
            float eased = EaseOutCubic(t);

            scanRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, eased));

            float alphaMul = Mathf.Sin(t * Mathf.PI);
            scanGroup.alpha = scanSweepColor.a * alphaMul;

            yield return null;
        }

        ClearScanSweep();
    }

    private void RestoreLastStates()
    {
        for (int i = 0; i < lastStates.Count; i++)
            RestoreState(lastStates[i]);
    }

    private void RestoreState(ItemState state)
    {
        if (state == null || state.rect == null || state.group == null)
            return;

        state.rect.position = state.targetWorldPosition;
        state.rect.localScale = state.targetLocalScale;

        state.group.alpha = state.targetAlpha;
        state.group.interactable = state.targetInteractable;
        state.group.blocksRaycasts = state.targetBlocksRaycasts;
    }

    private void RestoreRootCanvasGroup(float alpha, bool interactable, bool blocksRaycasts)
    {
        if (rootCanvasGroup == null)
            return;

        rootCanvasGroup.alpha = alpha;
        rootCanvasGroup.interactable = interactable;
        rootCanvasGroup.blocksRaycasts = blocksRaycasts;
    }

    private void ClearGhosts()
    {
        for (int i = spawnedGhosts.Count - 1; i >= 0; i--)
        {
            if (spawnedGhosts[i] != null)
                Destroy(spawnedGhosts[i]);
        }

        spawnedGhosts.Clear();
    }

    private void ClearScanSweep()
    {
        if (scanSweepObject != null)
        {
            Destroy(scanSweepObject);
            scanSweepObject = null;
        }
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();

        if (group == null)
            group = target.AddComponent<CanvasGroup>();

        return group;
    }

    private float DeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    private float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);

        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}