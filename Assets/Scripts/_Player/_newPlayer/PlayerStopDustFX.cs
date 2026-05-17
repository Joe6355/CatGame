using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStopDustFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Модуль движения игрока. Нужен, чтобы читать IsStopping и StopDirection.")]
    private PlayerMovementModule movementModule;

    [SerializeField, Tooltip("Rigidbody2D игрока. Нужен для скорости.")]
    private Rigidbody2D rb;

    [SerializeField, Tooltip("Particle System пыли при торможении.")]
    private ParticleSystem stopDustParticles;

    [SerializeField, Tooltip("Точка, где должна появляться пыль. Обычно пустышка у ног игрока.")]
    private Transform dustPoint;

    [Header("Emission")]
    [SerializeField, Min(0f), Tooltip("Минимальная скорость по X, чтобы пыль вообще появлялась.")]
    private float minSpeedForDust = 2.5f;

    [SerializeField, Min(0f), Tooltip("Сколько частиц выбрасывать единоразово при входе в Stop.")]
    private int burstOnStopEnter = 10;

    [SerializeField, Min(0f), Tooltip("Минимальный Rate over Time во время слабого торможения.")]
    private float minEmissionRate = 8f;

    [SerializeField, Min(0f), Tooltip("Максимальный Rate over Time во время сильного торможения.")]
    private float maxEmissionRate = 28f;

    [SerializeField, Min(0.01f), Tooltip("Скорость, на которой эффект считается максимальным.")]
    private float maxDustSpeed = 10f;

    [Header("Direction")]
    [SerializeField, Tooltip("Если ВКЛ — пыль летит назад относительно направления заноса.")]
    private bool emitOppositeToStopDirection = true;

    [SerializeField, Min(0f), Tooltip("Смещение точки пыли назад по X относительно направления заноса.")]
    private float backOffset = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private ParticleSystem.EmissionModule emissionModule;
    private ParticleSystem.VelocityOverLifetimeModule velocityModule;
    private ParticleSystem.ShapeModule shapeModule;

    private bool wasStopping = false;

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

        minSpeedForDust = Mathf.Max(0f, minSpeedForDust);
        burstOnStopEnter = Mathf.Max(0, burstOnStopEnter);
        minEmissionRate = Mathf.Max(0f, minEmissionRate);
        maxEmissionRate = Mathf.Max(minEmissionRate, maxEmissionRate);
        maxDustSpeed = Mathf.Max(0.01f, maxDustSpeed);
        backOffset = Mathf.Max(0f, backOffset);
    }

    private void Update()
    {
        if (movementModule == null || stopDustParticles == null)
            return;

        bool stopping = movementModule.IsStopping;
        float speedX = rb != null ? Mathf.Abs(rb.velocity.x) : 0f;

        bool shouldEmit =
            stopping &&
            speedX >= minSpeedForDust;

        if (shouldEmit)
        {
            UpdateDustPositionAndDirection();
            UpdateEmissionBySpeed(speedX);

            if (!wasStopping)
            {
                PlayParticles();

                if (burstOnStopEnter > 0)
                    stopDustParticles.Emit(burstOnStopEnter);

                if (debugLogs)
                    Debug.Log("[PlayerStopDustFX] Stop dust burst");
            }
            else if (!stopDustParticles.isPlaying)
            {
                PlayParticles();
            }
        }
        else
        {
            StopParticles(false);
        }

        wasStopping = shouldEmit;
    }

    private void CacheRefs()
    {
        if (movementModule == null)
            movementModule = GetComponentInParent<PlayerMovementModule>();

        if (rb == null)
            rb = GetComponentInParent<Rigidbody2D>();

        if (stopDustParticles == null)
            stopDustParticles = GetComponentInChildren<ParticleSystem>();
    }

    private void CacheParticleModules()
    {
        if (stopDustParticles == null)
            return;

        emissionModule = stopDustParticles.emission;
        velocityModule = stopDustParticles.velocityOverLifetime;
        shapeModule = stopDustParticles.shape;
    }

    private void UpdateDustPositionAndDirection()
    {
        float stopDir = movementModule.StopDirection;

        if (Mathf.Abs(stopDir) < 0.001f && rb != null && Mathf.Abs(rb.velocity.x) > 0.001f)
            stopDir = Mathf.Sign(rb.velocity.x);

        if (Mathf.Abs(stopDir) < 0.001f)
            stopDir = 1f;

        float dustFlyDir = emitOppositeToStopDirection ? -Mathf.Sign(stopDir) : Mathf.Sign(stopDir);

        if (dustPoint != null)
        {
            Vector3 pos = dustPoint.position;
            pos.x += dustFlyDir * backOffset;
            stopDustParticles.transform.position = pos;
        }

        Vector3 scale = stopDustParticles.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * dustFlyDir;
        scale.y = Mathf.Abs(scale.y);
        scale.z = Mathf.Abs(scale.z);
        stopDustParticles.transform.localScale = scale;

        float angleY = dustFlyDir > 0f ? 0f : 180f;
        stopDustParticles.transform.rotation = Quaternion.Euler(0f, angleY, 0f);
    }

    private void UpdateEmissionBySpeed(float speedX)
    {
        CacheParticleModules();

        float t = Mathf.Clamp01(speedX / maxDustSpeed);
        float rate = Mathf.Lerp(minEmissionRate, maxEmissionRate, t);

        emissionModule.rateOverTime = rate;
    }

    private void PlayParticles()
    {
        CacheParticleModules();

        emissionModule.enabled = true;

        if (!stopDustParticles.isPlaying)
            stopDustParticles.Play(true);
    }

    private void StopParticles(bool clear)
    {
        if (stopDustParticles == null)
            return;

        CacheParticleModules();

        emissionModule.enabled = false;

        if (clear)
            stopDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        else
            stopDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}