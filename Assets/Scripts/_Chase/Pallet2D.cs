using UnityEngine;

/// <summary>
/// Палетка: игрок может её "скинуть" (активировать) кнопкой.
/// В активном состоянии палетка падает и становится препятствием (IsBlocking=true).
/// Преследователь, упершись, начнет ломать и после ломания палетка удаляется.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Pallet2D : MonoBehaviour
{
    [Header("Состояние до активации")]
    [Tooltip("Коллайдер до активации (обычно IsTrigger=ON или Disabled), можно оставить тот же.")]
    [SerializeField] private Collider2D preCollider;
    [Tooltip("RigidBody до активации (обычно isKinematic=true/Gravity=0).")]
    [SerializeField] private Rigidbody2D preBody;

    [Header("Состояние после активации (падение)")]
    [Tooltip("Включить гравитацию, сделать коллайдер твёрдым и начать падать.")]
    [SerializeField] private float postGravityScale = 2.5f;
    [SerializeField] private bool postColliderIsTrigger = false;

    [Header("Общее")]
    [SerializeField] private bool startInactive = true;
    [SerializeField] private string palletTag = "Pallet";

    public bool IsBlocking { get; private set; } = false;
    public bool IsActivated { get; private set; } = false;

    private Collider2D ownCol;
    private Rigidbody2D body;

    private void Awake()
    {
        ownCol = GetComponent<Collider2D>();
        body = GetComponent<Rigidbody2D>();
        gameObject.tag = palletTag;

        if (preCollider == null) preCollider = ownCol;
        if (preBody == null) preBody = body;

        if (startInactive)
            ApplyPreState();
        else
            ApplyPostState();
    }

    private void ApplyPreState()
    {
        IsActivated = false;
        IsBlocking = false;

        if (preBody)
        {
            preBody.isKinematic = true;
            preBody.gravityScale = 0f;
            preBody.velocity = Vector2.zero;
            preBody.angularVelocity = 0f;
        }
        if (preCollider)
        {
            preCollider.enabled = true;
            preCollider.isTrigger = true; // до активации — не блокирует
        }
    }

    private void ApplyPostState()
    {
        IsActivated = true;
        IsBlocking = true;

        if (body)
        {
            body.isKinematic = false;
            body.gravityScale = postGravityScale;
        }
        if (ownCol)
        {
            ownCol.enabled = true;
            ownCol.isTrigger = postColliderIsTrigger; // обычно false => твердое препятствие
        }
    }

    /// <summary>Вызывается игроком при нажатии кнопки рядом с палеткой.</summary>
    public void Activate()
    {
        if (IsActivated) return;
        ApplyPostState();
    }

    /// <summary>Вызывается преследователем после "ломания".</summary>
    public void BreakNow()
    {
        Destroy(gameObject);
    }
}
