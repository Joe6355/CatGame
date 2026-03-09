using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerEnvironmentModule : MonoBehaviour
{
    // Активные зоны сугробов и их модификаторы
    private readonly Dictionary<SnowdriftArea2D, (float move, float jump)> activeSnow =
        new Dictionary<SnowdriftArea2D, (float move, float jump)>();

    // Одноразовый внешний ветер по X.
    // Обычно накапливается за кадр/тик физики и сбрасывается PlayerController после ApplyMovement().
    private float externalWindVX = 0f;

    // Итоговые множители от всех активных сугробов.
    private float snowMoveMultiplier = 1f;
    private float snowJumpMultiplier = 1f;

    /// <summary>
    /// Текущий внешний ветер по X, накопленный за этот физический тик.
    /// </summary>
    public float ExternalWindVX => externalWindVX;

    /// <summary>
    /// Итоговый множитель движения от всех активных сугробов.
    /// </summary>
    public float SnowMoveMultiplier => snowMoveMultiplier;

    /// <summary>
    /// Итоговый множитель прыжка от всех активных сугробов.
    /// </summary>
    public float SnowJumpMultiplier => snowJumpMultiplier;

    /// <summary>
    /// Добавить одноразовый внешний ветер по X.
    /// Обычно вызывается зонами ветра до ближайшего FixedUpdate.
    /// </summary>
    public void AddExternalWind(float vx)
    {
        externalWindVX += vx;
    }

    /// <summary>
    /// Сбросить накопленный внешний ветер.
    /// Вызывать после применения движения в физическом тике.
    /// </summary>
    public void ClearFrameWind()
    {
        externalWindVX = 0f;
    }

    /// <summary>
    /// Зарегистрировать активный сугроб.
    /// Если тот же area уже есть, его значения обновятся.
    /// </summary>
    public void RegisterSnow(SnowdriftArea2D area, float moveMul, float jumpMul)
    {
        if (area == null)
            return;

        activeSnow[area] = (Mathf.Clamp01(moveMul), Mathf.Clamp01(jumpMul));
        RecalcSnow();
    }

    /// <summary>
    /// Убрать сугроб из активных.
    /// </summary>
    public void UnregisterSnow(SnowdriftArea2D area)
    {
        if (area == null)
            return;

        if (activeSnow.Remove(area))
            RecalcSnow();
    }

    /// <summary>
    /// Полный сброс окружения.
    /// Можно использовать при респавне/сбросе состояния.
    /// </summary>
    public void ResetEnvironmentState()
    {
        activeSnow.Clear();
        externalWindVX = 0f;
        snowMoveMultiplier = 1f;
        snowJumpMultiplier = 1f;
    }

    private void OnDisable()
    {
        ResetEnvironmentState();
    }

    private void RecalcSnow()
    {
        if (activeSnow.Count == 0)
        {
            snowMoveMultiplier = 1f;
            snowJumpMultiplier = 1f;
            return;
        }

        float move = 1f;
        float jump = 1f;

        foreach (var kv in activeSnow.Values)
        {
            move = Mathf.Min(move, kv.move);
            jump = Mathf.Min(jump, kv.jump);
        }

        snowMoveMultiplier = Mathf.Clamp01(move);
        snowJumpMultiplier = Mathf.Clamp01(jump);
    }
}