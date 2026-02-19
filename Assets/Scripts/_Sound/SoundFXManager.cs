using System.Collections;
using System.Collections.Generic;
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
        //spawn gammeObject
        AudioSource audioSource = Instantiate(soundFXObject, spawntransform.position, Quaternion.identity);

        audioSource.clip = audioClip;

        audioSource.volume  = volume;

        audioSource.Play();

        float clipLength = audioSource.clip.length;

        Destroy(audioSource.gameObject, clipLength);
    }

    public void PlayRandomSoundFXClip(AudioClip[] audioClip, Transform spawntransform, float volume)
    {
        //assign a random index
        int rand = Random.Range(0, audioClip.Length);
        //spawn gammeObject
        AudioSource audioSource = Instantiate(soundFXObject, spawntransform.position, Quaternion.identity);

        audioSource.clip = audioClip[rand];

        audioSource.volume = volume;

        audioSource.Play();

        float clipLength = audioSource.clip.length;

        Destroy(audioSource.gameObject, clipLength);
    }
}
