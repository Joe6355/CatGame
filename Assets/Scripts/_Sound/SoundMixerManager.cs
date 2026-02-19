using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-1000)]
public class SoundMixerManager : MonoBehaviour
{
    public static SoundMixerManager Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer;

    // Exposed parameter names (как у тебя в микшере)
    private const string PARAM_MASTER = "masterVolume";
    private const string PARAM_SFX = "soundFXVolume";
    private const string PARAM_MUSIC = "musicVolume";

    // PlayerPrefs keys
    private const string KEY_MASTER = "vol_master";
    private const string KEY_SFX = "vol_sfx";
    private const string KEY_MUSIC = "vol_music";

    // значения 0..1 (удобно для UI)
    public float Master01 { get; private set; } = 0.5f;
    public float Sfx01 { get; private set; } = 0.5f;
    public float Music01 { get; private set; } = 0.5f;

    private void Awake()
    {
#if UNITY_EDITOR
        Debug.Log($"MixerManager Awake: mixer={(audioMixer != null)} " +
          $"master={Master01} music={Music01} sfx={Sfx01}");
#endif

        // singleton + защита от дублей
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAndApply();
    }

    public void LoadAndApply()
    {
        Master01 = PlayerPrefs.GetFloat(KEY_MASTER, 0.5f);
        Sfx01 = PlayerPrefs.GetFloat(KEY_SFX, 0.5f);
        Music01 = PlayerPrefs.GetFloat(KEY_MUSIC, 0.5f);

        ApplyToMixer(PARAM_MASTER, Master01);
        ApplyToMixer(PARAM_SFX, Sfx01);
        ApplyToMixer(PARAM_MUSIC, Music01);
    }

    public void SetMasterVolume(float level01)
    {
        Master01 = Clamp01Safe(level01);
        ApplyToMixer(PARAM_MASTER, Master01);
        Save(KEY_MASTER, Master01);
    }

    public void SetSoundFXVolume(float level01)
    {
        Sfx01 = Clamp01Safe(level01);
        ApplyToMixer(PARAM_SFX, Sfx01);
        Save(KEY_SFX, Sfx01);
    }

    public void SetMusicVolume(float level01)
    {
        Music01 = Clamp01Safe(level01);
        ApplyToMixer(PARAM_MUSIC, Music01);
        Save(KEY_MUSIC, Music01);
    }

    private void ApplyToMixer(string paramName, float level01)
    {
        // log10(0) = -inf, поэтому нельзя 0
        float db = Mathf.Log10(Clamp01Safe(level01)) * 20f; // 1 -> 0 dB, 0.0001 -> ~ -80 dB
        audioMixer.SetFloat(paramName, db);
    }

    private static float Clamp01Safe(float v) => Mathf.Clamp(v, 0.0001f, 1f);

    private static void Save(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }
    private void OnDisable()
    {
        // при выходе из Play Mode в Editor этот метод вызывается
        if (Application.isPlaying)
            PlayerPrefs.Save();
    }

}
