using System.Collections;
using UnityEngine;

public class SoundFXManager : MonoBehaviour
{
    public static SoundFXManager instance;

    [SerializeField] private AudioSource soundFXObject;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }


    public void PlaySoundFXClip(AudioClip audioClip, Transform spawntransform, float volume)
    {
        PlayClip(audioClip, spawntransform, volume);
    }

    public void PlayRandomSoundFXClip(AudioClip[] audioClip, Transform spawntransform, float volume)
    {
        if (audioClip == null || audioClip.Length == 0)
            return;

        PlayClip(audioClip[Random.Range(0, audioClip.Length)], spawntransform, volume);
    }

    private void PlayClip(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        if (audioClip == null || soundFXObject == null)
            return;

        Vector3 position = spawnTransform != null ? spawnTransform.position : transform.position;

        // A child of the DontDestroyOnLoad manager survives scene changes.
        AudioSource audioSource = Instantiate(soundFXObject, position, Quaternion.identity, transform);
        audioSource.clip = audioClip;
        audioSource.volume = Mathf.Clamp01(volume);
        audioSource.Play();

        float pitch = Mathf.Max(Mathf.Abs(audioSource.pitch), 0.01f);
        StartCoroutine(DestroyAfterDelay(audioSource.gameObject, audioClip.length / pitch + 0.1f));
    }

    private static IEnumerator DestroyAfterDelay(GameObject target, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (target != null)
            Destroy(target);
    }
}
