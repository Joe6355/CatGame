using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // ===============================
    //            ПАРАМЕТРЫ
    // ===============================

    [Header("Движение")]
    [SerializeField] private float moveSpeed = 5f;                // Базовая скорость бега по земле
    [SerializeField] private float fatigueSpeedMultiplier = 0.6f; // Во время усталости скорость умножается на это

    [Header("Прыжок (заряд)")]
    [SerializeField] private float maxJumpForce = 20f;     // Максимальная сила прыжка по вертикали
    [SerializeField] private float jumpTimeLimit = 1f;     // Максимальное время удержания для заряда
    [SerializeField] private float coyoteTime = 0.05f;     // Небольшой запас после схода с земли

    [Header("Отскок от стен/потолка (как в первой версии)")]
    [SerializeField, Range(0f, 1f)] private float wallBounceFraction = 0.33f;
    [SerializeField] private float damping = 0.5f;               // Затухание отскока
    [SerializeField] private float dampingExclusionTime = 0.2f;  // Без затухания сразу после прыжка

    [Header("Усталость (анти-спам)")]
    [SerializeField] private float fatigueDuration = 0.8f; // Сколько длится усталость после прыжка
    [SerializeField] private Image fatigueImage;           // Индикатор усталости (GameObject включается/выключается), альфа гаснет 1→0

    [Header("Назначение клавиш (PC)")]
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    // ======= НОВЫЙ GroundCheck с размерами =======
    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;

    [Tooltip("Использовать прямоугольный GroundCheck (OverlapBox). Если выкл — используется круг.")]
    [SerializeField] private bool useBoxGroundCheck = true;

    [Tooltip("Размеры прямоугольника GroundCheck (ширина, высота) в мировых единицах.")]
    [SerializeField] private Vector2 groundBoxSize = new Vector2(0.6f, 0.12f);

    [Tooltip("Смещение центра прямоугольника относительно Transform игрока (в локальных координатах). Обычно чуть ниже центра спрайта.")]
    [SerializeField] private Vector2 groundBoxOffset = new Vector2(0f, -0.2f);

    [Tooltip("Если используете круглый GroundCheck: центр")]
    [SerializeField] private Transform groundCheck;          // Пустышка под ногами (для кругового режима)
    [Tooltip("Если используете круглый GroundCheck: радиус")]
    [SerializeField] private float groundCheckRadius = 0.12f;

    [Header("UI шкалы прыжка")]
    [SerializeField] private Image jumpBarFill;              // Заполняемая часть (Image.type=Filled)
    [SerializeField] private Image jumpBarBG;                // Фон
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Vector3 barOffset = new Vector3(0f, 2f, 0f);

    [Header("Мобильное управление")]
    [Tooltip("ВЫКЛ = ПК (клавиатура). ВКЛ = мобильное (джойстик+кнопка).")]
    [SerializeField] private bool useMobileControls = false;
    [SerializeField] private Joystick mobileJoystick;        // Любой Joystick-скрипт (объект на Canvas)
    [SerializeField] private Button mobileJumpButton;        // Кнопка прыжка (удержание)

    // ===============================
    //           СЛУЖЕБНЫЕ
    // ===============================

    private Rigidbody2D rb;
    private bool isFacingRight = true;

    private bool isGrounded = false;
    private float lastGroundedTime = -999f; // для coyote-time

    private float inputX = 0f;            // желаемое направление по X (на земле)
    private bool isChargingJump = false;  // идёт заряд прыжка
    private float jumpStartHoldTime = 0f; // начало удержания кнопки прыжка
    private bool mobileJumpHeld = false;  // удержание мобильной кнопки

    private float airVx = 0f;                 // зафиксированная скорость по X на весь полёт
    private float lastJumpTime = -999f;       // время последнего прыжка (для dampingExclusionTime)

    // === как в первой версии ===
    private float jumpStartSpeed = 0f;        // горизонтальная скорость в момент прыжка (для знака в отскоке)

    private float fatigueEndTime = 0f;

    // "лок" после отрыва (защита толчка)
    [SerializeField] private float takeoffLockTime = 0.08f;
    private float takeoffLockUntil = 0f;
    private float groundCheckDisableUntil = 0f;

    // авто-скрытие UI мобилы
    private bool prevUseMobileControls;

    // ===============================
    //           ЖИЗНЕННЫЙ ЦИКЛ
    // ===============================

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

        prevUseMobileControls = !useMobileControls; // форсируем
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
    }

    // ===============================
    //        ОБРАБОТКА ВВОДА
    // ===============================

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

    // ===============================
    //         ЛОГИКА ПРЫЖКА
    // ===============================

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
        float verticalForce = CalculateJumpForce(hold);

        float speedMul = IsFatigued() ? fatigueSpeedMultiplier : 1f;
        float takeoffVx = (isFacingRight ? 1f : -1f) * moveSpeed * speedMul;

        jumpStartSpeed = takeoffVx;
        PerformJump(takeoffVx, verticalForce);
        UpdateJumpBar(0f);
        StartFatigue();
    }

    private float CalculateJumpForce(float duration)
    {
        return Mathf.Clamp01(duration / jumpTimeLimit) * maxJumpForce;
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

    // ===============================
    //        ДВИЖЕНИЕ ЗЕМЛЯ/ВОЗДУХ
    // ===============================

    private void ApplyMovement()
    {
        if (isChargingJump)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
            return;
        }

        if (IsAirborne())
        {
            rb.velocity = new Vector2(airVx, rb.velocity.y); // в полёте X фиксирован
        }
        else
        {
            float speedMul = IsFatigued() ? fatigueSpeedMultiplier : 1f;
            float vx = inputX * moveSpeed * speedMul;
            rb.velocity = new Vector2(vx, rb.velocity.y);

            if (Mathf.Abs(vx) > 0.01f)
            {
                bool faceRight = vx > 0f;
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

    // ===============================
    //        СТОЛКНОВЕНИЯ/ГРАУНД
    // ===============================

    private void CheckGrounded()
    {
        if (Time.time < groundCheckDisableUntil)
        {
            isGrounded = false;
            return;
        }

        bool hit = false;

        if (useBoxGroundCheck)
        {
            // Прямоугольная зона ног
            Vector2 center = (Vector2)transform.TransformPoint(groundBoxOffset);
            Vector2 size = new Vector2(
                groundBoxSize.x * Mathf.Abs(transform.lossyScale.x),
                groundBoxSize.y * Mathf.Abs(transform.lossyScale.y)
            );

            // Прямоугольник осево-выравненный: угол = 0 (стабильно при флипе по scale.x)
            hit = Physics2D.OverlapBox(center, size, 0f, groundMask);
        }
        else
        {
            // Круг как раньше
            if (groundCheck != null)
            {
                hit = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
            }
        }

        isGrounded = hit;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            airVx = 0f;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Точно как в первой версии
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
                float jumpForce = CalculateJumpForce(lastJumpTime - jumpStartHoldTime);
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

    // ===============================
    //               UI
    // ===============================

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
            float normalized = timeLeft / total; // 1..0
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

    // ===============================
    //            УСТАЛОСТЬ
    // ===============================

    private bool IsFatigued() => Time.time < fatigueEndTime;

    private void StartFatigue()
    {
        fatigueEndTime = Time.time + fatigueDuration;
        SetFatigueImageActive(true, 1f);
    }

    // ===============================
    //     МОБИЛЬНАЯ КНОПКА + UI
    // ===============================

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

    // ===============================
    //            ГИЗМОСЫ
    // ===============================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        if (useBoxGroundCheck)
        {
            // Рисуем прямоугольный GroundCheck
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
            // Круглый GroundCheck
            if (groundCheck != null)
                Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

    // Позвать, если внешний объект (поршень и т.п.) подбросил игрока во время зарядки
    public void CancelJumpCharge()
    {
        if (!isChargingJump) return;
        isChargingJump = false;
        UpdateJumpBar(0f);
    }

}

/// <summary>
/// Лёгкий обработчик удержания для UI-кнопки (без EventTrigger).
/// Просто навесь этот компонент на GameObject кнопки ИЛИ он добавится автоматически из кода.
/// </summary>
public class PointerHoldHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public event Action OnDown;
    public event Action OnUp;

    public void OnPointerDown(PointerEventData eventData) => OnDown?.Invoke();
    public void OnPointerUp(PointerEventData eventData) => OnUp?.Invoke();
}
