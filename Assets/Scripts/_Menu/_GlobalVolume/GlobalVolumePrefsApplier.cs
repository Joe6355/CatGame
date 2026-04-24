using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Volume))]
public class GlobalVolumePrefsApplier : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private Volume targetVolume;

    [SerializeField, Tooltip("Если ВКЛ — скрипт управляет эффектом через Weight. Это безопаснее, чем выключать сам Volume.")]
    private bool controlByWeight = true;

    [SerializeField, Range(0f, 1f), Tooltip("Какой Weight ставить, когда эффект включён.")]
    private float enabledWeight = 1f;

    [Header("Сохранение")]
    [SerializeField] private string prefsKey = "Settings.CRTPostFx";
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

        if (applyOnAwake)
            ApplySavedValue();
    }

    private void OnEnable()
    {
        ResolveVolume();

        if (applyOnEnable)
            ApplySavedValue();
    }

    private void OnValidate()
    {
        enabledWeight = Mathf.Clamp01(enabledWeight);

        if (targetVolume == null)
            targetVolume = GetComponent<Volume>();
    }

    public void ApplySavedValue()
    {
        bool enabled = ReadSavedValue();
        ApplyValue(enabled, false);
    }

    public void SetEnabledAndSave(bool enabled)
    {
        SaveValue(enabled);
        ApplyValue(enabled, true);
    }

    public void SetEnabledWithoutSave(bool enabled)
    {
        ApplyValue(enabled, true);
    }

    public bool ReadSavedValue()
    {
        if (string.IsNullOrEmpty(prefsKey))
            return defaultEnabled;

        return PlayerPrefs.GetInt(prefsKey, defaultEnabled ? 1 : 0) != 0;
    }

    public void SaveValue(bool enabled)
    {
        if (string.IsNullOrEmpty(prefsKey))
            return;

        PlayerPrefs.SetInt(prefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
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

    private void ResolveVolume()
    {
        if (targetVolume == null)
            targetVolume = GetComponent<Volume>();
    }
}