using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    public Transform target;      // герой
    public float smoothSpeed = 5f;
    public Vector2 offset;        // смещение по X и Y

    private float fixedZ;

    void Start()
    {
        // запоминаем Z камеры и больше его не меняем
        fixedZ = transform.position.z;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            fixedZ
        );

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smoothSpeed * Time.deltaTime
        );
    }
}
