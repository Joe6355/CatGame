using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PlayerOrbFeetTrigger2D : MonoBehaviour
{
    [Header("Feet -> Orb")]
    [SerializeField] private PlayerJumpModule jumpModule;
    [SerializeField] private PlayerGroundModule groundModule;

    [SerializeField, Tooltip("Если ВКЛ — орб можно активировать только когда игрок реально в воздухе.")]
    private bool requireAirbornePlayer = true;

    private Collider2D feetTrigger;

    private void Reset()
    {
        CacheRefs();
        EnsureTrigger();
    }

    private void Awake()
    {
        CacheRefs();
        EnsureTrigger();
    }

    private void OnValidate()
    {
        CacheRefs();
        EnsureTrigger();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryUseOrb(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryUseOrb(other);
    }

    private void TryUseOrb(Collider2D other)
    {
        if (other == null)
            return;

        if (jumpModule == null)
            return;

        if (requireAirbornePlayer && groundModule != null && groundModule.IsGrounded)
            return;

        OrbJumpRefresh2D orb = other.GetComponentInParent<OrbJumpRefresh2D>();
        if (orb == null)
            return;

        orb.TryConsumeByPlayer(jumpModule);
    }

    private void CacheRefs()
    {
        if (jumpModule == null)
            jumpModule = GetComponentInParent<PlayerJumpModule>();

        if (groundModule == null)
            groundModule = GetComponentInParent<PlayerGroundModule>();

        if (feetTrigger == null)
            feetTrigger = GetComponent<Collider2D>();
    }

    private void EnsureTrigger()
    {
        if (feetTrigger == null)
            feetTrigger = GetComponent<Collider2D>();

        if (feetTrigger != null)
            feetTrigger.isTrigger = true;
    }
}