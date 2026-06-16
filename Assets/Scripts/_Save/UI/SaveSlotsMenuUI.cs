using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatGame.SaveSystem
{
    public enum SaveSlotsMenuMode
    {
        Load,
        ManualSave
    }

    [DisallowMultipleComponent]
    public sealed class SaveSlotsMenuUI : MonoBehaviour
    {
        [Header("Rows")]
        [SerializeField, Tooltip("Строка автосейва. Рекомендация: верхняя строка меню 'Автосейв'.")]
        private SaveSlotRowUI autosaveRow;

        [SerializeField, Tooltip("Строки ручных слотов. Рекомендация: назначить все строки 'Сохранение 1..N'.")]
        private SaveSlotRowUI[] manualRows;

        [Header("Confirmation")]
        [SerializeField, Tooltip("Диалог подтверждения для загрузки, удаления и перезаписи. Рекомендация: назначить общий modal dialog.")]
        private SaveConfirmationDialog confirmationDialog;

        [Header("Behaviour")]
        [SerializeField, Tooltip("Скрывать панель после успешной загрузки. Рекомендация: включено.")]
        private bool closeAfterLoad = true;

        [SerializeField, Tooltip("Скрывать панель после успешного ручного сохранения. Рекомендация: выключено, чтобы игрок видел обновлённую дату слота.")]
        private bool closeAfterManualSave = false;

        [SerializeField, Tooltip("Разрешать загрузку и удаление ручных слотов прямо у лежанки. Рекомендация: включено, если меню лежанки должно быть полноценным меню сохранений.")]
        private bool allowLoadAndDeleteInManualSaveMode = true;

        [Header("Gameplay Input Block")]
        [SerializeField, Tooltip("Выключать игровой ввод через PlayerController.SetInputEnabled(false), пока открыто меню сохранений. Рекомендация: включено.")]
        private bool blockGameplayInputWhileOpen = true;

        [SerializeField, Tooltip("Ссылка на PlayerController кота. Рекомендация: можно оставить пустым, если включен Auto Find Player Controller.")]
        private PlayerController playerController;

        [SerializeField, Tooltip("Если PlayerController не назначен вручную — найти его автоматически в сцене. Рекомендация: включено.")]
        private bool autoFindPlayerController = true;

        [SerializeField, Tooltip("При закрытии меню не включать управление сразу, а подождать 1 кадр. Рекомендация: включено, чтобы Space/Submit не улетал в прыжок после закрытия UI.")]
        private bool reenableGameplayInputNextFrame = true;

        [SerializeField, Tooltip("После закрытия ждать отпускания Space/геймпадной кнопки прыжка перед возвратом управления. Рекомендация: включено, если после выхода из меню срабатывает прыжок.")]
        private bool waitForJumpInputReleaseOnClose = true;

        [SerializeField, Tooltip("Клавиша прыжка для защиты от буфера после UI. Рекомендация: Space.")]
        private KeyCode keyboardJumpFallback = KeyCode.Space;

        [SerializeField, Tooltip("Геймпадная кнопка прыжка для защиты от буфера после UI. Рекомендация: JoystickButton0 = A/Cross.")]
        private KeyCode gamepadJumpFallback = KeyCode.JoystickButton0;

        [Header("Time Scale")]
        [SerializeField, Tooltip("Ставить Time.timeScale = 0, пока открыта панель сохранений. Рекомендация: включено.")]
        private bool pauseGameWhileOpen = true;

        [Header("Keyboard / Gamepad Selection")]
        [SerializeField, Tooltip("Автоматически выбирать первую доступную кнопку при открытии панели. Рекомендация: включено.")]
        private bool selectFirstButtonOnOpen = true;

        [SerializeField, Tooltip("Если выбранная кнопка потерялась — вернуть выделение на первую доступную кнопку внутри меню. Рекомендация: включено.")]
        private bool keepSelectionInsideMenu = true;

        [SerializeField, Tooltip("Через сколько кадров после открытия выставлять выбранную кнопку. Рекомендация: 1.")]
        private int selectAfterFrames = 1;

        [Header("Debug")]
        [SerializeField, Tooltip("Писать технические сообщения в Console. Рекомендация: выключено.")]
        private bool verboseLogs = false;

        private SaveSlotsMenuMode mode;
        private float previousTimeScale = 1f;

        private bool timeScaleWasChanged;
        private bool gameplayInputWasBlocked;
        private bool modalStateApplied;
        private bool isClosing;
        private bool isOperationInProgress;

        private Coroutine selectRoutine;
        private Coroutine closeRoutine;

        private void Awake()
        {
            CachePlayerControllerIfNeeded();
        }

        private void OnEnable()
        {
            isClosing = false;
            isOperationInProgress = false;
            ApplyModalState();
            RefreshRowsAsync();
        }

        private void OnDisable()
        {
            StopSelectRoutine();

            if (modalStateApplied)
                RestoreModalStateImmediate();
        }

        private void Update()
        {
            if (!keepSelectionInsideMenu)
                return;

            if (!gameObject.activeInHierarchy)
                return;

            if (isClosing)
                return;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            GameObject selected = eventSystem.currentSelectedGameObject;

            if (selected == null || !IsSelectedObjectInsideMenu(selected))
                SelectFirstAvailableButton();
        }

        public void OpenForLoad()
        {
            mode = SaveSlotsMenuMode.Load;
            gameObject.SetActive(true);
            RefreshRowsAsync();
        }

        public void OpenForManualSave()
        {
            mode = SaveSlotsMenuMode.ManualSave;
            gameObject.SetActive(true);
            RefreshRowsAsync();
        }

        public void Close()
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (isClosing)
                return;

            if (closeRoutine != null)
                StopCoroutine(closeRoutine);

            closeRoutine = StartCoroutine(CloseRoutine());
        }

        private IEnumerator CloseRoutine()
        {
            isClosing = true;

            StopSelectRoutine();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            RestoreTimeScaleOnly();

            if (reenableGameplayInputNextFrame)
                yield return null;

            if (waitForJumpInputReleaseOnClose)
            {
                while (IsJumpInputHeld())
                    yield return null;
            }

            RestoreGameplayInputOnly();

            modalStateApplied = false;
            closeRoutine = null;

            gameObject.SetActive(false);
        }

        public async void RefreshRowsAsync()
        {
            if (!gameObject.activeInHierarchy || SaveManager.Instance == null)
                return;

            IReadOnlyList<SaveSlotMeta> metas = await SaveManager.Instance.GetSlotsMetaAsync();

            if (!gameObject.activeInHierarchy || isClosing)
                return;

            if (autosaveRow != null)
            {
                SaveSlotMeta autosaveMeta = FindMeta(metas, SaveConstants.AutoSaveSlotId);

                autosaveRow.Bind(
                    SaveConstants.AutoSaveSlotId,
                    "Автосейв",
                    autosaveMeta,
                    true,
                    false,
                    false,
                    OnLoadClicked,
                    null,
                    null);
            }

            if (manualRows != null)
            {
                for (int i = 0; i < manualRows.Length; i++)
                {
                    if (manualRows[i] == null)
                        continue;

                    string slotId = "slot_" + (i + 1);
                    SaveSlotMeta meta = FindMeta(metas, slotId);

                    bool allowLoadDelete =
                        mode == SaveSlotsMenuMode.Load ||
                        (mode == SaveSlotsMenuMode.ManualSave && allowLoadAndDeleteInManualSaveMode);

                    bool allowOverwrite = mode == SaveSlotsMenuMode.ManualSave;

                    manualRows[i].Bind(
                        slotId,
                        "Сохранение " + (i + 1),
                        meta,
                        allowLoadDelete,
                        allowLoadDelete,
                        allowOverwrite,
                        OnLoadClicked,
                        OnDeleteClicked,
                        OnOverwriteClicked);
                }
            }

            ScheduleSelectFirstAvailableButton();
        }

        private void OnLoadClicked(string slotId)
        {
            if (isOperationInProgress)
                return;

            if (confirmationDialog == null)
            {
                LoadSlotAsync(slotId);
                return;
            }

            confirmationDialog.Show(
                "Загрузить сохранение?",
                "Текущий несохранённый прогресс будет потерян.",
                delegate { LoadSlotAsync(slotId); });
        }

        private async void LoadSlotAsync(string slotId)
        {
            if (isOperationInProgress)
                return;

            isOperationInProgress = true;

            SaveManager saveManager = SaveManager.Instance;

            if (saveManager == null)
            {
                isOperationInProgress = false;
                return;
            }

            /*
             * ВАЖНО:
             * Перед загрузкой сцены надо вручную вернуть timeScale/input,
             * потому что Continue_Panel будет уничтожен вместе со старой сценой.
             * После await LoadSlotAsync этот MonoBehaviour уже может быть destroyed.
             */
            RestoreModalStateImmediate();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            try
            {
                await saveManager.LoadSlotAsync(slotId);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to load save slot: " + slotId + "\n" + ex);
            }
        }

        private void OnDeleteClicked(string slotId)
        {
            if (isOperationInProgress)
                return;

            if (confirmationDialog == null)
            {
                DeleteSlotAsync(slotId);
                return;
            }

            confirmationDialog.Show(
                "Удалить сохранение?",
                "Это действие нельзя отменить.",
                delegate { DeleteSlotAsync(slotId); });
        }

        private async void DeleteSlotAsync(string slotId)
        {
            if (isOperationInProgress)
                return;

            isOperationInProgress = true;

            try
            {
                await SaveManager.Instance.DeleteSlotAsync(slotId);
                RefreshRowsAsync();
            }
            finally
            {
                isOperationInProgress = false;
            }
        }

        private void OnOverwriteClicked(string slotId)
        {
            if (isOperationInProgress)
                return;

            if (confirmationDialog == null)
            {
                OverwriteSlotAsync(slotId);
                return;
            }

            confirmationDialog.Show(
                "Перезаписать сохранение?",
                "Старые данные в этом слоте будут заменены.",
                delegate { OverwriteSlotAsync(slotId); });
        }

        private async void OverwriteSlotAsync(string slotId)
        {
            if (isOperationInProgress)
                return;

            isOperationInProgress = true;

            try
            {
                await SaveManager.Instance.SaveToSlotAsync(slotId, true);

                SaveIconUI.ShowSmallSaved();
                RefreshRowsAsync();

                if (closeAfterManualSave)
                    Close();
            }
            finally
            {
                isOperationInProgress = false;
            }
        }

        private void ApplyModalState()
        {
            CachePlayerControllerIfNeeded();

            modalStateApplied = true;

            if (pauseGameWhileOpen && !timeScaleWasChanged)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                timeScaleWasChanged = true;
            }

            if (blockGameplayInputWhileOpen && !gameplayInputWasBlocked)
            {
                SetGameplayInputEnabled(false);
                gameplayInputWasBlocked = true;
            }
        }

        private void RestoreModalStateImmediate()
        {
            RestoreTimeScaleOnly();
            RestoreGameplayInputOnly();

            modalStateApplied = false;
            isClosing = false;
            closeRoutine = null;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void RestoreTimeScaleOnly()
        {
            if (!timeScaleWasChanged)
                return;

            Time.timeScale = previousTimeScale;
            timeScaleWasChanged = false;
        }

        private void RestoreGameplayInputOnly()
        {
            if (!gameplayInputWasBlocked)
                return;

            gameplayInputWasBlocked = false;
            SetGameplayInputEnabled(true);
        }

        private bool IsJumpInputHeld()
        {
            if (keyboardJumpFallback != KeyCode.None && Input.GetKey(keyboardJumpFallback))
                return true;

            if (gamepadJumpFallback != KeyCode.None && Input.GetKey(gamepadJumpFallback))
                return true;

            return false;
        }

        private void CachePlayerControllerIfNeeded()
        {
            if (playerController == null && autoFindPlayerController)
                playerController = FindObjectOfType<PlayerController>();
        }

        private void SetGameplayInputEnabled(bool enabled)
        {
            CachePlayerControllerIfNeeded();

            if (playerController != null)
            {
                playerController.SetInputEnabled(enabled);

                if (verboseLogs)
                    Debug.Log("SaveSlotsMenuUI: PlayerController input = " + enabled, playerController);
            }
            else if (verboseLogs)
            {
                Debug.LogWarning("SaveSlotsMenuUI: PlayerController not found. Gameplay input was not changed.", this);
            }
        }

        private void ScheduleSelectFirstAvailableButton()
        {
            if (!selectFirstButtonOnOpen)
                return;

            StopSelectRoutine();
            selectRoutine = StartCoroutine(SelectFirstAvailableButtonRoutine());
        }

        private IEnumerator SelectFirstAvailableButtonRoutine()
        {
            int frames = Mathf.Max(0, selectAfterFrames);

            for (int i = 0; i < frames; i++)
                yield return null;

            if (!gameObject.activeInHierarchy || isClosing)
                yield break;

            SelectFirstAvailableButton();
            selectRoutine = null;
        }

        private void StopSelectRoutine()
        {
            if (selectRoutine == null)
                return;

            StopCoroutine(selectRoutine);
            selectRoutine = null;
        }

        private void SelectFirstAvailableButton()
        {
            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null)
            {
                if (verboseLogs)
                    Debug.LogWarning("SaveSlotsMenuUI: EventSystem not found. Keyboard/gamepad navigation will not work.", this);

                return;
            }

            Selectable selectable = GetFirstAvailableSelectable();

            if (selectable == null)
            {
                if (verboseLogs)
                    Debug.LogWarning("SaveSlotsMenuUI: no available selectable button found.", this);

                return;
            }

            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(selectable.gameObject);

            if (verboseLogs)
                Debug.Log("SaveSlotsMenuUI selected: " + selectable.name, selectable);
        }

        private Selectable GetFirstAvailableSelectable()
        {
            if (mode == SaveSlotsMenuMode.Load)
            {
                Selectable autosaveSelectable = autosaveRow != null ? autosaveRow.GetPreferredSelectable() : null;

                if (autosaveSelectable != null)
                    return autosaveSelectable;
            }

            if (manualRows != null)
            {
                for (int i = 0; i < manualRows.Length; i++)
                {
                    if (manualRows[i] == null)
                        continue;

                    Selectable selectable = manualRows[i].GetPreferredSelectable();

                    if (selectable != null)
                        return selectable;
                }
            }

            Selectable fallbackAutosave = autosaveRow != null ? autosaveRow.GetPreferredSelectable() : null;

            if (fallbackAutosave != null)
                return fallbackAutosave;

            return null;
        }

        private bool IsSelectedObjectInsideMenu(GameObject selectedObject)
        {
            if (selectedObject == null)
                return false;

            if (!selectedObject.activeInHierarchy)
                return false;

            Selectable selectable = selectedObject.GetComponent<Selectable>();

            if (selectable == null || !selectable.interactable)
                return false;

            if (autosaveRow != null && autosaveRow.ContainsSelectable(selectedObject))
                return true;

            if (manualRows != null)
            {
                for (int i = 0; i < manualRows.Length; i++)
                {
                    if (manualRows[i] != null && manualRows[i].ContainsSelectable(selectedObject))
                        return true;
                }
            }

            return selectedObject.transform.IsChildOf(transform);
        }

        private static SaveSlotMeta FindMeta(IReadOnlyList<SaveSlotMeta> metas, string slotId)
        {
            if (metas != null)
            {
                for (int i = 0; i < metas.Count; i++)
                {
                    if (metas[i] != null && metas[i].slotId == slotId)
                        return metas[i];
                }
            }

            return new SaveSlotMeta { slotId = slotId, exists = false };
        }
    }
}