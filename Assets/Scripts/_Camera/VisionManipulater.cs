using System.Collections.Generic;
using UnityEngine;

public class VisionManipulater : MonoBehaviour
{
    [Header("Размер камеры если вышли вправо")]
    [SerializeField] private float rightCameraSize = 6f;

    [Header("Размер камеры если вышли влево")]
    [SerializeField] private float leftCameraSize = 6f;

    [Header("Смещение камеры по Y (TrackedObjectOffset.y)")]
    [SerializeField, Tooltip(
        "Сдвиг кадра ВВЕРХ/ВНИЗ в мировых единицах.\n" +
        "+ = камеру выше (видно больше вверх), - = ниже (видно больше вниз).\n" +
        "Пример: +1.5 чтобы лучше видеть платформы выше.")]
    private float rightCameraYOffset = 0f;

    [SerializeField, Tooltip(
        "Сдвиг кадра ВВЕРХ/ВНИЗ в мировых единицах.\n" +
        "+ = камеру выше, - = ниже.")]
    private float leftCameraYOffset = 0f;

    [Header("Дополнительно")]
    [SerializeField, Tooltip(
        "Если ВКЛ — при выходе из зоны меняем не только размер, но и вертикальный сдвиг.\n" +
        "Рекоменд: ВКЛ.")]
    private bool changeYOffsetToo = true;

    // считаем, сколько коллайдеров конкретного игрока сейчас внутри триггера
    private readonly Dictionary<Transform, int> _insideCounts = new Dictionary<Transform, int>();

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ищем PlayerController на объекте или в родителях (работает даже если коллайдер на child)
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        Transform t = player.transform;

        _insideCounts.TryGetValue(t, out int c);
        _insideCounts[t] = c + 1;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        Transform t = player.transform;

        if (!_insideCounts.TryGetValue(t, out int c)) return;

        c -= 1;
        if (c > 0)
        {
            _insideCounts[t] = c;
            return; // ещё не все коллайдеры вышли
        }

        _insideCounts.Remove(t); // игрок окончательно вышел

        // лучше брать центр вышедшего коллайдера, чем pivot игрока
        float x = other.bounds.center.x;

        bool exitedToRight = x > transform.position.x;

        float size = exitedToRight ? rightCameraSize : leftCameraSize;
        CamController.ChangeCameraSizeEvent?.Invoke(size);

        if (changeYOffsetToo)
        {
            float yOff = exitedToRight ? rightCameraYOffset : leftCameraYOffset;
            CamController.ChangeCameraYOffsetEvent?.Invoke(yOff);
        }
    }
}
