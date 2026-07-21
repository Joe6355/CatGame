using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-8500)]
[DisallowMultipleComponent]
public sealed class FpsCounterRuntime : MonoBehaviour
{
    public const string PrefsKey = "Settings.ShowFpsCounter";

    public static FpsCounterRuntime Instance { get; private set; }

    [Header("Visibility")]
    [SerializeField, Tooltip("Не показывать счетчик в сцене главного меню.")]
    private bool hideInMainMenu = true;

    [SerializeField, Tooltip("Имя сцены главного меню.")]
    private string mainMenuSceneName = "_MainMenu";

    [SerializeField, Tooltip("Значение по умолчанию при отсутствии PlayerPrefs.")]
    private bool defaultVisible = false;

    [Header("Counter")]
    [SerializeField, Min(0.05f), Tooltip("Как часто обновлять число FPS.")]
    private float refreshInterval = 0.25f;

    [SerializeField] private string textPrefix = "FPS: ";

    [Header("Runtime UI")]
    [SerializeField, Tooltip("Можно назначить свой TMP. Если None — Canvas и TMP создадутся автоматически.")]
    private TMP_Text fpsText;

    [SerializeField] private int canvasSortingOrder = 32500;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(20f, -20f);
    [SerializeField] private Vector2 textSize = new Vector2(260f, 60f);
    [SerializeField] private float fontSize = 30f;
    [SerializeField] private Color textColor = Color.white;

    private bool counterEnabled;
    private float accumulatedTime;
    private int accumulatedFrames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureRuntimeUi();

        counterEnabled = PlayerPrefs.GetInt(
            PrefsKey,
            defaultVisible ? 1 : 0
        ) != 0;

        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyVisibility();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (fpsText == null || !fpsText.gameObject.activeInHierarchy)
            return;

        float deltaTime = Time.unscaledDeltaTime;

        if (deltaTime <= 0f)
            return;

        accumulatedTime += deltaTime;
        accumulatedFrames++;

        if (accumulatedTime < Mathf.Max(0.05f, refreshInterval))
            return;

        int fps = Mathf.RoundToInt(accumulatedFrames / accumulatedTime);
        fpsText.text = textPrefix + fps;

        accumulatedTime = 0f;
        accumulatedFrames = 0;
    }

    public static void SetCounterEnabled(bool visible, bool save)
    {
        FpsCounterRuntime runtime = EnsureInstance();

        if (runtime == null)
            return;

        runtime.counterEnabled = visible;

        if (save)
        {
            PlayerPrefs.SetInt(PrefsKey, visible ? 1 : 0);
            PlayerPrefs.Save();
        }

        runtime.ResetMeasurement();
        runtime.ApplyVisibility();
    }

    public static bool ReadSavedCounterEnabled(bool defaultValue = false)
    {
        return PlayerPrefs.GetInt(
            PrefsKey,
            defaultValue ? 1 : 0
        ) != 0;
    }

    private static FpsCounterRuntime EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        FpsCounterRuntime existing =
            Object.FindObjectOfType<FpsCounterRuntime>();

        if (existing != null)
            return existing;

        GameObject runtimeObject = new GameObject("_FpsCounterRuntime");
        return runtimeObject.AddComponent<FpsCounterRuntime>();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetMeasurement();
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        EnsureRuntimeUi();

        if (fpsText == null)
            return;

        bool shouldShow =
            counterEnabled &&
            !ShouldHideInCurrentScene();

        fpsText.gameObject.SetActive(shouldShow);

        if (shouldShow && string.IsNullOrEmpty(fpsText.text))
            fpsText.text = textPrefix + "0";
    }

    private bool ShouldHideInCurrentScene()
    {
        if (!hideInMainMenu)
            return false;

        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid())
            return true;

        return string.Equals(
            activeScene.name,
            mainMenuSceneName,
            System.StringComparison.Ordinal
        );
    }

    private void ResetMeasurement()
    {
        accumulatedTime = 0f;
        accumulatedFrames = 0;

        if (fpsText != null)
            fpsText.text = textPrefix + "0";
    }

    private void EnsureRuntimeUi()
    {
        if (fpsText != null)
        {
            fpsText.raycastTarget = false;
            return;
        }

        Canvas existingCanvas = GetComponentInChildren<Canvas>(true);

        if (existingCanvas == null)
        {
            GameObject canvasObject = new GameObject("_FpsCounterCanvas");
            canvasObject.transform.SetParent(transform, false);

            existingCanvas = canvasObject.AddComponent<Canvas>();
            existingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            existingCanvas.overrideSorting = true;
            existingCanvas.sortingOrder = canvasSortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
        else
        {
            existingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            existingCanvas.overrideSorting = true;
            existingCanvas.sortingOrder = canvasSortingOrder;
        }

        GameObject textObject = new GameObject("_FpsCounterText");
        textObject.transform.SetParent(existingCanvas.transform, false);

        TextMeshProUGUI createdText =
            textObject.AddComponent<TextMeshProUGUI>();

        RectTransform rect = createdText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = textSize;

        createdText.fontSize = fontSize;
        createdText.color = textColor;
        createdText.alignment = TextAlignmentOptions.TopLeft;
        createdText.enableWordWrapping = false;
        createdText.raycastTarget = false;
        createdText.text = textPrefix + "0";

        fpsText = createdText;
    }
}
