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

        public string Label
        {
            get
            {
                if (refreshRate > 0)
                    return width + " X " + height + " @ " + refreshRate;

                return width + " X " + height;
            }
        }

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
    [Tooltip("Черный fullscreen Image поверх экрана. Raycast Target лучше выключить.")]
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

    private readonly List<ResolutionOption> resolutionOptions = new List<ResolutionOption>();

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
        RefreshPercentTexts();
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

    private void BuildResolutionOptions()
    {
        resolutionOptions.Clear();

        Dictionary<string, ResolutionOption> unique = new Dictionary<string, ResolutionOption>();
        Resolution[] unityResolutions = Screen.resolutions;

        for (int i = 0; i < unityResolutions.Length; i++)
        {
            Resolution resolution = unityResolutions[i];

            if (resolution.width <= 0 || resolution.height <= 0)
                continue;

            string key = resolution.width + "x" + resolution.height;
            int refreshRate = resolution.refreshRate;

            ResolutionOption option = new ResolutionOption(
                resolution.width,
                resolution.height,
                refreshRate
            );

            if (!unique.ContainsKey(key))
            {
                unique.Add(key, option);
                continue;
            }

            if (refreshRate > unique[key].refreshRate)
                unique[key] = option;
        }

        AddResolutionIfMissing(unique, defaultWidth, defaultHeight, defaultRefreshRate);

        Resolution current = Screen.currentResolution;
        AddResolutionIfMissing(unique, current.width, current.height, current.refreshRate);

        foreach (KeyValuePair<string, ResolutionOption> pair in unique)
            resolutionOptions.Add(pair.Value);

        resolutionOptions.Sort((a, b) =>
        {
            int widthCompare = a.width.CompareTo(b.width);
            if (widthCompare != 0)
                return widthCompare;

            int heightCompare = a.height.CompareTo(b.height);
            if (heightCompare != 0)
                return heightCompare;

            return a.refreshRate.CompareTo(b.refreshRate);
        });
    }

    private void AddResolutionIfMissing(Dictionary<string, ResolutionOption> unique, int width, int height, int refreshRate)
    {
        if (width <= 0 || height <= 0)
            return;

        string key = width + "x" + height;

        if (!unique.ContainsKey(key))
            unique.Add(key, new ResolutionOption(width, height, refreshRate));
    }

    private void BuildDropdownOptions()
    {
        List<string> resolutionLabels = new List<string>();

        for (int i = 0; i < resolutionOptions.Count; i++)
            resolutionLabels.Add(resolutionOptions[i].Label);

        SetDropdownOptions(resolutionTmpDropdown, resolutionDropdown, resolutionLabels);

        SetDropdownOptions(screenModeTmpDropdown, screenModeDropdown, new List<string>
        {
            "FULLSCREEN",
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

        if (screenModeTmpDropdown != null)
            screenModeTmpDropdown.onValueChanged.AddListener(OnScreenModeChanged);
        else if (screenModeDropdown != null)
            screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);

        if (vSyncTmpDropdown != null)
            vSyncTmpDropdown.onValueChanged.AddListener(OnVSyncChanged);
        else if (vSyncDropdown != null)
            vSyncDropdown.onValueChanged.AddListener(OnVSyncChanged);

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

        if (screenModeTmpDropdown != null)
            screenModeTmpDropdown.onValueChanged.RemoveListener(OnScreenModeChanged);

        if (screenModeDropdown != null)
            screenModeDropdown.onValueChanged.RemoveListener(OnScreenModeChanged);

        if (vSyncTmpDropdown != null)
            vSyncTmpDropdown.onValueChanged.RemoveListener(OnVSyncChanged);

        if (vSyncDropdown != null)
            vSyncDropdown.onValueChanged.RemoveListener(OnVSyncChanged);

        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);

        if (screenShakeSlider != null)
            screenShakeSlider.onValueChanged.RemoveListener(OnScreenShakeChanged);
    }

    private void ApplyPendingSettingsToUI()
    {
        suppressCallbacks = true;

        int resolutionIndex = FindResolutionIndex(
            pendingSettings.width,
            pendingSettings.height,
            pendingSettings.refreshRate
        );

        SetDropdownValueWithoutNotify(resolutionTmpDropdown, resolutionDropdown, resolutionIndex);
        SetDropdownValueWithoutNotify(screenModeTmpDropdown, screenModeDropdown, (int)pendingSettings.screenMode);
        SetDropdownValueWithoutNotify(vSyncTmpDropdown, vSyncDropdown, pendingSettings.vSync ? 1 : 0);

        if (brightnessSlider != null)
            brightnessSlider.SetValueWithoutNotify(Mathf.Clamp01(pendingSettings.brightness));

        if (screenShakeSlider != null)
            screenShakeSlider.SetValueWithoutNotify(Mathf.Clamp(pendingSettings.screenShake, 0f, screenShakeSlider.maxValue));

        suppressCallbacks = false;
        RefreshPercentTexts();
    }

    private void ReadPendingSettingsFromUI()
    {
        int resolutionIndex = GetDropdownValue(resolutionTmpDropdown, resolutionDropdown);
        resolutionIndex = Mathf.Clamp(resolutionIndex, 0, Mathf.Max(0, resolutionOptions.Count - 1));

        if (resolutionOptions.Count > 0)
        {
            ResolutionOption option = resolutionOptions[resolutionIndex];

            pendingSettings.width = option.width;
            pendingSettings.height = option.height;
            pendingSettings.refreshRate = option.refreshRate;
        }

        pendingSettings.screenMode = (VideoScreenMode)Mathf.Clamp(
            GetDropdownValue(screenModeTmpDropdown, screenModeDropdown),
            0,
            2
        );

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

        return 0;
    }

    private void OnResolutionChanged(int value)
    {
        if (suppressCallbacks)
            return;

        ReadPendingSettingsFromUI();
        AutoApplyAndSaveIfEnabled();
    }

    private void OnScreenModeChanged(int value)
    {
        if (suppressCallbacks)
            return;

        ReadPendingSettingsFromUI();
        AutoApplyAndSaveIfEnabled();
    }

    private void OnVSyncChanged(int value)
    {
        if (suppressCallbacks)
            return;

        ReadPendingSettingsFromUI();
        AutoApplyAndSaveIfEnabled();
    }

    private void OnBrightnessChanged(float value)
    {
        if (suppressCallbacks)
            return;

        pendingSettings.brightness = Mathf.Clamp01(value);
        RefreshBrightnessPercent();
        ApplyBrightnessPreview(pendingSettings.brightness);
        AutoApplyAndSaveIfEnabled();
    }

    private void OnScreenShakeChanged(float value)
    {
        if (suppressCallbacks)
            return;

        pendingSettings.screenShake = Mathf.Max(0f, value);
        RefreshScreenShakePercent();
        VideoSettingsPrefs.SetRuntimeScreenShakeMultiplier(pendingSettings.screenShake);
        AutoApplyAndSaveIfEnabled();
    }

    private void AutoApplyAndSaveIfEnabled()
    {
        if (!autoApplyAndSaveOnChange)
            return;

        ApplyAndSavePendingSettings();
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
            Debug.Log($"{nameof(VideoSettingsUI)}: video settings auto-applied and saved.", this);
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

    private void RefreshPercentTexts()
    {
        RefreshBrightnessPercent();
        RefreshScreenShakePercent();
    }

    private void RefreshBrightnessPercent()
    {
        float value = brightnessSlider != null ? brightnessSlider.value : pendingSettings.brightness;
        string text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";

        SetText(brightnessPercentTmpText, brightnessPercentText, text);
    }

    private void RefreshScreenShakePercent()
    {
        float value = screenShakeSlider != null ? screenShakeSlider.value : pendingSettings.screenShake;
        string text = Mathf.RoundToInt(Mathf.Max(0f, value) * 100f) + "%";

        SetText(screenShakePercentTmpText, screenShakePercentText, text);
    }

    private void SetDropdownOptions(TMP_Dropdown tmpDropdown, Dropdown legacyDropdown, List<string> labels)
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

    private void SetDropdownValueWithoutNotify(TMP_Dropdown tmpDropdown, Dropdown legacyDropdown, int value)
    {
        if (tmpDropdown != null)
        {
            tmpDropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, tmpDropdown.options.Count - 1)));
            tmpDropdown.RefreshShownValue();
            return;
        }

        if (legacyDropdown != null)
        {
            legacyDropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, legacyDropdown.options.Count - 1)));
            legacyDropdown.RefreshShownValue();
        }
    }

    private int GetDropdownValue(TMP_Dropdown tmpDropdown, Dropdown legacyDropdown)
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

        if (resolutionTmpDropdown == null && resolutionDropdown == null)
            Debug.LogWarning($"{nameof(VideoSettingsUI)}: Resolution Dropdown не назначен.", this);

        if (screenModeTmpDropdown == null && screenModeDropdown == null)
            Debug.LogWarning($"{nameof(VideoSettingsUI)}: Screen Mode Dropdown не назначен.", this);

        if (vSyncTmpDropdown == null && vSyncDropdown == null)
            Debug.LogWarning($"{nameof(VideoSettingsUI)}: VSync Dropdown не назначен.", this);

        if (brightnessSlider == null)
            Debug.LogWarning($"{nameof(VideoSettingsUI)}: Brightness Slider не назначен.", this);

        if (screenShakeSlider == null)
            Debug.LogWarning($"{nameof(VideoSettingsUI)}: Screen Shake Slider не назначен.", this);

        if (brightnessOverlay == null)
            Debug.LogWarning($"{nameof(VideoSettingsUI)}: Brightness Overlay не назначен. Яркость будет сохраняться, но визуально не затемнит экран.", this);
    }
}