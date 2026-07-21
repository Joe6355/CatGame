using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VideoSettingsUI : MonoBehaviour
{
    [System.Serializable]
    private struct ResolutionOption
    {
        public int width;
        public int height;
        public int refreshRate;

        public string Label => width + " X " + height;

        public ResolutionOption(int width, int height, int refreshRate)
        {
            this.width = width;
            this.height = height;
            this.refreshRate = refreshRate;
        }
    }

    [Header("Dropdowns - TMP")]
    [SerializeField] private TMP_Dropdown resolutionTmpDropdown;
    [SerializeField] private TMP_Dropdown screenModeTmpDropdown;
    [SerializeField] private TMP_Dropdown vSyncTmpDropdown;

    [Header("Dropdowns - Legacy UI")]
    [SerializeField] private Dropdown resolutionDropdown;
    [SerializeField] private Dropdown screenModeDropdown;
    [SerializeField] private Dropdown vSyncDropdown;

    [Header("Buttons - Cycle Values")]
    [Tooltip("Кнопка значения Resolution. По нажатию переключает разрешение по кругу.")]
    [SerializeField] private Button resolutionButton;

    [Tooltip("Внутренний TMP_Text кнопки Resolution.")]
    [SerializeField] private TMP_Text resolutionButtonText;

    [Tooltip("Кнопка значения Screen Mode. По нажатию переключает режим по кругу.")]
    [SerializeField] private Button screenModeButton;

    [Tooltip("Внутренний TMP_Text кнопки Screen Mode.")]
    [SerializeField] private TMP_Text screenModeButtonText;

    [Tooltip("Кнопка значения Vertical Synchronization. По нажатию переключает ON/OFF.")]
    [SerializeField] private Button vSyncButton;

    [Tooltip("Внутренний TMP_Text кнопки Vertical Synchronization.")]
    [SerializeField] private TMP_Text vSyncButtonText;

    [Tooltip("Если текст кнопки не назначен, попытаться найти дочерний TMP_Text с именем Label.")]
    [SerializeField] private bool autoFindButtonValueTexts = true;

    [Header("Sliders")]
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Slider screenShakeSlider;

    [Header("Percent Text - TMP")]
    [SerializeField] private TMP_Text brightnessPercentTmpText;
    [SerializeField] private TMP_Text screenShakePercentTmpText;

    [Header("Percent Text - Legacy UI")]
    [SerializeField] private Text brightnessPercentText;
    [SerializeField] private Text screenShakePercentText;

    [Header("Brightness Overlay")]
    [Tooltip("Необязательно. Если есть VideoSettingsRuntimeApplier, используется его fullscreen overlay.")]
    [SerializeField] private Image brightnessOverlay;

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

    [Header("Slider Limits")]
    [SerializeField] private float screenShakeSliderMaxValue = 2f;

    [Header("Apply Behaviour")]
    [Tooltip("Если включено, сохраненные настройки экрана применяются при запуске этого UI.")]
    [SerializeField] private bool applySavedDisplaySettingsOnAwake = true;

    [Tooltip("Если включено, любое изменение в UI сразу применяется и сохраняется без кнопки Apply.")]
    [SerializeField] private bool autoApplyAndSaveOnChange = true;

    [Header("Debug")]
    [SerializeField] private bool warnAboutMissingReferences = true;
    [SerializeField] private bool logApply = false;

    private static readonly Vector2Int[] RecommendedResolutions =
    {
        new Vector2Int(3840, 2160),
        new Vector2Int(3440, 1440),
        new Vector2Int(2560, 1440),
        new Vector2Int(2560, 1080),
        new Vector2Int(1920, 1200),
        new Vector2Int(1920, 1080),
        new Vector2Int(1680, 1050),
        new Vector2Int(1600, 900),
        new Vector2Int(1440, 900),
        new Vector2Int(1366, 768),
        new Vector2Int(1280, 800),
        new Vector2Int(1280, 720)
    };

    private const int MinimumResolutionWidth = 1280;
    private const int MinimumResolutionHeight = 720;
    private const int MaximumResolutionOptions = 6;
    private const float AspectRatioTolerance = 0.04f;

    private readonly List<ResolutionOption> resolutionOptions = new List<ResolutionOption>();

    private int currentResolutionIndex;
    private bool initialized;
    private bool eventsWired;
    private bool suppressCallbacks;
    private VideoSettingsData pendingSettings;

    private void Awake()
    {
        InitializeOnce();
    }

    private void OnEnable()
    {
        InitializeOnce();
        WireEvents();
        ApplyRuntimeOnlySettings();
        RefreshAllDisplayedValues();
    }

    private void OnDisable()
    {
        UnwireEvents();
    }

    private void OnDestroy()
    {
        UnwireEvents();
    }

    public void SaveAndApplyCurrentSettings()
    {
        InitializeOnce();
        ReadPendingSettingsFromUI();
        ApplyAndSavePendingSettings();
    }

    public void ReloadSavedSettings()
    {
        InitializeOnce();

        pendingSettings = VideoSettingsPrefs.ReadOrDefault(BuildDefaultSettings());
        ApplyPendingSettingsToUI();
        ApplyRuntimeOnlySettings();
    }

    public void DeleteSavedVideoSettingsForDebug()
    {
        VideoSettingsPrefs.DeleteSavedSettings();

        pendingSettings = BuildDefaultSettings();
        ApplyPendingSettingsToUI();
        ApplyAndSavePendingSettings();
    }

    private void InitializeOnce()
    {
        if (initialized)
            return;

        initialized = true;

        VideoSettingsPrefs.MigrateDefaultsIfNeeded(
            defaultSettingsVersion,
            forceMigrateDefaultsWhenVersionChanges
        );

        ResolveButtonValueTexts();
        BuildResolutionOptions();
        BuildDropdownOptions();
        ConfigureSliders();

        pendingSettings = VideoSettingsPrefs.ReadOrDefault(BuildDefaultSettings());
        ApplyPendingSettingsToUI();

        if (applySavedDisplaySettingsOnAwake)
        {
            VideoSettingsPrefs.ApplyDisplaySettings(
                pendingSettings,
                applyResolutionAndScreenMode: true,
                applyVSync: true
            );
        }

        ApplyRuntimeOnlySettings();
        ValidateReferences();
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

    private void ResolveButtonValueTexts()
    {
        if (!autoFindButtonValueTexts)
            return;

        if (resolutionButtonText == null)
            resolutionButtonText = FindButtonValueText(resolutionButton);

        if (screenModeButtonText == null)
            screenModeButtonText = FindButtonValueText(screenModeButton);

        if (vSyncButtonText == null)
            vSyncButtonText = FindButtonValueText(vSyncButton);
    }

    private TMP_Text FindButtonValueText(Button button)
    {
        if (button == null)
            return null;

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].gameObject.name == "Label")
                return texts[i];
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private void BuildResolutionOptions()
    {
        resolutionOptions.Clear();

        Dictionary<string, ResolutionOption> available =
            new Dictionary<string, ResolutionOption>();

        Resolution[] unityResolutions = Screen.resolutions;

        for (int i = 0; i < unityResolutions.Length; i++)
        {
            Resolution resolution = unityResolutions[i];

            if (resolution.width <= 0 || resolution.height <= 0)
                continue;

            string key = ResolutionKey(resolution.width, resolution.height);
            ResolutionOption option = new ResolutionOption(
                resolution.width,
                resolution.height,
                resolution.refreshRate
            );

            if (!available.ContainsKey(key) ||
                option.refreshRate > available[key].refreshRate)
            {
                available[key] = option;
            }
        }

        Resolution native = Screen.currentResolution;
        float nativeAspect = native.height > 0
            ? native.width / (float)native.height
            : 16f / 9f;

        for (int i = 0; i < RecommendedResolutions.Length; i++)
        {
            Vector2Int recommended = RecommendedResolutions[i];

            if (!IsSuitableResolution(
                    recommended.x,
                    recommended.y,
                    native.width,
                    native.height,
                    nativeAspect))
            {
                continue;
            }

            string key = ResolutionKey(recommended.x, recommended.y);

            if (available.TryGetValue(key, out ResolutionOption option))
                AddResolutionOptionIfMissing(option);
        }

        AddNativeResolutionIfMissing(native);

        if (resolutionOptions.Count < 2)
        {
            foreach (KeyValuePair<string, ResolutionOption> pair in available)
            {
                ResolutionOption option = pair.Value;

                if (!IsSuitableResolution(
                        option.width,
                        option.height,
                        native.width,
                        native.height,
                        nativeAspect))
                {
                    continue;
                }

                AddResolutionOptionIfMissing(option);
            }
        }

        resolutionOptions.Sort((a, b) =>
        {
            long aPixels = (long)a.width * a.height;
            long bPixels = (long)b.width * b.height;

            int pixelsCompare = bPixels.CompareTo(aPixels);

            if (pixelsCompare != 0)
                return pixelsCompare;

            return b.refreshRate.CompareTo(a.refreshRate);
        });

        if (resolutionOptions.Count > MaximumResolutionOptions)
        {
            resolutionOptions.RemoveRange(
                MaximumResolutionOptions,
                resolutionOptions.Count - MaximumResolutionOptions
            );
        }

        if (resolutionOptions.Count == 0)
        {
            resolutionOptions.Add(
                new ResolutionOption(
                    native.width > 0 ? native.width : defaultWidth,
                    native.height > 0 ? native.height : defaultHeight,
                    native.refreshRate
                )
            );
        }
    }

    private bool IsSuitableResolution(
        int width,
        int height,
        int nativeWidth,
        int nativeHeight,
        float nativeAspect)
    {
        if (width < MinimumResolutionWidth || height < MinimumResolutionHeight)
            return false;

        if (nativeWidth > 0 && width > nativeWidth)
            return false;

        if (nativeHeight > 0 && height > nativeHeight)
            return false;

        float aspect = width / (float)height;
        return Mathf.Abs(aspect - nativeAspect) <= AspectRatioTolerance;
    }

    private void AddNativeResolutionIfMissing(Resolution native)
    {
        if (native.width <= 0 || native.height <= 0)
            return;

        AddResolutionOptionIfMissing(
            new ResolutionOption(
                native.width,
                native.height,
                native.refreshRate
            )
        );
    }

    private void AddResolutionOptionIfMissing(ResolutionOption option)
    {
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            if (resolutionOptions[i].width == option.width &&
                resolutionOptions[i].height == option.height)
            {
                return;
            }
        }

        resolutionOptions.Add(option);
    }

    private string ResolutionKey(int width, int height)
    {
        return width + "x" + height;
    }

    private void BuildDropdownOptions()
    {
        List<string> resolutionLabels = new List<string>();

        for (int i = 0; i < resolutionOptions.Count; i++)
            resolutionLabels.Add(resolutionOptions[i].Label);

        SetDropdownOptions(resolutionTmpDropdown, resolutionDropdown, resolutionLabels);

        SetDropdownOptions(screenModeTmpDropdown, screenModeDropdown, new List<string>
        {
            "FULL SCREEN",
            "WINDOWED",
            "BORDERLESS"
        });

        SetDropdownOptions(vSyncTmpDropdown, vSyncDropdown, new List<string>
        {
            "OFF",
            "ON"
        });
    }

    private void ConfigureSliders()
    {
        if (brightnessSlider != null)
        {
            brightnessSlider.minValue = 0f;
            brightnessSlider.maxValue = 1f;
            brightnessSlider.wholeNumbers = false;
        }

        if (screenShakeSlider != null)
        {
            screenShakeSlider.minValue = 0f;
            screenShakeSlider.maxValue = Mathf.Max(0.01f, screenShakeSliderMaxValue);
            screenShakeSlider.wholeNumbers = false;
        }
    }

    private void WireEvents()
    {
        if (eventsWired)
            return;

        eventsWired = true;

        if (resolutionTmpDropdown != null)
            resolutionTmpDropdown.onValueChanged.AddListener(OnResolutionChanged);
        else if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        if (resolutionButton != null)
            resolutionButton.onClick.AddListener(CycleResolutionFromButton);

        if (screenModeTmpDropdown != null)
            screenModeTmpDropdown.onValueChanged.AddListener(OnScreenModeChanged);
        else if (screenModeDropdown != null)
            screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);

        if (vSyncTmpDropdown != null)
            vSyncTmpDropdown.onValueChanged.AddListener(OnVSyncChanged);
        else if (vSyncDropdown != null)
            vSyncDropdown.onValueChanged.AddListener(OnVSyncChanged);

        if (screenModeButton != null)
            screenModeButton.onClick.AddListener(CycleScreenModeFromButton);

        if (vSyncButton != null)
            vSyncButton.onClick.AddListener(ToggleVSyncFromButton);

        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);

        if (screenShakeSlider != null)
            screenShakeSlider.onValueChanged.AddListener(OnScreenShakeChanged);
    }

    private void UnwireEvents()
    {
        if (!eventsWired)
            return;

        eventsWired = false;

        if (resolutionTmpDropdown != null)
            resolutionTmpDropdown.onValueChanged.RemoveListener(OnResolutionChanged);

        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);

        if (resolutionButton != null)
            resolutionButton.onClick.RemoveListener(CycleResolutionFromButton);

        if (screenModeTmpDropdown != null)
            screenModeTmpDropdown.onValueChanged.RemoveListener(OnScreenModeChanged);

        if (screenModeDropdown != null)
            screenModeDropdown.onValueChanged.RemoveListener(OnScreenModeChanged);

        if (vSyncTmpDropdown != null)
            vSyncTmpDropdown.onValueChanged.RemoveListener(OnVSyncChanged);

        if (vSyncDropdown != null)
            vSyncDropdown.onValueChanged.RemoveListener(OnVSyncChanged);

        if (screenModeButton != null)
            screenModeButton.onClick.RemoveListener(CycleScreenModeFromButton);

        if (vSyncButton != null)
            vSyncButton.onClick.RemoveListener(ToggleVSyncFromButton);

        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);

        if (screenShakeSlider != null)
            screenShakeSlider.onValueChanged.RemoveListener(OnScreenShakeChanged);
    }

    private void ApplyPendingSettingsToUI()
    {
        suppressCallbacks = true;

        currentResolutionIndex = FindResolutionIndex(
            pendingSettings.width,
            pendingSettings.height,
            pendingSettings.refreshRate
        );

        if (currentResolutionIndex < 0)
            currentResolutionIndex = 0;

        if (resolutionOptions.Count > 0)
        {
            ResolutionOption selectedResolution =
                resolutionOptions[Mathf.Clamp(
                    currentResolutionIndex,
                    0,
                    resolutionOptions.Count - 1
                )];

            pendingSettings.width = selectedResolution.width;
            pendingSettings.height = selectedResolution.height;
            pendingSettings.refreshRate = selectedResolution.refreshRate;
        }

        SetDropdownValueWithoutNotify(
            resolutionTmpDropdown,
            resolutionDropdown,
            currentResolutionIndex
        );
        SetDropdownValueWithoutNotify(screenModeTmpDropdown, screenModeDropdown, (int)pendingSettings.screenMode);
        SetDropdownValueWithoutNotify(vSyncTmpDropdown, vSyncDropdown, pendingSettings.vSync ? 1 : 0);

        if (brightnessSlider != null)
            brightnessSlider.SetValueWithoutNotify(Mathf.Clamp01(pendingSettings.brightness));

        if (screenShakeSlider != null)
        {
            screenShakeSlider.SetValueWithoutNotify(
                Mathf.Clamp(pendingSettings.screenShake, 0f, screenShakeSlider.maxValue)
            );
        }

        suppressCallbacks = false;
        RefreshAllDisplayedValues();
    }

    private void ReadPendingSettingsFromUI()
    {
        if (resolutionButton == null && HasResolutionDropdown())
        {
            int resolutionIndex =
                GetDropdownValue(resolutionTmpDropdown, resolutionDropdown);

            resolutionIndex = Mathf.Clamp(
                resolutionIndex,
                0,
                Mathf.Max(0, resolutionOptions.Count - 1)
            );

            if (resolutionOptions.Count > 0)
            {
                currentResolutionIndex = resolutionIndex;
                ResolutionOption option = resolutionOptions[resolutionIndex];

                pendingSettings.width = option.width;
                pendingSettings.height = option.height;
                pendingSettings.refreshRate = option.refreshRate;
            }
        }

        if (HasScreenModeDropdown())
        {
            pendingSettings.screenMode = (VideoScreenMode)Mathf.Clamp(
                GetDropdownValue(screenModeTmpDropdown, screenModeDropdown),
                0,
                2
            );
        }

        if (HasVSyncDropdown())
            pendingSettings.vSync = GetDropdownValue(vSyncTmpDropdown, vSyncDropdown) == 1;

        if (brightnessSlider != null)
            pendingSettings.brightness = Mathf.Clamp01(brightnessSlider.value);

        if (screenShakeSlider != null)
            pendingSettings.screenShake = Mathf.Max(0f, screenShakeSlider.value);
    }

    private int FindResolutionIndex(int width, int height, int refreshRate)
    {
        int sameSizeIndex = -1;

        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            ResolutionOption option = resolutionOptions[i];

            if (option.width != width || option.height != height)
                continue;

            if (sameSizeIndex < 0)
                sameSizeIndex = i;

            if (refreshRate <= 0 || option.refreshRate == refreshRate)
                return i;
        }

        if (sameSizeIndex >= 0)
            return sameSizeIndex;

        return -1;
    }

    private void OnResolutionChanged(int value)
    {
        if (suppressCallbacks)
            return;

        ReadPendingSettingsFromUI();
        ApplyDisplayPartAndSave(
            applyResolutionAndScreenMode: true,
            applyVSync: false
        );
    }

    private void CycleResolutionFromButton()
    {
        if (resolutionOptions.Count == 0)
            return;

        currentResolutionIndex =
            (currentResolutionIndex + 1) % resolutionOptions.Count;

        ResolutionOption option = resolutionOptions[currentResolutionIndex];

        pendingSettings.width = option.width;
        pendingSettings.height = option.height;
        pendingSettings.refreshRate = option.refreshRate;

        SetDropdownValueWithoutNotify(
            resolutionTmpDropdown,
            resolutionDropdown,
            currentResolutionIndex
        );

        RefreshResolutionText();

        ApplyDisplayPartAndSave(
            applyResolutionAndScreenMode: true,
            applyVSync: false
        );
    }

    private void OnScreenModeChanged(int value)
    {
        if (suppressCallbacks)
            return;

        ReadPendingSettingsFromUI();
        RefreshScreenModeText();

        ApplyDisplayPartAndSave(
            applyResolutionAndScreenMode: true,
            applyVSync: false
        );
    }

    private void OnVSyncChanged(int value)
    {
        if (suppressCallbacks)
            return;

        ReadPendingSettingsFromUI();
        RefreshVSyncText();

        ApplyDisplayPartAndSave(
            applyResolutionAndScreenMode: false,
            applyVSync: true
        );
    }

    private void CycleScreenModeFromButton()
    {
        int nextValue = ((int)pendingSettings.screenMode + 1) % 3;
        pendingSettings.screenMode = (VideoScreenMode)nextValue;

        SetDropdownValueWithoutNotify(
            screenModeTmpDropdown,
            screenModeDropdown,
            nextValue
        );

        RefreshScreenModeText();

        ApplyDisplayPartAndSave(
            applyResolutionAndScreenMode: true,
            applyVSync: false
        );
    }

    private void ToggleVSyncFromButton()
    {
        pendingSettings.vSync = !pendingSettings.vSync;

        SetDropdownValueWithoutNotify(
            vSyncTmpDropdown,
            vSyncDropdown,
            pendingSettings.vSync ? 1 : 0
        );

        RefreshVSyncText();

        ApplyDisplayPartAndSave(
            applyResolutionAndScreenMode: false,
            applyVSync: true
        );
    }

    private void OnBrightnessChanged(float value)
    {
        if (suppressCallbacks)
            return;

        pendingSettings.brightness = Mathf.Clamp01(value);

        RefreshBrightnessPercent();
        ApplyBrightnessPreview(pendingSettings.brightness);
        SavePendingSettingsIfEnabled();
    }

    private void OnScreenShakeChanged(float value)
    {
        if (suppressCallbacks)
            return;

        pendingSettings.screenShake = Mathf.Max(0f, value);

        RefreshScreenShakePercent();
        VideoSettingsPrefs.SetRuntimeScreenShakeMultiplier(pendingSettings.screenShake);
        SavePendingSettingsIfEnabled();
    }

    private void ApplyDisplayPartAndSave(
        bool applyResolutionAndScreenMode,
        bool applyVSync)
    {
        if (!autoApplyAndSaveOnChange)
            return;

        VideoSettingsPrefs.ApplyDisplaySettings(
            pendingSettings,
            applyResolutionAndScreenMode,
            applyVSync
        );

        VideoSettingsPrefs.Save(pendingSettings, defaultSettingsVersion);

        if (logApply)
        {
            Debug.Log(
                $"{nameof(VideoSettingsUI)}: display setting changed. " +
                $"Mode={pendingSettings.screenMode}, VSync={pendingSettings.vSync}",
                this
            );
        }
    }

    private void SavePendingSettingsIfEnabled()
    {
        if (!autoApplyAndSaveOnChange)
            return;

        VideoSettingsPrefs.Save(pendingSettings, defaultSettingsVersion);
    }

    private void ApplyAndSavePendingSettings()
    {
        VideoSettingsPrefs.ApplyDisplaySettings(
            pendingSettings,
            applyResolutionAndScreenMode: true,
            applyVSync: true
        );

        ApplyRuntimeOnlySettings();
        VideoSettingsPrefs.Save(pendingSettings, defaultSettingsVersion);

        if (logApply)
            Debug.Log($"{nameof(VideoSettingsUI)}: video settings applied and saved.", this);
    }

    private void ApplyRuntimeOnlySettings()
    {
        if (VideoSettingsRuntimeApplier.Instance != null)
        {
            VideoSettingsRuntimeApplier.Instance.ApplyBrightnessOnly(pendingSettings.brightness);
            VideoSettingsRuntimeApplier.Instance.ApplyScreenShakeOnly(pendingSettings.screenShake);
            return;
        }

        ApplyBrightnessPreview(pendingSettings.brightness);
        VideoSettingsPrefs.SetRuntimeScreenShakeMultiplier(pendingSettings.screenShake);
    }

    private void ApplyBrightnessPreview(float brightness)
    {
        brightness = Mathf.Clamp01(brightness);

        if (VideoSettingsRuntimeApplier.Instance != null)
        {
            brightnessOverlay = VideoSettingsRuntimeApplier.Instance.GetBrightnessOverlay();
            VideoSettingsRuntimeApplier.Instance.ApplyBrightnessOnly(brightness);
            return;
        }

        if (brightnessOverlay == null)
            return;

        Color color = brightnessOverlay.color;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        color.a = Mathf.Clamp01((1f - brightness) * maxDarkeningAtZeroBrightness);

        brightnessOverlay.color = color;
        brightnessOverlay.raycastTarget = false;
    }

    private void RefreshAllDisplayedValues()
    {
        RefreshResolutionText();
        RefreshScreenModeText();
        RefreshVSyncText();
        RefreshPercentTexts();
    }

    private void RefreshResolutionText()
    {
        if (resolutionButtonText == null)
            return;

        resolutionButtonText.text =
            pendingSettings.width + " X " + pendingSettings.height;
    }

    private void RefreshScreenModeText()
    {
        if (screenModeButtonText == null)
            return;

        switch (pendingSettings.screenMode)
        {
            case VideoScreenMode.Windowed:
                screenModeButtonText.text = "WINDOWED";
                break;

            case VideoScreenMode.Borderless:
                screenModeButtonText.text = "BORDERLESS";
                break;

            case VideoScreenMode.Fullscreen:
            default:
                screenModeButtonText.text = "FULL SCREEN";
                break;
        }
    }

    private void RefreshVSyncText()
    {
        if (vSyncButtonText != null)
            vSyncButtonText.text = pendingSettings.vSync ? "ON" : "OFF";
    }

    private void RefreshPercentTexts()
    {
        RefreshBrightnessPercent();
        RefreshScreenShakePercent();
    }

    private void RefreshBrightnessPercent()
    {
        float value = brightnessSlider != null
            ? brightnessSlider.value
            : pendingSettings.brightness;

        string text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        SetText(brightnessPercentTmpText, brightnessPercentText, text);
    }

    private void RefreshScreenShakePercent()
    {
        float value = screenShakeSlider != null
            ? screenShakeSlider.value
            : pendingSettings.screenShake;

        string text = Mathf.RoundToInt(Mathf.Max(0f, value) * 100f) + "%";
        SetText(screenShakePercentTmpText, screenShakePercentText, text);
    }

    private bool HasResolutionDropdown()
    {
        return resolutionTmpDropdown != null || resolutionDropdown != null;
    }

    private bool HasResolutionControl()
    {
        return resolutionButton != null || HasResolutionDropdown();
    }

    private bool HasScreenModeDropdown()
    {
        return screenModeTmpDropdown != null || screenModeDropdown != null;
    }

    private bool HasVSyncDropdown()
    {
        return vSyncTmpDropdown != null || vSyncDropdown != null;
    }

    private void SetDropdownOptions(
        TMP_Dropdown tmpDropdown,
        Dropdown legacyDropdown,
        List<string> labels)
    {
        if (tmpDropdown != null)
        {
            tmpDropdown.ClearOptions();
            tmpDropdown.AddOptions(labels);
            return;
        }

        if (legacyDropdown != null)
        {
            legacyDropdown.ClearOptions();
            legacyDropdown.AddOptions(labels);
        }
    }

    private void SetDropdownValueWithoutNotify(
        TMP_Dropdown tmpDropdown,
        Dropdown legacyDropdown,
        int value)
    {
        if (tmpDropdown != null)
        {
            tmpDropdown.SetValueWithoutNotify(
                Mathf.Clamp(value, 0, Mathf.Max(0, tmpDropdown.options.Count - 1))
            );

            tmpDropdown.RefreshShownValue();
            return;
        }

        if (legacyDropdown != null)
        {
            legacyDropdown.SetValueWithoutNotify(
                Mathf.Clamp(value, 0, Mathf.Max(0, legacyDropdown.options.Count - 1))
            );

            legacyDropdown.RefreshShownValue();
        }
    }

    private int GetDropdownValue(
        TMP_Dropdown tmpDropdown,
        Dropdown legacyDropdown)
    {
        if (tmpDropdown != null)
            return tmpDropdown.value;

        if (legacyDropdown != null)
            return legacyDropdown.value;

        return 0;
    }

    private void SetText(TMP_Text tmpText, Text legacyText, string value)
    {
        if (tmpText != null)
        {
            tmpText.text = value;
            return;
        }

        if (legacyText != null)
            legacyText.text = value;
    }

    private void ValidateReferences()
    {
        if (!warnAboutMissingReferences)
            return;

        if (!HasResolutionControl())
        {
            Debug.LogWarning(
                $"{nameof(VideoSettingsUI)}: Resolution Dropdown/Button не назначен.",
                this
            );
        }

        if (!HasScreenModeDropdown() && screenModeButton == null)
        {
            Debug.LogWarning(
                $"{nameof(VideoSettingsUI)}: Screen Mode Dropdown/Button не назначен.",
                this
            );
        }

        if (!HasVSyncDropdown() && vSyncButton == null)
        {
            Debug.LogWarning(
                $"{nameof(VideoSettingsUI)}: VSync Dropdown/Button не назначен.",
                this
            );
        }

        if (brightnessSlider == null)
            Debug.LogWarning($"{nameof(VideoSettingsUI)}: Brightness Slider не назначен.", this);

        if (screenShakeSlider == null)
            Debug.LogWarning($"{nameof(VideoSettingsUI)}: Screen Shake Slider не назначен.", this);

        if (VideoSettingsRuntimeApplier.Instance == null && brightnessOverlay == null)
        {
            Debug.LogWarning(
                $"{nameof(VideoSettingsUI)}: нет VideoSettingsRuntimeApplier и не назначен Brightness Overlay.",
                this
            );
        }
    }
}
