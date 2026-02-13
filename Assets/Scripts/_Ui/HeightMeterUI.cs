using UnityEngine;
using TMPro;

public class HeightMeterUI : MonoBehaviour
{
    [Header("Что измеряем")]
    [SerializeField, Tooltip("Кого измеряем (обычно Player). Если пусто — будет искать объект с тегом Player.")]
    private Transform target;

    [SerializeField, Tooltip(
        "Нулевая точка (0 м). Если назначишь Transform — ноль будет по его Y.\n" +
        "Если пусто — ноль считается как worldY = 0.")]
    private Transform zeroPoint;

    [Header("Шаг высоты")]
    [SerializeField, Tooltip("Шаг квантования высоты (в метрах/юнитах). По умолчанию 3.")]
    private float stepMeters = 3f;

    [SerializeField, Tooltip("Если ВКЛ — отрицательная высота обрезается до 0.")]
    private bool clampToZero = true;

    [Header("UI")]
    [SerializeField, Tooltip("Сюда перетащи TMP_Text, который стоит в левом верхнем углу.")]
    private TMP_Text heightText;

    [SerializeField, Tooltip("Формат текста. {0} = высота числом.")]
    private string format = "Height: {0} m";

    // чтобы не спамить SetText каждый кадр
    private int lastShownValue = int.MinValue;

    private void Awake()
    {
        if (target == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) target = go.transform;
        }
    }

    private void Update()
    {
        if (heightText == null || target == null) return;

        float zeroY = (zeroPoint != null) ? zeroPoint.position.y : 0f;
        float raw = target.position.y - zeroY;

        if (clampToZero && raw < 0f) raw = 0f;

        float step = Mathf.Max(0.0001f, stepMeters);

        // "с шагом 3м": показываем ближайший НИЖНИЙ уровень (0, 3, 6, 9...)
        int steppedValue = Mathf.FloorToInt(raw / step) * Mathf.RoundToInt(step);

        // если step не целый, то лучше так:
        // int steppedValue = Mathf.FloorToInt(raw / step) * Mathf.RoundToInt(step);

        // но для любых шагов (в т.ч. нецелых) корректнее:
        // float stepped = Mathf.Floor(raw / step) * step;
        // int steppedValue = Mathf.RoundToInt(stepped);

        // сделаем универсально:
        float stepped = Mathf.Floor(raw / step) * step;
        int displayValue = Mathf.RoundToInt(stepped);

        if (displayValue == lastShownValue) return;
        lastShownValue = displayValue;

        heightText.SetText(format, displayValue);
    }
}
