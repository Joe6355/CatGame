using UnityEngine;

public class VisionManipulater : MonoBehaviour
{
    [Header("Размер камеры если вышли вправо")]
    [SerializeField] private float rightCameraSize = 6f;

    [Header("Размер камеры если вышли влево")]
    [SerializeField] private float leftCameraSize = 6f;

    private void OnTriggerExit2D(Collider2D other)
    {
        // Ищем PlayerController на объекте или в родителях (работает даже если коллайдер на child)
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        Transform t = player.transform; // корень игрока

        float size = (t.position.x > transform.position.x) ? rightCameraSize : leftCameraSize;
        CamController.ChangeCameraSizeEvent?.Invoke(size);
    }
}
