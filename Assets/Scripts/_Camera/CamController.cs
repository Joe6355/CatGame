using System;
using System.Collections;
using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class CamController : MonoBehaviour
{
    // Глобальные события (можно вызывать из любых скриптов)
    public static Action<float, float, float> CameraShake;         // strength, time, fadeTime
    public static Action<float> ChangeCameraSizeEvent;             // new orthographic size
    public static Action<Transform> ChangeFollowTargetEvent;       // new follow target

    [Header("ScreenX offsets (A/D)")]
    [SerializeField] private float leftOffset = 0.35f;   // 0..1
    [SerializeField] private float rightOffset = 0.65f;  // 0..1
    [SerializeField] private bool enableADTest = true;   // чтобы можно было выключить

    [Header("Optional")]
    [SerializeField] private bool useUnscaledTime = false; // если используешь паузу Time.timeScale=0

    [HideInInspector] public CinemachineFramingTransposer transposer;

    private CinemachineBasicMultiChannelPerlin perlin;
    private CinemachineVirtualCamera vcam;

    private Coroutine shakeCo;
    private Coroutine sizeCo;
    private float sizeFrom;

    private void OnEnable()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();

        // FramingTransposer нужен для m_ScreenX (смещение)
        transposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();

        // Perlin нужен для тряски (добавь на VirtualCamera компонент Noise!)
        perlin = vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

        CameraShake += Shake;
        ChangeCameraSizeEvent += ChangeCameraSize;
        ChangeFollowTargetEvent += ChangeFollowTarget;
    }

    private void OnDisable()
    {
        CameraShake -= Shake;
        ChangeCameraSizeEvent -= ChangeCameraSize;
        ChangeFollowTargetEvent -= ChangeFollowTarget;
    }

    private void Update()
    {
        if (!enableADTest) return;
        if (transposer == null) return;

        if (Input.GetKeyDown(KeyCode.A))
            transposer.m_ScreenX = leftOffset;

        if (Input.GetKeyDown(KeyCode.D))
            transposer.m_ScreenX = rightOffset;
    }

    // =========================
    // Public API (через события)
    // =========================

    private void Shake(float strength, float time, float fadeTime)
    {
        if (perlin == null)
        {
            // Если хочешь тряску, на VirtualCamera добавь Noise (CinemachineBasicMultiChannelPerlin)
            // или в Cinemachine 3: добавь Noise в соответствующей секции.
            return;
        }

        if (shakeCo != null) StopCoroutine(shakeCo);
        shakeCo = StartCoroutine(ShakeRoutine(strength, time, fadeTime));
    }

    private void ChangeCameraSize(float newSize)
    {
        if (vcam == null) return;

        // ВАЖНО: заставляет Cinemachine пересчитать позицию и "прилипнуть" к цели
        vcam.PreviousStateIsValid = false;

        if (sizeCo != null) StopCoroutine(sizeCo);

        sizeFrom = vcam.m_Lens.OrthographicSize;
        sizeCo = StartCoroutine(ChangeSizeRoutine(newSize, 1f));
    }

    private void ChangeFollowTarget(Transform followObject)
    {
        if (vcam == null) return;
        if (followObject == null) return;

        vcam.m_Follow = followObject;
    }

    // =========================
    // Coroutines
    // =========================

    private IEnumerator ShakeRoutine(float strength, float time, float fadeTime)
    {
        float origin = Mathf.Max(0f, strength);
        float cur = origin;

        perlin.m_AmplitudeGain = cur;

        // Hold time
        if (time > 0f)
            yield return useUnscaledTime ? new WaitForSecondsRealtime(time) : new WaitForSeconds(time);

        // Fade out
        float t = 0f;
        float dur = Mathf.Max(0.0001f, fadeTime);

        while (t < dur)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;

            float k = 1f - Mathf.Clamp01(t / dur);
            cur = origin * k;

            perlin.m_AmplitudeGain = cur;
            yield return null;
        }

        perlin.m_AmplitudeGain = 0f;
        shakeCo = null;
    }

    private IEnumerator ChangeSizeRoutine(float newSize, float duration)
    {
        float from = sizeFrom;
        float to = newSize;

        if (Mathf.Approximately(from, to))
        {
            sizeCo = null;
            yield break;
        }

        float t = 0f;
        float dur = Mathf.Max(0.0001f, duration);

        while (t < dur)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;

            float a = Mathf.Clamp01(t / dur);
            float eased = EaseInOut(a);

            vcam.m_Lens.OrthographicSize = Mathf.Lerp(from, to, eased);
            yield return null;
        }

        vcam.m_Lens.OrthographicSize = to;
        sizeCo = null;
    }

    // =========================
    // Helpers
    // =========================

    private float EaseInOut(float x)
    {
        // как в оригинале, но безопасно
        x = Mathf.Clamp01(x);
        return x < 0.5f ? x * x * 2f : (1f - (1f - x) * (1f - x) * 2f);
    }

    // =========================
    // Примеры вызова (если нужно)
    // =========================
    // CamController.CameraShake?.Invoke(1.2f, 0.1f, 0.25f);
    // CamController.ChangeCameraSizeEvent?.Invoke(6.5f);
    // CamController.ChangeFollowTargetEvent?.Invoke(bossTransform);
}
