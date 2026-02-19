using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    // чтобы не плодить слушателей при повторных включени€х панели
    private bool _wired;

    private void Awake()
    {
        Wire();
    }

    private void Start() => Sync();
    private void OnEnable() => Sync();

    private void Wire()
    {
        if (_wired) return;
        _wired = true;

        // Ќа вс€кий: убираем старые слушатели (если они могли остатьс€ из инспектора/копий)
        masterSlider.onValueChanged.RemoveAllListeners();
        musicSlider.onValueChanged.RemoveAllListeners();
        sfxSlider.onValueChanged.RemoveAllListeners();

        masterSlider.onValueChanged.AddListener(v => SoundMixerManager.Instance?.SetMasterVolume(v));
        musicSlider.onValueChanged.AddListener(v => SoundMixerManager.Instance?.SetMusicVolume(v));
        sfxSlider.onValueChanged.AddListener(v => SoundMixerManager.Instance?.SetSoundFXVolume(v));
    }

    private void Sync()
    {
        var m = SoundMixerManager.Instance;
        if (m == null) return;

        // примен€ем сохранЄнные значени€ в микшер
        m.LoadAndApply();

        // выставл€ем UI без триггера событий (чтобы не дергать лишний раз)
        masterSlider.SetValueWithoutNotify(m.Master01);
        musicSlider.SetValueWithoutNotify(m.Music01);
        sfxSlider.SetValueWithoutNotify(m.Sfx01);
    }
}
