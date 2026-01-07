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

    [SerializeField, Tooltip("Койот-тайм: сколько секунд после схода с платформы ещё можно начать заряд прыжка.\nРекоменд: 0.05–0.15 (часто 0.08–0.12).")]
    private float coyoteTime = 0.05f;

    [Header("Отскок от стен/потолка")]
    [SerializeField, Range(0f, 1f), Tooltip("Доля силы заряда, превращаемая в отскок по X от стены.\n0.33 = 33% от силы заряда.\nРекоменд: 0.2–0.5 (часто 0.3–0.4).")]
    private float wallBounceFraction = 0.33f;

    [SerializeField, Tooltip("Демпфирование отскока (уменьшение силы), если прошло достаточно времени после прыжка.\n1 = без демпфа, 0.5 = в 2 раза слабее.\nРекоменд: 0.4–0.8 (часто 0.5–0.7).")]
    private float damping = 0.5f;

    [SerializeField, Tooltip("Окно после прыжка, в которое демпфирование не применяется (чтобы отскок сразу после прыжка был бодрее).\nРекоменд: 0.1–0.3 сек (часто 0.15–0.25).")]
    private float dampingExclusionTime = 0.2f;

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

    // (в инспекторе не нужен) когда до какого времени разрешён air control
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

    // =========================
    // INTERNAL STATE (не в инспекторе)
    // =========================
    private Rigidbody2D rb;
    private bool isFacingRight = true;
    private bool isGrounded = false;
    private float lastGroundedTime = -999f;

    private float inputX = 0f;
    private bool isChargingJump = false;
    private float jumpStartHoldTime = 0f;
    private bool mobileJumpHeld = false;

    private float airVx = 0f;
    private float lastJumpTime = -999f;
    private float jumpStartSpeed = 0f;
    private float fatigueEndTime = 0f;

    [Header("Takeoff / GroundCheck Locks")]
    [SerializeField, Tooltip("Время после прыжка, когда считаем персонажа 'в воздухе' и ограничиваем приземление/переворот логики.\nУбирает залипание к земле в момент старта прыжка.\nРекоменд: 0.05–0.12 (часто 0.06–0.10).")]
    private float takeoffLockTime = 0.08f;

    private float takeoffLockUntil = 0f;
    private float groundCheckDisableUntil = 0f;

    private bool prevUseMobileControls;
    private bool isOnIce = false;
    private Collider2D lastGroundCol = null;

    // Moving Platform
    private float platformVX = 0f;
    private MovingPlatform2D currentPlatform = null;

    // Сугробы (замедление/ослабление прыжка)
    private float snowMoveMul = 1f;
    private float snowJumpMul = 1f;
    private readonly Dictionary<SnowdriftArea2D, (float move, float jump)> activeSnow = new();

    // ==== ВЕТЕР: аддитивная внешняя скорость по X (зона ветра добавляет Δv за кадр) ====
    private float externalWindVX = 0f;
    public void AddExternalWindVX(float vx) { externalWindVX += vx; }
    // ================================================================================

    // ==== ПУБЛИЧНЫЙ API ДЛЯ ОТРИСОВКИ ТРАЕКТОРИИ ПРЫЖКА ====
    public bool IsChargingJumpPublic => isChargingJump;
    public float GetGravityScale() => rb ? rb.gravityScale : 1f;

    public Vector2 GetPredictedJumpVelocity()
    {
        float hold = Mathf.Clamp(Time.time - jumpStartHoldTime, 0f, jumpTimeLimit);
        float verticalForce = Mathf.Clamp01(hold / jumpTimeLimit) * maxJumpForce * snowJumpMul;

        float speedMul = IsFatigued() ? fatigueSpeedMultiplier : 1f;
        float takeoffVx = platformVX + (isFacingRight ? 1f : -1f) * moveSpeed * speedMul * snowMoveMul;

        return new Vector2(takeoffVx, verticalForce);
    }
    // ================================================================================

    private void Awake() => rb = GetComponent<Rigidbody2D>();

    private void Start()
    {
        if (mobileJumpButton != null)
        {
            var hold = mobileJumpButton.gameObject.GetComponent<PointerHoldHandler>();
            if (hold == null) hold = mobileJumpButton.gameObject.AddComponent<PointerHoldHandler>();
            hold.OnDown += OnMobileJumpDown;
            hold.OnUp += OnMobileJumpUp;
        }

        UpdateJumpBar(0f);
        SetFatigueImageActive(false, 0f);

        prevUseMobileControls = !useMobileControls;
        ApplyMobileUIVisibility();
    }

    private void OnValidate() => ApplyMobileUIVisibility();

    private void Update()
    {
        if (prevUseMobileControls != useMobileControls)
            ApplyMobileUIVisibility();

        if (!useMobileControls) HandleDesktopInput();
        else HandleMobileInput();

        UpdateJumpBarPosition();
        UpdateFatigueUI();
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        ApplyMovement();

        // Сбрасываем внешний ветер после применения — зона задаст новый в свой FixedUpdate
        externalWindVX = 0f;
    }

    private void HandleDesktopInput()
    {
        int dir = 0;
        if (Input.GetKey(leftKey)) dir -= 1;
        if (Input.GetKey(rightKey)) dir += 1;
        inputX = (dir != 0) ? dir : Input.GetAxisRaw("Horizontal");

        if (Mathf.Abs(inputX) > 0.01f && IsGroundMovementAllowed())
        {
            bool faceRight = inputX > 0f;
            if (faceRight != isFacingRight) Flip();
        }

        bool canStartCharge = CanStartJumpCharge();

        if (Input.GetKeyDown(jumpKey) && canStartCharge)
            BeginJumpCharge();

        if (Input.GetKey(jumpKey) && isChargingJump)
            ContinueJumpCharge();

        if (Input.GetKeyUp(jumpKey) && isChargingJump)
            ReleaseJumpChargeAndJump();
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
            if (!isChargingJump && CanStartJumpCharge())
                BeginJumpCharge();

            if (isChargingJump)
                ContinueJumpCharge();
        }
        else
        {
            if (isChargingJump)
                ReleaseJumpChargeAndJump();
        }
    }

    private bool CanStartJumpCharge()
    {
        bool groundedOrCoyote = isGrounded || (Time.time - lastGroundedTime) <= coyoteTime;
        return groundedOrCoyote && !IsFatigued();
    }

    private void BeginJumpCharge()
    {
        isChargingJump = true;
        jumpStartHoldTime = Time.time;

        if (Mathf.Abs(inputX) > 0.01f && IsGroundMovementAllowed())
        {
            bool faceRight = inputX > 0f;
            if (faceRight != isFacingRight) Flip();
        }
    }

    private void ContinueJumpCharge()
    {
        // Потеряли землю и «койот» истёк — отменяем заряд и скрываем UI
        if (!isGrounded && (Time.time - lastGroundedTime) > coyoteTime)
        {
            CancelJumpCharge();
            return;
        }

        float hold = Mathf.Clamp(Time.time - jumpStartHoldTime, 0f, jumpTimeLimit);
        float normalized = hold / jumpTimeLimit;
        UpdateJumpBar(normalized);
    }

    private void ReleaseJumpChargeAndJump()
    {
        if (!isGrounded)
        {
            isChargingJump = false;
            UpdateJumpBar(0f);
            return;
        }

        isChargingJump = false;

        float hold = Mathf.Clamp(Time.time - jumpStartHoldTime, 0f, jumpTimeLimit);
        float verticalForce = Mathf.Clamp01(hold / jumpTimeLimit) * maxJumpForce * snowJumpMul;

        float speedMul = IsFatigued() ? fatigueSpeedMultiplier : 1f;
        float takeoffVx = platformVX + (isFacingRight ? 1f : -1f) * moveSpeed * speedMul * snowMoveMul;

        jumpStartSpeed = takeoffVx;
        PerformJump(takeoffVx, verticalForce);
        UpdateJumpBar(0f);
        StartFatigue();
    }

    private void PerformJump(float horizontalSpeed, float verticalForce)
    {
        lastJumpTime = Time.time;

        airVx = horizontalSpeed;
        rb.velocity = new Vector2(horizontalSpeed, verticalForce);

        isGrounded = false;
        takeoffLockUntil = Time.time + takeoffLockTime;
        groundCheckDisableUntil = Time.time + takeoffLockTime;
    }

    // Внешние пинки (поршни/ветер/пар)
    public void ExternalPistonLaunch(Vector2 dir, float force, bool resetAlongBefore)
    {
        if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;
        dir = dir.normalized;

        Vector2 v = rb.velocity;
        float vAlong = Vector2.Dot(v, dir);
        Vector2 vOrtho = v - vAlong * dir;

        if (resetAlongBefore) vAlong = 0f;

        float finalAlong = force;
        rb.velocity = vOrtho + dir * finalAlong;

        isGrounded = false;
        takeoffLockUntil = Time.time + 0.08f;
        groundCheckDisableUntil = Time.time + 0.08f;

        airVx = rb.velocity.x;
        lastJumpTime = Time.time;

        isChargingJump = false;
        UpdateJumpBar(0f);
    }

    public void AllowAirControlFor(float seconds)
    {
        airControlUnlockUntil = Mathf.Max(airControlUnlockUntil, Time.time + Mathf.Max(0f, seconds));
    }

    private void ApplyMovement()
    {
        // Во время зарядки
        if (isChargingJump)
        {
            bool onMovingGroundByEffector = isGrounded && lastGroundCol && lastGroundCol.GetComponent<SurfaceEffector2D>() != null;
            bool carriedByPlatform = isGrounded && Mathf.Abs(platformVX) > 0.0001f;

            if (isGrounded && !isOnIce && !onMovingGroundByEffector && !carriedByPlatform)
            {
                // фиксируемся по X, но ветер всё равно дует
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
            if (Time.time < airControlUnlockUntil)
            {
                float speedMul = (IsFatigued() ? fatigueSpeedMultiplier : 1f) * snowMoveMul;
                float vx = inputX * airControlSpeed * speedMul + externalWindVX;
                rb.velocity = new Vector2(vx, rb.velocity.y);

                if (Mathf.Abs(vx) > 0.01f)
                {
                    bool faceRight = vx > 0f;
                    if (faceRight != isFacingRight) Flip();
                }
            }
            else
            {
                rb.velocity = new Vector2(airVx + externalWindVX, rb.velocity.y);
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

            // перенос от платформы + ветер
            newVx += platformVX + externalWindVX;

            rb.velocity = new Vector2(newVx, rb.velocity.y);

            if (Mathf.Abs(newVx) > 0.01f)
            {
                bool faceRight = newVx > 0f;
                if (faceRight != isFacingRight) Flip();
            }
        }
    }

    private bool IsAirborne() => !isGrounded || Time.time < takeoffLockUntil;
    private bool IsGroundMovementAllowed() => isGrounded && Time.time >= takeoffLockUntil;

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        var s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }

    // ==== Grounded с Edge-Assist ====
    private void CheckGrounded()
    {
        if (Time.time < groundCheckDisableUntil)
        {
            isGrounded = false;
            currentPlatform = null;
            platformVX = 0f;
            return;
        }

        Collider2D groundCol = null;

        // 1) Базовый GroundCheck
        if (useBoxGroundCheck)
        {
            Vector2 center = (Vector2)transform.TransformPoint(groundBoxOffset);
            Vector2 size = new Vector2(
                groundBoxSize.x * Mathf.Abs(transform.lossyScale.x),
                groundBoxSize.y * Mathf.Abs(transform.lossyScale.y)
            );
            groundCol = Physics2D.OverlapBox(center, size, 0f, groundMask);
        }
        else
        {
            if (groundCheck != null)
                groundCol = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
        }

        bool grounded = groundCol != null;

        // 2) Узкие зонды под краями стоп
        if (!grounded && useEdgeAssist)
        {
            Vector2 feetCenter = (useBoxGroundCheck)
                ? (Vector2)transform.TransformPoint(groundBoxOffset)
                : (groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position);

            float width = (useBoxGroundCheck ? groundBoxSize.x : groundCheckRadius * 2f) * Mathf.Abs(transform.lossyScale.x);

            float half = Mathf.Max(0.05f, edgeProbeHalfWidth);
            float y = feetCenter.y - 0.001f;
            Vector2 leftPos = new Vector2(feetCenter.x - half, y);
            Vector2 rightPos = new Vector2(feetCenter.x + half, y);
            Vector2 probeSize = new Vector2(Mathf.Min(half * 0.9f, width * 0.5f), edgeProbeHeight);

            Collider2D edgeCol = Physics2D.OverlapBox(leftPos, probeSize, 0f, groundMask);
            if (!edgeCol)
                edgeCol = Physics2D.OverlapBox(rightPos, probeSize, 0f, groundMask);

            if (edgeCol != null)
            {
                grounded = true;
                groundCol = edgeCol;
            }
        }

        // 3) Снап-лучи вниз (центр/лево/право)
        if (!grounded && useEdgeAssist && snapProbeDistance > 0.001f)
        {
            int hits = 0;
            RaycastHit2D hCenter = Physics2D.Raycast(transform.position, Vector2.down, snapProbeDistance, groundMask);
            if (hCenter && hCenter.normal.y >= snapMinNormalY) { groundCol = hCenter.collider; hits++; }

            if (hits == 0)
            {
                Vector2 feetCenter = (useBoxGroundCheck)
                    ? (Vector2)transform.TransformPoint(groundBoxOffset)
                    : (groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position);

                float half = Mathf.Max(0.05f, edgeProbeHalfWidth);
                Vector2 left = new Vector2(feetCenter.x - half, transform.position.y);
                Vector2 right = new Vector2(feetCenter.x + half, transform.position.y);

                RaycastHit2D hL = Physics2D.Raycast(left, Vector2.down, snapProbeDistance, groundMask);
                if (hL && hL.normal.y >= snapMinNormalY) { groundCol = hL.collider; hits++; }

                if (hits == 0)
                {
                    RaycastHit2D hR = Physics2D.Raycast(right, Vector2.down, snapProbeDistance, groundMask);
                    if (hR && hR.normal.y >= snapMinNormalY) { groundCol = hR.collider; hits++; }
                }
            }

            if (hits > 0) grounded = true;
        }

        isGrounded = grounded;
        lastGroundCol = groundCol;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            airVx = 0f;
            airControlUnlockUntil = 0f;

            isOnIce = lastGroundCol != null && lastGroundCol.CompareTag("Ice");

            currentPlatform = lastGroundCol ? lastGroundCol.GetComponentInParent<MovingPlatform2D>() : null;
            platformVX = (currentPlatform != null && currentPlatform.parentRider) ? currentPlatform.FrameVelocity.x : 0f;

            if (!Input.GetKey(jumpKey)) UpdateJumpBar(0f);
        }
        else
        {
            isOnIce = false;
            currentPlatform = null;
            platformVX = 0f;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            isChargingJump = false;
        }
        else if (collision.gameObject.CompareTag("Wall") && !isGrounded)
        {
            Vector3 contactNormal = collision.contacts[0].normal;
            if (Mathf.Abs(contactNormal.x) > 0.9f)
            {
                float jumpForce = Mathf.Clamp01((Time.time - jumpStartHoldTime) / jumpTimeLimit) * maxJumpForce;
                BounceOffWall(jumpForce, jumpStartSpeed);
            }
        }
        else if (collision.gameObject.CompareTag("Ceiling"))
        {
            BounceOffCeiling();
        }
    }

    private void BounceOffWall(float jumpForce, float jumpStartSpeedParam)
    {
        float currentDamping = (Time.time - lastJumpTime < dampingExclusionTime) ? 1f : damping;
        float wallBounceForce = jumpForce * wallBounceFraction * Mathf.Sign(jumpStartSpeedParam) * currentDamping;

        float newVx = (isFacingRight ? -1f : 1f) * wallBounceForce;

        rb.velocity = new Vector2(newVx, rb.velocity.y);
        airVx = newVx;

        if (Mathf.Abs(newVx) > 0.01f)
        {
            bool faceRight = newVx > 0f;
            if (faceRight != isFacingRight) Flip();
        }

        takeoffLockUntil = Time.time + 0.02f;
        groundCheckDisableUntil = Time.time + 0.02f;
    }

    private void BounceOffCeiling()
    {
        rb.velocity = new Vector2(rb.velocity.x, -Mathf.Abs(rb.velocity.y));
    }

    private void UpdateJumpBar(float normalized)
    {
        bool show = normalized > 0f;

        if (jumpBarFill != null)
        {
            jumpBarFill.enabled = show;
            jumpBarFill.fillAmount = Mathf.Clamp01(normalized);
        }
        if (jumpBarBG != null)
            jumpBarBG.enabled = show;
    }

    private void UpdateJumpBarPosition()
    {
        if ((jumpBarFill == null && jumpBarBG == null) || mainCamera == null || uiCanvas == null)
            return;

        Vector3 worldPos = transform.position + barOffset;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        RectTransform canvasRect = uiCanvas.transform as RectTransform;
        if (canvasRect == null) return;

        Vector2 uiPos;
        Camera camForUI = (uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : uiCanvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, camForUI, out uiPos);

        if (jumpBarFill != null)
            jumpBarFill.rectTransform.anchoredPosition = uiPos;
        if (jumpBarBG != null)
            jumpBarBG.rectTransform.anchoredPosition = uiPos;
    }

    private void UpdateFatigueUI()
    {
        if (fatigueImage == null) return;

        if (IsFatigued())
        {
            float total = Mathf.Max(fatigueDuration, 0.0001f);
            float timeLeft = Mathf.Clamp(fatigueEndTime - Time.time, 0f, total);
            float normalized = timeLeft / total;
            SetFatigueImageActive(true, normalized);
        }
        else
        {
            SetFatigueImageActive(false, 0f);
        }
    }

    private void SetFatigueImageActive(bool active, float alpha01)
    {
        var go = fatigueImage.gameObject;
        if (go.activeSelf != active) go.SetActive(active);

        var c = fatigueImage.color;
        c.a = Mathf.Clamp01(alpha01);
        fatigueImage.color = c;
    }

    private bool IsFatigued() => Time.time < fatigueEndTime;

    private void StartFatigue()
    {
        fatigueEndTime = Time.time + fatigueDuration;
        SetFatigueImageActive(true, 1f);
    }

    private void OnMobileJumpDown()
    {
        mobileJumpHeld = true;
        if (!isChargingJump && CanStartJumpCharge())
            BeginJumpCharge();
    }

    private void OnMobileJumpUp()
    {
        mobileJumpHeld = false;
        if (isChargingJump)
            ReleaseJumpChargeAndJump();
    }

    private void ApplyMobileUIVisibility()
    {
        prevUseMobileControls = useMobileControls;

        bool showMobile = useMobileControls;
        if (mobileJoystick != null) mobileJoystick.gameObject.SetActive(showMobile);
        if (mobileJumpButton != null) mobileJumpButton.gameObject.SetActive(showMobile);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        if (useBoxGroundCheck)
        {
            Vector2 center = Application.isPlaying
                ? (Vector2)transform.TransformPoint(groundBoxOffset)
                : (Vector2)(transform.position + (Vector3)groundBoxOffset);

            Vector2 size = new Vector2(
                groundBoxSize.x * Mathf.Abs(transform.lossyScale.x),
                groundBoxSize.y * Mathf.Abs(transform.lossyScale.y)
            );

            Gizmos.DrawWireCube(center, size);
        }
        else
        {
            if (groundCheck != null)
                Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (useEdgeAssist)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.6f);
            Vector2 feetCenter = (useBoxGroundCheck)
                ? (Vector2)transform.TransformPoint(groundBoxOffset)
                : (groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position);

            float half = Mathf.Max(0.05f, edgeProbeHalfWidth);
            float y = feetCenter.y - 0.001f;
            Vector2 probeSize = new Vector2(half * 0.9f, edgeProbeHeight);

            Gizmos.DrawWireCube(new Vector3(feetCenter.x - half, y, 0f), new Vector3(probeSize.x, probeSize.y, 0f));
            Gizmos.DrawWireCube(new Vector3(feetCenter.x + half, y, 0f), new Vector3(probeSize.x, probeSize.y, 0f));

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * snapProbeDistance);
            Gizmos.DrawLine(new Vector3(feetCenter.x - half, transform.position.y, 0f),
                            new Vector3(feetCenter.x - half, transform.position.y - snapProbeDistance, 0f));
            Gizmos.DrawLine(new Vector3(feetCenter.x + half, transform.position.y, 0f),
                            new Vector3(feetCenter.x + half, transform.position.y - snapProbeDistance, 0f));
        }
    }

    public void CancelJumpCharge()
    {
        if (!isChargingJump) return;
        isChargingJump = false;
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

    public void SetInputEnabled(bool enabled) { }
}

/// <summary>
/// Лёгкий обработчик удержания для UI-кнопки (без EventTrigger).
/// В инспекторе почти не настраивается — просто добавляется на кнопку прыжка автоматически.
/// </summary>
public class PointerHoldHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public event Action OnDown;
    public event Action OnUp;

    public void OnPointerDown(PointerEventData eventData) => OnDown?.Invoke();
    public void OnPointerUp(PointerEventData eventData) => OnUp?.Invoke();
}
