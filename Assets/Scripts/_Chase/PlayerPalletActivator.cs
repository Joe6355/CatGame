using UnityEngine;

/// <summary>
/// Вешается на игрока. Позволяет активировать (скидывать) ближайшую палетку по кнопке.
/// </summary>
public class PlayerPalletActivator : MonoBehaviour
{
    [Header("Клавиша")]
    [SerializeField, Tooltip(
        "Кнопка активации палетки (скинуть/включить ближайшую).\n" +
        "Работает в Update по нажатию (GetKeyDown).\n" +
        "Рекоменд: F / E (часто используют E).")]
    private KeyCode activateKey = KeyCode.F; // можно переназначить

    [Header("Поиск палетки")]
    [SerializeField, Tooltip(
        "Радиус поиска палетки вокруг игрока (в мировых единицах).\n" +
        "Чем больше — тем дальше можно активировать.\n" +
        "Рекоменд: 0.8–2.0 (часто 1.0–1.5).")]
    private float activateRadius = 1.2f;

    [SerializeField, Tooltip(
        "LayerMask палеток: поиск идёт только по этим слоям.\n" +
        "Рекоменд: создать слой 'Pallet' и выбрать его здесь.\n" +
        "Важно: если маска пустая/неправильная — палетки не найдутся.")]
    private LayerMask palletMask;

    [SerializeField, Tooltip(
        "Тег объектов палеток (доп. фильтр после LayerMask).\n" +
        "Рекоменд: оставить 'Pallet' и назначить этот Tag всем палеткам.\n" +
        "Если тег не совпадает — объект будет игнорироваться.")]
    private string palletTag = "Pallet";

    [Header("Подсветка / Gizmos (опционально)")]
    [SerializeField, Tooltip(
        "Если ВКЛ — рисуем сферу радиуса поиска в Gizmos.\n" +
        "Видно в Scene, а в Game — если включить кнопку Gizmos.\n" +
        "Рекоменд: ВКЛ (true) на этапе настройки, потом можно выключить.")]
    private bool drawGizmos = true;

    [SerializeField, Tooltip(
        "Цвет Gizmos-сферы радиуса поиска.\n" +
        "Рекоменд: полупрозрачный цвет (alpha ~0.15–0.35), чтобы не мешал.")]
    private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.2f);

    private void Update()
    {
        // Нажали кнопку — пытаемся активировать ближайшую палетку
        if (!Input.GetKeyDown(activateKey)) return;

        // Ищем все коллайдеры палеток в радиусе (с учётом palletMask)
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, activateRadius, palletMask);

        Pallet2D best = null;
        float bestDist = float.MaxValue;

        foreach (var h in hits)
        {
            // защита от мусора
            if (!h || !h.gameObject.activeInHierarchy) continue;

            // доп. фильтр по тегу (чтобы случайно не активировать лишнее)
            if (!h.CompareTag(palletTag)) continue;

            // Палетка может быть на этом объекте или в родителе
            var p = h.GetComponent<Pallet2D>() ?? h.GetComponentInParent<Pallet2D>();

            // если палетки нет или уже активирована — пропускаем
            if (p == null || p.IsActivated) continue;

            // расстояние до ближайшей точки коллайдера (не до центра) — работает приятнее, особенно у больших палеток
            float d = Vector2.SqrMagnitude(
                (Vector2)h.bounds.ClosestPoint(transform.position) - (Vector2)transform.position
            );

            // выбираем ближайшую подходящую палетку
            if (d < bestDist) { bestDist = d; best = p; }
        }

        // Активируем найденную
        if (best != null) best.Activate();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Рисуем только когда объект выделен (и только в редакторе)
        if (!drawGizmos) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, activateRadius);
    }
#endif
}
