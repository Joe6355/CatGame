using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerLedgeModule : MonoBehaviour
{
    private enum LedgeState
    {
        None,
        Hanging,
        Climbing
    }

    [Header("Ledge Hang")]
    [SerializeField, Tooltip("Если ВКЛ — игрок может автоматически цепляться за ручные ledge-триггеры.")]
    private bool enableLedgeHang = true;

    [SerializeField, Min(0f), Tooltip("Максимум секунд, сколько можно висеть на краю до авто-срыва.")]
    private float hangDuration = 2f;

    [SerializeField, Min(0f), Tooltip("Длительность подтягивания от точки hangPoint до standPoint.")]
    private float climbDuration = 0.18f;

    [SerializeField, Min(0f), Tooltip("Небольшой кулдаун после выхода с ledge, чтобы не было мгновенного повторного зацепа.")]
    private float regrabCooldown = 0.2f;

    [SerializeField, Tooltip("Зацеп разрешён только если вертикальная скорость не выше этого порога.")]
    private float maxCatchUpwardVelocity = 1.25f;

    [SerializeField, Min(0f), Tooltip("Минимальная горизонтальная скорость/намерение движения в сторону ledge, чтобы автозацеп вообще сработал.")]
    private float minApproachHorizontalSpeed = 0.05f;

    [SerializeField, Min(0f), Tooltip("Горизонтальный толчок от края при спрыге вниз.")]
    private float dropAwaySpeed = 1.5f;

    [SerializeField, Min(0f), Tooltip("Вертикальная скорость вниз при спрыге с края.")]
    private float dropDownSpeed = 2.5f;

    [SerializeField, Tooltip("Если ВКЛ — во время climb используется более мягкая кривая вместо линейного lerp.")]
    private bool useSmoothStepOnClimb = true;

    [SerializeField, Min(0f), Tooltip("Минимальное расстояние до точки, при котором не делаем лишний снап каждый тик.")]
    private float hangSnapEpsilon = 0.0005f;

    private Rigidbody2D rb;

    private LedgeState state = LedgeState.None;
    private ManualLedgePoint2D candidatePoint = null;
    private float candidateSqrDistance = float.MaxValue;

    private ManualLedgePoint2D currentPoint = null;
    private float hangUntil = -999f;
    private float regrabBlockedUntil = -999f;
    private float climbStartedAt = -999f;
    private Vector2 climbFrom = Vector2.zero;
    private Vector2 climbTo = Vector2.zero;

    private RigidbodyType2D savedBodyType;
    private RigidbodyInterpolation2D savedInterpolation;
    private float savedGravityScale;
    private CollisionDetectionMode2D savedCollisionDetection;
    private bool physicsOverrideActive = false;

    public bool IsActive => state != LedgeState.None;
    public bool BlocksStandardController => state == LedgeState.Hanging || state == LedgeState.Climbing;
    public bool IsHanging => state == LedgeState.Hanging;
    public bool IsClimbing => state == LedgeState.Climbing;
    public bool CurrentFaceRight => currentPoint != null ? currentPoint.PlayerFacesRightWhileHanging : true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnDisable()
    {
        ForceCancel();
    }

    public void ReportCandidateStay(ManualLedgePoint2D point)
    {
        if (!enableLedgeHang || point == null || !point.isActiveAndEnabled)
            return;

        Vector2 from = rb != null ? rb.position : (Vector2)transform.position;
        float sqr = (point.HangPointPosition - from).sqrMagnitude;

        if (candidatePoint == null || sqr < candidateSqrDistance)
        {
            candidatePoint = point;
            candidateSqrDistance = sqr;
        }
    }

    public void ReportCandidateExit(ManualLedgePoint2D point)
    {
        if (point == null)
            return;

        if (candidatePoint == point)
            ClearCandidate();

        if (currentPoint == point && state == LedgeState.Hanging)
            BeginDrop();
    }

    public void TickLedge(
        float intendedInputX,
        bool isGrounded,
        bool ledgeUpPressed,
        bool ledgeDownPressed,
        PlayerMovementModule movementModule,
        PlayerJumpModule jumpModule)
    {
        if (!enableLedgeHang)
            return;

        if (state == LedgeState.None)
        {
            TryAutoCatch(intendedInputX, isGrounded, movementModule, jumpModule);
            return;
        }

        if (movementModule != null)
            movementModule.TryFaceByInput(CurrentFaceRight ? 1f : -1f, true, true);

        if (state != LedgeState.Hanging)
            return;

        if (ledgeUpPressed)
        {
            BeginClimb(movementModule, jumpModule);
            return;
        }

        if (ledgeDownPressed || Time.time >= hangUntil)
            BeginDrop();
    }

    public bool ApplyLedgeMotion(Rigidbody2D body, PlayerMovementModule movementModule)
    {
        if (!enableLedgeHang || body == null)
            return false;

        switch (state)
        {
            case LedgeState.Hanging:
                {
                    if (currentPoint == null)
                    {
                        ForceCancel();
                        return false;
                    }

                    if (movementModule != null)
                        movementModule.TryFaceByInput(CurrentFaceRight ? 1f : -1f, true, true);

                    Vector2 target = currentPoint.HangPointPosition;
                    body.velocity = Vector2.zero;
                    body.angularVelocity = 0f;

                    if (((Vector2)body.position - target).sqrMagnitude > hangSnapEpsilon * hangSnapEpsilon)
                        body.position = target;

                    return true;
                }

            case LedgeState.Climbing:
                {
                    if (currentPoint == null)
                    {
                        ForceCancel();
                        return false;
                    }

                    if (movementModule != null)
                        movementModule.TryFaceByInput(CurrentFaceRight ? 1f : -1f, true, true);

                    float duration = Mathf.Max(0.0001f, climbDuration);
                    float t = Mathf.Clamp01((Time.time - climbStartedAt) / duration);
                    if (useSmoothStepOnClimb)
                        t = t * t * (3f - 2f * t);

                    Vector2 target = Vector2.Lerp(climbFrom, climbTo, t);

                    body.velocity = Vector2.zero;
                    body.angularVelocity = 0f;
                    body.position = target;

                    if (t >= 0.999f)
                    {
                        body.position = climbTo;
                        ExitPhysicsOverride();

                        state = LedgeState.None;
                        currentPoint = null;
                        regrabBlockedUntil = Time.time + Mathf.Max(0f, regrabCooldown);
                        ClearCandidate();
                    }

                    return true;
                }

            default:
                return false;
        }
    }

    public void ForceCancel()
    {
        ExitPhysicsOverride();

        state = LedgeState.None;
        currentPoint = null;
        hangUntil = -999f;
        climbStartedAt = -999f;
        climbFrom = Vector2.zero;
        climbTo = Vector2.zero;
        ClearCandidate();
    }

    private void TryAutoCatch(
        float intendedInputX,
        bool isGrounded,
        PlayerMovementModule movementModule,
        PlayerJumpModule jumpModule)
    {
        if (candidatePoint == null || rb == null)
            return;

        if (Time.time < regrabBlockedUntil)
            return;

        if (isGrounded)
            return;

        if (rb.velocity.y > maxCatchUpwardVelocity)
            return;

        float requiredApproachDir = candidatePoint.RequiredApproachDirectionX;

        float approachX = Mathf.Abs(rb.velocity.x) >= Mathf.Abs(intendedInputX)
            ? rb.velocity.x
            : intendedInputX;

        if (Mathf.Abs(approachX) < minApproachHorizontalSpeed)
            return;

        if (Mathf.Abs(requiredApproachDir) > 0.001f && Mathf.Sign(approachX) != Mathf.Sign(requiredApproachDir))
            return;

        currentPoint = candidatePoint;
        state = LedgeState.Hanging;
        hangUntil = Time.time + Mathf.Max(0f, hangDuration);
        climbStartedAt = -999f;
        climbFrom = currentPoint.HangPointPosition;
        climbTo = currentPoint.StandPointPosition;

        EnterPhysicsOverride();

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = currentPoint.HangPointPosition;

        if (movementModule != null)
        {
            movementModule.ResetSprint();
            movementModule.TryFaceByInput(CurrentFaceRight ? 1f : -1f, true, true);
        }

        jumpModule?.ResetJumpInputState();
        ClearCandidate();
    }

    private void BeginClimb(PlayerMovementModule movementModule, PlayerJumpModule jumpModule)
    {
        if (currentPoint == null)
        {
            ForceCancel();
            return;
        }

        state = LedgeState.Climbing;
        climbStartedAt = Time.time;
        climbFrom = rb != null ? rb.position : currentPoint.HangPointPosition;
        climbTo = currentPoint.StandPointPosition;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        movementModule?.ResetSprint();
        jumpModule?.ResetJumpInputState();
    }

    private void BeginDrop()
    {
        Vector2 newVelocity;

        if (currentPoint != null)
        {
            float awayDir = currentPoint.PlayerFacesRightWhileHanging ? -1f : 1f;
            newVelocity = new Vector2(awayDir * Mathf.Abs(dropAwaySpeed), -Mathf.Abs(dropDownSpeed));
        }
        else
        {
            newVelocity = new Vector2(0f, -Mathf.Abs(dropDownSpeed));
        }

        ExitPhysicsOverride();

        if (rb != null)
        {
            rb.velocity = newVelocity;
            rb.angularVelocity = 0f;
        }

        state = LedgeState.None;
        currentPoint = null;
        hangUntil = -999f;
        climbStartedAt = -999f;
        climbFrom = Vector2.zero;
        climbTo = Vector2.zero;
        regrabBlockedUntil = Time.time + Mathf.Max(0f, regrabCooldown);
        ClearCandidate();
    }

    private void EnterPhysicsOverride()
    {
        if (rb == null || physicsOverrideActive)
            return;

        savedBodyType = rb.bodyType;
        savedInterpolation = rb.interpolation;
        savedGravityScale = rb.gravityScale;
        savedCollisionDetection = rb.collisionDetectionMode;

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.None;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        rb.bodyType = RigidbodyType2D.Kinematic;

        physicsOverrideActive = true;
    }

    private void ExitPhysicsOverride()
    {
        if (rb == null || !physicsOverrideActive)
            return;

        rb.bodyType = savedBodyType;
        rb.gravityScale = savedGravityScale;
        rb.interpolation = savedInterpolation;
        rb.collisionDetectionMode = savedCollisionDetection;
        rb.angularVelocity = 0f;

        physicsOverrideActive = false;
    }

    private void ClearCandidate()
    {
        candidatePoint = null;
        candidateSqrDistance = float.MaxValue;
    }
}