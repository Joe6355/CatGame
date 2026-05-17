using UnityEngine;

[DisallowMultipleComponent]
public class PlayerPresentationModule : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField, Tooltip("Animator персонажа. Можно повесить на игрока или на дочерний объект со SpriteRenderer.")]
    private Animator animator;

    [Header("Пороги Run")]
    [SerializeField, Min(0f), Tooltip("Минимальная горизонтальная скорость, чтобы включить Run.")]
    private float runSpeedThreshold = 0.15f;

    [SerializeField, Min(0f), Tooltip("Минимальный ввод по X, чтобы включить Run.")]
    private float inputRunThreshold = 0.1f;

    [Header("Пороги Stop")]
    [SerializeField, Min(0f), Tooltip("Минимальная скорость по X, при которой отпускание движения считается Stop/заносом.")]
    private float stopSpeedThreshold = 3.0f;

    [SerializeField, Min(0f), Tooltip("Минимальный ввод по X. Если ввод меньше этого значения, считаем, что игрок отпустил движение.")]
    private float stopInputDeadZone = 0.08f;

    [SerializeField, Tooltip("Если ВКЛ — Stop включается не только от IsSprintSkidActive, но и когда игрок отпустил движение, но ещё катится по земле.")]
    private bool useGroundSlideAsStop = true;

    [Header("Hook")]
    [SerializeField, Tooltip("Если ВКЛ — Hook включается при wall slide и ledge.")]
    private bool useHookForLedgeAndWall = true;

    [SerializeField, Tooltip("Если ВКЛ — Hook включается ещё и во время лазания по забору/лестнице.")]
    private bool useHookForFenceClimb = false;

    [Header("Имена параметров Animator")]
    [SerializeField] private string groundedBool = "Grounded";
    [SerializeField] private string runningBool = "Running";
    [SerializeField] private string jumpingBool = "Jumping";
    [SerializeField] private string stoppingBool = "Stopping";
    [SerializeField] private string hookingBool = "Hooking";

    [SerializeField] private string speedXFloat = "SpeedX";
    [SerializeField] private string speedYFloat = "SpeedY";
    [SerializeField] private string inputXFloat = "InputX";

    private int groundedHash;
    private int runningHash;
    private int jumpingHash;
    private int stoppingHash;
    private int hookingHash;

    private int speedXHash;
    private int speedYHash;
    private int inputXHash;

    private bool hashesReady = false;

    private void Reset()
    {
        CacheAnimator();
        RebuildHashes();
    }

    private void Awake()
    {
        CacheAnimator();
        RebuildHashes();
    }

    private void OnValidate()
    {
        CacheAnimator();
        RebuildHashes();

        runSpeedThreshold = Mathf.Max(0f, runSpeedThreshold);
        inputRunThreshold = Mathf.Max(0f, inputRunThreshold);

        stopSpeedThreshold = Mathf.Max(0f, stopSpeedThreshold);
        stopInputDeadZone = Mathf.Max(0f, stopInputDeadZone);
    }

    public void RefreshPresentation(
        Rigidbody2D rb,
        bool isGrounded,
        float inputX,
        bool isSprintSkidActive,
        bool isWallSliding,
        bool isLedgeActive,
        bool isLedgeHanging,
        bool isLedgeClimbing,
        bool isFenceClimbing,
        Vector2 fenceMoveInput,
        bool isJumpHoldActive,
        bool isChargingJump,
        float jumpBarNormalized,
        bool isApexThrowAvailable)
    {
        if (animator == null)
            return;

        if (!hashesReady)
            RebuildHashes();

        Vector2 velocity = rb != null ? rb.velocity : Vector2.zero;

        float absSpeedX = Mathf.Abs(velocity.x);
        float absInputX = Mathf.Abs(inputX);

        bool hookByLedgeOrWall =
            useHookForLedgeAndWall &&
            (isWallSliding || isLedgeActive || isLedgeHanging || isLedgeClimbing);

        bool hookByFence =
            useHookForFenceClimb &&
            isFenceClimbing;

        bool hooking = hookByLedgeOrWall || hookByFence;

        bool jumping =
            !isGrounded &&
            !hooking &&
            !isFenceClimbing;

        bool groundSlideStop =
            useGroundSlideAsStop &&
            isGrounded &&
            !jumping &&
            !hooking &&
            !isFenceClimbing &&
            absInputX <= stopInputDeadZone &&
            absSpeedX >= stopSpeedThreshold;

        bool stopping =
            isGrounded &&
            !jumping &&
            !hooking &&
            !isFenceClimbing &&
            (isSprintSkidActive || groundSlideStop);

        bool running =
            isGrounded &&
            !jumping &&
            !hooking &&
            !stopping &&
            (absInputX > inputRunThreshold || absSpeedX > runSpeedThreshold);

        SetBoolIfExists(groundedHash, groundedBool, isGrounded);
        SetBoolIfExists(runningHash, runningBool, running);
        SetBoolIfExists(jumpingHash, jumpingBool, jumping);
        SetBoolIfExists(stoppingHash, stoppingBool, stopping);
        SetBoolIfExists(hookingHash, hookingBool, hooking);

        SetFloatIfExists(speedXHash, speedXFloat, velocity.x);
        SetFloatIfExists(speedYHash, speedYFloat, velocity.y);
        SetFloatIfExists(inputXHash, inputXFloat, inputX);
    }

    public void ForceRefreshPositionOnly()
    {
        // Оставлено для совместимости со старым кодом.
    }

    private void CacheAnimator()
    {
        if (animator != null)
            return;

        animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void RebuildHashes()
    {
        groundedHash = Animator.StringToHash(groundedBool);
        runningHash = Animator.StringToHash(runningBool);
        jumpingHash = Animator.StringToHash(jumpingBool);
        stoppingHash = Animator.StringToHash(stoppingBool);
        hookingHash = Animator.StringToHash(hookingBool);

        speedXHash = Animator.StringToHash(speedXFloat);
        speedYHash = Animator.StringToHash(speedYFloat);
        inputXHash = Animator.StringToHash(inputXFloat);

        hashesReady = true;
    }

    private void SetBoolIfExists(int hash, string parameterName, bool value)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
            return;

        animator.SetBool(hash, value);
    }

    private void SetFloatIfExists(int hash, string parameterName, float value)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
            return;

        animator.SetFloat(hash, value);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter p = parameters[i];

            if (p.type == type && p.name == parameterName)
                return true;
        }

        return false;
    }
}