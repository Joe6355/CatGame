using UnityEngine;

public class SimpleSmoothCamera2D : MonoBehaviour
{
    public Transform target;

    [Header("Offset")]
    public Vector2 offset = new Vector2(0f, 1.5f);

    [Header("Smoothing")]
    [Tooltip("Меньше = камера быстрее")]
    public float smoothTime = 0.15f;

    private Vector3 velocity;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desired = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desired,
            ref velocity,
            smoothTime
        );
    }
}
