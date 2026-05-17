using UnityEngine;

[DisallowMultipleComponent]
public class PlayerWallSlideDustFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Модуль стены игрока. Нужен для IsWallSliding и IsFastWallSliding.")]
    private PlayerBounceModule bounceModule;

    [SerializeField, Tooltip("Модуль движения игрока. Нужен для направления взгляда, если направление стены не удалось определить.")]
    private PlayerMovementModule movementModule;

    [SerializeField, Tooltip("Rigidbody2D игрока. Нужен для скорости падения.")]
    private Rigidbody2D rb;

    [SerializeField, Tooltip("Particle System пыли при сползании по стене.")]
    private ParticleSystem wallDustParticles;

    [SerializeField, Tooltip("Базовая точка появления пыли. Обычно пустышка у лап игрока.")]
    private Transform dustPoint;

    [Header("Emission")]
    [SerializeField, Min(0f), Tooltip("Минимальная скорость падения вниз, чтобы пыль появилась.")]
    private float minFallSpeedForDust = 0.3f;

    [SerializeField, Min(0f), Tooltip("Rate over Time при обычном wall slide.")]
    private float normalSlideEmissionRate = 12f;

    [SerializeField, Min(0f), Tooltip("Rate over Time при быстром wall slide вниз.")]
    private float fastSlideEmissionRate = 30f;

    [SerializeField, Min(0f), Tooltip("Сколько частиц выбросить при первом касании wall slide.")]
    private int burstOnWallSlideEnter = 5;

    [SerializeField, Min(0.01f), Tooltip("Скорость падения, при которой эффект считается максимальным.")]
    private float maxFallSpeed = 8f;

    [Header("Position")]
    [SerializeField, Min(0f), Tooltip("Смещение пыли в сторону стены.")]
    private float wallSideOffset = 0.22f;

    [SerializeField, Min(0f), Tooltip("Смещение пыли вниз от Dust Point.")]
    private float downOffset = 0.08f;

    [Header("Direction")]
    [SerializeField, Tooltip("Если ВКЛ — пыль летит от стены наружу.")]
    private bool emitAwayFromWall = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private ParticleSystem.EmissionModule emissionModule;
    private bool wasEmitting = false;

    private void Reset()
    {
        CacheRefs();
    }

    private void Awake()
    {
        CacheRefs();
        CacheParticleModules();
        StopParticles(true);
    }

    private void OnValidate()
    {
        CacheRefs();

        minFallSpeedForDust = Mathf.Max(0f, minFallSpeedForDust);
        normalSlideEmissionRate = Mathf.Max(0f, normalSlideEmissionRate);
        fastSlideEmissionRate = Mathf.Max(normalSlideEmissionRate, fastSlideEmissionRate);
        burstOnWallSlideEnter = Mathf.Max(0, burstOnWallSlideEnter);
        maxFallSpeed = Mathf.Max(0.01f, maxFallSpeed);
        wallSideOffset = Mathf.Max(0f, wallSideOffset);
        downOffset = Mathf.Max(0f, downOffset);
    }

    private void Update()
    {
        if (bounceModule == null || wallDustParticles == null)
            return;

        bool wallSliding = bounceModule.IsWallSliding;
        bool fastWallSliding = bounceModule.IsFastWallSliding;

        float fallSpeed = rb != null ? Mathf.Max(0f, -rb.velocity.y) : 0f;

        bool shouldEmit =
            wallSliding &&
            fallSpeed >= minFallSpeedForDust;

        if (shouldEmit)
        {
            UpdateDustPositionAndDirection();
            UpdateEmission(fallSpeed, fastWallSliding);

            if (!wasEmitting)
            {
                PlayParticles();

                if (burstOnWallSlideEnter > 0)
                    wallDustParticles.Emit(burstOnWallSlideEnter);

                if (debugLogs)
                    Debug.Log("[PlayerWallSlideDustFX] Wall slide dust started");
            }
            else if (!wallDustParticles.isPlaying)
            {
                PlayParticles();
            }
        }
        else
        {
            StopParticles(false);
        }

        wasEmitting = shouldEmit;
    }

    private void CacheRefs()
    {
        if (bounceModule == null)
            bounceModule = GetComponentInParent<PlayerBounceModule>();

        if (movementModule == null)
            movementModule = GetComponentInParent<PlayerMovementModule>();

        if (rb == null)
            rb = GetComponentInParent<Rigidbody2D>();

        if (wallDustParticles == null)
            wallDustParticles = GetComponentInChildren<ParticleSystem>();
    }

    private void CacheParticleModules()
    {
        if (wallDustParticles == null)
            return;

        emissionModule = wallDustParticles.emission;
    }

    private void UpdateDustPositionAndDirection()
    {
        float wallDir = GetWallDirectionFallback();

        Vector3 basePos =
            dustPoint != null
                ? dustPoint.position
                : transform.position;

        Vector3 pos = basePos;
        pos.x += wallDir * wallSideOffset;
        pos.y -= downOffset;

        wallDustParticles.transform.position = pos;

        float dustFlyDir = emitAwayFromWall ? -wallDir : wallDir;

        Vector3 scale = wallDustParticles.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * dustFlyDir;
        scale.y = Mathf.Abs(scale.y);
        scale.z = Mathf.Abs(scale.z);
        wallDustParticles.transform.localScale = scale;

        float angleY = dustFlyDir > 0f ? 0f : 180f;
        wallDustParticles.transform.rotation = Quaternion.Euler(0f, angleY, 0f);
    }

    private float GetWallDirectionFallback()
    {
        /*
         * wallDir:
         * -1 = стена слева от игрока
         *  1 = стена справа от игрока
         *
         * У PlayerBounceModule текущее направление стены закрыто private,
         * поэтому берём безопасный fallback:
         * когда игрок скользит по стене, обычно он смотрит в сторону стены.
         */

        if (movementModule != null)
            return movementModule.IsFacingRight ? 1f : -1f;

        if (rb != null && Mathf.Abs(rb.velocity.x) > 0.01f)
            return Mathf.Sign(rb.velocity.x);

        return transform.lossyScale.x >= 0f ? 1f : -1f;
    }

    private void UpdateEmission(float fallSpeed, bool fastWallSliding)
    {
        CacheParticleModules();

        float t = Mathf.Clamp01(fallSpeed / maxFallSpeed);

        float baseRate = fastWallSliding
            ? fastSlideEmissionRate
            : normalSlideEmissionRate;

        float rate = Mathf.Lerp(baseRate * 0.45f, baseRate, t);

        emissionModule.rateOverTime = rate;
    }

    private void PlayParticles()
    {
        CacheParticleModules();

        emissionModule.enabled = true;

        if (!wallDustParticles.isPlaying)
            wallDustParticles.Play(true);
    }

    private void StopParticles(bool clear)
    {
        if (wallDustParticles == null)
            return;

        CacheParticleModules();

        emissionModule.enabled = false;

        if (clear)
            wallDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        else
            wallDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}