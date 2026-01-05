using UnityEngine;

[RequireComponent(typeof(Transform))]
public class PlayerSquashStretch : MonoBehaviour
{
    [Header("Настройки эффектов")]
    [SerializeField] private float maxStretchY = 1.2f;
    [SerializeField] private float maxSquashY = 0.8f;
    [SerializeField] private float maxSquashX = 1.2f;
    [SerializeField] private float wallSquashAmount = 0.8f;
    [SerializeField] private float speedMultiplier = 0.02f;
    [SerializeField] private float smoothTime = 0.1f;

    private Transform _tr;
    private Rigidbody2D _rb;
    private Vector3 _targetScale;
    private Vector3 _currentVelocity;
    private Vector3 _originalScale;

    private void Awake()
    {
        _tr = transform;
        _rb = GetComponent<Rigidbody2D>();
        _originalScale = new Vector3(Mathf.Abs(_tr.localScale.x), _tr.localScale.y, _tr.localScale.z);
        _targetScale = _originalScale;
    }

    private void Update()
    {
        UpdateSquashStretch();
        ApplyScale();
    }

    private void UpdateSquashStretch()
    {
        float vy = _rb.velocity.y;
        float vx = _rb.velocity.x;

        // Прыжок вверх
        if (vy > 0.01f)
        {
            _targetScale.y = Mathf.Lerp(_originalScale.y, _originalScale.y * maxStretchY, vy * speedMultiplier);
            _targetScale.x = Mathf.Lerp(_originalScale.x, _originalScale.x * (2f - _targetScale.y / _originalScale.y), vy * speedMultiplier);
        }
        // Падение
        else if (vy < -0.01f)
        {
            _targetScale.y = Mathf.Lerp(_originalScale.y, _originalScale.y * maxSquashY, -vy * speedMultiplier);
            _targetScale.x = Mathf.Lerp(_originalScale.x, _originalScale.x * maxSquashX, -vy * speedMultiplier);
        }
        else
        {
            _targetScale = _originalScale;
        }

        // Столкновение со стеной
        RaycastHit2D hit = Physics2D.Raycast(_tr.position, Vector2.right * Mathf.Sign(vx), 0.1f, LayerMask.GetMask("Wall"));
        if (hit.collider != null)
        {
            _targetScale.x = _originalScale.x * wallSquashAmount;
            _targetScale.y = _originalScale.y * (2f - wallSquashAmount);
        }

        _targetScale.x = Mathf.Clamp(_targetScale.x, _originalScale.x * 0.8f, _originalScale.x * 1.2f);
        _targetScale.y = Mathf.Clamp(_targetScale.y, _originalScale.y * 0.8f, _originalScale.y * 1.2f);
    }

    private void ApplyScale()
    {
        Vector3 newScale = Vector3.SmoothDamp(new Vector3(Mathf.Abs(_tr.localScale.x), _tr.localScale.y, _tr.localScale.z),
                                              _targetScale, ref _currentVelocity, smoothTime);
        // Сохраняем знак x из исходного localScale для направления
        newScale.x *= Mathf.Sign(_tr.localScale.x);
        _tr.localScale = newScale;
    }

    public void TriggerWallBounce()
    {
        _targetScale.x = _originalScale.x * wallSquashAmount;
        _targetScale.y = _originalScale.y * (2f - wallSquashAmount);
    }
}
