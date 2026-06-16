using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-9000)]
[DisallowMultipleComponent]
public sealed class VideoSettingsRuntimeApplier : MonoBehaviour
{
    public static VideoSettingsRuntimeApplier Instance { get; private set; }

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool destroyDuplicates = true;

    [Header("Apply")]
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyOnEnable = false;
    [SerializeField] private bool applyOnSceneLoaded = true;

    [Header("What To Apply")]
    [SerializeField] private bool applyResolutionAndScreenMode = true;
    [SerializeField] private bool applyVSync = true;
    [SerializeField] private bool applyBrightness = true;
    [SerializeField] private bool applyScreenShakeMultiplier = true;

    [Header("Brightness Overlay")]
    [Tooltip("Лучше назначить сюда Image BrightnessOverlay, который лежит дочерним объектом у этого runtime-объекта.")]
    [SerializeField] private Image brightnessOverlay;

    [Tooltip("Если BrightnessOverlay не назначен, скрипт сам создаст отдельный Canvas с черным overlay.")]
    [SerializeField] private bool autoCreateBrightnessOverlayIfMissing = true;

    [SerializeField] private string runtimeCanvasName = "VideoSettingsRuntimeCanvas";
    [SerializeField] private string brightnessOverlayName = "BrightnessOverlay";

    [Tooltip("Чем больше значение, тем выше overlay над остальным UI.")]
    [SerializeField] private int overlaySortingOrder = 32000;

    [Range(0f, 1f)]
    [SerializeField] private float maxDarkeningAtZeroBrightness = 0.65f;

    [Header("Default Values From Inspector")]
    [SerializeField] private int defaultSettingsVersion = 1;
    [SerializeField] private bool forceMigrateDefaultsWhenVersionChanges = false;

    [SerializeField] private int defaultWidth = 1920;
    [SerializeField] private int defaultHeight = 1080;
    [SerializeField] private int defaultRefreshRate = 0;
    [SerializeField] private VideoScreenMode defaultScreenMode = VideoScreenMode.Fullscreen;
    [SerializeField] private bool defaultVSync = true;

    [Range(0f, 1f)]
    [SerializeField] private float defaultBrightness = 0.51f;

    [Min(0f)]
    [SerializeField] private float defaultScreenShake = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool warnAboutMissingOverlay = true;
    [SerializeField] private bool logApply = false;

    private bool subscribedToSceneLoaded;

    private void Awake()
    {
        if (destroyDuplicates && Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            if (transform.parent != null)
                transform.SetParent(null);

            DontDestroyOnLoad(gameObject);
        }

        if (applyBrightness)
            EnsureBrightnessOverlay();

        if (applyOnSceneLoaded)
            SubscribeSceneLoaded();

        if (applyOnAwake)
            ApplySavedVideoSettings();
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        if (applyOnSceneLoaded)
            SubscribeSceneLoaded();

        if (applyOnEnable)
            ApplySavedVideoSettings();
    }

    private void OnDisable()
    {
        if (!gameObject.scene.isLoaded)
            return;

        UnsubscribeSceneLoaded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        UnsubscribeSceneLoaded();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySavedVideoSettings();
    }

    public void ApplySavedVideoSettings()
    {
        VideoSettingsPrefs.MigrateDefaultsIfNeeded(
            defaultSettingsVersion,
            forceMigrateDefaultsWhenVersionChanges
        );

        VideoSettingsData data = VideoSettingsPrefs.ReadOrDefault(BuildDefaultSettings());

        ApplyVideoSettingsData(data);
    }

    public void ApplyVideoSettingsData(VideoSettingsData data)
    {
        if (applyResolutionAndScreenMode || applyVSync)
        {
            VideoSettingsPrefs.ApplyDisplaySettings(
                data,
                applyResolutionAndScreenMode,
                applyVSync
            );
        }

        if (applyBrightness)
            ApplyBrightness(data.brightness);

        if (applyScreenShakeMultiplier)
            VideoSettingsPrefs.SetRuntimeScreenShakeMultiplier(data.screenShake);

        if (logApply)
        {
            Debug.Log(
                $"{nameof(VideoSettingsRuntimeApplier)}: applied video settings. " +
                $"Resolution={data.width}x{data.height}@{data.refreshRate}, " +
                $"Mode={data.screenMode}, VSync={data.vSync}, " +
                $"Brightness={data.brightness}, ScreenShake={data.screenShake}",
                this
            );
        }
    }

    public void ApplyBrightnessOnly(float brightness)
    {
        ApplyBrightness(brightness);
    }

    public void ApplyScreenShakeOnly(float screenShake)
    {
        VideoSettingsPrefs.SetRuntimeScreenShakeMultiplier(screenShake);
    }

    public Image GetBrightnessOverlay()
    {
        EnsureBrightnessOverlay();
        return brightnessOverlay;
    }

    private VideoSettingsData BuildDefaultSettings()
    {
        VideoSettingsData data = new VideoSettingsData
        {
            width = defaultWidth,
            height = defaultHeight,
            refreshRate = defaultRefreshRate,
            screenMode = defaultScreenMode,
            vSync = defaultVSync,
            brightness = defaultBrightness,
            screenShake = defaultScreenShake
        };

        if (data.width <= 0)
            data.width = Screen.currentResolution.width;

        if (data.height <= 0)
            data.height = Screen.currentResolution.height;

        return data;
    }

    private void ApplyBrightness(float brightness)
    {
        EnsureBrightnessOverlay();

        if (brightnessOverlay == null)
        {
            if (warnAboutMissingOverlay)
            {
                Debug.LogWarning(
                    $"{nameof(VideoSettingsRuntimeApplier)}: Brightness Overlay не назначен и не был создан. " +
                    "Яркость сохранится, но визуально не применится.",
                    this
                );
            }

            return;
        }

        Color color = brightnessOverlay.color;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        color.a = Mathf.Clamp01((1f - Mathf.Clamp01(brightness)) * maxDarkeningAtZeroBrightness);

        brightnessOverlay.color = color;
        brightnessOverlay.raycastTarget = false;
    }

    private void EnsureBrightnessOverlay()
    {
        if (brightnessOverlay != null)
        {
            brightnessOverlay.raycastTarget = false;
            return;
        }

        if (!autoCreateBrightnessOverlayIfMissing)
            return;

        Transform existingCanvas = transform.Find(runtimeCanvasName);
        Canvas canvas;

        if (existingCanvas != null && existingCanvas.TryGetComponent(out canvas))
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = overlaySortingOrder;
        }
        else
        {
            GameObject canvasObject = new GameObject(runtimeCanvasName);
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = overlaySortingOrder;

            CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
        }

        Transform existingOverlay = canvas.transform.Find(brightnessOverlayName);

        if (existingOverlay != null && existingOverlay.TryGetComponent(out brightnessOverlay))
        {
            brightnessOverlay.raycastTarget = false;
            StretchToFullscreen(brightnessOverlay.rectTransform);
            return;
        }

        GameObject overlayObject = new GameObject(brightnessOverlayName);
        overlayObject.transform.SetParent(canvas.transform, false);

        brightnessOverlay = overlayObject.AddComponent<Image>();
        brightnessOverlay.color = new Color(0f, 0f, 0f, 0f);
        brightnessOverlay.raycastTarget = false;

        StretchToFullscreen(brightnessOverlay.rectTransform);
    }

    private void StretchToFullscreen(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.anchoredPosition = Vector2.zero;
    }

    private void SubscribeSceneLoaded()
    {
        if (subscribedToSceneLoaded)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        subscribedToSceneLoaded = true;
    }

    private void UnsubscribeSceneLoaded()
    {
        if (!subscribedToSceneLoaded)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        subscribedToSceneLoaded = false;
    }
}