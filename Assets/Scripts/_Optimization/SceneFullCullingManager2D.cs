using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Менеджер полного включения/выключения объектов на сцене.
/// 
/// Логика:
/// - ищет все SceneFullCullingTarget2D;
/// - считает дистанцию от activationCenter;
/// - если объект далеко — полностью выключает root GameObject;
/// - если объект снова рядом — включает обратно.
/// 
/// ВАЖНО:
/// - этот менеджер должен всегда оставаться активным;
/// - не добавляй в цели игрока, камеру, EventSystem, Canvas, AudioManager, SaveManager и сам OptimizationManager;
/// - для шипов и опасностей ставь большой радиус включения.
/// </summary>
public class SceneFullCullingManager2D: MonoBehaviour
{
    [Header("Центр активации")]
    [SerializeField, Tooltip("От кого считать расстояние. Обычно Player или Main Camera.")]
    private Transform activationCenter;

    [Header("Радиусы")]
    [SerializeField, Min(0.1f), Tooltip("Если объект ближе этой дистанции — он включается.")]
    private float enableDistance = 35f;

    [SerializeField, Min(0.1f), Tooltip("Если объект дальше этой дистанции — он выключается. Должно быть больше enableDistance.")]
    private float disableDistance = 45f;

    [Header("Проверка")]
    [SerializeField, Min(0.02f), Tooltip("Как часто проверять объекты. Нормально: 0.2–0.5.")]
    private float checkInterval = 0.25f;

    [SerializeField, Min(1), Tooltip("Сколько объектов максимум проверять за один проход корутины. Если объектов мало — можно поставить 99999.")]
    private int maxChecksPerStep = 99999;

    [Header("Поиск объектов")]
    [SerializeField, Tooltip("Если ВКЛ — менеджер сам найдёт все SceneFullCullingTarget2D на сцене.")]
    private bool autoFindTargetsOnAwake = true;

    [SerializeField, Tooltip("Если ВКЛ — менеджер попробует найти даже выключенные объекты с SceneFullCullingTarget2D.")]
    private bool includeInactiveTargets = true;

    [SerializeField, Tooltip("Если ВКЛ — при старте сцены сразу применит включение/выключение.")]
    private bool applyStateOnStart = true;

    [Header("Ручной список")]
    [SerializeField, Tooltip("Сюда можно вручную добавить цели, если не хочешь использовать автопоиск.")]
    private SceneFullCullingTarget2D[] manualTargets;

    [Header("Защита")]
    [SerializeField, Tooltip("Если ВКЛ — менеджер не будет выключать объекты, которые являются родителями activationCenter.")]
    private bool protectActivationCenterParents = true;

    [Header("Отладка")]
    [SerializeField, Tooltip("Если ВКЛ — пишет служебные сообщения в консоль.")]
    private bool debugLogs = false;

    [SerializeField, Tooltip("Если ВКЛ — рисует радиусы в Scene View при выделении менеджера.")]
    private bool drawGizmos = true;

    private readonly List<SceneFullCullingTarget2D> targets = new List<SceneFullCullingTarget2D>();

    private Coroutine checkRoutine;
    private int checkIndex;

    private void Reset()
    {
        activationCenter = transform;
    }

    private void Awake()
    {
        if (activationCenter == null)
            activationCenter = transform;

        ValidateDistances();
        CollectTargets();

        if (applyStateOnStart)
            CheckAllTargets(true);
    }

    private void OnEnable()
    {
        if (checkRoutine != null)
            StopCoroutine(checkRoutine);

        checkRoutine = StartCoroutine(CheckLoop());
    }

    private void OnDisable()
    {
        if (checkRoutine != null)
        {
            StopCoroutine(checkRoutine);
            checkRoutine = null;
        }
    }

    private void OnValidate()
    {
        ValidateDistances();
    }

    private void ValidateDistances()
    {
        if (enableDistance < 0.1f)
            enableDistance = 0.1f;

        if (disableDistance <= enableDistance)
            disableDistance = enableDistance + 1f;

        if (checkInterval < 0.02f)
            checkInterval = 0.02f;

        if (maxChecksPerStep < 1)
            maxChecksPerStep = 1;
    }

    private IEnumerator CheckLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(checkInterval);

        while (true)
        {
            CheckTargetsStep(false);
            yield return wait;
        }
    }

    private void CollectTargets()
    {
        targets.Clear();

        if (autoFindTargetsOnAwake)
        {
            SceneFullCullingTarget2D[] foundTargets = FindTargetsInScene();

            for (int i = 0; i < foundTargets.Length; i++)
                AddTarget(foundTargets[i]);
        }

        if (manualTargets != null)
        {
            for (int i = 0; i < manualTargets.Length; i++)
                AddTarget(manualTargets[i]);
        }

        if (debugLogs)
            Debug.Log($"[SceneFullCullingManager2D] Найдено объектов для culling: {targets.Count}", this);
    }

    private SceneFullCullingTarget2D[] FindTargetsInScene()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<SceneFullCullingTarget2D>(
            includeInactiveTargets ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
#else
        return Object.FindObjectsOfType<SceneFullCullingTarget2D>(includeInactiveTargets);
#endif
    }

    private void AddTarget(SceneFullCullingTarget2D target)
    {
        if (target == null)
            return;

        if (targets.Contains(target))
            return;

        if (target.gameObject == gameObject)
            return;

        if (activationCenter != null && protectActivationCenterParents)
        {
            if (activationCenter == target.transform || activationCenter.IsChildOf(target.transform))
            {
                if (debugLogs)
                    Debug.LogWarning($"[SceneFullCullingManager2D] Пропущен объект, потому что он содержит activationCenter: {target.name}", target);

                return;
            }
        }

        targets.Add(target);
    }

    private void CheckAllTargets(bool isStartCheck)
    {
        if (activationCenter == null)
            return;

        for (int i = 0; i < targets.Count; i++)
            CheckTarget(targets[i], isStartCheck);
    }

    private void CheckTargetsStep(bool isStartCheck)
    {
        if (activationCenter == null)
            return;

        if (targets.Count <= 0)
            return;

        int checks = Mathf.Min(maxChecksPerStep, targets.Count);

        for (int i = 0; i < checks; i++)
        {
            if (checkIndex >= targets.Count)
                checkIndex = 0;

            SceneFullCullingTarget2D target = targets[checkIndex];
            checkIndex++;

            CheckTarget(target, isStartCheck);
        }
    }

    private void CheckTarget(SceneFullCullingTarget2D target, bool isStartCheck)
    {
        if (target == null)
            return;

        if (target.AlwaysActive)
        {
            target.SetActiveByCulling(true);
            return;
        }

        if (isStartCheck && !target.AllowStartDisabled)
            return;

        Vector3 centerPosition = activationCenter.position;
        Vector3 targetPosition = target.GetCullingPosition();

        float distanceSqr = (targetPosition - centerPosition).sqrMagnitude;

        float currentEnableDistance = enableDistance + target.ExtraEnableDistance;
        float currentDisableDistance = disableDistance + target.ExtraDisableDistance;

        if (currentDisableDistance <= currentEnableDistance)
            currentDisableDistance = currentEnableDistance + 1f;

        float enableDistanceSqr = currentEnableDistance * currentEnableDistance;
        float disableDistanceSqr = currentDisableDistance * currentDisableDistance;

        bool isActive = target.IsActiveByCulling();

        if (isActive)
        {
            if (distanceSqr >= disableDistanceSqr)
                target.SetActiveByCulling(false);
        }
        else
        {
            if (distanceSqr <= enableDistanceSqr)
                target.SetActiveByCulling(true);
        }
    }

    [ContextMenu("Refresh Targets")]
    public void RefreshTargets()
    {
        CollectTargets();
        CheckAllTargets(false);
    }

    [ContextMenu("Enable All Targets")]
    public void EnableAllTargets()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == null)
                continue;

            targets[i].SetActiveByCulling(true);
        }
    }

    [ContextMenu("Disable Far Targets Now")]
    public void DisableFarTargetsNow()
    {
        CheckAllTargets(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Transform centerTransform = activationCenter != null ? activationCenter : transform;
        Vector3 center = centerTransform.position;

        Gizmos.DrawWireSphere(center, enableDistance);
        Gizmos.DrawWireSphere(center, disableDistance);
    }
}