using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PlayerBounceModule : MonoBehaviour
{
    private enum WallSide
    {
        None,
        Left,
        Right
    }

    public struct WallJumpResult
    {
        public bool DidJump;
        public float TakeoffVx;
    }

    [Header("Ссылки")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D bodyCollider;

    [Header("Wall Slide + Wall Jump")]
    [SerializeField, Tooltip("Если ВКЛ — модуль включает скольжение по стенам и wall jump. Старый bounce по коллизиям больше не используется.")]
    private bool enableWallSlideAndJump = true;

    [SerializeField, Tooltip("Слои, которые считаются wall-jump стенами.")]
    private LayerMask wallMask;

    [SerializeField, Min(0.01f), Tooltip("Насколько далеко влево/вправо от коллайдера игрока проверять стену.")]
    private float wallCheckDistance = 0.12f;

    [SerializeField, Range(0f, 1f), Tooltip("Минимальная |normal.x|, чтобы поверхность считалась боковой стеной.")]
    private float wallNormalMinAbsX = 0.45f;

    [SerializeField, Range(0f, 1f), Tooltip("Мёртвая зона горизонтального ввода для логики стен.")]
    private float wallInputDeadZone = 0.08f;

    [Header("Wall Slide")]
    [SerializeField, Tooltip("Если ВКЛ — slide включается только когда игрок жмёт в сторону стены.")]
    private bool requireInputTowardWallForSlide = true;

    [SerializeField, Min(0f), Tooltip("Если игрок ещё летит вверх быстрее этого значения, slide не включаем.")]
    private float wallSlideEnterMaxUpwardSpeed = 1.25f;

    [SerializeField, Min(0f), Tooltip("Максимальная скорость падения вниз во время wall slide.")]
    private float wallSlideMaxFallSpeed = 2.75f;

    [Header("Wall Drop Down")]
    [SerializeField, Tooltip("Если ВКЛ — во время wall slide можно нажать S / стик вниз, чтобы сорваться со стены и начать падать вниз.")]
    private bool enableWallSlideDropDown = true;

    [SerializeField, Min(0f), Tooltip("Сколько секунд после wall drop нельзя снова прилипнуть к этой же стене.")]
    private float wallSlideDropSuppressTime = 0.18f;

    [SerializeField, Min(0f), Tooltip("Минимальная скорость падения вниз, которая будет задана при wall drop.")]
    private float wallSlideDropMinFallSpeed = 6f;

    [Header("Wall Jump")]
    [SerializeField, Min(0f), Tooltip("Память стены после потери контакта. В течение этого окна можно всё ещё сделать wall jump.")]
    private float wallJumpMemoryTime = 0.12f;

    [SerializeField, Min(0f), Tooltip("Короткая блокировка повторного использования той же стены после wall jump.")]
    private float sameWallRegrabBlockTime = 0.14f;

    [SerializeField, Min(0f), Tooltip("Короткая пауза после wall jump, пока slide не включается обратно.")]
    private float wallSlideSuppressAfterJumpTime = 0.10f;

    [SerializeField, Tooltip("Если ВКЛ — для wall jump тоже нужно держать ввод в сторону стены.")]
    private bool requireInputTowardWallForJump = false;

    [SerializeField, Min(0f), Tooltip("Горизонтальная скорость wall jump.")]
    private float wallJumpHorizontalSpeed = 9.5f;

    [SerializeField, Min(0f), Tooltip("Вертикальная скорость wall jump.")]
    private float wallJumpVerticalSpeed = 12.5f;

    [SerializeField, Tooltip("Если ВКЛ — X после wall jump ставится жёстко.")]
    private bool overrideHorizontalSpeed = true;

    [SerializeField, Tooltip("Если ВКЛ — Y после wall jump ставится жёстко.")]
    private bool overrideVerticalSpeed = true;

    [Header("Debug")]
    [SerializeField, Tooltip("Рисовать gizmo проверки стены.")]
    private bool drawWallGizmos = true;

    [SerializeField, Tooltip("Если ВКЛ — gizmo рисуется всегда. Если ВЫКЛ — только при выделении.")]
    private bool drawWallGizmosAlways = false;

    [SerializeField, Tooltip("Если ВКЛ — писать в консоль вход/выход со стены, slide, wall jump и wall drop.")]
    private bool debugLogs = true;

    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[8];
    private ContactFilter2D wallFilter;

    private WallSide currentWallSide = WallSide.None;
    private Vector2 currentWallNormal = Vector2.zero;
    private Collider2D currentWallCollider = null;
    private float currentWallDistance = float.PositiveInfinity;

    private WallSide lastWallSide = WallSide.None;
    private Collider2D lastWallCollider = null;
    private float lastWallTouchTime = -999f;

    private WallSide blockedWallSide = WallSide.None;
    private float blockedWallSideUntil = -999f;
    private float slideSuppressedUntil = -999f;

    private bool isWallSliding = false;

    public bool IsWallSliding => isWallSliding;
    public bool HasWallContact => currentWallSide != WallSide.None;
    public bool CanDropFromWallSlide => enableWallSlideAndJump && enableWallSlideDropDown && isWallSliding;

    private void Reset()
    {
        CacheComponents();
        ConfigureWallFilter();
    }

    private void Awake()
    {
        CacheComponents();
        ConfigureWallFilter();
    }

    private void OnValidate()
    {
        CacheComponents();
        ConfigureWallFilter();

        wallCheckDistance = Mathf.Max(0.01f, wallCheckDistance);
        wallNormalMinAbsX = Mathf.Clamp01(wallNormalMinAbsX);
        wallInputDeadZone = Mathf.Clamp01(wallInputDeadZone);

        wallSlideEnterMaxUpwardSpeed = Mathf.Max(0f, wallSlideEnterMaxUpwardSpeed);
        wallSlideMaxFallSpeed = Mathf.Max(0f, wallSlideMaxFallSpeed);

        wallSlideDropSuppressTime = Mathf.Max(0f, wallSlideDropSuppressTime);
        wallSlideDropMinFallSpeed = Mathf.Max(0f, wallSlideDropMinFallSpeed);

        wallJumpMemoryTime = Mathf.Max(0f, wallJumpMemoryTime);
        sameWallRegrabBlockTime = Mathf.Max(0f, sameWallRegrabBlockTime);
        wallSlideSuppressAfterJumpTime = Mathf.Max(0f, wallSlideSuppressAfterJumpTime);

        wallJumpHorizontalSpeed = Mathf.Max(0f, wallJumpHorizontalSpeed);
        wallJumpVerticalSpeed = Mathf.Max(0f, wallJumpVerticalSpeed);
    }

    public void NotifyJumpImpulse(float now)
    {
        slideSuppressedUntil = Mathf.Max(
            slideSuppressedUntil,
            now + Mathf.Max(0f, wallSlideSuppressAfterJumpTime * 0.5f));

        isWallSliding = false;
    }

    public void RefreshWallState(float inputX, bool isGrounded, float now)
    {
        if (!enableWallSlideAndJump)
        {
            ClearCurrentWallContact(false);
            SetWallSliding(false, null);
            return;
        }

        CacheComponents();
        ConfigureWallFilter();

        WallSide previousSide = currentWallSide;
        Collider2D previousCollider = currentWallCollider;

        DetectCurrentWall();

        if (currentWallSide != WallSide.None)
        {
            lastWallSide = currentWallSide;
            lastWallCollider = currentWallCollider;
            lastWallTouchTime = now;
        }

        if (previousSide != currentWallSide || previousCollider != currentWallCollider)
            LogWallContactChange(previousSide, previousCollider, currentWallSide, currentWallCollider);

        bool shouldSlide = ShouldWallSlide(inputX, isGrounded, now);
        SetWallSliding(shouldSlide, currentWallCollider);
    }

    public void ApplyWallSlide(Rigidbody2D body, float inputX, bool isGrounded, float now)
    {
        if (!enableWallSlideAndJump || body == null)
            return;

        RefreshWallState(inputX, isGrounded, now);

        if (!isWallSliding)
            return;

        float minVy = -Mathf.Abs(wallSlideMaxFallSpeed);
        if (body.velocity.y < minVy)
            body.velocity = new Vector2(body.velocity.x, minVy);
    }

    public bool TryStartWallSlideDrop(float now)
    {
        if (!enableWallSlideAndJump || !enableWallSlideDropDown || !isWallSliding || rb == null)
            return false;

        WallSide dropSide = currentWallSide != WallSide.None ? currentWallSide : lastWallSide;
        Collider2D dropCollider = currentWallCollider != null ? currentWallCollider : lastWallCollider;

        float newVy = Mathf.Min(rb.velocity.y, -Mathf.Abs(wallSlideDropMinFallSpeed));
        rb.velocity = new Vector2(rb.velocity.x, newVy);

        if (dropSide != WallSide.None)
        {
            blockedWallSide = dropSide;
            blockedWallSideUntil = now + Mathf.Max(wallSlideDropSuppressTime, sameWallRegrabBlockTime);
        }

        slideSuppressedUntil = now + Mathf.Max(0f, wallSlideDropSuppressTime);

        isWallSliding = false;
        currentWallSide = WallSide.None;
        currentWallNormal = Vector2.zero;
        currentWallCollider = null;
        currentWallDistance = float.PositiveInfinity;

        lastWallSide = WallSide.None;
        lastWallCollider = null;
        lastWallTouchTime = -999f;

        if (debugLogs)
        {
            string colliderName = dropCollider != null ? dropCollider.name : "<none>";
            Debug.Log(
                $"[PlayerBounceModule] Wall drop down: side={dropSide}, collider={colliderName}, vy={newVy:0.###}",
                this);
        }

        return true;
    }

    public WallJumpResult TryPerformWallJump(
        float inputX,
        bool isGrounded,
        PlayerJumpModule jumpModule,
        PlayerMovementModule movementModule,
        float externalWindVX,
        float now)
    {
        WallJumpResult result = default;

        if (!enableWallSlideAndJump || rb == null)
            return result;

        RefreshWallState(inputX, isGrounded, now);

        if (isGrounded)
            return result;

        WallSide sourceWall = ResolveWallJumpSource(inputX, now);
        if (sourceWall == WallSide.None)
            return result;

        if (sourceWall == blockedWallSide && now < blockedWallSideUntil)
        {
            if (debugLogs)
            {
                Debug.Log(
                    $"[PlayerBounceModule] Wall jump blocked: same wall lock. side={sourceWall}, remaining={blockedWallSideUntil - now:0.000}s",
                    this);
            }

            return result;
        }

        if (requireInputTowardWallForJump && !IsInputTowardWall(inputX, sourceWall))
            return result;

        float jumpDir = sourceWall == WallSide.Left ? 1f : -1f;

        float localVx = rb.velocity.x - externalWindVX;
        float newLocalVx = overrideHorizontalSpeed
            ? jumpDir * Mathf.Abs(wallJumpHorizontalSpeed)
            : localVx + jumpDir * Mathf.Abs(wallJumpHorizontalSpeed);

        float newVy = overrideVerticalSpeed
            ? Mathf.Abs(wallJumpVerticalSpeed)
            : Mathf.Max(rb.velocity.y, Mathf.Abs(wallJumpVerticalSpeed));

        rb.velocity = new Vector2(newLocalVx + externalWindVX, newVy);
        rb.angularVelocity = 0f;

        movementModule?.TryFaceByInput(jumpDir, true, false);
        movementModule?.ResetSprint();
        jumpModule?.RegisterExternalJump(now, newVy, false, newLocalVx + externalWindVX, jumpDir > 0f);

        blockedWallSide = sourceWall;
        blockedWallSideUntil = now + Mathf.Max(0f, sameWallRegrabBlockTime);
        slideSuppressedUntil = now + Mathf.Max(0f, wallSlideSuppressAfterJumpTime);
        isWallSliding = false;

        if (debugLogs)
        {
            string wallName = lastWallCollider != null ? lastWallCollider.name : "<none>";
            Debug.Log(
                $"[PlayerBounceModule] Wall jump: side={sourceWall}, collider={wallName}, vx={newLocalVx:0.###}, vy={newVy:0.###}",
                this);
        }

        result.DidJump = true;
        result.TakeoffVx = newLocalVx;
        return result;
    }

    public void ResetWallState()
    {
        ClearCurrentWallContact(false);
        lastWallSide = WallSide.None;
        lastWallCollider = null;
        lastWallTouchTime = -999f;
        blockedWallSide = WallSide.None;
        blockedWallSideUntil = -999f;
        slideSuppressedUntil = -999f;
        isWallSliding = false;
    }

    public void ResetBounceState()
    {
        ResetWallState();
    }

    private bool ShouldWallSlide(float inputX, bool isGrounded, float now)
    {
        if (!enableWallSlideAndJump)
            return false;

        if (rb == null)
            return false;

        if (isGrounded)
            return false;

        if (currentWallSide == WallSide.None)
            return false;

        if (now < slideSuppressedUntil)
            return false;

        if (currentWallSide == blockedWallSide && now < blockedWallSideUntil)
            return false;

        if (rb.velocity.y > wallSlideEnterMaxUpwardSpeed)
            return false;

        if (requireInputTowardWallForSlide && !IsInputTowardWall(inputX, currentWallSide))
            return false;

        return true;
    }

    private WallSide ResolveWallJumpSource(float inputX, float now)
    {
        if (currentWallSide != WallSide.None)
        {
            if (!requireInputTowardWallForJump || IsInputTowardWall(inputX, currentWallSide))
                return currentWallSide;
        }

        if (lastWallSide == WallSide.None)
            return WallSide.None;

        if (now - lastWallTouchTime > wallJumpMemoryTime)
            return WallSide.None;

        if (requireInputTowardWallForJump && !IsInputTowardWall(inputX, lastWallSide))
            return WallSide.None;

        return lastWallSide;
    }

    private bool IsInputTowardWall(float inputX, WallSide side)
    {
        if (Mathf.Abs(inputX) <= wallInputDeadZone)
            return false;

        if (side == WallSide.Left)
            return inputX < -wallInputDeadZone;

        if (side == WallSide.Right)
            return inputX > wallInputDeadZone;

        return false;
    }

    private void DetectCurrentWall()
    {
        ClearCurrentWallContact(false);

        if (bodyCollider == null)
            return;

        float bestDist = float.PositiveInfinity;
        RaycastHit2D bestHit = default;
        WallSide bestSide = WallSide.None;

        if (TryGetBestWallHit(Vector2.left, out RaycastHit2D leftHit))
        {
            bestHit = leftHit;
            bestDist = leftHit.distance;
            bestSide = WallSide.Left;
        }

        if (TryGetBestWallHit(Vector2.right, out RaycastHit2D rightHit))
        {
            if (bestSide == WallSide.None || rightHit.distance < bestDist)
            {
                bestHit = rightHit;
                bestDist = rightHit.distance;
                bestSide = WallSide.Right;
            }
        }

        if (bestSide == WallSide.None)
            return;

        currentWallSide = bestSide;
        currentWallNormal = bestHit.normal;
        currentWallCollider = bestHit.collider;
        currentWallDistance = bestHit.distance;
    }

    private bool TryGetBestWallHit(Vector2 direction, out RaycastHit2D bestHit)
    {
        bestHit = default;

        if (bodyCollider == null)
            return false;

        int hitCount = bodyCollider.Cast(
            direction,
            wallFilter,
            wallHits,
            Mathf.Max(0.01f, wallCheckDistance));

        float bestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = wallHits[i];
            if (hit.collider == null)
                continue;

            if (Mathf.Abs(hit.normal.x) < wallNormalMinAbsX)
                continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private void ClearCurrentWallContact(bool logExit)
    {
        if (logExit && currentWallSide != WallSide.None)
            LogWallContactChange(currentWallSide, currentWallCollider, WallSide.None, null);

        currentWallSide = WallSide.None;
        currentWallNormal = Vector2.zero;
        currentWallCollider = null;
        currentWallDistance = float.PositiveInfinity;
    }

    private void SetWallSliding(bool value, Collider2D slideCollider)
    {
        if (isWallSliding == value)
            return;

        isWallSliding = value;

        if (!debugLogs)
            return;

        if (isWallSliding)
        {
            string colliderName = slideCollider != null ? slideCollider.name : "<none>";
            Debug.Log(
                $"[PlayerBounceModule] Wall slide START on {currentWallSide}, collider={colliderName}",
                this);
        }
        else
        {
            Debug.Log("[PlayerBounceModule] Wall slide END", this);
        }
    }

    private void LogWallContactChange(
        WallSide previousSide,
        Collider2D previousCollider,
        WallSide newSide,
        Collider2D newCollider)
    {
        if (!debugLogs)
            return;

        if (previousSide != WallSide.None && newSide == WallSide.None)
        {
            string prevName = previousCollider != null ? previousCollider.name : "<none>";
            Debug.Log(
                $"[PlayerBounceModule] Wall contact EXIT: side={previousSide}, collider={prevName}",
                this);
            return;
        }

        if (newSide != WallSide.None)
        {
            string newName = newCollider != null ? newCollider.name : "<none>";
            Debug.Log(
                $"[PlayerBounceModule] Wall contact ENTER: side={newSide}, collider={newName}",
                this);
        }
    }

    private void CacheComponents()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();
    }

    private void ConfigureWallFilter()
    {
        wallFilter.useTriggers = false;
        wallFilter.useLayerMask = true;
        wallFilter.layerMask = wallMask;
        wallFilter.useNormalAngle = false;
    }

    private void OnDrawGizmos()
    {
        if (!drawWallGizmos || !drawWallGizmosAlways)
            return;

        DrawWallGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawWallGizmos || drawWallGizmosAlways)
            return;

        DrawWallGizmos();
    }

    private void DrawWallGizmos()
    {
        CacheComponents();
        if (bodyCollider == null)
            return;

        Bounds b = bodyCollider.bounds;
        float dist = Mathf.Max(0.01f, wallCheckDistance);

        Vector3 center = b.center;
        Vector3 leftBoxCenter = center + Vector3.left * (b.extents.x + dist * 0.5f);
        Vector3 rightBoxCenter = center + Vector3.right * (b.extents.x + dist * 0.5f);
        Vector3 castBoxSize = new Vector3(dist, b.size.y, 0.02f);

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireCube(center, b.size);

        Color leftColor = currentWallSide == WallSide.Left
            ? new Color(1f, 0.35f, 0.2f, 0.95f)
            : new Color(0.95f, 0.9f, 0.2f, 0.95f);

        Color rightColor = currentWallSide == WallSide.Right
            ? new Color(1f, 0.35f, 0.2f, 0.95f)
            : new Color(0.95f, 0.9f, 0.2f, 0.95f);

        Gizmos.color = leftColor;
        Gizmos.DrawWireCube(leftBoxCenter, castBoxSize);
        Gizmos.DrawLine(center, center + Vector3.left * (b.extents.x + dist));

        Gizmos.color = rightColor;
        Gizmos.DrawWireCube(rightBoxCenter, castBoxSize);
        Gizmos.DrawLine(center, center + Vector3.right * (b.extents.x + dist));

        if (Application.isPlaying)
        {
            if (lastWallSide != WallSide.None && Time.time - lastWallTouchTime <= wallJumpMemoryTime)
            {
                Gizmos.color = new Color(0.4f, 1f, 0.45f, 0.9f);
                Vector3 memoryPos = center + (lastWallSide == WallSide.Left ? Vector3.left : Vector3.right) * (b.extents.x + dist + 0.08f);
                Gizmos.DrawWireSphere(memoryPos, 0.05f);
            }

            if (isWallSliding)
            {
                Gizmos.color = new Color(0.2f, 1f, 1f, 0.9f);
                Gizmos.DrawLine(center + Vector3.up * b.extents.y, center + Vector3.down * b.extents.y);
            }
        }
    }

    private void OnDisable()
    {
        ResetWallState();
    }
}