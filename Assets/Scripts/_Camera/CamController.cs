using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class CamController : MonoBehaviour
{
    public static Action<float, float, float> CameraShake;
    public static Action<float> ChangeCameraSizeEvent;
    public static Action<Transform> ChangeFollowTargetEvent;
    public static Action<float> ChangeCameraYOffsetEvent;
    public static Action<float> ChangeSprintZoomBlendEvent;

    [Header("ScreenX offsets (A/D)")]
    [SerializeField] private float leftOffset = 0.35f;
    [SerializeField] private float rightOffset = 0.65f;
    [SerializeField] private bool enableADTest = false;

    [SerializeField, Tooltip("Плавность смены ScreenX при тесте A/D (сек).")]
    private float screenXLerpTime = 0.25f;

    [Header("Vertical Offset (TrackedObjectOffset)")]
    [SerializeField, Tooltip("Сколько секунд плавно менять вертикальный сдвиг камеры (TrackedObjectOffset.y).")]
    private float yOffsetLerpTime = 0.35f;

    [Header("Базовый размер камеры")]
    [SerializeField, Min(0.01f), Tooltip("Запасной базовый orthographic size. Нужен как страховка, если runtime-база почему-то не считалась из Lens.")]
    private float fallbackBaseOrthoSize = 5.625f;

    [SerializeField, Tooltip("Если ВКЛ — при старте брать базовый размер из текущего Lens.OrthographicSize виртуальной камеры.")]
    private bool readBaseSizeFromLensOnStart = true;

    [Header("Спринт: динамическое отдаление камеры")]
    [SerializeField, Tooltip("Если ВКЛ — камера будет плавно отдаляться по сигналу спринта и возвращаться обратно после него.")]
    private bool enableSprintZoom = true;

    [SerializeField, Min(0f), Tooltip("На сколько единиц orthographic size дополнительно отдалять камеру на полном спринте.")]
    private float sprintZoomOutSizeDelta = 0.9f;

    [SerializeField, Min(0.01f), Tooltip("Сколько секунд плавно переходить к новому уровню спринтового отдаления камеры.")]
    private float sprintZoomLerpTime = 0.18f;

    [Header("Optional")]
    [SerializeField] private bool useUnscaledTime = false;

    [HideInInspector] public CinemachineFramingTransposer transposer;

    private CinemachineBasicMultiChannelPerlin perlin;
    private CinemachineVirtualCamera vcam;
    private CinemachineConfiner2D confiner2D;
    private MethodInfo confinerInvalidateMethod;

    private Coroutine shakeCo;
    private Coroutine sizeCo;
    private Coroutine yOffsetCo;
    private Coroutine screenXCo;

    private float baseSizeCurrent = 5.625f;
    private float baseSizeTarget = 5.625f;
    private float sprintZoomBlendCurrent = 0f;
    private float sprintZoomBlendTarget = 0f;
    private bool runtimeBaseInitialized = false;

    private void Reset()
    {
        CacheComponents();
    }

    private void Awake()
    {
        CacheComponents();
        EnsureBaseSizeInitialized(false);
    }

    private void Start()
    {
        EnsureBaseSizeInitialized(true);
        ApplyCombinedLensSize(true);
    }

    private void OnValidate()
    {
        fallbackBaseOrthoSize = Mathf.Max(0.01f, fallbackBaseOrthoSize);
        sprintZoomOutSizeDelta = Mathf.Max(0f, sprintZoomOutSizeDelta);
        sprintZoomLerpTime = Mathf.Max(0.01f, sprintZoomLerpTime);
        screenXLerpTime = Mathf.Max(0.0001f, screenXLerpTime);
        yOffsetLerpTime = Mathf.Max(0.0001f, yOffsetLerpTime);

        if (!Application.isPlaying)
            return;

        CacheComponents();
        EnsureBaseSizeInitialized(false);
        ApplyCombinedLensSize(true);
    }

    private void OnEnable()
    {
        CacheComponents();
        EnsureBaseSizeInitialized(false);
        sprintZoomBlendCurrent = 0f;
        sprintZoomBlendTarget = 0f;

        CameraShake += Shake;
        ChangeCameraSizeEvent += ChangeCameraSize;
        ChangeFollowTargetEvent += ChangeFollowTarget;
        ChangeCameraYOffsetEvent += ChangeCameraYOffset;
        ChangeSprintZoomBlendEvent += ChangeSprintZoomBlend;
    }

    private void OnDisable()
    {
        CameraShake -= Shake;
        ChangeCameraSizeEvent -= ChangeCameraSize;
        ChangeFollowTargetEvent -= ChangeFollowTarget;
        ChangeCameraYOffsetEvent -= ChangeCameraYOffset;
        ChangeSprintZoomBlendEvent -= ChangeSprintZoomBlend;
    }

    private void Update()
    {
        if (!enableADTest || transposer == null)
            return;

        if (Input.GetKeyDown(KeyCode.A)) SetScreenX(leftOffset, screenXLerpTime);
        if (Input.GetKeyDown(KeyCode.D)) SetScreenX(rightOffset, screenXLerpTime);
    }

    private void LateUpdate()
    {
        UpdateSprintZoomRuntime();
    }

    private void Shake(float strength, float time, float fadeTime)
    {
        if (perlin == null) return;

        if (shakeCo != null) StopCoroutine(shakeCo);
        shakeCo = StartCoroutine(ShakeRoutine(strength, time, fadeTime));
    }

    private void ChangeCameraSize(float newSize)
    {
        EnsureBaseSizeInitialized(false);

        if (sizeCo != null)
            StopCoroutine(sizeCo);

        baseSizeTarget = Mathf.Max(0.01f, newSize);
        sizeCo = StartCoroutine(ChangeBaseSizeRoutine(baseSizeTarget, 1f));
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

    private void ChangeSprintZoomBlend(float blend)
    {
        sprintZoomBlendTarget = enableSprintZoom ? Mathf.Clamp01(blend) : 0f;
    }

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

    private IEnumerator ShakeRoutine(float strength, float time, float fadeTime)
    {
        float origin = Mathf.Max(0f, strength);
        float cur = origin;
        perlin.m_AmplitudeGain = cur;

        if (time > 0f)
            yield return useUnscaledTime ? new WaitForSecondsRealtime(time) : new WaitForSeconds(time);

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

    private IEnumerator ChangeBaseSizeRoutine(float newBaseSize, float duration)
    {
        float from = Mathf.Max(0.01f, baseSizeCurrent);
        float to = Mathf.Max(0.01f, newBaseSize);

        if (Mathf.Approximately(from, to))
        {
            baseSizeCurrent = to;
            ApplyCombinedLensSize(true);
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
            baseSizeCurrent = Mathf.Lerp(from, to, eased);
            ApplyCombinedLensSize();
            yield return null;
        }

        baseSizeCurrent = to;
        ApplyCombinedLensSize(true);
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

    private void UpdateSprintZoomRuntime()
    {
        if (vcam == null)
            return;

        EnsureBaseSizeInitialized(false);

        float desiredBlend = enableSprintZoom ? sprintZoomBlendTarget : 0f;
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        sprintZoomBlendCurrent = dt > 0f
            ? Mathf.MoveTowards(sprintZoomBlendCurrent, desiredBlend, dt / Mathf.Max(0.01f, sprintZoomLerpTime))
            : desiredBlend;

        ApplyCombinedLensSize();
    }

    private void ApplyCombinedLensSize(bool force = false)
    {
        if (vcam == null)
            return;

        float safeBase = Mathf.Max(0.01f, baseSizeCurrent);
        float sprintAdd = enableSprintZoom ? sprintZoomOutSizeDelta * sprintZoomBlendCurrent : 0f;
        float targetSize = safeBase + sprintAdd;

        if (!force && Mathf.Abs(vcam.m_Lens.OrthographicSize - targetSize) < 0.0001f)
            return;

        vcam.m_Lens.OrthographicSize = targetSize;
        InvalidateConfiner();
    }

    private void EnsureBaseSizeInitialized(bool allowLensResync)
    {
        if (vcam == null)
            return;

        float lensSize = Mathf.Max(0.01f, vcam.m_Lens.OrthographicSize);
        float fallback = Mathf.Max(0.01f, fallbackBaseOrthoSize);

        if (!runtimeBaseInitialized)
        {
            float resolved = readBaseSizeFromLensOnStart ? lensSize : fallback;
            if (resolved <= 0.01f)
                resolved = fallback;

            baseSizeCurrent = resolved;
            baseSizeTarget = resolved;
            runtimeBaseInitialized = true;
            return;
        }

        if (!allowLensResync)
            return;

        bool noSprint = Mathf.Abs(sprintZoomBlendCurrent) <= 0.0001f && Mathf.Abs(sprintZoomBlendTarget) <= 0.0001f;
        bool noSizeTween = sizeCo == null;

        if (noSprint && noSizeTween && lensSize > 0.01f)
        {
            baseSizeCurrent = lensSize;
            baseSizeTarget = lensSize;
        }
    }

    private void CacheComponents()
    {
        if (vcam == null)
            vcam = GetComponent<CinemachineVirtualCamera>();

        if (transposer == null && vcam != null)
            transposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();

        if (perlin == null && vcam != null)
            perlin = vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

        if (confiner2D == null)
            confiner2D = GetComponent<CinemachineConfiner2D>();

        if (confiner2D != null && confinerInvalidateMethod == null)
        {
            var t = confiner2D.GetType();
            confinerInvalidateMethod =
                t.GetMethod("InvalidateCache", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? t.GetMethod("InvalidatePathCache", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }

    private void InvalidateConfiner()
    {
        if (confiner2D == null || confinerInvalidateMethod == null)
            return;

        confinerInvalidateMethod.Invoke(confiner2D, null);
    }

    private float EaseInOut(float x)
    {
        x = Mathf.Clamp01(x);
        return x < 0.5f ? x * x * 2f : (1f - (1f - x) * (1f - x) * 2f);
    }
}
