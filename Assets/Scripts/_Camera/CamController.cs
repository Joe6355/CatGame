using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class CamController : MonoBehaviour
{
    public static Action<float, float, float> CameraShake;     // strength, time, fadeTime
    public static Action<float> ChangeCameraSizeEvent;         // new ortho size
    public static Action<Transform> ChangeFollowTargetEvent;   // new follow target

    [Header("ScreenX offsets (A/D)")]
    [SerializeField] private float leftOffset = 0.35f;
    [SerializeField] private float rightOffset = 0.65f;

    [SerializeField, Tooltip("Если ВКЛ — тест: A/D двигают ScreenX.\nВ игре лучше дергать это из кода по направлению движения.")]
    private bool enableADTest = false;

    [SerializeField, Tooltip("Сколько секунд занимает плавный сдвиг ScreenX.\nРекоменд: 0.08–0.20 (часто 0.12).")]
    private float screenXBlendTime = 0.12f;

    [SerializeField, Tooltip("Куда возвращаться, когда не жмём A/D.\nОбычно 0.5 (центр).")]
    private float centerOffset = 0.5f;

    [Header("Optional")]
    [SerializeField] private bool useUnscaledTime = false;

    [HideInInspector] public CinemachineFramingTransposer transposer;

    private CinemachineBasicMultiChannelPerlin perlin;
    private CinemachineVirtualCamera vcam;

    private CinemachineConfiner2D confiner2D;
    private MethodInfo confinerInvalidateMethod;

    private Coroutine shakeCo;
    private Coroutine sizeCo;
    private float sizeFrom;

    private Coroutine screenXCo;
    private float screenXTarget;

    private void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
        transposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
        perlin = vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

        confiner2D = GetComponent<CinemachineConfiner2D>();
        if (confiner2D != null)
        {
            var t = confiner2D.GetType();
            confinerInvalidateMethod =
                t.GetMethod("InvalidateCache", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? t.GetMethod("InvalidatePathCache", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        if (transposer != null)
            screenXTarget = transposer.m_ScreenX;
        else
            screenXTarget = centerOffset;
    }

    private void OnEnable()
    {
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

        // Вариант 1 (рекоменд): плавно реагируем на удержание A/D и возвращаемся в центр
        float target = centerOffset;

        if (Input.GetKey(KeyCode.A)) target = leftOffset;
        else if (Input.GetKey(KeyCode.D)) target = rightOffset;

        SetScreenX(target);

        // Вариант 2 (если хочешь только по нажатию — раскомментируй и убери вариант 1)
        /*
        if (Input.GetKeyDown(KeyCode.A)) SetScreenX(leftOffset);
        if (Input.GetKeyDown(KeyCode.D)) SetScreenX(rightOffset);
        */
    }

    // Можно дергать из PlayerController: -1 = влево, +1 = вправо, 0 = центр
    public void SetLookSide(int dir)
    {
        if (transposer == null) return;

        float target = centerOffset;
        if (dir < 0) target = leftOffset;
        else if (dir > 0) target = rightOffset;

        SetScreenX(target);
    }

    private void SetScreenX(float target)
    {
        target = Mathf.Clamp01(target);
        if (Mathf.Approximately(screenXTarget, target)) return;

        screenXTarget = target;

        if (screenXCo != null) StopCoroutine(screenXCo);
        screenXCo = StartCoroutine(ScreenXBlendRoutine(screenXTarget, Mathf.Max(0.0001f, screenXBlendTime)));
    }

    private IEnumerator ScreenXBlendRoutine(float target, float duration)
    {
        if (transposer == null)
        {
            screenXCo = null;
            yield break;
        }

        float start = transposer.m_ScreenX;
        if (Mathf.Approximately(start, target))
        {
            screenXCo = null;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;

            float a = Mathf.Clamp01(t / duration);
            float eased = EaseInOut(a);

            transposer.m_ScreenX = Mathf.Lerp(start, target, eased);
            yield return null;
        }

        transposer.m_ScreenX = target;
        screenXCo = null;
    }

    private void InvalidateConfiner()
    {
        if (confiner2D == null) return;
        if (confinerInvalidateMethod == null) return;
        confinerInvalidateMethod.Invoke(confiner2D, null);
    }

    private void Shake(float strength, float time, float fadeTime)
    {
        if (perlin == null) return;

        if (shakeCo != null) StopCoroutine(shakeCo);
        shakeCo = StartCoroutine(ShakeRoutine(strength, time, fadeTime));
    }

    private void ChangeCameraSize(float newSize)
    {
        if (vcam == null) return;

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
    }

    private IEnumerator ShakeRoutine(float strength, float time, float fadeTime)
    {
        float origin = Mathf.Max(0f, strength);
        perlin.m_AmplitudeGain = origin;

        if (time > 0f)
            yield return useUnscaledTime ? new WaitForSecondsRealtime(time) : new WaitForSeconds(time);

        float t = 0f;
        float dur = Mathf.Max(0.0001f, fadeTime);

        while (t < dur)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;

            float k = 1f - Mathf.Clamp01(t / dur);
            perlin.m_AmplitudeGain = origin * k;

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

            // важно при зуме, если включен Confiner2D
            InvalidateConfiner();

            yield return null;
        }

        vcam.m_Lens.OrthographicSize = to;
        InvalidateConfiner();

        sizeCo = null;
    }

    private float EaseInOut(float x)
    {
        x = Mathf.Clamp01(x);
        return x < 0.5f ? x * x * 2f : (1f - (1f - x) * (1f - x) * 2f);
    }
}
