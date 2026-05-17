using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHardLandingImpactFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Ground module игрока. Нужен для JustLanded / IsGrounded.")]
    private PlayerGroundModule groundModule;

    [SerializeField, Tooltip("Rigidbody2D игрока. Нужен для отслеживания скорости падения.")]
    private Rigidbody2D rb;

    [SerializeField, Tooltip("Particle System эффекта сильного приземления.")]
    private ParticleSystem impactParticles;

    [SerializeField, Tooltip("Точка появления эффекта. Обычно пустышка под лапами кота.")]
    private Transform impactPoint;

    [Header("Hard Landing Threshold")]
    [SerializeField, Min(0f), Tooltip("Минимальная скорость падения вниз, после которой появляется эффект.")]
    private float minFallSpeed = 10f;

    [SerializeField, Min(0f), Tooltip("Скорость падения, на которой эффект становится максимальным.")]
    private float maxFallSpeed = 20f;

    [Header("Burst")]
    [SerializeField, Min(0), Tooltip("Сколько частиц выбрасывать при минимальном сильном падении.")]
    private int minBurstCount = 10;

    [SerializeField, Min(0), Tooltip("Сколько частиц выбрасывать при очень сильном падении.")]
    private int maxBurstCount = 32;

    [SerializeField, Min(0f), Tooltip("Минимальная горизонтальная скорость разлёта частиц.")]
    private float minSideSpeed = 1.8f;

    [SerializeField, Min(0f), Tooltip("Максимальная горизонтальная скорость разлёта частиц.")]
    private float maxSideSpeed = 5.5f;

    [SerializeField, Min(0f), Tooltip("Минимальная вертикальная скорость частиц вверх.")]
    private float minUpSpeed = 0.5f;

    [SerializeField, Min(0f), Tooltip("Максимальная вертикальная скорость частиц вверх.")]
    private float maxUpSpeed = 2.2f;

    [SerializeField, Min(0f), Tooltip("Случайный разброс позиции спавна по X.")]
    private float spawnSpreadX = 0.16f;

    [SerializeField, Min(0f), Tooltip("Случайный разброс позиции спавна по Y.")]
    private float spawnSpreadY = 0.04f;

    [Header("Extra Pebble Feel")]
    [SerializeField, Tooltip("Если ВКЛ — часть частиц будет лететь быстрее и ниже, как мелкие камни.")]
    private bool addPebbleLikeParticles = true;

    [SerializeField, Range(0f, 1f), Tooltip("Доля частиц, которые будут вести себя как камешки.")]
    private float pebbleChance = 0.25f;

    [SerializeField, Min(1f), Tooltip("Множитель боковой скорости для камешков.")]
    private float pebbleSideSpeedMultiplier = 1.45f;

    [Header("Cooldown")]
    [SerializeField, Min(0f), Tooltip("Защита от повторного срабатывания несколько раз подряд.")]
    private float triggerCooldown = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool wasGrounded = false;
    private bool hasAirborneData = false;
    private float minAirborneVelocityY = 0f;
    private float lastTriggerTime = -999f;

    private void Reset()
    {
        CacheRefs();
    }

    private void Awake()
    {
        CacheRefs();

        if (impactParticles != null)
            impactParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnValidate()
    {
        CacheRefs();

        minFallSpeed = Mathf.Max(0f, minFallSpeed);
        maxFallSpeed = Mathf.Max(minFallSpeed + 0.01f, maxFallSpeed);

        minBurstCount = Mathf.Max(0, minBurstCount);
        maxBurstCount = Mathf.Max(minBurstCount, maxBurstCount);

        minSideSpeed = Mathf.Max(0f, minSideSpeed);
        maxSideSpeed = Mathf.Max(minSideSpeed, maxSideSpeed);

        minUpSpeed = Mathf.Max(0f, minUpSpeed);
        maxUpSpeed = Mathf.Max(minUpSpeed, maxUpSpeed);

        spawnSpreadX = Mathf.Max(0f, spawnSpreadX);
        spawnSpreadY = Mathf.Max(0f, spawnSpreadY);

        pebbleChance = Mathf.Clamp01(pebbleChance);
        pebbleSideSpeedMultiplier = Mathf.Max(1f, pebbleSideSpeedMultiplier);

        triggerCooldown = Mathf.Max(0f, triggerCooldown);
    }

    private void Update()
    {
        if (groundModule == null || rb == null || impactParticles == null)
            return;

        bool grounded = groundModule.IsGrounded;

        if (!grounded)
        {
            TrackAirborneFall();
        }

        bool landedThisFrame =
            groundModule.JustLanded ||
            (!wasGrounded && grounded);

        if (landedThisFrame)
        {
            TryPlayImpact();
            ResetAirborneTracking();
        }

        if (grounded && wasGrounded)
        {
            ResetAirborneTracking();
        }

        wasGrounded = grounded;
    }

    private void TrackAirborneFall()
    {
        float vy = rb.velocity.y;

        if (!hasAirborneData)
        {
            hasAirborneData = true;
            minAirborneVelocityY = vy;
            return;
        }

        if (vy < minAirborneVelocityY)
            minAirborneVelocityY = vy;
    }

    private void TryPlayImpact()
    {
        if (!hasAirborneData)
            return;

        if (Time.time < lastTriggerTime + triggerCooldown)
            return;

        float fallSpeed = Mathf.Max(0f, -minAirborneVelocityY);

        if (fallSpeed < minFallSpeed)
            return;

        float strength = Mathf.InverseLerp(minFallSpeed, maxFallSpeed, fallSpeed);

        EmitImpact(strength);

        lastTriggerTime = Time.time;

        if (debugLogs)
            Debug.Log($"[PlayerHardLandingImpactFX] Hard landing impact. fallSpeed={fallSpeed:0.00}, strength={strength:0.00}");
    }

    private void EmitImpact(float strength)
    {
        Vector3 basePos =
            impactPoint != null
                ? impactPoint.position
                : transform.position;

        impactParticles.transform.position = basePos;

        int count = Mathf.RoundToInt(Mathf.Lerp(minBurstCount, maxBurstCount, Mathf.Clamp01(strength)));

        if (count <= 0)
            return;

        if (!impactParticles.isPlaying)
            impactParticles.Play(true);

        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;

            float sideSpeed = Random.Range(minSideSpeed, maxSideSpeed);
            float upSpeed = Random.Range(minUpSpeed, maxUpSpeed);

            bool pebble = addPebbleLikeParticles && Random.value < pebbleChance;

            if (pebble)
            {
                sideSpeed *= pebbleSideSpeedMultiplier;
                upSpeed *= Random.Range(0.45f, 0.8f);
            }

            Vector3 spawnPos = basePos + new Vector3(
                Random.Range(-spawnSpreadX, spawnSpreadX),
                Random.Range(-spawnSpreadY, spawnSpreadY),
                0f);

            Vector3 velocity = new Vector3(
                side * sideSpeed * Mathf.Lerp(0.8f, 1.25f, strength),
                upSpeed * Mathf.Lerp(0.8f, 1.35f, strength),
                0f);

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = spawnPos,
                velocity = velocity,
                startSize = Random.Range(0.06f, 0.16f) * Mathf.Lerp(0.85f, 1.35f, strength),
                startLifetime = Random.Range(0.25f, 0.55f),
                rotation = Random.Range(0f, 360f)
            };

            impactParticles.Emit(emitParams, 1);
        }
    }

    private void ResetAirborneTracking()
    {
        hasAirborneData = false;
        minAirborneVelocityY = 0f;
    }

    private void CacheRefs()
    {
        if (groundModule == null)
            groundModule = GetComponentInParent<PlayerGroundModule>();

        if (rb == null)
            rb = GetComponentInParent<Rigidbody2D>();

        if (impactParticles == null)
            impactParticles = GetComponentInChildren<ParticleSystem>();
    }
}