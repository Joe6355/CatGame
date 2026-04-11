using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class OrbJumpRefresh2D : MonoBehaviour
{
    [Header("Orb Jump Refresh")]
    [SerializeField, Tooltip("Если ВКЛ — орб доступен сразу при старте сцены.")]
    private bool availableOnStart = true;

    [SerializeField, Min(0f), Tooltip("Через сколько секунд орб снова появляется после успешного срабатывания.")]
    private float respawnDelay = 3f;

    [SerializeField, Tooltip("Если ВКЛ — орб не будет тратиться, если у игрока уже есть доступный заряд от орба.")]
    private bool requireFreeReceiverSlot = true;

    [SerializeField, Tooltip("Если назначен объект визуала, он будет скрываться/показываться вместе с орбом.")]
    private GameObject visualRoot;

    private Collider2D triggerCollider;
    private SpriteRenderer[] spriteRenderers;
    private Animator[] animators;

    private bool isAvailable = true;
    private float respawnAt = -999f;

    public bool IsAvailable => isAvailable;

    private void Reset()
    {
        triggerCollider = GetComponent<Collider2D>();
        EnsureTrigger();
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        CacheVisualComponents();
        EnsureTrigger();

        SetAvailable(availableOnStart);
        if (!availableOnStart)
            respawnAt = Time.time + Mathf.Max(0f, respawnDelay);
    }

    private void OnValidate()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider2D>();

        EnsureTrigger();
    }

    private void Update()
    {
        if (isAvailable)
            return;

        if (Time.time < respawnAt)
            return;

        SetAvailable(true);
    }

    public bool TryConsumeByPlayer(PlayerJumpModule jumpModule)
    {
        if (!isAvailable || jumpModule == null)
            return false;

        if (requireFreeReceiverSlot && !jumpModule.CanReceiveOrbJumpRefresh)
            return false;

        bool granted = jumpModule.GrantOrbJumpRefresh(Time.time);
        if (!granted)
            return false;

        Consume();
        return true;
    }

    private void Consume()
    {
        SetAvailable(false);
        respawnAt = Time.time + Mathf.Max(0f, respawnDelay);
    }

    private void SetAvailable(bool value)
    {
        isAvailable = value;

        if (triggerCollider != null)
            triggerCollider.enabled = value;

        if (visualRoot != null)
            visualRoot.SetActive(value);
        else
            SetLocalVisualsEnabled(value);
    }

    private void CacheVisualComponents()
    {
        Transform root = visualRoot != null ? visualRoot.transform : transform;
        spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        animators = root.GetComponentsInChildren<Animator>(true);
    }

    private void SetLocalVisualsEnabled(bool enabledState)
    {
        if (spriteRenderers == null || animators == null)
            CacheVisualComponents();

        if (spriteRenderers != null)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                    spriteRenderers[i].enabled = enabledState;
            }
        }

        if (animators != null)
        {
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                    animators[i].enabled = enabledState;
            }
        }
    }

    private void EnsureTrigger()
    {
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }
}