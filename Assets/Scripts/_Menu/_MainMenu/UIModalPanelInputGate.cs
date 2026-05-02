using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class UIModalPanelInputGate : MonoBehaviour
{
    private struct CanvasGroupState
    {
        public int lockCount;
        public float alpha;
        public bool interactable;
        public bool blocksRaycasts;
        public bool ignoreParentGroups;
    }

    private static readonly Dictionary<CanvasGroup, CanvasGroupState> SavedStates = new Dictionary<CanvasGroup, CanvasGroupState>();

    [Header("Эта верхняя панель")]
    [SerializeField] private CanvasGroup modalCanvasGroup;

    [Tooltip("Image/Graphic на верхней панели, который должен блокировать мышь. Можно оставить пустым.")]
    [SerializeField] private Graphic modalRaycastBlocker;

    [SerializeField] private bool forceModalActiveStateOnEnable = true;

    [Header("Что выбрать при открытии")]
    [SerializeField] private GameObject firstSelected;

    [SerializeField] private bool clearSelectionOnOpen = true;

    [Header("Нижние панели, которые надо заблокировать")]
    [Tooltip("Сюда кидай CanvasGroup нижних кнопок/панелей. Они останутся видимыми, но перестанут ловить ввод.")]
    [SerializeField] private CanvasGroup[] panelsToLockWhileOpen;

    [Tooltip("Если ВКЛ — нижние панели остаются видимыми. Если ВЫКЛ — ещё и скрываются через Alpha = 0.")]
    [SerializeField] private bool keepLockedPanelsVisible = true;

    [Tooltip("Если selected сейчас находится внутри заблокированной панели — selection будет очищен.")]
    [SerializeField] private bool clearSelectionFromLockedPanels = true;

    [Header("Возврат selection")]
    [SerializeField] private bool restorePreviousSelectionOnClose = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly List<CanvasGroup> lockedByThisInstance = new List<CanvasGroup>();
    private GameObject previousSelected;

    private void Reset()
    {
        modalCanvasGroup = GetComponent<CanvasGroup>();
        modalRaycastBlocker = GetComponent<Graphic>();
    }

    private void Awake()
    {
        if (modalCanvasGroup == null)
            modalCanvasGroup = GetComponent<CanvasGroup>();

        if (modalRaycastBlocker == null)
            modalRaycastBlocker = GetComponent<Graphic>();
    }

    private void OnEnable()
    {
        previousSelected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        ApplyModalActiveState();
        LockPanelsBelow();
        SelectFirstObject();
    }

    private void OnDisable()
    {
        UnlockPanelsBelow();

        if (restorePreviousSelectionOnClose)
            RestorePreviousSelection();
    }

    private void ApplyModalActiveState()
    {
        if (!forceModalActiveStateOnEnable)
            return;

        if (modalCanvasGroup != null)
        {
            modalCanvasGroup.alpha = 1f;
            modalCanvasGroup.interactable = true;
            modalCanvasGroup.blocksRaycasts = true;
        }

        if (modalRaycastBlocker != null)
            modalRaycastBlocker.raycastTarget = true;
    }

    private void LockPanelsBelow()
    {
        lockedByThisInstance.Clear();

        if (panelsToLockWhileOpen == null)
            return;

        for (int i = 0; i < panelsToLockWhileOpen.Length; i++)
        {
            CanvasGroup group = panelsToLockWhileOpen[i];

            if (group == null)
                continue;

            if (group == modalCanvasGroup)
                continue;

            LockCanvasGroup(group);
        }
    }

    private void LockCanvasGroup(CanvasGroup group)
    {
        if (group == null)
            return;

        if (lockedByThisInstance.Contains(group))
            return;

        CanvasGroupState state;

        if (!SavedStates.TryGetValue(group, out state))
        {
            state = new CanvasGroupState
            {
                lockCount = 0,
                alpha = group.alpha,
                interactable = group.interactable,
                blocksRaycasts = group.blocksRaycasts,
                ignoreParentGroups = group.ignoreParentGroups
            };
        }

        state.lockCount++;
        SavedStates[group] = state;

        if (!keepLockedPanelsVisible)
            group.alpha = 0f;

        group.interactable = false;
        group.blocksRaycasts = false;

        lockedByThisInstance.Add(group);

        if (clearSelectionFromLockedPanels)
            ClearSelectionIfInside(group.transform);

        if (debugLogs)
            Debug.Log($"[UIModalPanelInputGate] Locked: {group.name}", group);
    }

    private void UnlockPanelsBelow()
    {
        for (int i = lockedByThisInstance.Count - 1; i >= 0; i--)
        {
            CanvasGroup group = lockedByThisInstance[i];

            if (group == null)
                continue;

            UnlockCanvasGroup(group);
        }

        lockedByThisInstance.Clear();
    }

    private void UnlockCanvasGroup(CanvasGroup group)
    {
        if (group == null)
            return;

        CanvasGroupState state;

        if (!SavedStates.TryGetValue(group, out state))
            return;

        state.lockCount--;

        if (state.lockCount <= 0)
        {
            group.alpha = state.alpha;
            group.interactable = state.interactable;
            group.blocksRaycasts = state.blocksRaycasts;
            group.ignoreParentGroups = state.ignoreParentGroups;

            SavedStates.Remove(group);

            if (debugLogs)
                Debug.Log($"[UIModalPanelInputGate] Restored: {group.name}", group);

            return;
        }

        SavedStates[group] = state;

        if (!keepLockedPanelsVisible)
            group.alpha = 0f;

        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void SelectFirstObject()
    {
        if (EventSystem.current == null)
            return;

        if (clearSelectionOnOpen)
            EventSystem.current.SetSelectedGameObject(null);

        if (firstSelected != null && firstSelected.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    private void RestorePreviousSelection()
    {
        if (EventSystem.current == null)
            return;

        if (previousSelected == null)
            return;

        if (!previousSelected.activeInHierarchy)
            return;

        EventSystem.current.SetSelectedGameObject(previousSelected);
    }

    private void ClearSelectionIfInside(Transform lockedRoot)
    {
        if (EventSystem.current == null)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;

        if (selected == null)
            return;

        if (lockedRoot == null)
            return;

        Transform selectedTransform = selected.transform;

        if (selectedTransform == lockedRoot || selectedTransform.IsChildOf(lockedRoot))
            EventSystem.current.SetSelectedGameObject(null);
    }
}