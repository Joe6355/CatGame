using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }
    private AudioSource _src;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _src = GetComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = true;
    }

    private void Start()
    {
        // ГЛАВНОЕ: применяем микшер до старта музыки
        if (SoundMixerManager.Instance != null)
            SoundMixerManager.Instance.LoadAndApply();

        if (!_src.isPlaying)
            _src.Play();
    }
}
