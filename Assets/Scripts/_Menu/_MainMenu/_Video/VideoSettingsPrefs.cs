using UnityEngine;

public enum VideoScreenMode
{
    Fullscreen = 0,
    Windowed = 1,
    Borderless = 2
}

[System.Serializable]
public struct VideoSettingsData
{
    public int width;
    public int height;
    public int refreshRate;
    public VideoScreenMode screenMode;
    public bool vSync;
    public float brightness;
    public float screenShake;
}

public static class VideoSettingsPrefs
{
    public const string WidthKey = "Settings.Video.Width";
    public const string HeightKey = "Settings.Video.Height";
    public const string RefreshRateKey = "Settings.Video.RefreshRate";
    public const string ScreenModeKey = "Settings.Video.ScreenMode";
    public const string VSyncKey = "Settings.Video.VSync";
    public const string BrightnessKey = "Settings.Video.Brightness";
    public const string ScreenShakeKey = "Settings.Video.ScreenShake";
    public const string DefaultsVersionKey = "Settings.Video.DefaultsVersion";

    private static bool hasRuntimeScreenShakeOverride;
    private static float runtimeScreenShakeMultiplier = 1f;

    public static VideoSettingsData ReadOrDefault(VideoSettingsData defaults)
    {
        VideoSettingsData data = Normalize(defaults);

        if (PlayerPrefs.HasKey(WidthKey))
            data.width = PlayerPrefs.GetInt(WidthKey, data.width);

        if (PlayerPrefs.HasKey(HeightKey))
            data.height = PlayerPrefs.GetInt(HeightKey, data.height);

        if (PlayerPrefs.HasKey(RefreshRateKey))
            data.refreshRate = PlayerPrefs.GetInt(RefreshRateKey, data.refreshRate);

        if (PlayerPrefs.HasKey(ScreenModeKey))
            data.screenMode = (VideoScreenMode)PlayerPrefs.GetInt(ScreenModeKey, (int)data.screenMode);

        if (PlayerPrefs.HasKey(VSyncKey))
            data.vSync = PlayerPrefs.GetInt(VSyncKey, data.vSync ? 1 : 0) == 1;

        if (PlayerPrefs.HasKey(BrightnessKey))
            data.brightness = PlayerPrefs.GetFloat(BrightnessKey, data.brightness);

        if (PlayerPrefs.HasKey(ScreenShakeKey))
            data.screenShake = PlayerPrefs.GetFloat(ScreenShakeKey, data.screenShake);

        return Normalize(data);
    }

    public static void Save(VideoSettingsData data, int defaultsVersion)
    {
        data = Normalize(data);

        PlayerPrefs.SetInt(WidthKey, data.width);
        PlayerPrefs.SetInt(HeightKey, data.height);
        PlayerPrefs.SetInt(RefreshRateKey, data.refreshRate);
        PlayerPrefs.SetInt(ScreenModeKey, (int)data.screenMode);
        PlayerPrefs.SetInt(VSyncKey, data.vSync ? 1 : 0);
        PlayerPrefs.SetFloat(BrightnessKey, data.brightness);
        PlayerPrefs.SetFloat(ScreenShakeKey, data.screenShake);
        PlayerPrefs.SetInt(DefaultsVersionKey, defaultsVersion);
        PlayerPrefs.Save();

        SetRuntimeScreenShakeMultiplier(data.screenShake);
    }

    public static void ApplyDisplaySettings(VideoSettingsData data, bool applyResolutionAndScreenMode, bool applyVSync)
    {
        data = Normalize(data);

        if (applyVSync)
            QualitySettings.vSyncCount = data.vSync ? 1 : 0;

        if (!applyResolutionAndScreenMode)
            return;

        FullScreenMode unityMode = ToUnityFullScreenMode(data.screenMode);

        if (data.refreshRate > 0)
            Screen.SetResolution(data.width, data.height, unityMode, data.refreshRate);
        else
            Screen.SetResolution(data.width, data.height, unityMode);
    }

    public static FullScreenMode ToUnityFullScreenMode(VideoScreenMode mode)
    {
        switch (mode)
        {
            case VideoScreenMode.Windowed:
                return FullScreenMode.Windowed;

            case VideoScreenMode.Borderless:
                return FullScreenMode.FullScreenWindow;

            case VideoScreenMode.Fullscreen:
            default:
                return FullScreenMode.ExclusiveFullScreen;
        }
    }

    public static float GetScreenShakeMultiplier(float fallback = 1f)
    {
        if (hasRuntimeScreenShakeOverride)
            return Mathf.Max(0f, runtimeScreenShakeMultiplier);

        if (PlayerPrefs.HasKey(ScreenShakeKey))
            return Mathf.Max(0f, PlayerPrefs.GetFloat(ScreenShakeKey, fallback));

        return Mathf.Max(0f, fallback);
    }

    public static void SetRuntimeScreenShakeMultiplier(float value)
    {
        hasRuntimeScreenShakeOverride = true;
        runtimeScreenShakeMultiplier = Mathf.Max(0f, value);
    }

    public static void ClearRuntimeScreenShakeOverride()
    {
        hasRuntimeScreenShakeOverride = false;
        runtimeScreenShakeMultiplier = 1f;
    }

    public static bool HasAnySavedVideoSettings()
    {
        return PlayerPrefs.HasKey(WidthKey)
            || PlayerPrefs.HasKey(HeightKey)
            || PlayerPrefs.HasKey(RefreshRateKey)
            || PlayerPrefs.HasKey(ScreenModeKey)
            || PlayerPrefs.HasKey(VSyncKey)
            || PlayerPrefs.HasKey(BrightnessKey)
            || PlayerPrefs.HasKey(ScreenShakeKey);
    }

    public static void MigrateDefaultsIfNeeded(int defaultsVersion, bool forceMigrateDefaults)
    {
        if (!forceMigrateDefaults)
        {
            if (!PlayerPrefs.HasKey(DefaultsVersionKey))
            {
                PlayerPrefs.SetInt(DefaultsVersionKey, defaultsVersion);
                PlayerPrefs.Save();
            }

            return;
        }

        int savedVersion = PlayerPrefs.GetInt(DefaultsVersionKey, int.MinValue);

        if (savedVersion == defaultsVersion)
            return;

        DeleteSavedSettings();
        PlayerPrefs.SetInt(DefaultsVersionKey, defaultsVersion);
        PlayerPrefs.Save();
    }

    public static void DeleteSavedSettings()
    {
        PlayerPrefs.DeleteKey(WidthKey);
        PlayerPrefs.DeleteKey(HeightKey);
        PlayerPrefs.DeleteKey(RefreshRateKey);
        PlayerPrefs.DeleteKey(ScreenModeKey);
        PlayerPrefs.DeleteKey(VSyncKey);
        PlayerPrefs.DeleteKey(BrightnessKey);
        PlayerPrefs.DeleteKey(ScreenShakeKey);
        PlayerPrefs.DeleteKey(DefaultsVersionKey);
        ClearRuntimeScreenShakeOverride();
    }

    private static VideoSettingsData Normalize(VideoSettingsData data)
    {
        Resolution current = Screen.currentResolution;

        if (data.width <= 0)
            data.width = current.width > 0 ? current.width : 1920;

        if (data.height <= 0)
            data.height = current.height > 0 ? current.height : 1080;

        if (data.refreshRate < 0)
            data.refreshRate = 0;

        data.brightness = Mathf.Clamp01(data.brightness);
        data.screenShake = Mathf.Max(0f, data.screenShake);

        if (!System.Enum.IsDefined(typeof(VideoScreenMode), data.screenMode))
            data.screenMode = VideoScreenMode.Fullscreen;

        return data;
    }
}
