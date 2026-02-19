using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private void Start() => Sync();
    private void OnEnable() => Sync();

    private void Sync()
    {
        var m = SoundMixerManager.Instance;
        if (m == null) return;

        // вот это важно: применить реальные сохранённые громкости
        m.LoadAndApply();

        masterSlider.SetValueWithoutNotify(m.Master01);
        musicSlider.SetValueWithoutNotify(m.Music01);
        sfxSlider.SetValueWithoutNotify(m.Sfx01);
    }
}
