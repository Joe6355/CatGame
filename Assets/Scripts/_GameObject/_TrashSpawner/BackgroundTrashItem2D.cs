using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundTrashItem2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Runtime Debug")]
    [SerializeField] private bool initialized;

    private BackgroundTrashSpawner2D spawner;

    private float fallSpeed;
    private float horizontalSpeed;
    private float driftAmplitude;
    private float driftFrequency;
    private float rotationSpeed;
    private float lifetime;
    private float fadeDuration;

    private float startAlpha = 1f;
    private float age;
    private float driftSeed;
    private Vector3 startPosition;

    private bool isDying;

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Init(
        BackgroundTrashSpawner2D spawner,
        float fallSpeed,
        float horizontalSpeed,
        float driftAmplitude,
        float driftFrequency,
        float rotationSpeed,
        float lifetime,
        float fadeDuration,
        float alpha,
        bool flipX,
        bool flipY,
        int sortingOrder)
    {
        this.spawner = spawner;
        this.fallSpeed = fallSpeed;
        this.horizontalSpeed = horizontalSpeed;
        this.driftAmplitude = driftAmplitude;
        this.driftFrequency = driftFrequency;
        this.rotationSpeed = rotationSpeed;
        this.lifetime = Mathf.Max(0.1f, lifetime);
        this.fadeDuration = Mathf.Clamp(fadeDuration, 0f, this.lifetime);

        age = 0f;
        isDying = false;
        initialized = true;

        startPosition = transform.position;
        driftSeed = Random.Range(0f, 999f);

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = flipX;
            spriteRenderer.flipY = flipY;
            spriteRenderer.sortingOrder = sortingOrder;

            Color c = spriteRenderer.color;
            c.a = Mathf.Clamp01(alpha);
            spriteRenderer.color = c;
            startAlpha = c.a;
        }
    }

    private void Update()
    {
        if (!initialized)
            return;

        float dt = Time.deltaTime;

        age += dt;

        Move(dt);
        Rotate(dt);
        HandleFade();

        if (age >= lifetime)
        {
            Kill();
        }
    }

    private void Move(float dt)
    {
        float wave = Mathf.Sin((age + driftSeed) * driftFrequency);
        float driftX = wave * driftAmplitude;

        Vector3 pos = transform.position;

        pos.x += horizontalSpeed * dt;
        pos.x += driftX * dt;
        pos.y -= fallSpeed * dt;

        transform.position = pos;
    }

    private void Rotate(float dt)
    {
        transform.Rotate(0f, 0f, rotationSpeed * dt);
    }

    private void HandleFade()
    {
        if (spriteRenderer == null)
            return;

        if (fadeDuration <= 0f)
            return;

        float fadeStartTime = lifetime - fadeDuration;

        if (age < fadeStartTime)
            return;

        isDying = true;

        float t = Mathf.InverseLerp(fadeStartTime, lifetime, age);
        float alpha = Mathf.Lerp(startAlpha, 0f, t);

        Color c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }

    private void Kill()
    {
        if (spawner != null)
            spawner.NotifyTrashDestroyed();

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!initialized)
            return;

        if (!isDying && spawner != null)
            spawner.NotifyTrashDestroyed();
    }
}