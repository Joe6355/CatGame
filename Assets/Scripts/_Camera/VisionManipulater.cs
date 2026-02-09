using System.Collections.Generic;
using UnityEngine;

public class VisionManipulater : MonoBehaviour
{
    [Header("–азмер камеры если вышли вправо")]
    [SerializeField] private float rightCameraSize = 6f;

    [Header("–азмер камеры если вышли влево")]
    [SerializeField] private float leftCameraSize = 6f;

    // считаем, сколько коллайдеров конкретного игрока сейчас внутри триггера
    private readonly Dictionary<Transform, int> _insideCounts = new Dictionary<Transform, int>();

    private void OnTriggerEnter2D(Collider2D other)
    {
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
            return; // ещЄ не все коллайдеры вышли
        }

        _insideCounts.Remove(t); // игрок окончательно вышел

        // лучше брать центр вышедшего коллайдера, чем pivot игрока
        float x = other.bounds.center.x;

        float size = (x > transform.position.x) ? rightCameraSize : leftCameraSize;
        CamController.ChangeCameraSizeEvent?.Invoke(size);
    }
}
