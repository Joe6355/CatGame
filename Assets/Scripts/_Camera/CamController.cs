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

    [Header("ScreenX offsets")]
    [SerializeField, Range(0f, 1f), Tooltip(
        "Положение игрока на экране, когда камера смотрит/смещается ВЛЕВО.\n" +
        "Для твоей камеры обычно норм: 0.7.\n" +
        "Чем больше значение, тем правее игрок на экране, и тем больше видно слева.")]
    private float leftOffset = 0.7f;

    [SerializeField, Range(0f, 1f), Tooltip(
        "Положение игрока на экране, когда камера смотрит/смещается ВПРАВО.\n" +
        "Для твоей камеры обычно норм: 0.3.\n" +
        "Чем меньше значение, тем левее игрок на экране, и тем больше видно справа.")]
    private float rightOffset = 0.3f;

    [SerializeField, Tooltip(
        "Включает горизонтальное смещение камеры влево/вправо.\n" +
        "Если выключить, камера будет держать нейтральный ScreenX.")]
    private bool enableHorizontalLookOffset = true;

    [SerializeField, Tooltip(
        "Если включено, камера по X будет смотреть туда, куда повернут игрок.\n" +
        "Работает через PlayerMovementModule.IsFacingRight.\n" +
        "Это лучше для геймпада и ребиндов, потому что камера не зависит от сырого Input.GetAxisRaw.")]
    private bool usePlayerFacingForHorizontalLook = true;

    [SerializeField, Tooltip(
        "Если включено, камера дополнительно читает клавиши A/D напрямую.\n" +
        "Используется как запасной вариант, если Use Player Facing For Horizontal Look выключен или модуль движения не найден.")]
    private bool useKeyboardADForOffset = true;

    [SerializeField, Tooltip(
        "Если включено, камера читает горизонтальную ось из старого Unity Input Manager.\n" +
        "Обычно это ось Horizontal.")]
    private bool useAxisForOffset = true;

    [SerializeField, Tooltip(
        "Название горизонтальной оси в Project Settings -> Input Manager.\n" +
        "Обычно: Horizontal.")]
    private string horizontalAxisName = "Horizontal";

    [SerializeField, Range(0f, 1f), Tooltip(
        "Мёртвая зона горизонтального ввода камеры.\n" +
        "Пока ось меньше этого значения, камера не меняет горизонтальное смещение.\n" +
        "Работает только для fallback-режима через клавиши/ось.")]
    private float horizontalAxisDeadZone = 0.25f;

    [SerializeField, Tooltip(
        "Если включено, когда нет горизонтального ввода, камера возвращается к нейтральному ScreenX.\n" +
        "При Use Player Facing For Horizontal Look это почти не используется, потому что камера смотрит по направлению персонажа.")]
    private bool returnToNeutralWhenNoInput = true;

    [SerializeField, Tooltip(
        "Если включено, при старте игры нейтральный ScreenX берётся из текущих настроек Cinemachine Framing Transposer.\n" +
        "Для твоей камеры это важно, потому что базовый ScreenX примерно 0.15, а не 0.5.")]
    private bool readNeutralScreenXFromCinemachineOnStart = true;

    [SerializeField, Range(0f, 1f), Tooltip(
        "Нейтральный ScreenX камеры.\n" +
        "Используется только если Read Neutral Screen X From Cinemachine On Start выключен.\n" +
        "Для твоей композиции обычно 0.15.")]
    private float neutralScreenX = 0.15f;

    [SerializeField, Min(0.0001f), Tooltip(
        "Плавность горизонтального смещения камеры.\n" +
        "Больше значение = мягче и медленнее.\n" +
        "Меньше значение = резче и быстрее.")]
    private float screenXSmoothTime = 0.45f;

    [Header("Vertical Offset From Zones")]
    [SerializeField, Min(0.0001f), Tooltip(
        "Плавность изменения Tracked Object Offset по Y от зон камеры.\n" +
        "Это не ручной взгляд вверх/вниз, а базовый вертикальный сдвиг от VisionManipulater/триггеров.")]
    private float zoneYOffsetSmoothTime = 0.35f;

    [Header("Manual Vertical Camera Look")]
    [SerializeField, Tooltip(
        "Включает ручной обзор камеры вверх/вниз.\n" +
        "Управление: стрелки вверх/вниз и/или правый стик геймпада.")]
    private bool enableManualVerticalLook = true;

    [SerializeField, Tooltip(
        "Если включено, смотреть вверх/вниз можно только когда игрок стоит на земле и не занят спец-состояниями.\n" +
        "Блокирует обзор в прыжке, на стене, на ledge и на лестнице/заборе.")]
    private bool manualVerticalLookOnlyWhenStanding = true;

    [SerializeField, Tooltip(
        "Если включено, стрелка вверх смотрит вверх, стрелка вниз смотрит вниз.")]
    private bool useKeyboardArrowsForVerticalLook = true;

    [SerializeField, Tooltip(
        "Если включено, W/S тоже будут использоваться для ручного обзора вверх/вниз.\n" +
        "Обычно лучше держать выключенным, потому что W/S могут использоваться для лестниц, ledge и других действий.")]
    private bool useKeyboardWSForVerticalLook = false;

    [SerializeField, Tooltip(
        "Если включено, вертикаль правого стика геймпада будет двигать камеру вверх/вниз.\n" +
        "Ось должна существовать в Project Settings -> Input Manager.")]
    private bool useRightStickForVerticalLook = true;

    [SerializeField, Tooltip(
        "Основное имя вертикальной оси правого стика в старом Input Manager.\n" +
        "Рекомендуемое имя: RightStickVertical.")]
    private string rightStickVerticalAxisName = "RightStickVertical";

    [SerializeField, Tooltip(
        "Запасные имена вертикальной оси правого стика.\n" +
        "Скрипт проверит их, если основная ось не работает или возвращает 0.\n" +
        "Можно оставить как есть.")]
    private string[] rightStickVerticalFallbackAxisNames =
    {
        "RightStickY",
        "JoystickRightY",
        "LookY",
        "CameraY"
    };

    [SerializeField, Range(0f, 1f), Tooltip(
        "Мёртвая зона вертикального обзора.\n" +
        "Чем больше значение, тем сильнее нужно отклонить стик, чтобы камера начала двигаться.\n" +
        "Для стика обычно норм: 0.20–0.30.")]
    private float verticalLookDeadZone = 0.25f;

    [SerializeField, Range(0f, 0.45f), Tooltip(
        "Сила вертикального смещения через ScreenY.\n" +
        "Чем больше значение, тем дальше камера смотрит вверх/вниз.\n" +
        "Рекомендовано: 0.18–0.25.")]
    private float manualVerticalScreenYShift = 0.22f;

    [SerializeField, Min(0.0001f), Tooltip(
        "Плавность ручного взгляда вверх/вниз.\n" +
        "Больше значение = мягче, но медленнее.\n" +
        "Меньше значение = быстрее, но может выглядеть резче.")]
    private float manualVerticalLookSmoothTime = 0.18f;

    [SerializeField, Min(0f), Tooltip(
        "Задержка перед началом ручного взгляда вверх/вниз.\n" +
        "0 = камера реагирует сразу.\n" +
        "Если поставить 0.05–0.15, случайные короткие касания стика будут меньше сдвигать камеру.")]
    private float manualVerticalLookStartDelay = 0f;

    [SerializeField, Tooltip(
        "Если включено, после отпускания стика/стрелки камера возвращается к исходному ScreenY.")]
    private bool returnVerticalLookToCenterWhenNoInput = true;

    [SerializeField, Tooltip(
        "Инвертирует вертикаль правого стика.\n" +
        "Включи, если стик вверх двигает камеру вниз, а стик вниз двигает вверх.")]
    private bool invertRightStickVertical = false;

    [Header("Manual Look Dead Zone Override")]
    [SerializeField, Tooltip(
        "Если включено, во время ручного взгляда вверх/вниз вертикальная Dead Zone Height у Cinemachine временно уменьшается.\n" +
        "Это нужно, потому что большая Dead Zone Height может мешать камере двигаться вверх/вниз.")]
    private bool reduceVerticalDeadZoneWhileManualLook = true;

    [SerializeField, Range(0f, 1f), Tooltip(
        "Временное значение Dead Zone Height во время ручного взгляда вверх/вниз.\n" +
        "Меньше значение = камера легче двигается по вертикали.\n" +
        "Рекомендовано: 0.01–0.05.")]
    private float manualLookDeadZoneHeight = 0.02f;

    [SerializeField, Min(0.0001f), Tooltip(
        "Плавность изменения Dead Zone Height при входе/выходе из ручного взгляда.\n" +
        "Меньше значение = быстрее уменьшается/возвращается dead zone.")]
    private float deadZoneHeightSmoothTime = 0.08f;

    [Header("Manual Look Standing Check")]
    [SerializeField, Tooltip(
        "Ссылка на PlayerController игрока.\n" +
        "Можно не заполнять вручную, если Cinemachine Follow указывает на игрока или его дочерний объект.")]
    private PlayerController playerController;

    [SerializeField, Tooltip(
        "Ссылка на PlayerGroundModule.\n" +
        "Используется для проверки, что игрок стоит на земле.")]
    private PlayerGroundModule playerGroundModule;

    [SerializeField, Tooltip(
        "Ссылка на PlayerLedgeModule.\n" +
        "Используется, чтобы запретить ручной обзор во время зацепа/подтягивания.")]
    private PlayerLedgeModule playerLedgeModule;

    [SerializeField, Tooltip(
        "Ссылка на PlayerFenceClimbModule.\n" +
        "Используется, чтобы запретить ручной обзор во время лазания по лестнице/забору.")]
    private PlayerFenceClimbModule playerFenceClimbModule;

    [SerializeField, Tooltip(
        "Ссылка на PlayerBounceModule.\n" +
        "Используется, чтобы запретить ручной обзор во время wall slide.")]
    private PlayerBounceModule playerBounceModule;

    [SerializeField, Tooltip(
        "Ссылка на PlayerMovementModule.\n" +
        "Используется для чтения направления персонажа через IsFacingRight.")]
    private PlayerMovementModule playerMovementModule;

    [SerializeField, Tooltip(
        "Ссылка на Rigidbody2D игрока.\n" +
        "Используется только если включена проверка скорости через Use Rigidbody Velocity For Standing Check.")]
    private Rigidbody2D playerRigidbody;

    [SerializeField, Range(0f, 1f), Tooltip(
        "Мёртвая зона горизонтального ввода для проверки, стоит ли игрок спокойно.\n" +
        "Если горизонтальная ось больше этого значения, ручной взгляд вверх/вниз блокируется.")]
    private float standingInputDeadZone = 0.5f;

    [SerializeField, Tooltip(
        "Если включено, ручной взгляд вверх/вниз блокируется при горизонтальном вводе по оси Horizontal.\n" +
        "Если геймпад даёт небольшой дрейф стика, можно выключить или поднять Standing Input Dead Zone.")]
    private bool useHorizontalAxisForStandingCheck = true;

    [SerializeField, Tooltip(
        "Если включено, скрипт дополнительно проверяет скорость Rigidbody2D.\n" +
        "Если игрок даже чуть скользит, ручной взгляд может блокироваться.\n" +
        "Обычно лучше держать выключенным.")]
    private bool useRigidbodyVelocityForStandingCheck = false;

    [SerializeField, Min(0f), Tooltip(
        "Максимальная скорость по X, при которой игрок считается стоящим.\n" +
        "Работает только если Use Rigidbody Velocity For Standing Check включён.")]
    private float standingMaxHorizontalSpeed = 0.35f;

    [SerializeField, Min(0f), Tooltip(
        "Максимальная скорость по Y, при которой игрок считается стоящим.\n" +
        "Работает только если Use Rigidbody Velocity For Standing Check включён.")]
    private float standingMaxVerticalSpeed = 0.12f;

    [SerializeField, Tooltip(
        "Если включено, ручной взгляд вверх/вниз блокируется во время wall slide.")]
    private bool blockManualLookWhileWallSliding = true;

    [SerializeField, Tooltip(
        "Если включено, когда игрок перестал считаться стоящим, камера возвращается к обычному вертикальному положению.")]
    private bool returnManualLookToCenterWhenNotStanding = true;

    [Header("Базовый размер камеры")]
    [SerializeField, Min(0.01f), Tooltip(
        "Запасной базовый Orthographic Size.\n" +
        "Используется как страховка, если размер камеры не удалось нормально прочитать из Cinemachine Lens.")]
    private float fallbackBaseOrthoSize = 5.625f;

    [SerializeField, Tooltip(
        "Если включено, при старте базовый размер камеры берётся из текущего Lens.OrthographicSize виртуальной камеры.")]
    private bool readBaseSizeFromLensOnStart = true;

    [Header("Спринт: динамическое отдаление камеры")]
    [SerializeField, Tooltip(
        "Если включено, камера может плавно отдаляться при спринте.\n" +
        "Сила спринта приходит через ChangeSprintZoomBlendEvent.")]
    private bool enableSprintZoom = true;

    [SerializeField, Min(0f), Tooltip(
        "На сколько увеличить Orthographic Size при полном спринте.\n" +
        "0 = спринт не отдаляет камеру.")]
    private float sprintZoomOutSizeDelta = 0.9f;

    [SerializeField, Min(0.01f), Tooltip(
        "Плавность изменения зума при спринте.\n" +
        "Меньше значение = быстрее камера отдаляется/возвращается.")]
    private float sprintZoomLerpTime = 0.18f;

    [Header("Debug")]
    [SerializeField, Tooltip(
        "Если включено, в поле Manual Look State будет выводиться причина, почему ручной обзор работает или заблокирован.")]
    private bool debugManualLook = true;

    [SerializeField, Tooltip(
        "Текущее состояние ручного вертикального обзора.\n" +
        "ACTIVE = работает.\n" +
        "RETURNING = возвращается.\n" +
        "BLOCKED = заблокировано условием.")]
    private string manualLookState = "";

    [Header("Optional")]
    [SerializeField, Tooltip(
        "Если включено, камера использует unscaled time.\n" +
        "Полезно, если камера должна двигаться даже при Time.timeScale = 0.\n" +
        "Обычно для игрового процесса лучше выключено.")]
    private bool useUnscaledTime = false;

    [HideInInspector]
    public CinemachineFramingTransposer transposer;

    private CinemachineBasicMultiChannelPerlin perlin;
    private CinemachineVirtualCamera vcam;
    private CinemachineConfiner2D confiner2D;
    private MethodInfo confinerInvalidateMethod;

    private Coroutine shakeCo;
    private Coroutine sizeCo;

    private float baseSizeCurrent = 5.625f;
    private float baseSizeTarget = 5.625f;
    private float sprintZoomBlendCurrent = 0f;
    private float sprintZoomBlendTarget = 0f;
    private bool runtimeBaseInitialized = false;

    private float screenXTarget = 0.15f;
    private float screenXVelocity = 0f;

    private float baseScreenY = 0.5f;
    private float screenYTarget = 0.5f;
    private float screenYVelocity = 0f;

    private float baseDeadZoneHeight = 0.44f;
    private float deadZoneHeightTarget = 0.44f;
    private float deadZoneHeightVelocity = 0f;

    private Vector3 trackedOffsetCurrent;
    private Vector3 trackedOffsetTarget;
    private Vector3 trackedOffsetVelocity;

    private float baseZoneYOffset = 0f;

    private float manualVerticalLookHeldTimer = 0f;
    private bool manualLookCurrentlyActive = false;
    private bool runtimeValuesInitialized = false;

    private void Reset()
    {
        CacheComponents();
        ResolvePlayerReferences();
        InitializeRuntimeValues();
    }

    private void Awake()
    {
        CacheComponents();
        ResolvePlayerReferences();
        EnsureBaseSizeInitialized(false);
        InitializeRuntimeValues();
    }

    private void Start()
    {
        CacheComponents();
        ResolvePlayerReferences();
        EnsureBaseSizeInitialized(true);
        InitializeRuntimeValues();

        ApplyCombinedLensSize(true);
        ApplyTrackedObjectOffset(true);
    }

    private void OnValidate()
    {
        leftOffset = Mathf.Clamp01(leftOffset);
        rightOffset = Mathf.Clamp01(rightOffset);
        neutralScreenX = Mathf.Clamp01(neutralScreenX);

        horizontalAxisDeadZone = Mathf.Clamp01(horizontalAxisDeadZone);
        verticalLookDeadZone = Mathf.Clamp01(verticalLookDeadZone);
        standingInputDeadZone = Mathf.Clamp01(standingInputDeadZone);

        screenXSmoothTime = Mathf.Max(0.0001f, screenXSmoothTime);
        zoneYOffsetSmoothTime = Mathf.Max(0.0001f, zoneYOffsetSmoothTime);
        manualVerticalLookSmoothTime = Mathf.Max(0.0001f, manualVerticalLookSmoothTime);
        manualVerticalLookStartDelay = Mathf.Max(0f, manualVerticalLookStartDelay);

        manualVerticalScreenYShift = Mathf.Clamp(manualVerticalScreenYShift, 0f, 0.45f);
        manualLookDeadZoneHeight = Mathf.Clamp01(manualLookDeadZoneHeight);
        deadZoneHeightSmoothTime = Mathf.Max(0.0001f, deadZoneHeightSmoothTime);

        fallbackBaseOrthoSize = Mathf.Max(0.01f, fallbackBaseOrthoSize);
        sprintZoomOutSizeDelta = Mathf.Max(0f, sprintZoomOutSizeDelta);
        sprintZoomLerpTime = Mathf.Max(0.01f, sprintZoomLerpTime);

        standingMaxHorizontalSpeed = Mathf.Max(0f, standingMaxHorizontalSpeed);
        standingMaxVerticalSpeed = Mathf.Max(0f, standingMaxVerticalSpeed);

        if (!Application.isPlaying)
            return;

        CacheComponents();
        ResolvePlayerReferences();
        EnsureBaseSizeInitialized(false);
        InitializeRuntimeValues();

        ApplyCombinedLensSize(true);
        ApplyTrackedObjectOffset(true);
    }

    private void OnEnable()
    {
        CacheComponents();
        ResolvePlayerReferences();
        EnsureBaseSizeInitialized(false);
        InitializeRuntimeValues();

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
        ResolvePlayerReferences();

        UpdateHorizontalLookTarget();
        UpdateManualVerticalLookTarget();
    }

    private void LateUpdate()
    {
        UpdateSprintZoomRuntime();
        UpdateSmoothCameraOffsets();
    }

    private void UpdateHorizontalLookTarget()
    {
        if (transposer == null)
            return;

        if (!enableHorizontalLookOffset)
        {
            screenXTarget = neutralScreenX;
            return;
        }

        ResolvePlayerReferences();

        if (usePlayerFacingForHorizontalLook && playerMovementModule != null)
        {
            screenXTarget = playerMovementModule.IsFacingRight ? rightOffset : leftOffset;
            return;
        }

        float horizontal = ReadHorizontalOffsetInput();

        if (horizontal < -horizontalAxisDeadZone)
        {
            screenXTarget = leftOffset;
            return;
        }

        if (horizontal > horizontalAxisDeadZone)
        {
            screenXTarget = rightOffset;
            return;
        }

        if (returnToNeutralWhenNoInput)
            screenXTarget = neutralScreenX;
    }

    private void UpdateManualVerticalLookTarget()
    {
        if (transposer == null)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        bool canManualLookByState =
            !manualVerticalLookOnlyWhenStanding ||
            IsPlayerStandingForManualVerticalLook();

        float rawVerticalInput = 0f;

        if (enableManualVerticalLook && canManualLookByState)
            rawVerticalInput = ReadManualVerticalLookInput();

        float processedVerticalInput = ApplyAxisDeadZone(rawVerticalInput, verticalLookDeadZone);
        bool wantsVerticalLook = Mathf.Abs(processedVerticalInput) > 0.0001f;

        if (wantsVerticalLook)
            manualVerticalLookHeldTimer += dt;
        else
            manualVerticalLookHeldTimer = 0f;

        bool delayPassed = manualVerticalLookHeldTimer >= manualVerticalLookStartDelay;

        manualLookCurrentlyActive = wantsVerticalLook && delayPassed && canManualLookByState;

        if (manualLookCurrentlyActive)
        {
            screenYTarget = Mathf.Clamp01(baseScreenY + processedVerticalInput * manualVerticalScreenYShift);

            if (debugManualLook)
            {
                manualLookState =
                    "ACTIVE input=" + processedVerticalInput.ToString("0.00") +
                    " targetScreenY=" + screenYTarget.ToString("0.00");
            }
        }
        else
        {
            if (returnVerticalLookToCenterWhenNoInput)
                screenYTarget = baseScreenY;

            if (debugManualLook)
            {
                if (!enableManualVerticalLook)
                    manualLookState = "BLOCKED: manual look OFF";
                else if (!canManualLookByState)
                    manualLookState = "BLOCKED: player is not standing";
                else if (!wantsVerticalLook)
                    manualLookState = "RETURNING: no vertical input";
                else if (!delayPassed)
                    manualLookState = "WAITING: start delay";
            }
        }

        if (!canManualLookByState && returnManualLookToCenterWhenNotStanding)
            screenYTarget = baseScreenY;

        bool needsVerticalCameraControl =
            manualLookCurrentlyActive ||
            Mathf.Abs(transposer.m_ScreenY - baseScreenY) > 0.001f ||
            Mathf.Abs(screenYTarget - baseScreenY) > 0.001f;

        if (reduceVerticalDeadZoneWhileManualLook && needsVerticalCameraControl)
            deadZoneHeightTarget = manualLookDeadZoneHeight;
        else
            deadZoneHeightTarget = baseDeadZoneHeight;
    }

    private void UpdateSmoothCameraOffsets()
    {
        if (transposer == null)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
            return;

        float oldScreenX = transposer.m_ScreenX;
        float oldScreenY = transposer.m_ScreenY;
        float oldDeadZoneHeight = transposer.m_DeadZoneHeight;
        Vector3 oldOffset = trackedOffsetCurrent;

        float newScreenX = Mathf.SmoothDamp(
            oldScreenX,
            screenXTarget,
            ref screenXVelocity,
            screenXSmoothTime,
            Mathf.Infinity,
            dt
        );

        float newScreenY = Mathf.SmoothDamp(
            oldScreenY,
            screenYTarget,
            ref screenYVelocity,
            manualVerticalLookSmoothTime,
            Mathf.Infinity,
            dt
        );

        float newDeadZoneHeight = Mathf.SmoothDamp(
            oldDeadZoneHeight,
            deadZoneHeightTarget,
            ref deadZoneHeightVelocity,
            deadZoneHeightSmoothTime,
            Mathf.Infinity,
            dt
        );

        trackedOffsetTarget.y = baseZoneYOffset;

        trackedOffsetCurrent = Vector3.SmoothDamp(
            trackedOffsetCurrent,
            trackedOffsetTarget,
            ref trackedOffsetVelocity,
            zoneYOffsetSmoothTime,
            Mathf.Infinity,
            dt
        );

        if (Mathf.Abs(newScreenX - oldScreenX) > 0.0001f)
            transposer.m_ScreenX = newScreenX;

        if (Mathf.Abs(newScreenY - oldScreenY) > 0.0001f)
            transposer.m_ScreenY = newScreenY;

        if (Mathf.Abs(newDeadZoneHeight - oldDeadZoneHeight) > 0.0001f)
            transposer.m_DeadZoneHeight = Mathf.Clamp01(newDeadZoneHeight);

        if ((trackedOffsetCurrent - oldOffset).sqrMagnitude > 0.000001f)
            ApplyTrackedObjectOffset(true);
    }

    private bool IsPlayerStandingForManualVerticalLook()
    {
        ResolvePlayerReferences();

        if (playerGroundModule != null && !playerGroundModule.IsGrounded)
            return false;

        if (playerLedgeModule != null && playerLedgeModule.BlocksStandardController)
            return false;

        if (playerFenceClimbModule != null && playerFenceClimbModule.BlocksStandardController)
            return false;

        if (blockManualLookWhileWallSliding && playerBounceModule != null && playerBounceModule.IsWallSliding)
            return false;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D))
            return false;

        if (useHorizontalAxisForStandingCheck)
        {
            float axis = ReadAxisSafe(horizontalAxisName);
            if (Mathf.Abs(axis) > standingInputDeadZone)
                return false;
        }

        if (useRigidbodyVelocityForStandingCheck && playerRigidbody != null)
        {
            if (Mathf.Abs(playerRigidbody.velocity.x) > standingMaxHorizontalSpeed)
                return false;

            if (Mathf.Abs(playerRigidbody.velocity.y) > standingMaxVerticalSpeed)
                return false;
        }

        return true;
    }

    private float ReadHorizontalOffsetInput()
    {
        float result = 0f;

        if (useAxisForOffset && !string.IsNullOrWhiteSpace(horizontalAxisName))
            result = ReadAxisSafe(horizontalAxisName);

        if (useKeyboardADForOffset)
        {
            if (Input.GetKey(KeyCode.A))
                result = -1f;

            if (Input.GetKey(KeyCode.D))
                result = 1f;
        }

        return Mathf.Clamp(result, -1f, 1f);
    }

    private float ReadManualVerticalLookInput()
    {
        float result = 0f;

        if (useRightStickForVerticalLook)
        {
            float stickY = ReadRightStickVerticalAxis();

            if (invertRightStickVertical)
                stickY = -stickY;

            result = stickY;
        }

        if (useKeyboardArrowsForVerticalLook)
        {
            bool up = Input.GetKey(KeyCode.UpArrow);
            bool down = Input.GetKey(KeyCode.DownArrow);

            if (up && !down)
                result = 1f;
            else if (down && !up)
                result = -1f;
            else if (up && down)
                result = 0f;
        }

        if (useKeyboardWSForVerticalLook)
        {
            bool up = Input.GetKey(KeyCode.W);
            bool down = Input.GetKey(KeyCode.S);

            if (up && !down)
                result = 1f;
            else if (down && !up)
                result = -1f;
            else if (up && down)
                result = 0f;
        }

        return Mathf.Clamp(result, -1f, 1f);
    }

    private float ReadRightStickVerticalAxis()
    {
        float best = 0f;

        if (!string.IsNullOrWhiteSpace(rightStickVerticalAxisName))
            best = ReadAxisSafe(rightStickVerticalAxisName);

        if (rightStickVerticalFallbackAxisNames != null)
        {
            for (int i = 0; i < rightStickVerticalFallbackAxisNames.Length; i++)
            {
                string axisName = rightStickVerticalFallbackAxisNames[i];

                if (string.IsNullOrWhiteSpace(axisName))
                    continue;

                float value = ReadAxisSafe(axisName);

                if (Mathf.Abs(value) > Mathf.Abs(best))
                    best = value;
            }
        }

        return Mathf.Clamp(best, -1f, 1f);
    }

    private float ReadAxisSafe(string axisName)
    {
        if (string.IsNullOrWhiteSpace(axisName))
            return 0f;

        try
        {
            return Input.GetAxisRaw(axisName);
        }
        catch
        {
            return 0f;
        }
    }

    private float ApplyAxisDeadZone(float value, float deadZone)
    {
        value = Mathf.Clamp(value, -1f, 1f);
        deadZone = Mathf.Clamp01(deadZone);

        float abs = Mathf.Abs(value);
        if (abs <= deadZone)
            return 0f;

        float sign = Mathf.Sign(value);
        float normalized = Mathf.InverseLerp(deadZone, 1f, abs);

        return sign * normalized;
    }

    private void Shake(float strength, float time, float fadeTime)
    {
        if (perlin == null)
            return;

        if (shakeCo != null)
            StopCoroutine(shakeCo);

        float userMultiplier = VideoSettingsPrefs.GetScreenShakeMultiplier(1f);
        shakeCo = StartCoroutine(ShakeRoutine(strength * userMultiplier, time, fadeTime));
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
        if (vcam == null || followObject == null)
            return;

        vcam.m_Follow = followObject;
        vcam.PreviousStateIsValid = false;

        ClearAutoPlayerReferences();
        ResolvePlayerReferences();

        InvalidateConfiner();
    }

    private void ChangeCameraYOffset(float newYOffsetY)
    {
        if (transposer == null)
            return;

        InitializeRuntimeValues();

        baseZoneYOffset = newYOffsetY;
        trackedOffsetTarget.y = baseZoneYOffset;
    }

    private void ChangeSprintZoomBlend(float blend)
    {
        sprintZoomBlendTarget = enableSprintZoom ? Mathf.Clamp01(blend) : 0f;
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

    private void ApplyTrackedObjectOffset(bool force = false)
    {
        if (transposer == null)
            return;

        if (!force && (transposer.m_TrackedObjectOffset - trackedOffsetCurrent).sqrMagnitude < 0.000001f)
            return;

        transposer.m_TrackedObjectOffset = trackedOffsetCurrent;
    }

    private void InitializeRuntimeValues()
    {
        if (runtimeValuesInitialized || transposer == null)
            return;

        if (readNeutralScreenXFromCinemachineOnStart)
            neutralScreenX = transposer.m_ScreenX;

        screenXTarget = neutralScreenX;

        baseScreenY = transposer.m_ScreenY;
        screenYTarget = baseScreenY;

        baseDeadZoneHeight = transposer.m_DeadZoneHeight;
        deadZoneHeightTarget = baseDeadZoneHeight;

        trackedOffsetCurrent = transposer.m_TrackedObjectOffset;
        trackedOffsetTarget = trackedOffsetCurrent;

        baseZoneYOffset = transposer.m_TrackedObjectOffset.y;

        runtimeValuesInitialized = true;
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

        bool noSprint = Mathf.Abs(sprintZoomBlendCurrent) <= 0.0001f &&
                        Mathf.Abs(sprintZoomBlendTarget) <= 0.0001f;

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
            Type t = confiner2D.GetType();

            confinerInvalidateMethod =
                t.GetMethod("InvalidateCache", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? t.GetMethod("InvalidatePathCache", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }

    private void ResolvePlayerReferences()
    {
        Transform source = null;

        if (playerController == null && vcam != null && vcam.m_Follow != null)
            playerController = vcam.m_Follow.GetComponentInParent<PlayerController>();

        if (playerController != null)
            source = playerController.transform;
        else if (vcam != null && vcam.m_Follow != null)
            source = vcam.m_Follow;

        if (source == null)
            return;

        if (playerRigidbody == null)
            playerRigidbody = source.GetComponentInParent<Rigidbody2D>();

        if (playerGroundModule == null)
            playerGroundModule = source.GetComponentInParent<PlayerGroundModule>();

        if (playerLedgeModule == null)
            playerLedgeModule = source.GetComponentInParent<PlayerLedgeModule>();

        if (playerFenceClimbModule == null)
            playerFenceClimbModule = source.GetComponentInParent<PlayerFenceClimbModule>();

        if (playerBounceModule == null)
            playerBounceModule = source.GetComponentInParent<PlayerBounceModule>();

        if (playerMovementModule == null)
            playerMovementModule = source.GetComponentInParent<PlayerMovementModule>();
    }

    private void ClearAutoPlayerReferences()
    {
        playerController = null;
        playerRigidbody = null;
        playerGroundModule = null;
        playerLedgeModule = null;
        playerFenceClimbModule = null;
        playerBounceModule = null;
        playerMovementModule = null;
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
        return x < 0.5f
            ? x * x * 2f
            : 1f - (1f - x) * (1f - x) * 2f;
    }
}