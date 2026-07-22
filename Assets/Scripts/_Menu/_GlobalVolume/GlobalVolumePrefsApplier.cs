using UnityEngine;
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Volume))]
public class GlobalVolumePrefsApplier : MonoBehaviour
{
    public static event Action<string, bool> SavedValueChanged;

    [Header("Volume")]
    [SerializeField] private Volume targetVolume;

    [SerializeField, Tooltip("Если ВКЛ — скрипт управляет эффектом через Weight. Это безопаснее, чем выключать сам Volume.")]
    private bool controlByWeight = true;

    [SerializeField, Range(0f, 1f), Tooltip("Какой Weight ставить, когда эффект включён.")]
    private float enabledWeight = 1f;

    [Header("Фиксированное состояние renderer feature для сцены")]
    [SerializeField] private ScriptableRendererData rendererData;
    [SerializeField] private bool enforceRendererFeatureState;
    [SerializeField] private string rendererFeatureName = "FullScreenPassRendererFeature";
    [SerializeField] private bool rendererFeatureActive;

    private ScriptableRendererFeature rendererFeature;

    [Header("Сохранение")]
    [SerializeField] private string prefsKey = "Settings.MenuCRTPostFx";
    [SerializeField] private string legacyPrefsKey = "Settings.CRTPostFx";
    [SerializeField] private bool defaultEnabled = true;
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    public bool IsEnabledNow
    {
        get
        {
            if (targetVolume == null)
                return defaultEnabled;

            if (controlByWeight)
                return targetVolume.weight > 0.001f;

            return targetVolume.enabled;
        }
    }

    private void Awake()
    {
        ResolveVolume();
        ApplyRendererFeatureState();

        if (applyOnAwake)
            ApplySavedValue();
    }

    private void OnEnable()
    {
        SavedValueChanged -= OnSavedValueChanged;
        SavedValueChanged += OnSavedValueChanged;

        ResolveVolume();
        ApplyRendererFeatureState();

        if (applyOnEnable)
            ApplySavedValue();
    }

    private void OnDisable()
    {
        SavedValueChanged -= OnSavedValueChanged;
    }

    private void OnValidate()
    {
        enabledWeight = Mathf.Clamp01(enabledWeight);

        if (targetVolume == null)
            targetVolume = GetComponent<Volume>();

        rendererFeature = null;
        ApplyRendererFeatureState();

    }

    public void ApplySavedValue()
    {
        bool enabled = ReadSavedValue();
        ApplyValue(enabled, false);
    }

    public void SetEnabledAndSave(bool enabled)
    {
        SetSavedValueAndNotify(prefsKey, enabled);
    }

    public void SetEnabledWithoutSave(bool enabled)
    {
        ApplyValue(enabled, true);
    }

    public bool ReadSavedValue()
    {
        if (string.IsNullOrEmpty(prefsKey))
            return defaultEnabled;

        if (!PlayerPrefs.HasKey(prefsKey) &&
            !string.IsNullOrEmpty(legacyPrefsKey) &&
            PlayerPrefs.HasKey(legacyPrefsKey))
        {
            return PlayerPrefs.GetInt(legacyPrefsKey, defaultEnabled ? 1 : 0) != 0;
        }

        return PlayerPrefs.GetInt(prefsKey, defaultEnabled ? 1 : 0) != 0;
    }

    public void SaveValue(bool enabled)
    {
        if (string.IsNullOrEmpty(prefsKey))
            return;

        PlayerPrefs.SetInt(prefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetSavedValueAndNotify(string key, bool enabled)
    {
        SetValueAndNotify(key, enabled, true);
    }

    public static void SetValueAndNotify(string key, bool enabled, bool save)
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (save)
        {
            PlayerPrefs.SetInt(key, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        SavedValueChanged?.Invoke(key, enabled);
    }

    private void ApplyValue(bool enabled, bool fromRuntime)
    {
        ResolveVolume();

        if (targetVolume == null)
            return;

        if (controlByWeight)
        {
            targetVolume.enabled = true;
            targetVolume.weight = enabled ? enabledWeight : 0f;
        }
        else
        {
            targetVolume.enabled = enabled;
        }

        if (debugLog && fromRuntime)
            Debug.Log("[GlobalVolumePrefsApplier] CRT/PostFX enabled = " + enabled);
    }

    private void OnSavedValueChanged(string changedKey, bool enabled)
    {
        if (!string.Equals(prefsKey, changedKey, StringComparison.Ordinal))
            return;

        ApplyValue(enabled, true);
    }

    private void ResolveVolume()
    {
        if (targetVolume == null)
            targetVolume = GetComponent<Volume>();
    }

    private void ApplyRendererFeatureState()
    {
        if (!enforceRendererFeatureState || rendererData == null || string.IsNullOrEmpty(rendererFeatureName))
            return;

        if (rendererFeature == null)
        {
            for (int i = 0; i < rendererData.rendererFeatures.Count; i++)
            {
                ScriptableRendererFeature candidate = rendererData.rendererFeatures[i];

                if (candidate != null && string.Equals(candidate.name, rendererFeatureName, StringComparison.Ordinal))
                {
                    rendererFeature = candidate;
                    break;
                }
            }
        }

        if (rendererFeature != null)
            rendererFeature.SetActive(rendererFeatureActive);
        else if (debugLog)
            Debug.LogWarning("[GlobalVolumePrefsApplier] Renderer feature not found: " + rendererFeatureName, this);
    }

}
