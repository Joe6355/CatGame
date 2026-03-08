using System;
using System.Collections.Generic; // для сугробов
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField, Tooltip("Базовая скорость бега по земле (без льда/снега/усталости).\nРекоменд: 3–8 (часто 4–6).")]
    private float moveSpeed = 5f;

    [SerializeField, Tooltip("Множитель скорости при усталости (анти-спам прыжка).\n0.6 = скорость падает до 60%.\nРекоменд: 0.5–0.8 (часто 0.6–0.7).")]
    private float fatigueSpeedMultiplier = 0.6f;

    [Header("Прыжок (заряд)")]
    [SerializeField, Tooltip("Максимальная вертикальная скорость (сила) прыжка при полном заряде.\nРекоменд: 10–25 (часто 16–22).")]
    private float maxJumpForce = 20f;

    [SerializeField, Tooltip("Время, за которое прыжок заряжается до maxJumpForce.\nРекоменд: 0.25–1.0 сек (часто 0.5–0.8).")]
    private float jumpTimeLimit = 1f;

    [SerializeField, Tooltip("Сила слабого прыжка, если отпустили кнопку ДО входа в режим заряда.\nРекоменд: 4–12 (зависит от gravityScale).")]
    private float shortJumpForce = 8f;

    [SerializeField, Tooltip("Задержка (сек) чтобы ВОЙТИ в режим заряда.\nЕсли отпустить раньше — будет слабый прыжок.\nРекоменд: 0.3–1.5 (по задаче поставим 1.0).")]
    private float chargeEnterDelay = 1f;

    [SerializeField, Tooltip("Койот-тайм: сколько секунд после схода с платформы ещё можно начать заряд прыжка.\nРекоменд: 0.05–0.15 (часто 0.08–0.12).")]
    private float coyoteTime = 0.05f;

    [Header("Буферизация прыжка")]
    [SerializeField, Tooltip("Буфер прыжка: если нажать кнопку ДО приземления, прыжок сработает автоматически.\nРекоменд: 0.08–0.15 (часто 0.1).")]
    private float jumpBufferTime = 0.1f;
    private float lastJumpPressedTime = -999f;

    private enum BufferedJumpKind { None, Hold, Short }
    private BufferedJumpKind bufferedJumpKind = BufferedJumpKind.None;

    private enum HoldJumpSource { None, Keyboard, GamepadCharge, Mobile }
    private HoldJumpSource currentHoldJumpSource = HoldJumpSource.None;
    private HoldJumpSource bufferedHoldSource = HoldJumpSource.None;

    [Header("Отскок от стен/потолка")]
    [SerializeField, Range(0f, 1f), Tooltip("Доля силы заряда, превращаемая в отскок по X от стены.\n0.33 = 33% от силы заряда.\nРекоменд: 0.2–0.5 (часто 0.3–0.4).")]
    private float wallBounceFraction = 0.33f;

    [SerializeField, Tooltip("Демпфирование отскока (уменьшение силы), если прошло достаточно времени после прыжка.\n1 = без демпфа, 0.5 = в 2 раза слабее.\nРекоменд: 0.4–0.8 (часто 0.5–0.7).")]
    private float damping = 0.5f;

    [SerializeField, Tooltip("Окно после прыжка, в которое демпфирование не применяется (чтобы отскок сразу после прыжка был бодрее).\nРекоменд: 0.1–0.3 сек (часто 0.15–0.25).")]
    private float dampingExclusionTime = 0.2f;

    [SerializeField, Tooltip("Минимальная |скорость по Y| чтобы считать, что мы в воздухе для отскока от стены.\nЕсли меньше — отскок всё равно разрешается в течение wallBounceApexWindow после прыжка (вершина дуги).\nРекоменд: 0.03–0.10 (часто 0.05).")]
    private float wallBounceMinAbsY = 0.05f;

    [SerializeField, Tooltip("Окно после прыжка/пинка, когда отскок от стены разрешён даже если скорость по Y почти 0 (вершина дуги).\nРекоменд: 0.3–0.9 сек (часто 0.6).")]
    private float wallBounceApexWindow = 0.6f;

    [SerializeField, Tooltip("Порог 'боковости' стены по нормали (|normal.x|). На углах нормаль бывает неидеальной.\nМеньше = чаще срабатывает отскок на углах.\nРекоменд: 0.40–0.60 (часто 0.45–0.55).")]
    private float wallNormalMinAbsX = 0.45f;

    [Header("Усталость (анти-спам)")]
    [SerializeField, Tooltip("Сколько длится усталость после прыжка: нельзя начать новый заряд, скорость снижена.\nРекоменд: 0.3–1.2 сек (часто 0.6–0.9).")]
    private float fatigueDuration = 0.8f;

    [SerializeField, Tooltip("UI-изображение усталости (картинка/иконка), у которого меняется прозрачность.\nРекоменд: назначить Image на Canvas (можно оставить пустым, если не нужно).")]
    private Image fatigueImage;

    [Header("Назначение клавиш (PC)")]
    [SerializeField, Tooltip("Клавиша движения влево (PC).\nРекоменд: A или LeftArrow.")]
    private KeyCode leftKey = KeyCode.A;

    [SerializeField, Tooltip("Клавиша движения вправо (PC).\nРекоменд: D или RightArrow.")]
    private KeyCode rightKey = KeyCode.D;

    [SerializeField, Tooltip("Клавиша прыжка (PC).\nРекоменд: Space.")]
    private KeyCode jumpKey = KeyCode.Space;

    [Header("Геймпад (прыжок)")]
    [SerializeField, Tooltip("Если ВКЛ — в desktop-режиме (useMobileControls = false) можно прыгать с геймпада.\nДвижение у тебя уже работает отдельно, тут только кнопки прыжка.")]
    private bool useGamepadJump = true;

    [SerializeField, Tooltip("Кнопка геймпада для обычного прыжка:\n- тап = короткий\n- удержание = заряд/долгий\nОбычно это A / Cross (JoystickButton0).")]
    private KeyCode gamepadChargeJumpKey = KeyCode.JoystickButton0;

    [SerializeField, Tooltip("Отдельная кнопка геймпада для МГНОВЕННОГО короткого прыжка.\nОбычно это B / Circle (JoystickButton1).")]
    private KeyCode gamepadShortJumpKey = KeyCode.JoystickButton1;

    [Header("Legacy Rebind (KeyCode)")]
    [SerializeField, Tooltip("If ON and LegacyKeycodeRebind exists, this controller reads keys from it (UI rebind). If OFF or missing, uses local KeyCode fields below.")]
    private bool useLegacyKeycodeRebind = true;

    [SerializeField, Tooltip("If ON, when no keyboard move key is pressed we use InputManager axis 'Horizontal' as fallback (usually gamepad stick). NOTE: if your InputManager Horizontal still has A/D, those may move unless we block the common keys below.")]
    private bool useInputManagerAxisFallback = true;

    [Header("Ground Check")]
    [SerializeField, Tooltip("Слои, которые считаются землёй (Ground/Platform и т.п.).\nРекоменд: выделить отдельный слой Ground и выбрать его здесь.")]
    private LayerMask groundMask;

    [SerializeField, Tooltip("Если ВКЛ — проверяем землю OverlapBox под ногами.\nЕсли ВЫКЛ — проверяем OverlapCircle (groundCheck + radius).\nРекоменд: ВКЛ (true) для платформера.")]
    private bool useBoxGroundCheck = true;

    [SerializeField, Tooltip("Размер бокса проверки земли (ширина/высота).\nШирина обычно чуть меньше ширины персонажа, высота — тонкая.\nРекоменд: X 0.4–0.9, Y 0.06–0.15.")]
    private Vector2 groundBoxSize = new Vector2(0.6f, 0.12f);

    [SerializeField, Tooltip("Смещение бокса проверки земли от центра персонажа (локально).\nОбычно немного вниз.\nРекоменд: Y -0.15..-0.30, X чаще 0.")]
    private Vector2 groundBoxOffset = new Vector2(0f, -0.2f);

    [SerializeField, Tooltip("Точка для OverlapCircle, если useBoxGroundCheck выключен.\nРекоменд: пустышка GroundCheck под ногами.")]
    private Transform groundCheck;

    [SerializeField, Tooltip("Радиус круга проверки земли, если useBoxGroundCheck выключен.\nРекоменд: 0.08–0.18 (часто 0.10–0.14).")]
    private float groundCheckRadius = 0.12f;

    [Header("Ground Edge Assist (борьба с краями)")]
    [SerializeField, Tooltip("Если ВКЛ — включается помощь на краях платформ: доп. зонды и снап-лучи вниз.\nРекоменд: ВКЛ (true) — сильно улучшает контроль на краях.")]
    private bool useEdgeAssist = true;

    [SerializeField, Tooltip("Половина ширины 'стоп' для боковых зондов (влево/вправо от центра).\nБольше = легче цепляться за край, но может цепляться 'слишком'.\nРекоменд: 0.12–0.30 (часто 0.18–0.24).")]
    private float edgeProbeHalfWidth = 0.22f;

    [SerializeField, Tooltip("Высота тонких боксов под краями стоп.\nОчень тонкая, чтобы не ловить стены.\nРекоменд: 0.03–0.08 (часто 0.05–0.07).")]
    private float edgeProbeHeight = 0.06f;

    [SerializeField, Tooltip("Дистанция снап-лучей вниз: насколько далеко 'нащупываем' землю.\nБольше = легче приземлиться на край.\nРекоменд: 0.06–0.18 (часто 0.10–0.14).")]
    private float snapProbeDistance = 0.12f;

    [SerializeField, Tooltip("Максимальная эффективная дистанция снап-лучей. Даже если snapProbeDistance больше, будет использовано не больше этого значения.\nНужен, чтобы Edge Assist не считал игрока 'на земле' слишком высоко и не ломал отскоки.\nРекоменд: 0.12–0.22 (часто 0.18).")]
    private float snapProbeDistanceMax = 0.18f;

    [SerializeField, Tooltip("Снап-лучи вниз срабатывают только когда игрок НЕ летит вверх.\nЕсли rb.velocity.y выше этого порога — снап-лучи игнорируются, чтобы не ломать прыжки/отскоки.\nРекоменд: 0.0–0.05 (часто 0.02).")]
    private float snapOnlyWhenFallingY = 0.02f;

    [SerializeField, Range(0f, 1f), Tooltip("Минимальная вертикальная компонента нормали (normal.y), чтобы считать поверхность землёй.\nЧем больше — тем меньше шанс 'прилипнуть' к стенам.\nРекоменд: 0.3–0.6 (часто 0.35–0.45).")]
    private float snapMinNormalY = 0.35f;

    [Header("UI шкалы прыжка")]
    [SerializeField, Tooltip("Заполняемая часть шкалы заряда прыжка (Image Fill).\nРекоменд: Image с Fill Amount, можно оставить пустым если шкала не нужна.")]
    private Image jumpBarFill;

    [SerializeField, Tooltip("Фон шкалы заряда прыжка.\nРекоменд: назначить, если нужен фон (можно пусто).")]
    private Image jumpBarBG;

    [SerializeField, Tooltip("Камера, которая рендерит мир и по которой считаем ScreenPoint.\nРекоменд: Main Camera.")]
    private Camera mainCamera;

    [SerializeField, Tooltip("Canvas, в котором лежит UI шкалы.\nРекоменд: Canvas в режиме Screen Space Overlay или Camera.")]
    private Canvas uiCanvas;

    [SerializeField, Tooltip("Смещение шкалы от позиции игрока в мире (в мировых единицах).\nНапр: (0,2,0) — над головой.\nРекоменд: Y 1.0–2.5 (зависит от размера спрайта).")]
    private Vector3 barOffset = new Vector3(0f, 2f, 0f);

    [Header("Мобильное управление")]
    [SerializeField, Tooltip("Если ВКЛ — используем мобильное управление (джойстик + кнопка), PC ввод игнорится.\nРекоменд: true для мобилки, false для ПК.")]
    private bool useMobileControls = false;

    [SerializeField, Tooltip("Ссылka на Joystick (обычно из пакета Joystick).\nРекоменд: назначить, если useMobileControls = true.")]
    private Joystick mobileJoystick;

    [SerializeField, Tooltip("UI кнопка прыжка для мобилки (нажатие/удержание).\nРекоменд: назначить, если useMobileControls = true.")]
    private Button mobileJumpButton;

    [Header("Air Control")]
    [SerializeField, Tooltip("Скорость управления в воздухе, когда air-control временно разрешён (AllowAirControlFor).\nРекоменд: 3–10 (часто 4–7).")]
    private float airControlSpeed = 5f;

    [SerializeField, Tooltip("Если ВКЛ — управление в воздухе доступно всегда. Если ВЫКЛ — работает текущая логика с временным разрешением через AllowAirControlFor().")]
    private bool enableAirControlInAir = false;

    private float airControlUnlockUntil = 0f;

    [Header("Лёд (Tag = \"Ice\")")]
    [SerializeField, Tooltip("Ускорение на льду (как быстро набираем скорость к target).\nМеньше = более скользко.\nРекоменд: 1–6 (часто 2–4).")]
    private float iceAccel = 2.5f;

    [SerializeField, Tooltip("Торможение на льду при смене направления.\nМеньше = более скользко.\nРекоменд: 0.5–4 (часто 1–2).")]
    private float iceBrake = 1.2f;

    [SerializeField, Tooltip("Максимальная скорость на льду как множитель от moveSpeed.\n1.15 = на льду можно чуть быстрее.\nРекоменд: 1.0–1.4 (часто 1.1–1.25).")]
    private float iceMaxSpeedMul = 1.15f;

    [SerializeField, Tooltip("Ускорение на обычной земле. Очень большое значение делает движение почти мгновенным.\nРекоменд: 20–200 (для плавности) или 9999 (мгновенно).")]
    private float normalAccel = 9999f;

    [SerializeField, Tooltip("Торможение/смена направления на обычной земле.\nРекоменд: 20–200 (плавно) или 9999 (мгновенно).")]
    private float normalBrake = 9999f;

    [Header("Блокировка игрового ввода (после меню)")]
    [SerializeField, Tooltip("Если ВКЛ — после возврата из паузы/меню игрок не начнёт двигаться/прыгать, пока все зажатые игровые кнопки не будут отпущены.")]
    private bool waitReleaseAfterInputEnable = true;

    [SerializeField, Tooltip("Небольшая доп. задержка после отпускания всех игровых кнопок перед возвратом управления. Нужна как страховка от 'протекания' ввода из UI.\nРекоменд: 0.03–0.12 сек (часто 0.05–0.08).")]
    private float postMenuInputUnlockDelay = 0.06f;

    [SerializeField, Tooltip("Порог нейтрального положения оси Horizontal, ниже которого считаем, что стик/ось отпущены.\nРекоменд: 0.15–0.35 (часто 0.2).")]
    private float inputReleaseAxisDeadZone = 0.2f;

    // =========================
    // INTERNAL STATE (не в инспекторе)
    // =========================
    private Rigidbody2D rb;
    private bool isFacingRight = true;
    private bool isGrounded = false;
    private float lastGroundedTime = -999f;

    private float inputX = 0f;

    private bool isJumpHoldActive = false;
    private bool isChargingJump = false;

    private float jumpButtonDownTime = 0f;
    private float jumpStartHoldTime = 0f;

    private bool mobileJumpHeld = false;

    private float airVx = 0f;
    private float lastJumpTime = -999f;
    private float jumpStartSpeed = 0f;
    private float fatigueEndTime = 0f;

    private float lastAppliedJumpForce = 0f;

    private float platformVX = 0f;
    private float externalWindVX = 0f;

    private bool isOnIce = false;

    private bool wasGroundedLastFrame = false;

    private Collider2D lastGroundCol;

    private float flipDeadZone = 0.05f;

    private float lastBounceTime = -999f;

    private readonly Dictionary<SnowdriftArea2D, (float move, float jump)> activeSnow = new Dictionary<SnowdriftArea2D, (float move, float jump)>();
    private float snowMoveMul = 1f;
    private float snowJumpMul = 1f;

    private bool prevUseMobileControls = false;

    private bool gameplayInputEnabled = true;
    private bool waitForGameplayInputRelease = false;
    private float gameplayInputUnlockAtUnscaled = -1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (mainCamera == null) mainCamera = Camera.main;

        if (mobileJumpButton != null)
        {
            var handler = mobileJumpButton.GetComponent<PointerHoldHandler>();
            if (handler == null) handler = mobileJumpButton.gameObject.AddComponent<PointerHoldHandler>();

            handler.OnDown -= OnMobileJumpDown;
            handler.OnUp -= OnMobileJumpUp;

            handler.OnDown += OnMobileJumpDown;
            handler.OnUp += OnMobileJumpUp;
        }

        prevUseMobileControls = useMobileControls;
        ApplyMobileUIVisibility();
    }

    private void Start()
    {
        UpdateJumpBar(0f);
        UpdateFatigueUI();
        UpdateJumpBarPosition();
    }

    private void Update()
    {
        if (prevUseMobileControls != useMobileControls)
        {
            prevUseMobileControls = useMobileControls;
            ApplyMobileUIVisibility();
        }

        if (IsGameplayInputAllowedThisFrame())
        {
            if (!useMobileControls) HandleDesktopInput();
            else HandleMobileInput();

            TryConsumeJumpBuffer();
        }
        else
        {
            ResetGameplayInputState(false);
        }

        UpdateJumpBarPosition();
        UpdateFatigueUI();
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        ApplyMovement();
        externalWindVX = 0f;
    }

    private bool GetKeyboardChargeJumpDown()
    {
        var rb = (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null) ? LegacyKeycodeRebind.I : null;
        if (rb != null) return rb.GetDown(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold);
        return Input.GetKeyDown(jumpKey);
    }

    private bool GetKeyboardChargeJumpHeld()
    {
        var rb = (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null) ? LegacyKeycodeRebind.I : null;
        if (rb != null) return rb.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold);
        return Input.GetKey(jumpKey);
    }

    private bool GetKeyboardChargeJumpUp()
    {
        var rb = (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null) ? LegacyKeycodeRebind.I : null;
        if (rb != null) return rb.GetUp(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold);
        return Input.GetKeyUp(jumpKey);
    }

    private bool GetGamepadChargeJumpDown()
    {
        if (!useGamepadJump) return false;

        var rb = (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null) ? LegacyKeycodeRebind.I : null;
        if (rb != null) return rb.GetDown(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKeyDown(gamepadChargeJumpKey);
    }

    private bool GetGamepadChargeJumpHeld()
    {
        if (!useGamepadJump) return false;

        var rb = (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null) ? LegacyKeycodeRebind.I : null;
        if (rb != null) return rb.GetHeld(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKey(gamepadChargeJumpKey);
    }

    private bool GetGamepadChargeJumpUp()
    {
        if (!useGamepadJump) return false;

        var rb = (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null) ? LegacyKeycodeRebind.I : null;
        if (rb != null) return rb.GetUp(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold);

        return Input.GetKeyUp(gamepadChargeJumpKey);
    }

    private bool GetDesktopShortJumpDown()
    {
        var rb = (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null) ? LegacyKeycodeRebind.I : null;

        if (rb != null)
        {
            bool kb = rb.GetDown(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpShort);
            bool gp = useGamepadJump && rb.GetDown(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpShort);
            return kb || gp;
        }

        return useGamepadJump && Input.GetKeyDown(gamepadShortJumpKey);
    }

    private void HandleDesktopInput()
    {
        if (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null && LegacyKeycodeRebind.I.IsRebinding)
        {
            inputX = 0f;
            return;
        }

        int dir = 0;
        var rb = (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null) ? LegacyKeycodeRebind.I : null;

        if (rb != null)
        {
            dir = rb.GetKeyboardMoveDir();
        }
        else
        {
            if (Input.GetKey(leftKey)) dir -= 1;
            if (Input.GetKey(rightKey)) dir += 1;
        }

        float axis = 0f;
        if (dir == 0 && useInputManagerAxisFallback)
        {
            if (!(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)))
                axis = Input.GetAxisRaw("Horizontal");
        }

        inputX = (dir != 0) ? dir : axis;
        if (Mathf.Abs(inputX) > 0.01f && IsGroundMovementAllowed())
        {
            bool faceRight = inputX > 0f;
            if (faceRight != isFacingRight) Flip();
        }

        if (GetDesktopShortJumpDown() && !isJumpHoldActive)
        {
            lastJumpPressedTime = Time.time;
            bufferedJumpKind = BufferedJumpKind.Short;
            bufferedHoldSource = HoldJumpSource.None;

            TryPerformDedicatedShortJump();
        }

        bool canStartHold = CanStartJumpCharge();

        bool keyboardChargeDown = GetKeyboardChargeJumpDown();
        bool gamepadChargeDown = GetGamepadChargeJumpDown();

        if (keyboardChargeDown || gamepadChargeDown)
        {
            lastJumpPressedTime = Time.time;
            bufferedJumpKind = BufferedJumpKind.Hold;
            bufferedHoldSource = (gamepadChargeDown && !keyboardChargeDown)
                ? HoldJumpSource.GamepadCharge
                : HoldJumpSource.Keyboard;

            if (canStartHold)
                BeginJumpHold(bufferedHoldSource);
        }

        if (isJumpHoldActive)
        {
            bool held = IsHoldInputStillHeld(currentHoldJumpSource);
            bool released = IsHoldInputReleased(currentHoldJumpSource);

            if (held)
                UpdateJumpHold();

            if (released)
                ReleaseJumpHoldAndJump();
        }
    }

    private void HandleMobileInput()
    {
        inputX = mobileJoystick != null ? mobileJoystick.Horizontal : 0f;

        if (Mathf.Abs(inputX) > 0.01f && IsGroundMovementAllowed())
        {
            bool faceRight = inputX > 0f;
            if (faceRight != isFacingRight) Flip();
        }

        if (mobileJumpHeld)
        {
            lastJumpPressedTime = Time.time;
            bufferedJumpKind = BufferedJumpKind.Hold;
            bufferedHoldSource = HoldJumpSource.Mobile;
        }

        if (mobileJumpHeld)
        {
            if (!isJumpHoldActive && CanStartJumpCharge())
                BeginJumpHold(HoldJumpSource.Mobile);

            if (isJumpHoldActive)
                UpdateJumpHold();
        }
        else
        {
            if (isJumpHoldActive)
                ReleaseJumpHoldAndJump();
        }
    }
    private bool IsHoldInputStillHeld(HoldJumpSource source)
    {
        switch (source)
        {
            case HoldJumpSource.GamepadCharge:
                return GetGamepadChargeJumpHeld();

            case HoldJumpSource.Mobile:
                return mobileJumpHeld;

            case HoldJumpSource.Keyboard:
            case HoldJumpSource.None:
            default:
                return GetKeyboardChargeJumpHeld();
        }
    }

    private bool IsHoldInputReleased(HoldJumpSource source)
    {
        switch (source)
        {
            case HoldJumpSource.GamepadCharge:
                return GetGamepadChargeJumpUp();

            case HoldJumpSource.Mobile:
                return !mobileJumpHeld;

            case HoldJumpSource.Keyboard:
            case HoldJumpSource.None:
            default:
                return GetKeyboardChargeJumpUp();
        }
    }

    private bool IsWithinGroundedJumpWindow()
    {
        return isGrounded || (Time.time - lastGroundedTime) <= coyoteTime;
    }

    private bool CanStartJumpCharge()
    {
        return IsWithinGroundedJumpWindow() && !IsFatigued();
    }

    private void BeginJumpHold(HoldJumpSource source = HoldJumpSource.Keyboard)
    {
        isJumpHoldActive = true;
        isChargingJump = false;
        bufferedJumpKind = BufferedJumpKind.None;
        currentHoldJumpSource = source;
        bufferedHoldSource = HoldJumpSource.None;

        jumpButtonDownTime = Time.time;
        UpdateJumpBar(0f);

        if (Mathf.Abs(inputX) > 0.01f && IsGroundMovementAllowed())
        {
            bool faceRight = inputX > 0f;
            if (faceRight != isFacingRight) Flip();
        }
    }

    private void UpdateJumpHold()
    {
        if (!isGrounded && (Time.time - lastGroundedTime) > coyoteTime)
        {
            CancelJumpCharge();
            return;
        }

        if (!isChargingJump)
        {
            float held = Time.time - jumpButtonDownTime;

            if (held < chargeEnterDelay)
            {
                UpdateJumpBar(0f);
                return;
            }

            isChargingJump = true;
            jumpStartHoldTime = Time.time;
        }

        float hold = Mathf.Clamp(Time.time - jumpStartHoldTime, 0f, jumpTimeLimit);
        float normalized = hold / jumpTimeLimit;
        UpdateJumpBar(normalized);
    }

    private void ReleaseJumpHoldAndJump()
    {
        bool canJumpNow = IsWithinGroundedJumpWindow();

        if (!canJumpNow)
        {
            isJumpHoldActive = false;
            isChargingJump = false;
            currentHoldJumpSource = HoldJumpSource.None;
            bufferedJumpKind = BufferedJumpKind.None;
            UpdateJumpBar(0f);
            return;
        }

        float verticalForce;

        if (!isChargingJump)
        {
            verticalForce = shortJumpForce * snowJumpMul;
        }
        else
        {
            float hold = Mathf.Clamp(Time.time - jumpStartHoldTime, 0f, jumpTimeLimit);
            verticalForce = Mathf.Clamp01(hold / jumpTimeLimit) * maxJumpForce * snowJumpMul;
        }

        isJumpHoldActive = false;
        isChargingJump = false;
        currentHoldJumpSource = HoldJumpSource.None;
        bufferedJumpKind = BufferedJumpKind.None;

        float speedMul = IsFatigued() ? fatigueSpeedMultiplier : 1f;
        float takeoffVx = platformVX + (isFacingRight ? 1f : -1f) * moveSpeed * speedMul * snowMoveMul;

        jumpStartSpeed = takeoffVx;
        PerformJump(takeoffVx, verticalForce);

        UpdateJumpBar(0f);
        StartFatigue();
    }

    private bool TryPerformDedicatedShortJump()
    {
        if (!CanStartJumpCharge())
            return false;

        isJumpHoldActive = false;
        isChargingJump = false;
        currentHoldJumpSource = HoldJumpSource.None;

        float verticalForce = shortJumpForce * snowJumpMul;

        float speedMul = IsFatigued() ? fatigueSpeedMultiplier : 1f;
        float takeoffVx = platformVX + (isFacingRight ? 1f : -1f) * moveSpeed * speedMul * snowMoveMul;

        jumpStartSpeed = takeoffVx;
        PerformJump(takeoffVx, verticalForce);

        UpdateJumpBar(0f);
        StartFatigue();

        bufferedJumpKind = BufferedJumpKind.None;
        return true;
    }

    private void TryConsumeJumpBuffer()
    {
        if (bufferedJumpKind == BufferedJumpKind.None) return;

        if (Time.time - lastJumpPressedTime > jumpBufferTime)
        {
            bufferedJumpKind = BufferedJumpKind.None;
            bufferedHoldSource = HoldJumpSource.None;
            return;
        }

        if (!CanStartJumpCharge()) return;

        if (bufferedJumpKind == BufferedJumpKind.Short)
        {
            TryPerformDedicatedShortJump();
            bufferedJumpKind = BufferedJumpKind.None;
            bufferedHoldSource = HoldJumpSource.None;
            return;
        }

        if (bufferedJumpKind == BufferedJumpKind.Hold)
        {
            HoldJumpSource source = bufferedHoldSource;

            if (source == HoldJumpSource.None)
                source = HoldJumpSource.Keyboard;

            if (IsHoldInputStillHeld(source))
            {
                BeginJumpHold(source);
            }
            else
            {
                TryPerformDedicatedShortJump();
            }

            bufferedJumpKind = BufferedJumpKind.None;
            bufferedHoldSource = HoldJumpSource.None;
        }
    }

    private void PerformJump(float vx, float vy)
    {
        lastJumpTime = Time.time;
        lastAppliedJumpForce = vy;

        rb.velocity = new Vector2(vx + externalWindVX, vy);

        airVx = vx;
        lastBounceTime = Time.time;
    }

    private void StartFatigue()
    {
        fatigueEndTime = Time.time + fatigueDuration;
    }

    private bool IsFatigued()
    {
        return Time.time < fatigueEndTime;
    }

    private bool IsAirborne()
    {
        return !isGrounded;
    }

    private bool IsGroundMovementAllowed()
    {
        return !isJumpHoldActive && !isChargingJump;
    }

    public void AllowAirControlFor(float duration)
    {
        airControlUnlockUntil = Mathf.Max(airControlUnlockUntil, Time.time + Mathf.Max(0f, duration));
    }

    public void AddExternalWind(float vx)
    {
        externalWindVX += vx;
    }

    private void ApplyMovement()
    {
        if (isChargingJump)
        {
            bool onMovingGroundByEffector = isGrounded && lastGroundCol && lastGroundCol.GetComponent<SurfaceEffector2D>() != null;
            bool carriedByPlatform = isGrounded && Mathf.Abs(platformVX) > 0.0001f;

            if (isGrounded && !isOnIce && !onMovingGroundByEffector && !carriedByPlatform)
            {
                rb.velocity = new Vector2(externalWindVX, rb.velocity.y);
            }
            else if (carriedByPlatform)
            {
                rb.velocity = new Vector2(platformVX + externalWindVX, rb.velocity.y);
            }
            return;
        }

        if (IsAirborne())
        {
            if (enableAirControlInAir || Time.time < airControlUnlockUntil)
            {
                float speedMul = (IsFatigued() ? fatigueSpeedMultiplier : 1f) * snowMoveMul;
                float vx = inputX * airControlSpeed * speedMul + externalWindVX;
                rb.velocity = new Vector2(vx, rb.velocity.y);

                if (Mathf.Abs(vx) > flipDeadZone)
                {
                    bool faceRight = vx > 0f;
                    if (faceRight != isFacingRight) Flip();
                }
            }
            else
            {
                rb.velocity = new Vector2(airVx + externalWindVX, rb.velocity.y);

                float vx = rb.velocity.x;
                if (Mathf.Abs(vx) > flipDeadZone)
                {
                    bool faceRight = vx > 0f;
                    if (faceRight != isFacingRight) Flip();
                }
            }
        }
        else
        {
            float speedMul = (IsFatigued() ? fatigueSpeedMultiplier : 1f) * snowMoveMul;
            float target = inputX * moveSpeed * speedMul;

            float maxSpeed = moveSpeed * (isOnIce ? iceMaxSpeedMul : 1f) * snowMoveMul;
            float accel = isOnIce ? iceAccel : normalAccel;
            float brake = isOnIce ? iceBrake : normalBrake;

            float cur = rb.velocity.x;
            float rate = (Mathf.Sign(target) == Mathf.Sign(cur) || Mathf.Approximately(cur, 0f)) ? accel : brake;

            float newVx = Mathf.MoveTowards(cur, Mathf.Clamp(target, -maxSpeed, +maxSpeed), rate * Time.fixedDeltaTime);

            newVx += platformVX + externalWindVX;

            rb.velocity = new Vector2(newVx, rb.velocity.y);

            if (Mathf.Abs(newVx) > flipDeadZone)
            {
                bool faceRight = newVx > 0f;
                if (faceRight != isFacingRight) Flip();
            }
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }

    private void ApplyMobileUIVisibility()
    {
        if (mobileJoystick != null) mobileJoystick.gameObject.SetActive(useMobileControls);
        if (mobileJumpButton != null) mobileJumpButton.gameObject.SetActive(useMobileControls);
    }

    private void OnMobileJumpDown()
    {
        mobileJumpHeld = true;
    }

    private void OnMobileJumpUp()
    {
        mobileJumpHeld = false;
    }

    private void UpdateJumpBar(float normalized)
    {
        if (jumpBarFill != null) jumpBarFill.fillAmount = Mathf.Clamp01(normalized);

        bool show = normalized > 0f || isJumpHoldActive || isChargingJump;
        if (jumpBarFill != null) jumpBarFill.enabled = show;
        if (jumpBarBG != null) jumpBarBG.enabled = show;
    }

    private void UpdateJumpBarPosition()
    {
        if (jumpBarFill == null && jumpBarBG == null) return;
        if (mainCamera == null || uiCanvas == null) return;

        Vector3 worldPos = transform.position + barOffset;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        RectTransform canvasRect = uiCanvas.transform as RectTransform;
        if (canvasRect == null) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera, out localPoint);

        if (jumpBarFill != null) jumpBarFill.rectTransform.anchoredPosition = localPoint;
        if (jumpBarBG != null) jumpBarBG.rectTransform.anchoredPosition = localPoint;
    }

    private void UpdateFatigueUI()
    {
        if (fatigueImage == null) return;

        float alpha = IsFatigued() ? 1f : 0f;
        Color c = fatigueImage.color;
        c.a = alpha;
        fatigueImage.color = c;
    }

    private void CheckGrounded()
    {
        wasGroundedLastFrame = isGrounded;

        Vector2 origin = (Vector2)transform.position + groundBoxOffset;

        bool grounded = false;
        Collider2D col = null;

        if (useBoxGroundCheck)
        {
            col = Physics2D.OverlapBox(origin, groundBoxSize, 0f, groundMask);
            grounded = col != null;
        }
        else
        {
            Vector2 p = groundCheck ? (Vector2)groundCheck.position : origin;
            col = Physics2D.OverlapCircle(p, groundCheckRadius, groundMask);
            grounded = col != null;
        }

        if (!grounded && useEdgeAssist)
        {
            if (rb.velocity.y <= snapOnlyWhenFallingY)
            {
                float half = Mathf.Max(0.05f, edgeProbeHalfWidth);
                float y = origin.y - 0.001f;
                Vector2 size = new Vector2(half * 0.9f, edgeProbeHeight);

                Collider2D leftProbe = Physics2D.OverlapBox(new Vector2(origin.x - half, y), size, 0f, groundMask);
                Collider2D rightProbe = Physics2D.OverlapBox(new Vector2(origin.x + half, y), size, 0f, groundMask);

                if (leftProbe != null || rightProbe != null)
                {
                    float dist = Mathf.Min(snapProbeDistance, snapProbeDistanceMax);

                    RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, dist, groundMask);
                    if (hit.collider != null && hit.normal.y >= snapMinNormalY)
                    {
                        grounded = true;
                        col = hit.collider;
                    }
                    else
                    {
                        RaycastHit2D hitL = Physics2D.Raycast(new Vector2(origin.x - half, transform.position.y), Vector2.down, dist, groundMask);
                        if (hitL.collider != null && hitL.normal.y >= snapMinNormalY) { grounded = true; col = hitL.collider; }

                        if (!grounded)
                        {
                            RaycastHit2D hitR = Physics2D.Raycast(new Vector2(origin.x + half, transform.position.y), Vector2.down, dist, groundMask);
                            if (hitR.collider != null && hitR.normal.y >= snapMinNormalY) { grounded = true; col = hitR.collider; }
                        }
                    }
                }
            }
        }

        isGrounded = grounded;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            lastGroundCol = col;
        }
        else if ((Time.time - lastGroundedTime) > coyoteTime)
        {
            lastGroundCol = null;
        }

        if (!wasGroundedLastFrame && isGrounded)
        {
            TryConsumeJumpBuffer();
        }

        if (lastGroundCol != null)
        {
            isOnIce = lastGroundCol.CompareTag("Ice");

            var eff = lastGroundCol.GetComponent<SurfaceEffector2D>();
            if (eff != null)
                platformVX = eff.speed;
            else
                platformVX = 0f;
        }
        else
        {
            isOnIce = false;
            platformVX = 0f;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleBounce(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleBounce(collision);
    }

    private void HandleBounce(Collision2D collision)
    {
        if (collision == null) return;
        if (Time.time - lastBounceTime < 0.02f) return;

        if (collision.contactCount <= 0) return;

        ContactPoint2D cp = collision.GetContact(0);
        Vector2 n = cp.normal;

        bool isWall = Mathf.Abs(n.x) >= wallNormalMinAbsX && n.y < 0.6f;
        bool isCeil = n.y <= -0.6f;

        if (!isWall && !isCeil) return;

        float absY = Mathf.Abs(rb.velocity.y);
        bool allowApex = (Time.time - lastJumpTime) <= wallBounceApexWindow;

        if (!allowApex && absY < wallBounceMinAbsY)
            return;

        float bounce = lastAppliedJumpForce * wallBounceFraction;
        if ((Time.time - lastJumpTime) > dampingExclusionTime)
            bounce *= damping;

        if (isWall)
        {
            float dir = Mathf.Sign(n.x);
            float bouncedVx = bounce * dir;

            rb.velocity = new Vector2(bouncedVx, rb.velocity.y);
            airVx = bouncedVx;
            lastBounceTime = Time.time;
        }
        else if (isCeil)
        {
            rb.velocity = new Vector2(rb.velocity.x, -Mathf.Abs(rb.velocity.y));
            airVx = rb.velocity.x - externalWindVX;
            lastBounceTime = Time.time;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.6f);

        if (useBoxGroundCheck)
        {
            Vector2 origin = (Vector2)transform.position + groundBoxOffset;
            Gizmos.DrawWireCube(origin, groundBoxSize);
        }
        else
        {
            Vector2 p = groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position;
            Gizmos.DrawWireSphere(p, groundCheckRadius);
        }

        if (useEdgeAssist)
        {
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.6f);

            Vector2 feetCenter = useBoxGroundCheck
                ? (Vector2)transform.position + groundBoxOffset
                : (groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position);

            float half = Mathf.Max(0.05f, edgeProbeHalfWidth);
            float y = feetCenter.y - 0.001f;
            Vector2 probeSize = new Vector2(half * 0.9f, edgeProbeHeight);

            Gizmos.DrawWireCube(new Vector3(feetCenter.x - half, y, 0f), new Vector3(probeSize.x, probeSize.y, 0f));
            Gizmos.DrawWireCube(new Vector3(feetCenter.x + half, y, 0f), new Vector3(probeSize.x, probeSize.y, 0f));

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            float gizSnap = Mathf.Min(snapProbeDistance, snapProbeDistanceMax);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * gizSnap);
            Gizmos.DrawLine(new Vector3(feetCenter.x - half, transform.position.y, 0f),
                            new Vector3(feetCenter.x - half, transform.position.y - gizSnap, 0f));
            Gizmos.DrawLine(new Vector3(feetCenter.x + half, transform.position.y, 0f),
                            new Vector3(feetCenter.x + half, transform.position.y - gizSnap, 0f));
        }
    }

    public void CancelJumpCharge()
    {
        if (!isJumpHoldActive && !isChargingJump) return;
        isJumpHoldActive = false;
        isChargingJump = false;
        currentHoldJumpSource = HoldJumpSource.None;
        UpdateJumpBar(0f);
    }

    // ==== API сугробов ====
    public void RegisterSnow(SnowdriftArea2D area, float moveMul, float jumpMul)
    {
        activeSnow[area] = (Mathf.Clamp01(moveMul), Mathf.Clamp01(jumpMul));
        RecalcSnow();
    }

    public void UnregisterSnow(SnowdriftArea2D area)
    {
        if (activeSnow.Remove(area)) RecalcSnow();
    }

    private void RecalcSnow()
    {
        if (activeSnow.Count == 0) { snowMoveMul = 1f; snowJumpMul = 1f; return; }
        float m = 1f, j = 1f;
        foreach (var kv in activeSnow.Values) { m = Mathf.Min(m, kv.move); j = Mathf.Min(j, kv.jump); }
        snowMoveMul = Mathf.Clamp01(m);
        snowJumpMul = Mathf.Clamp01(j);
    }

    public void SetInputEnabled(bool enabled)
    {
        gameplayInputEnabled = enabled;

        if (!enabled)
        {
            waitForGameplayInputRelease = false;
            gameplayInputUnlockAtUnscaled = -1f;
            ResetGameplayInputState(true);
            return;
        }

        ResetGameplayInputState(true);

        if (waitReleaseAfterInputEnable)
        {
            waitForGameplayInputRelease = true;
            gameplayInputUnlockAtUnscaled = Time.unscaledTime + Mathf.Max(0f, postMenuInputUnlockDelay);
        }
        else
        {
            waitForGameplayInputRelease = false;
            gameplayInputUnlockAtUnscaled = Time.unscaledTime + Mathf.Max(0f, postMenuInputUnlockDelay);
        }
    }

    private bool IsGameplayInputAllowedThisFrame()
    {
        if (!gameplayInputEnabled)
            return false;

        if (waitForGameplayInputRelease)
        {
            if (AreAnyGameplayInputsStillHeld())
                return false;

            waitForGameplayInputRelease = false;
            gameplayInputUnlockAtUnscaled = Time.unscaledTime + Mathf.Max(0f, postMenuInputUnlockDelay);
            return false;
        }

        if (Time.unscaledTime < gameplayInputUnlockAtUnscaled)
            return false;

        return true;
    }

    private bool AreAnyGameplayInputsStillHeld()
    {
        if (useMobileControls)
        {
            if (mobileJumpHeld)
                return true;

            if (mobileJoystick != null && Mathf.Abs(mobileJoystick.Horizontal) > inputReleaseAxisDeadZone)
                return true;

            return false;
        }

        var rebind = (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null) ? LegacyKeycodeRebind.I : null;

        if (rebind != null)
        {
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveLeft)) return true;
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.MoveRight)) return true;
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpHold)) return true;
            if (rebind.GetHeld(LegacyKeycodeRebind.Device.Keyboard, LegacyKeycodeRebind.Action.JumpShort)) return true;

            if (useGamepadJump)
            {
                if (rebind.GetHeld(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpHold)) return true;
                if (rebind.GetHeld(LegacyKeycodeRebind.Device.Gamepad, LegacyKeycodeRebind.Action.JumpShort)) return true;
            }
        }
        else
        {
            if (Input.GetKey(leftKey) || Input.GetKey(rightKey) || Input.GetKey(jumpKey))
                return true;

            if (useGamepadJump && (Input.GetKey(gamepadChargeJumpKey) || Input.GetKey(gamepadShortJumpKey)))
                return true;
        }

        if (useInputManagerAxisFallback && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > inputReleaseAxisDeadZone)
            return true;

        return false;
    }

    private void ResetGameplayInputState(bool clearMobileHold)
    {
        inputX = 0f;

        lastJumpPressedTime = -999f;
        bufferedJumpKind = BufferedJumpKind.None;
        bufferedHoldSource = HoldJumpSource.None;

        jumpButtonDownTime = 0f;
        jumpStartHoldTime = 0f;

        if (clearMobileHold)
            mobileJumpHeld = false;

        CancelJumpCharge();
    }


    // ==== Public API для JumpTrajectory2D ====

    public bool IsChargingJumpPublic
    {
        get
        {
            // Показываем траекторию, пока игрок держит кнопку прыжка:
            // до входа в заряд это будет короткий прыжок,
            // после входа в заряд — уже заряжаемый прыжок.
            return isJumpHoldActive || isChargingJump;
        }
    }

    public Vector2 GetPredictedJumpVelocity()
    {
        float speedMul = IsFatigued() ? fatigueSpeedMultiplier : 1f;

        float predictedVx =
            platformVX +
            (isFacingRight ? 1f : -1f) * moveSpeed * speedMul * snowMoveMul +
            externalWindVX;

        float predictedVy;

        if (!isChargingJump)
        {
            // Пока заряд ещё не начался — показываем слабый прыжок.
            predictedVy = shortJumpForce * snowJumpMul;
        }
        else
        {
            float hold = Mathf.Clamp(Time.time - jumpStartHoldTime, 0f, jumpTimeLimit);
            float normalized = Mathf.Clamp01(hold / jumpTimeLimit);
            predictedVy = normalized * maxJumpForce * snowJumpMul;
        }

        return new Vector2(predictedVx, predictedVy);
    }

    public float GetGravityScale()
    {
        return rb != null ? rb.gravityScale : 1f;
    }

}

public class PointerHoldHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public event Action OnDown;
    public event Action OnUp;

    private bool isPressed = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        OnDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ReleaseIfNeeded();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ReleaseIfNeeded();
    }

    private void OnDisable()
    {
        ReleaseIfNeeded();
    }

    private void ReleaseIfNeeded()
    {
        if (!isPressed)
            return;

        isPressed = false;
        OnUp?.Invoke();
    }
}