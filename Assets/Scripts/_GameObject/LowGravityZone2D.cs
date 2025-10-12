using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LowGravityZone2D : MonoBehaviour
{
    [Header("Кого трогать")]
    [SerializeField] private LayerMask affectedLayers; // обычно слой Player

    [Header("Эффект")]
    [SerializeField, Tooltip("Множитель к текущему gravityScale игрока (<1 = слабее гравитация)")]
    private float gravityScaleMultiplier = 0.35f;

    [SerializeField, Tooltip("Плавно менять гравитацию при входе/выходе")]
    private bool smoothTransition = true;

    [SerializeField, Tooltip("Длительность плавного перехода, сек")]
    private float blendTime = 0.15f;

    // Храним исходный gravityScale для каждого Rigidbody2D, чтобы вернуть при выходе
    private readonly Dictionary<Rigidbody2D, float> original = new();
    private readonly Dictionary<Rigidbody2D, Coroutine> blends = new();

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnValidate()
    {
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;
    }

    private bool Affects(Collider2D other)
    {
        return (affectedLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Affects(other)) return;

        var rb = other.attachedRigidbody;
        if (!rb) return;

        if (!original.ContainsKey(rb))
            original[rb] = rb.gravityScale; // запомним, чтобы вернуть

        SetGravityScale(rb, original[rb] * gravityScaleMultiplier);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!Affects(other)) return;

        var rb = other.attachedRigidbody;
        if (!rb) return;

        if (original.TryGetValue(rb, out float orig))
        {
            SetGravityScale(rb, orig); // возвращаем как было
            original.Remove(rb);
        }
    }

    private void SetGravityScale(Rigidbody2D rb, float target)
    {
        if (!smoothTransition || blendTime <= 0f)
        {
            rb.gravityScale = target;
            return;
        }

        if (blends.TryGetValue(rb, out var c)) StopCoroutine(c);
        blends[rb] = StartCoroutine(BlendGravity(rb, target, blendTime));
    }

    private IEnumerator BlendGravity(Rigidbody2D rb, float target, float time)
    {
        float start = rb.gravityScale;
        float t = 0f;
        while (t < time && rb) // на случай, если объект уничтожили
        {
            t += Time.deltaTime;
            rb.gravityScale = Mathf.Lerp(start, target, t / time);
            yield return null;
        }
        if (rb) rb.gravityScale = target;
        blends.Remove(rb);
    }
}
