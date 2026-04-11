using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PressureButton2D : MonoBehaviour
{
    [Header("Кого считаем «нажимателем»")]
    [Tooltip("Реагировать на игрока (Tag = Player).")]
    public bool acceptPlayer = true;
    [Tooltip("Реагировать на ящики (Tag = Box или box).")]
    public bool acceptBox = true;

    [Header("Условие нажатия")]
    [Tooltip("Требовать, чтобы объект НАЖИМАЛ СВЕРХУ (по нормали контакта). Для триггера отключи.")]
    public bool requireTopContact = true;
    [Range(0f, 1f)]
    public float topContactMinY = 0.4f;

    [Header("Какие двери управляем")]
    public DoorToggle[] doors;

    [Header("Визуал (необязательно)")]
    [Tooltip("Что включать, когда кнопка нажата (например, «вдавленный» спрайт).")]
    public GameObject pressedVisual;
    [Tooltip("Что включать, когда кнопка отпущена.")]
    public GameObject idleVisual;

    // — runtime —
    private Collider2D _col;
    private readonly HashSet<int> _pressingIds = new HashSet<int>(); // instanceID коллайдеров/ригидов
    private bool _pressed;

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (!_col) Debug.LogWarning("[PressureButton2D] Нет Collider2D.");
        UpdateVisual();
        UpdateDoors(false);
    }

    // ——— TRIGGER ———
    void OnTriggerEnter2D(Collider2D other) { if (_col.isTrigger) TryAdd(other, fromTrigger: true, topOK: true); }
    void OnTriggerExit2D(Collider2D other) { if (_col.isTrigger) TryRemove(other); }

    // ——— COLLISION ———
    void OnCollisionEnter2D(Collision2D c) { if (!_col.isTrigger) TryAdd(c.collider, false, TopOK(c)); }
    void OnCollisionStay2D(Collision2D c) { if (!_col.isTrigger) TryAdd(c.collider, false, TopOK(c)); }
    void OnCollisionExit2D(Collision2D c) { if (!_col.isTrigger) TryRemove(c.collider); }

    private bool TopOK(Collision2D c)
    {
        if (!requireTopContact) return true;
        Vector2 n = Vector2.zero;
        for (int i = 0; i < c.contactCount; i++) n += c.GetContact(i).normal;
        if (c.contactCount > 0) n /= c.contactCount;
        return n.y >= topContactMinY;
    }

    private void TryAdd(Collider2D other, bool fromTrigger, bool topOK)
    {
        if (!IsValidTag(other)) return;
        if (!fromTrigger && requireTopContact && !topOK) return;

        int id = GetOwnerId(other);
        if (_pressingIds.Add(id))
        {
            SetPressed(true);
        }
    }

    private void TryRemove(Collider2D other)
    {
        int id = GetOwnerId(other);
        if (_pressingIds.Remove(id))
        {
            if (_pressingIds.Count == 0) SetPressed(false);
        }
    }

    private int GetOwnerId(Collider2D c)
    {
        // объединяем все коллайдеры одного объекта по instanceID Rigidbody/Transform
        if (c.attachedRigidbody) return c.attachedRigidbody.GetInstanceID();
        return c.transform.GetInstanceID();
    }

    private bool IsValidTag(Collider2D other)
    {
        if (acceptPlayer && other.CompareTag("Player")) return true;
        if (acceptBox && (other.CompareTag("Box") || other.CompareTag("box"))) return true;
        return false;
    }

    private void SetPressed(bool value)
    {
        if (_pressed == value) return;
        _pressed = value;
        UpdateVisual();
        UpdateDoors(_pressed);
    }

    private void UpdateVisual()
    {
        if (pressedVisual) pressedVisual.SetActive(_pressed);
        if (idleVisual) idleVisual.SetActive(!_pressed);
    }

    private void UpdateDoors(bool pressed)
    {
        if (doors == null) return;
        foreach (var d in doors)
            if (d) d.SetOpen(pressed);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _pressed ? Color.green : Color.red;
        var b = GetComponent<Collider2D>();
        if (b is BoxCollider2D box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.offset, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (b is CircleCollider2D cir)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(cir.offset, cir.radius);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
#endif
}
