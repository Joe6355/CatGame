using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class CamController : MonoBehaviour
{
    // Глобальные события (можно вызывать из любых скриптов)
    public static Action<float, float, float> CameraShake;         // strength, time, fadeTime
    public static Action<float> ChangeCameraSizeEvent;             // new orthographic size
    public static Action<Transform> ChangeFollowTargetEvent;       // new follow target
    public static Action<float> ChangeCameraYOffsetEvent;          // new Y offset (TrackedObjectOffset.y)

    [Header("ScreenX offsets (A/D)")]
    [SerializeField] private float leftOffset = 0.35f;   // 0..1
    [SerializeField] private float rightOffset = 0.65f;  // 0..1
    [SerializeField] private bool enableADTest = false;  // включай только для теста

    [SerializeField, Tooltip("Плавность смены ScreenX при тесте A/D (сек).")]
    private float screenXLerpTime = 0.25f;

    [Header("Vertical Offset (TrackedObjectOffset)")]
    [SerializeField, Tooltip("Сколько секунд плавно менять вертикальный сдвиг камеры (TrackedObjectOffset.y).")]
    private float yOffsetLerpTime = 0.35f;

    [Header("Optional")]
    [SerializeField] private bool useUnscaledTime = false; // если используешь паузу Time.timeScale=0

    [HideInInspector] public CinemachineFramingTransposer transposer;

    private CinemachineBasicMultiChannelPerlin perlin;
    private CinemachineVirtualCamera vcam;

    private CinemachineConfiner2D confiner2D;
    private MethodInfo confinerInvalidateMethod;

    private Coroutine shakeCo;
    private Coroutine sizeCo;
    private Coroutine yOffsetCo;
    private Coroutine screenXCo;

    private float sizeFrom;

    private void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();

        // FramingTransposer нужен для ScreenX и TrackedObjectOffset
        transposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();

        // Perlin нужен для тряски (добавь на VirtualCamera компонент Noise!)
        perlin = vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

        // Confiner2D (если есть) — чтобы при зуме/смещении не было глюков границ
        confiner2D = GetComponent<CinemachineConfiner2D>();
        if (confiner2D != null)
        {
            var t = confiner2D.GetType();
            confinerInvalidateMethod =
                t.GetMethod("InvalidateCache", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? t.GetMethod("InvalidatePathCache", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }

    private void OnEnable()
    {
        CameraShake += Shake;
        ChangeCameraSizeEvent += ChangeCameraSize;
        ChangeFollowTargetEvent += ChangeFollowTarget;
        ChangeCameraYOffsetEvent += ChangeCameraYOffset;
    }

    private void OnDisable()
    {
        CameraShake -= Shake;
        ChangeCameraSizeEvent -= ChangeCameraSize;
        ChangeFollowTargetEvent -= ChangeFollowTarget;
        ChangeCameraYOffsetEvent -= ChangeCameraYOffset;
    }

    private void Update()
    {
        if (!enableADTest) return;
        if (transposer == null) return;

        if (Input.GetKeyDown(KeyCode.A)) SetScreenX(leftOffset, screenXLerpTime);
        if (Input.GetKeyDown(KeyCode.D)) SetScreenX(rightOffset, screenXLerpTime);
    }

    // =========================
    // Public API (через события)
    // =========================

    private void Shake(float strength, float time, float fadeTime)
    {
        if (perlin == null) return;

        if (shakeCo != null) StopCoroutine(shakeCo);
        shakeCo = StartCoroutine(ShakeRoutine(strength, time, fadeTime));
    }

    private void ChangeCameraSize(float newSize)
    {
        if (vcam == null) return;

        // Заставляет Cinemachine пересчитать позицию и "прилипнуть" к цели
        vcam.PreviousStateIsValid = false;
        InvalidateConfiner();

        if (sizeCo != null) StopCoroutine(sizeCo);

        sizeFrom = vcam.m_Lens.OrthographicSize;
        sizeCo = StartCoroutine(ChangeSizeRoutine(newSize, 1f));
    }

    private void ChangeFollowTarget(Transform followObject)
    {
        if (vcam == null || followObject == null) return;

        vcam.m_Follow = followObject;
        vcam.PreviousStateIsValid = false;
        InvalidateConfiner();
    }

    private void ChangeCameraYOffset(float newYOffsetY)
    {
        if (transposer == null) return;

        if (vcam != null) vcam.PreviousStateIsValid = false;
        InvalidateConfiner();

        if (yOffsetCo != null) StopCoroutine(yOffsetCo);
        yOffsetCo = StartCoroutine(ChangeYOffsetRoutine(newYOffsetY, yOffsetLerpTime));
    }

    // =========================
    // Helpers for ScreenX test
    // =========================

    private void SetScreenX(float x, float duration)
    {
        if (transposer == null) return;

        if (screenXCo != null) StopCoroutine(screenXCo);
        screenXCo = StartCoroutine(ChangeScreenXRoutine(x, duration));
    }

    private IEnumerator ChangeScreenXRoutine(float targetX, float duration)
    {
        float from = transposer.m_ScreenX;
        float to = Mathf.Clamp01(targetX);

        if (Mathf.Approximately(from, to))
        {
            screenXCo = null;
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

            transposer.m_ScreenX = Mathf.Lerp(from, to, eased);
            yield return null;
        }

        transposer.m_ScreenX = to;
        screenXCo = null;
    }

    private void InvalidateConfiner()
    {
        if (confiner2D == null) return;
        if (confinerInvalidateMethod == null) return;
        confinerInvalidateMethod.Invoke(confiner2D, null);
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
            InvalidateConfiner();

            yield return null;
        }

        vcam.m_Lens.OrthographicSize = to;
        InvalidateConfiner();

        sizeCo = null;
    }

    private IEnumerator ChangeYOffsetRoutine(float newY, float duration)
    {
        Vector3 from = transposer.m_TrackedObjectOffset;
        Vector3 to = new Vector3(from.x, newY, from.z);

        if (Mathf.Approximately(from.y, to.y))
        {
            yOffsetCo = null;
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

            transposer.m_TrackedObjectOffset = Vector3.Lerp(from, to, eased);
            InvalidateConfiner();

            yield return null;
        }

        transposer.m_TrackedObjectOffset = to;
        InvalidateConfiner();

        yOffsetCo = null;
    }

    // =========================
    // Helpers
    // =========================

    private float EaseInOut(float x)
    {
        x = Mathf.Clamp01(x);
        return x < 0.5f ? x * x * 2f : (1f - (1f - x) * (1f - x) * 2f);
    }

    // =========================
    // Примеры вызова (если нужно)
    // =========================
    // CamController.CameraShake?.Invoke(1.2f, 0.1f, 0.25f);
    // CamController.ChangeCameraSizeEvent?.Invoke(6.5f);
    // CamController.ChangeCameraYOffsetEvent?.Invoke(1.5f);
    // CamController.ChangeFollowTargetEvent?.Invoke(bossTransform);
}
