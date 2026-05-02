using UnityEngine;

/// <summary>
/// Маркер объекта, который можно полностью включать/выключать через SceneFullCullingManager2D.
/// 
/// Вешать на root объекта:
/// - Lamp
/// - Spike
/// - Rat
/// - Decor
/// - любой другой объект, который можно полностью выключить вне зоны игрока
/// 
/// Сам объект может быть выключен через gameObject.SetActive(false),
/// потому что обратно его включает внешний активный менеджер.
/// </summary>
public class SceneFullCullingTarget2D: MonoBehaviour
{
    [Header("Точка проверки")]
    [SerializeField, Tooltip("Если указано — расстояние считается от этой точки. Если пусто — от transform объекта.")]
    private Transform cullingPoint;

    [Header("Настройки объекта")]
    [SerializeField, Tooltip("Если ВКЛ — менеджер никогда не выключит этот объект.")]
    private bool alwaysActive = false;

    [SerializeField, Tooltip("Если ВКЛ — объект может быть выключен сразу при старте сцены, если он далеко от центра активации.")]
    private bool allowStartDisabled = true;

    [SerializeField, Min(0f), Tooltip("Индивидуальная добавка к дистанции включения. Полезно для шипов, врагов, больших объектов.")]
    private float extraEnableDistance = 0f;

    [SerializeField, Min(0f), Tooltip("Индивидуальная добавка к дистанции выключения. Обычно 0.")]
    private float extraDisableDistance = 0f;

    [Header("Отладка")]
    [SerializeField, Tooltip("Если ВКЛ — пишет в консоль, когда объект включается/выключается.")]
    private bool debugLogs = false;

    public bool AlwaysActive => alwaysActive;
    public bool AllowStartDisabled => allowStartDisabled;
    public float ExtraEnableDistance => extraEnableDistance;
    public float ExtraDisableDistance => extraDisableDistance;

    public Vector3 GetCullingPosition()
    {
        if (cullingPoint != null)
            return cullingPoint.position;

        return transform.position;
    }

    public bool IsActiveByCulling()
    {
        return gameObject.activeSelf;
    }

    public void SetActiveByCulling(bool active)
    {
        if (alwaysActive)
            active = true;

        if (gameObject.activeSelf == active)
            return;

        gameObject.SetActive(active);

        if (debugLogs)
            Debug.Log($"[SceneFullCullingTarget2D] {(active ? "ON" : "OFF")}: {name}", this);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 point = cullingPoint != null ? cullingPoint.position : transform.position;

        Gizmos.DrawWireSphere(point, 0.25f);
    }
}