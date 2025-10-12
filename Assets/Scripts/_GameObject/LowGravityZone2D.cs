using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LowGravityZone2D : MonoBehaviour
{
    [Header("���� �������")]
    [SerializeField] private LayerMask affectedLayers; // ������ ���� Player

    [Header("������")]
    [SerializeField, Tooltip("��������� � �������� gravityScale ������ (<1 = ������ ����������)")]
    private float gravityScaleMultiplier = 0.35f;

    [SerializeField, Tooltip("������ ������ ���������� ��� �����/������")]
    private bool smoothTransition = true;

    [SerializeField, Tooltip("������������ �������� ��������, ���")]
    private float blendTime = 0.15f;

    // ������ �������� gravityScale ��� ������� Rigidbody2D, ����� ������� ��� ������
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
            original[rb] = rb.gravityScale; // ��������, ����� �������

        SetGravityScale(rb, original[rb] * gravityScaleMultiplier);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!Affects(other)) return;

        var rb = other.attachedRigidbody;
        if (!rb) return;

        if (original.TryGetValue(rb, out float orig))
        {
            SetGravityScale(rb, orig); // ���������� ��� ����
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
        while (t < time && rb) // �� ������, ���� ������ ����������
        {
            t += Time.deltaTime;
            rb.gravityScale = Mathf.Lerp(start, target, t / time);
            yield return null;
        }
        if (rb) rb.gravityScale = target;
        blends.Remove(rb);
    }
}
