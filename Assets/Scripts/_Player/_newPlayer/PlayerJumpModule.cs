using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerJumpModule : MonoBehaviour
{
    [Header("Прыжок (заряд)")]
    [SerializeField, Tooltip("Максимальная вертикальная скорость (сила) прыжка при полном заряде.\nРекоменд: 10–25 (часто 16–22).")]
    private float maxJumpForce = 20f;

    [SerializeField, Tooltip("Время, за которое прыжок заряжается до maxJumpForce.\nРекоменд: 0.25–1.0 сек (часто 0.5–0.8).")]
    private float jumpTimeLimit = 1f;

    [SerializeField, Tooltip("Сила слабого прыжка, если отпустили кнопку ДО входа в режим заряда.\nРекоменд: 4–12 (зависит от gravityScale).")]
    private float shortJumpForce = 8f;

    [SerializeField, Tooltip("Задержка (сек), чтобы ВОЙТИ в режим заряда.\nЕсли отпустить раньше — будет слабый прыжок.\nРекоменд: 0.3–1.5.")]
    private float chargeEnterDelay = 1f;

    [SerializeField, Tooltip("Койот-тайм: сколько секунд после схода с платформы ещё можно начать заряд прыжка.\nРекоменд: 0.05–0.15 (часто 0.08–0.12).")]
    private float coyoteTime = 0.05f;

    [Header("Буферизация прыжка")]
    [SerializeField, Tooltip("Буфер прыжка: если нажать кнопку ДО приземления, прыжок сработает автоматически.\nРекоменд: 0.08–0.15 (часто 0.1).")]
    private float jumpBufferTime = 0.1f;

    [Header("Усталость (анти-спам)")]
    [SerializeField, Tooltip("Сколько длится усталость после прыжка: нельзя начать новый заряд, скорость снижена.\nРекоменд: 0.3–1.2 сек (часто 0.6–0.9).")]
    private float fatigueDuration = 0.8f;

    private enum BufferedJumpKind
    {
        None,
        Hold,
        Short
    }

    public struct JumpContext
    {
        public float Now;
        public bool IsGrounded;
        public float LastGroundedTime;
        public bool IsFacingRight;
        public float MoveSpeed;
        public float FatigueSpeedMultiplier;
        public float PlatformVX;
        public float SnowMoveMul;
        public float SnowJumpMul;
        public float ExternalWindVX;
        public bool SprintChargedJumpReady;
        public Rigidbody2D Rigidbody;
    }

    public struct JumpActionResult
    {
        public bool DidJump;
        public float TakeoffVx;
    }

    private float lastJumpPressedTime = -999f;
    private BufferedJumpKind bufferedJumpKind = BufferedJumpKind.None;

    private PlayerInputModule.HoldSource currentHoldJumpSource = PlayerInputModule.HoldSource.None;
    private PlayerInputModule.HoldSource bufferedHoldSource = PlayerInputModule.HoldSource.None;

    private bool isJumpHoldActive = false;
    private bool isChargingJump = false;
    private bool isInstantFullChargeActive = false;

    private float jumpButtonDownTime = 0f;
    private float jumpStartHoldTime = 0f;

    private float fatigueEndTime = -999f;

    private float lastJumpTime = -999f;
    private float lastAppliedJumpForce = 0f;

    private float currentBarNormalized = 0f;

    public float CoyoteTime => coyoteTime;
    public bool IsJumpHoldActive => isJumpHoldActive;
    public bool IsChargingJump => isChargingJump;

    // Наружу показываем только реальный charge.
    public bool IsChargingJumpPublic => isChargingJump;

    public float CurrentBarNormalized => currentBarNormalized;
    public float LastJumpTime => lastJumpTime;
    public float LastAppliedJumpForce => lastAppliedJumpForce;
    public PlayerInputModule.HoldSource CurrentHoldSource => currentHoldJumpSource;

    public bool IsFatigued(float now)
    {
        return now < fatigueEndTime;
    }

    public bool IsWithinGroundedJumpWindow(JumpContext ctx)
    {
        return ctx.IsGrounded || (ctx.Now - ctx.LastGroundedTime) <= coyoteTime;
    }

    public bool CanStartJumpCharge(JumpContext ctx)
    {
        return IsWithinGroundedJumpWindow(ctx) && !IsFatigued(ctx.Now);
    }

    public void MarkShortJumpPressed(float now)
    {
        lastJumpPressedTime = now;
        bufferedJumpKind = BufferedJumpKind.Short;
        bufferedHoldSource = PlayerInputModule.HoldSource.None;
    }

    public void MarkHoldJumpPressed(PlayerInputModule.HoldSource source, float now)
    {
        lastJumpPressedTime = now;
        bufferedJumpKind = BufferedJumpKind.Hold;
        bufferedHoldSource = source;
    }

    public void BeginJumpHold(PlayerInputModule.HoldSource source, float now, bool instantFullCharge)
    {
        isJumpHoldActive = true;
        isChargingJump = false;
        isInstantFullChargeActive = false;

        bufferedJumpKind = BufferedJumpKind.None;
        currentHoldJumpSource = source;
        bufferedHoldSource = PlayerInputModule.HoldSource.None;

        jumpButtonDownTime = now;
        jumpStartHoldTime = 0f;
        currentBarNormalized = 0f;

        if (instantFullCharge)
        {
            isChargingJump = true;
            isInstantFullChargeActive = true;
            jumpStartHoldTime = now;
            currentBarNormalized = 1f;
        }
    }

    public void UpdateJumpHold(float now, bool isStillWithinGroundedWindow)
    {
        if (!isStillWithinGroundedWindow)
        {
            CancelJumpCharge();
            return;
        }

        if (isInstantFullChargeActive)
        {
            isChargingJump = true;
            currentBarNormalized = 1f;
            return;
        }

        if (!isChargingJump)
        {
            float held = now - jumpButtonDownTime;

            if (held < chargeEnterDelay)
            {
                currentBarNormalized = 0f;
                return;
            }

            isChargingJump = true;
            jumpStartHoldTime = jumpButtonDownTime + chargeEnterDelay;
        }

        float safeTimeLimit = Mathf.Max(0.0001f, jumpTimeLimit);
        float hold = Mathf.Clamp(now - jumpStartHoldTime, 0f, safeTimeLimit);
        currentBarNormalized = Mathf.Clamp01(hold / safeTimeLimit);
    }

    public JumpActionResult ReleaseJumpHoldAndMaybeJump(JumpContext ctx)
    {
        if (!IsWithinGroundedJumpWindow(ctx))
        {
            ClearHoldState();
            return default;
        }

        float verticalForce;

        if (!isChargingJump)
        {
            verticalForce = shortJumpForce * ctx.SnowJumpMul;
        }
        else if (isInstantFullChargeActive)
        {
            verticalForce = maxJumpForce * ctx.SnowJumpMul;
        }
        else
        {
            float safeTimeLimit = Mathf.Max(0.0001f, jumpTimeLimit);
            float hold = Mathf.Clamp(ctx.Now - jumpStartHoldTime, 0f, safeTimeLimit);
            verticalForce = Mathf.Clamp01(hold / safeTimeLimit) * maxJumpForce * ctx.SnowJumpMul;
        }

        ClearHoldState();

        float speedMul = IsFatigued(ctx.Now) ? ctx.FatigueSpeedMultiplier : 1f;
        float takeoffVx =
            ctx.PlatformVX +
            (ctx.IsFacingRight ? 1f : -1f) * ctx.MoveSpeed * speedMul * ctx.SnowMoveMul;

        PerformJump(ctx.Rigidbody, ctx.Now, takeoffVx, verticalForce, ctx.ExternalWindVX);
        StartFatigue(ctx.Now);

        return new JumpActionResult
        {
            DidJump = true,
            TakeoffVx = takeoffVx
        };
    }

    public JumpActionResult TryPerformDedicatedShortJump(JumpContext ctx)
    {
        if (!CanStartJumpCharge(ctx))
            return default;

        ClearHoldState();

        float verticalForce = shortJumpForce * ctx.SnowJumpMul;

        float speedMul = IsFatigued(ctx.Now) ? ctx.FatigueSpeedMultiplier : 1f;
        float takeoffVx =
            ctx.PlatformVX +
            (ctx.IsFacingRight ? 1f : -1f) * ctx.MoveSpeed * speedMul * ctx.SnowMoveMul;

        PerformJump(ctx.Rigidbody, ctx.Now, takeoffVx, verticalForce, ctx.ExternalWindVX);
        StartFatigue(ctx.Now);

        bufferedJumpKind = BufferedJumpKind.None;
        bufferedHoldSource = PlayerInputModule.HoldSource.None;

        return new JumpActionResult
        {
            DidJump = true,
            TakeoffVx = takeoffVx
        };
    }

    public JumpActionResult TryConsumeJumpBuffer(
        JumpContext ctx,
        Func<PlayerInputModule.HoldSource, bool> isHoldInputStillHeld)
    {
        if (bufferedJumpKind == BufferedJumpKind.None)
            return default;

        if (ctx.Now - lastJumpPressedTime > jumpBufferTime)
        {
            bufferedJumpKind = BufferedJumpKind.None;
            bufferedHoldSource = PlayerInputModule.HoldSource.None;
            return default;
        }

        if (!CanStartJumpCharge(ctx))
            return default;

        if (bufferedJumpKind == BufferedJumpKind.Short)
        {
            JumpActionResult shortResult = TryPerformDedicatedShortJump(ctx);
            bufferedJumpKind = BufferedJumpKind.None;
            bufferedHoldSource = PlayerInputModule.HoldSource.None;
            return shortResult;
        }

        if (bufferedJumpKind == BufferedJumpKind.Hold)
        {
            PlayerInputModule.HoldSource source = bufferedHoldSource;
            if (source == PlayerInputModule.HoldSource.None)
                source = PlayerInputModule.HoldSource.Keyboard;

            if (isHoldInputStillHeld != null && isHoldInputStillHeld(source))
            {
                BeginJumpHold(source, ctx.Now, ctx.SprintChargedJumpReady);
            }
            else
            {
                JumpActionResult shortResult = TryPerformDedicatedShortJump(ctx);
                bufferedJumpKind = BufferedJumpKind.None;
                bufferedHoldSource = PlayerInputModule.HoldSource.None;
                return shortResult;
            }

            bufferedJumpKind = BufferedJumpKind.None;
            bufferedHoldSource = PlayerInputModule.HoldSource.None;
        }

        return default;
    }

    public void CancelJumpCharge()
    {
        if (!isJumpHoldActive && !isChargingJump)
            return;

        ClearHoldState();
    }

    public void ResetJumpInputState()
    {
        lastJumpPressedTime = -999f;
        bufferedJumpKind = BufferedJumpKind.None;
        bufferedHoldSource = PlayerInputModule.HoldSource.None;

        jumpButtonDownTime = 0f;
        jumpStartHoldTime = 0f;

        ClearHoldState();
    }

    public Vector2 GetPredictedJumpVelocity(JumpContext ctx)
    {
        float speedMul = IsFatigued(ctx.Now) ? ctx.FatigueSpeedMultiplier : 1f;

        float predictedVx =
            ctx.PlatformVX +
            (ctx.IsFacingRight ? 1f : -1f) * ctx.MoveSpeed * speedMul * ctx.SnowMoveMul +
            ctx.ExternalWindVX;

        float predictedVy;

        if (!isChargingJump)
        {
            predictedVy = shortJumpForce * ctx.SnowJumpMul;
        }
        else if (isInstantFullChargeActive)
        {
            predictedVy = maxJumpForce * ctx.SnowJumpMul;
        }
        else
        {
            float safeTimeLimit = Mathf.Max(0.0001f, jumpTimeLimit);
            float hold = Mathf.Clamp(ctx.Now - jumpStartHoldTime, 0f, safeTimeLimit);
            float normalized = Mathf.Clamp01(hold / safeTimeLimit);
            predictedVy = normalized * maxJumpForce * ctx.SnowJumpMul;
        }

        return new Vector2(predictedVx, predictedVy);
    }

    private void PerformJump(Rigidbody2D rb, float now, float vx, float vy, float externalWindVX)
    {
        lastJumpTime = now;
        lastAppliedJumpForce = vy;

        if (rb != null)
            rb.velocity = new Vector2(vx + externalWindVX, vy);
    }

    private void StartFatigue(float now)
    {
        fatigueEndTime = now + fatigueDuration;
    }

    private void ClearHoldState()
    {
        isJumpHoldActive = false;
        isChargingJump = false;
        isInstantFullChargeActive = false;
        currentHoldJumpSource = PlayerInputModule.HoldSource.None;
        currentBarNormalized = 0f;
    }

    private void OnDisable()
    {
        ResetJumpInputState();
        fatigueEndTime = -999f;
        lastJumpTime = -999f;
        lastAppliedJumpForce = 0f;
        currentBarNormalized = 0f;
    }
}