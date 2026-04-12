using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerFenceClimbModule : MonoBehaviour
{
    public struct FenceTickResult
    {
        public bool DidJumpOff;
        public float TakeoffVx;
    }

    private enum FenceState
    {
        None,
        Climbing
    }

    [Header("Fence / Ladder Climb")]
    [SerializeField, Tooltip("Если ВКЛ — игрок может входить в режим лазания по лестницам/заборам.")]
    private bool enableFenceClimb = true;

    [SerializeField, Min(0f), Tooltip("Горизонтальная скорость движения по лестнице/забору.")]
    private float horizontalClimbSpeed = 2.4f;

    [SerializeField, Min(0f), Tooltip("Вертикальная скорость движения по лестнице/забору.")]
    private float verticalClimbSpeed = 2.8f;

    [SerializeField, Min(0f), Tooltip("Небольшой внутренний отступ от краёв trigger-зоны.")]
    private float innerBoundsPadding = 0.02f;

    [SerializeField, Min(0f), Tooltip("Вертикальная скорость вниз при выходе с лестницы по F/X.")]
    private float exitDropDownSpeed = 1.5f;

    [SerializeField, Min(0f), Tooltip("Кулдаун после выхода/прыжка с лестницы, чтобы не словить мгновенный повторный захват.")]
    private float regrabCooldown = 0.15f;

    [SerializeField, Tooltip("Если ВКЛ — при входе на лестницу спринт сбрасывается.")]
    private bool resetSprintOnEnter = true;

    [SerializeField, Tooltip("Если ВКЛ — при входе на лестницу сбрасывается состояние прыжка/буфера.")]
    private bool resetJumpStateOnEnter = true;

    [Header("Прыжок с лестницы")]
    [SerializeField, Tooltip("Если ВКЛ — с лестницы можно прыгнуть по Space / A на геймпаде.")]
    private bool allowJumpOffFence = true;

    [SerializeField, Min(0f), Tooltip("Горизонтальная скорость прыжка с лестницы.")]
    private float fenceJumpHorizontalSpeed = 5f;

    [SerializeField, Min(0f), Tooltip("Вертикальная скорость прыжка с лестницы.")]
    private float fenceJumpVerticalSpeed = 10f;

    [Header("Анимация-заглушка")]
    [SerializeField, Tooltip("Опциональный Animator для состояний взаимодействия с лестницей/забором.")]
    private Animator interactionAnimator;

    [SerializeField, Tooltip("Bool-параметр Animator: игрок сейчас на лестнице/заборе.")]
    private string animatorFenceActiveBool = "FenceActive";

    [SerializeField, Tooltip("Bool-параметр Animator: на лестнице/заборе используем позу спиной к камере.")]
    private string animatorFenceBackFacingBool = "FenceBackFacing";

    [SerializeField, Tooltip("Float-параметр Animator: горизонтальный ввод на лестнице/заборе (-1..1).")]
    private string animatorFenceMoveXFloat = "FenceMoveX";

    [SerializeField, Tooltip("Float-параметр Animator: вертикальный ввод на лестнице/забору (-1..1).")]
    private string animatorFenceMoveYFloat = "FenceMoveY";

    [SerializeField, Tooltip("Trigger-параметр Animator: вход на лестницу/забор.")]
    private string animatorFenceEnterTrigger = "FenceEnter";

    [SerializeField, Tooltip("Trigger-параметр Animator: выход с лестницы/забора.")]
    private string animatorFenceExitTrigger = "FenceExit";

    [SerializeField, Tooltip("Если ВКЛ — во время лазания будет выставляться back-facing bool для будущих анимаций.")]
    private bool setBackFacingBoolWhileClimbing = true;

    private Rigidbody2D rb;
    private Collider2D mainBodyCollider;

    private FenceState state = FenceState.None;

    private FenceClimbZone2D candidateZone = null;
    private float candidateScore = float.MaxValue;

    private FenceClimbZone2D currentZone = null;
    private Vector2 moveInput = Vector2.zero;

    private RigidbodyType2D savedBodyType;
    private RigidbodyInterpolation2D savedInterpolation;
    private float savedGravityScale;
    private CollisionDetectionMode2D savedCollisionDetection;
    private bool physicsOverrideActive = false;

    private float regrabBlockedUntil = -999f;

    public bool IsActive => state == FenceState.Climbing;
    public bool BlocksStandardController => state == FenceState.Climbing;
    public bool HasCandidateZone => candidateZone != null;
    public Vector2 MoveInput => moveInput;
    public bool UsesBackFacingFenceAnimation => setBackFacingBoolWhileClimbing && state == FenceState.Climbing;

    private void Reset()
    {
        CacheComponents();
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnValidate()
    {
        CacheComponents();

        horizontalClimbSpeed = Mathf.Max(0f, horizontalClimbSpeed);
        verticalClimbSpeed = Mathf.Max(0f, verticalClimbSpeed);
        innerBoundsPadding = Mathf.Max(0f, innerBoundsPadding);
        exitDropDownSpeed = Mathf.Max(0f, exitDropDownSpeed);
        regrabCooldown = Mathf.Max(0f, regrabCooldown);
        fenceJumpHorizontalSpeed = Mathf.Max(0f, fenceJumpHorizontalSpeed);
        fenceJumpVerticalSpeed = Mathf.Max(0f, fenceJumpVerticalSpeed);
    }

    private void OnDisable()
    {
        ForceCancel(false);
    }

    public void ClearMoveInput()
    {
        moveInput = Vector2.zero;
        UpdateAnimatorState(state == FenceState.Climbing, Vector2.zero, UsesBackFacingFenceAnimation);
    }

    public void ReportZoneStay(FenceClimbZone2D zone)
    {
        if (!enableFenceClimb || zone == null || !zone.isActiveAndEnabled)
            return;

        Vector2 from = rb != null ? rb.position : (Vector2)transform.position;
        float score = (zone.GetReferencePoint() - from).sqrMagnitude;

        if (candidateZone == null || score < candidateScore)
        {
            candidateZone = zone;
            candidateScore = score;
        }
    }

    public void ReportZoneExit(FenceClimbZone2D zone)
    {
        if (zone == null)
            return;

        if (candidateZone == zone)
            ClearCandidate();

        if (currentZone == zone && state == FenceState.Climbing)
            ExitClimb(false, true, true);
    }

    public FenceTickResult TickFence(
        float intendedInputX,
        float intendedInputY,
        bool togglePressed,
        bool jumpPressed,
        PlayerMovementModule movementModule,
        PlayerJumpModule jumpModule)
    {
        FenceTickResult result = default;

        if (!enableFenceClimb)
            return result;

        if (state == FenceState.None)
        {
            moveInput = Vector2.zero;

            if (togglePressed)
                TryBeginClimb(movementModule, jumpModule);

            UpdateAnimatorState(false, Vector2.zero, false);
            return result;
        }

        if (jumpPressed && allowJumpOffFence)
        {
            result = JumpOffFence(intendedInputX, movementModule, jumpModule);
            return result;
        }

        if (togglePressed)
        {
            ExitClimb(true, true, true);
            return result;
        }

        moveInput = Vector2.ClampMagnitude(new Vector2(
            Mathf.Clamp(intendedInputX, -1f, 1f),
            Mathf.Clamp(intendedInputY, -1f, 1f)), 1f);

        UpdateAnimatorState(true, moveInput, setBackFacingBoolWhileClimbing);
        return result;
    }

    public bool ApplyFenceMotion(Rigidbody2D body)
    {
        if (!enableFenceClimb || state != FenceState.Climbing || body == null)
            return false;

        if (currentZone == null || !currentZone.isActiveAndEnabled)
        {
            ExitClimb(false, true, true);
            return false;
        }

        Vector2 desired = body.position;
        desired.x += moveInput.x * horizontalClimbSpeed * Time.fixedDeltaTime;
        desired.y += moveInput.y * verticalClimbSpeed * Time.fixedDeltaTime;

        Vector2 actorExtents = GetActorExtents();
        Vector2 clamped = currentZone.ClampPlayerPosition(desired, actorExtents, innerBoundsPadding);

        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.position = clamped;

        return true;
    }

    public void ForceCancel(bool playExitTrigger = false)
    {
        ExitClimb(playExitTrigger, false, false);
        ClearCandidate();
    }

    private void TryBeginClimb(PlayerMovementModule movementModule, PlayerJumpModule jumpModule)
    {
        if (candidateZone == null || rb == null)
            return;

        if (Time.time < regrabBlockedUntil)
            return;

        currentZone = candidateZone;
        state = FenceState.Climbing;
        moveInput = Vector2.zero;

        if (resetSprintOnEnter)
            movementModule?.ResetSprint();

        if (resetJumpStateOnEnter)
            jumpModule?.ResetJumpInputState();

        EnterPhysicsOverride();

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = currentZone.ClampPlayerPosition(rb.position, GetActorExtents(), innerBoundsPadding);

        currentZone.HidePromptImmediate();

        UpdateAnimatorState(true, Vector2.zero, setBackFacingBoolWhileClimbing);
        SetAnimatorTrigger(animatorFenceEnterTrigger);

        ClearCandidate();
    }

    private FenceTickResult JumpOffFence(float intendedInputX, PlayerMovementModule movementModule, PlayerJumpModule jumpModule)
    {
        FenceTickResult result = default;

        if (rb == null)
            return result;

        float dir;

        if (Mathf.Abs(intendedInputX) > 0.01f)
            dir = Mathf.Sign(intendedInputX);
        else
            dir = movementModule != null && movementModule.IsFacingRight ? 1f : -1f;

        if (movementModule != null)
            movementModule.TryFaceByInput(dir, true, true);

        ExitPhysicsOverride();

        float vx = dir * Mathf.Abs(fenceJumpHorizontalSpeed);
        float vy = Mathf.Abs(fenceJumpVerticalSpeed);

        rb.velocity = new Vector2(vx, vy);
        rb.angularVelocity = 0f;

        jumpModule?.ResetJumpInputState();
        movementModule?.ResetSprint();

        state = FenceState.None;
        currentZone = null;
        moveInput = Vector2.zero;
        regrabBlockedUntil = Time.time + Mathf.Max(0f, regrabCooldown);

        UpdateAnimatorState(false, Vector2.zero, false);
        SetAnimatorTrigger(animatorFenceExitTrigger);
        ClearCandidate();

        result.DidJumpOff = true;
        result.TakeoffVx = vx;
        return result;
    }

    private void ExitClimb(bool playExitTrigger, bool applyDropVelocity, bool blockRegrab)
    {
        if (state == FenceState.None)
        {
            UpdateAnimatorState(false, Vector2.zero, false);
            return;
        }

        ExitPhysicsOverride();

        if (rb != null)
        {
            rb.angularVelocity = 0f;

            if (applyDropVelocity)
                rb.velocity = new Vector2(0f, -Mathf.Abs(exitDropDownSpeed));
        }

        state = FenceState.None;
        currentZone = null;
        moveInput = Vector2.zero;

        if (blockRegrab)
            regrabBlockedUntil = Time.time + Mathf.Max(0f, regrabCooldown);

        UpdateAnimatorState(false, Vector2.zero, false);

        if (playExitTrigger)
            SetAnimatorTrigger(animatorFenceExitTrigger);
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

    private Vector2 GetActorExtents()
    {
        CacheComponents();

        if (mainBodyCollider == null)
            return Vector2.zero;

        Bounds bounds = mainBodyCollider.bounds;
        return bounds.extents;
    }

    private void CacheComponents()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (interactionAnimator == null)
            interactionAnimator = GetComponentInChildren<Animator>();

        if (mainBodyCollider == null)
        {
            Collider2D[] all = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && !all[i].isTrigger)
                {
                    mainBodyCollider = all[i];
                    break;
                }
            }
        }
    }

    private void ClearCandidate()
    {
        candidateZone = null;
        candidateScore = float.MaxValue;
    }

    private void UpdateAnimatorState(bool fenceActive, Vector2 currentMoveInput, bool backFacing)
    {
        if (interactionAnimator == null)
            return;

        SetAnimatorBool(animatorFenceActiveBool, fenceActive);
        SetAnimatorBool(animatorFenceBackFacingBool, backFacing);
        SetAnimatorFloat(animatorFenceMoveXFloat, currentMoveInput.x);
        SetAnimatorFloat(animatorFenceMoveYFloat, currentMoveInput.y);
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        if (interactionAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
            return;

        interactionAnimator.SetTrigger(parameterName);
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (interactionAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
            return;

        interactionAnimator.SetBool(parameterName, value);
    }

    private void SetAnimatorFloat(string parameterName, float value)
    {
        if (interactionAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
            return;

        interactionAnimator.SetFloat(parameterName, value);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (interactionAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = interactionAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == expectedType && parameters[i].name == parameterName)
                return true;
        }

        return false;
    }
}