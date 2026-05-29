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

    [Tooltip("Небольшой разброс от центра.")]
    [SerializeField] private float centerScatter = 0f;

    [Header("Анимация")]
    [Tooltip("Запускать эффект каждый раз при SetActive(true).")]
    [SerializeField] private bool playOnEnable = true;

    [Tooltip("Длительность вылета одного элемента.")]
    [SerializeField] private float duration = 0.24f;

    [Tooltip("Задержка между появлением соседних элементов.")]
    [SerializeField] private float itemStagger = 0.035f;

    [Tooltip("Начальный масштаб элемента в центре.")]
    [SerializeField] private float startScale = 0.72f;

    [Tooltip("Плавное появление через прозрачность.")]
    [SerializeField] private bool useFade = true;

    [Tooltip("Анимация масштаба.")]
    [SerializeField] private bool useScale = true;

    [Tooltip("Лёгкий перелёт за финальную позицию.")]
    [SerializeField] private bool usePositionOvershoot = true;

    [Tooltip("Блокировать клики, пока идёт анимация.")]
    [SerializeField] private bool blockInputDuringAnimation = true;

    [Tooltip("Использовать unscaled time. Нужно для меню и паузы.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Layout Fix")]
    [Tooltip("Ждать один кадр после включения панели, чтобы Layout Group успел выставить позиции.")]
    [SerializeField] private bool waitOneFrameBeforeCapture = true;

    [Tooltip("Принудительно обновлять Layout перед захватом позиций.")]
    [SerializeField] private bool forceRebuildLayoutBeforeCapture = true;

    [Header("Шлейф")]
    [Tooltip("Включить копии-шлейфы за элементами.")]
    [SerializeField] private bool enableTrail = true;

    [Tooltip("Куда складывать шлейфные копии. Лучше отдельный объект вне Layout Group.")]
    [SerializeField] private RectTransform trailParent;

    [Tooltip("Сколько копий шлейфа максимум создать для одного элемента.")]
    [SerializeField] private int maxTrailCopiesPerItem = 4;

    [Tooltip("Как часто создавать копию шлейфа.")]
    [SerializeField] private float trailSpawnEvery = 0.03f;

    [Tooltip("Сколько живёт одна копия шлейфа.")]
    [SerializeField] private float trailLife = 0.12f;

    [Tooltip("Общая прозрачность шлейфа.")]
    [Range(0f, 1f)]
    [SerializeField] private float trailAlpha = 0.22f;

    [Tooltip("Удалять у копий лишние скрипты.")]
    [SerializeField] private bool stripGhostScripts = true;

    [Header("Цвет шлейфа")]
    [SerializeField] private bool overrideTrailColor = true;

    [SerializeField] private Color trailColor = new Color(1f, 0.55f, 0f, 1f);

    [Tooltip("Красить все Image/Text внутри копии.")]
    [SerializeField] private bool colorAllChildGraphics = true;

    [Tooltip("Сохранять исходную альфу отдельных Image/Text.")]
    [SerializeField] private bool preserveOriginalGraphicAlpha = true;

    private RectTransform root;
    private Coroutine routine;

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

        RestoreLastStates();
        ClearGhosts();
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

        if (waitOneFrameBeforeCapture)
            yield return null;

        Canvas.ForceUpdateCanvases();

        if (forceRebuildLayoutBeforeCapture)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Canvas.ForceUpdateCanvases();
        }

        List<ItemState> states = BuildStates();

        lastStates.Clear();
        lastStates.AddRange(states);

        if (states.Count == 0)
        {
            routine = null;
            yield break;
        }

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

                state.rect.position = Vector3.LerpUnclamped(
                    state.startWorldPosition,
                    state.targetWorldPosition,
                    positionT
                );

                if (useScale)
                {
                    Vector3 start = state.targetLocalScale * startScale;
                    Vector3 end = state.targetLocalScale;
                    state.rect.localScale = Vector3.LerpUnclamped(start, end, scaleT);
                }

                if (useFade)
                    state.group.alpha = Mathf.Lerp(0f, state.targetAlpha, alphaT);

                if (enableTrail && t > 0.05f && t < 0.92f)
                {
                    state.trailTimer += dt;

                    if (state.trailTimer >= trailSpawnEvery && state.trailCopies < maxTrailCopiesPerItem)
                    {
                        state.trailTimer = 0f;
                        state.trailCopies++;
                        SpawnTrailGhost(state);
                    }
                }
            }

            yield return null;
        }

        for (int i = 0; i < states.Count; i++)
            RestoreState(states[i]);

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

        if (item == trailParent)
            return;

        if (used.Contains(item))
            return;

        if (!includeInactiveChildren && !item.gameObject.activeSelf)
            return;

        CanvasGroup group = item.GetComponent<CanvasGroup>();
        if (group == null)
            group = item.gameObject.AddComponent<CanvasGroup>();

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

    private void SpawnTrailGhost(ItemState state)
    {
        if (state == null || state.rect == null)
            return;

        Transform parentForGhost = trailParent != null
            ? trailParent
            : FindCanvasTransform();

        if (parentForGhost == null)
            parentForGhost = state.rect.parent;

        GameObject ghost = Instantiate(state.rect.gameObject, parentForGhost, true);
        ghost.name = state.rect.name + "_TrailGhost";

        RectTransform ghostRect = ghost.GetComponent<RectTransform>();
        ghostRect.position = state.rect.position;
        ghostRect.rotation = state.rect.rotation;
        ghostRect.localScale = state.rect.lossyScale;

        LayoutElement layoutElement = ghost.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = ghost.AddComponent<LayoutElement>();

        layoutElement.ignoreLayout = true;

        if (stripGhostScripts)
            StripGhostScripts(ghost);

        if (overrideTrailColor)
            ApplyTrailColor(ghost);

        CanvasGroup ghostGroup = ghost.GetComponent<CanvasGroup>();
        if (ghostGroup == null)
            ghostGroup = ghost.AddComponent<CanvasGroup>();

        ghostGroup.alpha = trailAlpha;
        ghostGroup.interactable = false;
        ghostGroup.blocksRaycasts = false;

        CanvasGroup[] allGroups = ghost.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < allGroups.Length; i++)
        {
            allGroups[i].interactable = false;
            allGroups[i].blocksRaycasts = false;
        }

        spawnedGhosts.Add(ghost);

        StartCoroutine(FadeAndDestroyGhost(ghost, ghostGroup));
    }

    private Transform FindCanvasTransform()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.transform : null;
    }

    private void ApplyTrailColor(GameObject ghost)
    {
        Graphic[] graphics = colorAllChildGraphics
            ? ghost.GetComponentsInChildren<Graphic>(true)
            : ghost.GetComponents<Graphic>();

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null)
                continue;

            Color finalColor = trailColor;

            if (preserveOriginalGraphicAlpha)
                finalColor.a = graphic.color.a;
            else
                finalColor.a = trailColor.a;

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

            if (mb is CanvasGroup)
                continue;

            if (mb is BaseMeshEffect)
                continue;

            if (mb is LayoutElement)
                continue;

            Destroy(mb);
        }
    }

    private IEnumerator FadeAndDestroyGhost(GameObject ghost, CanvasGroup ghostGroup)
    {
        float time = 0f;
        float startAlpha = ghostGroup != null ? ghostGroup.alpha : trailAlpha;

        while (time < trailLife)
        {
            if (ghost == null || ghostGroup == null)
                yield break;

            time += DeltaTime();

            float t = Mathf.Clamp01(time / trailLife);
            ghostGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

            yield return null;
        }

        if (ghost != null)
        {
            spawnedGhosts.Remove(ghost);
            Destroy(ghost);
        }
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

    private void ClearGhosts()
    {
        for (int i = spawnedGhosts.Count - 1; i >= 0; i--)
        {
            if (spawnedGhosts[i] != null)
                Destroy(spawnedGhosts[i]);
        }

        spawnedGhosts.Clear();
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