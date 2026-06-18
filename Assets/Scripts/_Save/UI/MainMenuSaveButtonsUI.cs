using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class MainMenuSaveButtonsUI : MonoBehaviour
    {
        [Header("Save Manager")]
        [SerializeField, Tooltip("SaveManager из сцены главного меню. Если не назначен, скрипт попробует найти SaveManager.Instance.")]
        private SaveManager saveManager;

        [SerializeField, Tooltip("Автоматически искать SaveManager, если поле Save Manager пустое.")]
        private bool autoFindSaveManager = true;

        [Header("Continue Button")]
        [SerializeField, Tooltip("Кнопка Продолжить. Если сохранений нет, она станет некликабельной.")]
        private Button continueButton;

        [SerializeField, Tooltip("CanvasGroup кнопки Продолжить. Нужен для прозрачности. Если пусто, скрипт попробует найти его на кнопке.")]
        private CanvasGroup continueCanvasGroup;

        [Header("Fallback Selection")]
        [SerializeField, Tooltip("Кнопка Новая игра. Будет автоматически выделяться, если Продолжить недоступна.")]
        private Button newGameButton;

        [SerializeField, Tooltip("Автоматически выделять кнопку Новая игра, если Продолжить недоступна.")]
        private bool selectNewGameWhenContinueDisabled = true;

        [SerializeField, Tooltip("Автоматически выделять кнопку Продолжить, если сохранения есть.")]
        private bool selectContinueWhenAvailable = false;

        [Header("Visual State")]
        [SerializeField, Range(0f, 1f), Tooltip("Прозрачность кнопки Продолжить, когда сохранения есть.")]
        private float enabledAlpha = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("Прозрачность кнопки Продолжить, когда сохранений нет.")]
        private float disabledAlpha = 0.25f;

        [SerializeField, Tooltip("Отключать клики по CanvasGroup, когда сохранений нет.")]
        private bool blockRaycastsWhenDisabled = true;

        [SerializeField, Tooltip("Отключать interactable у Button, когда сохранений нет.")]
        private bool setButtonInteractable = true;

        [Header("Refresh")]
        [SerializeField, Tooltip("Проверять сохранения при включении объекта.")]
        private bool refreshOnEnable = true;

        [SerializeField, Tooltip("Проверять сохранения при возвращении фокуса в окно Unity/игры.")]
        private bool refreshOnApplicationFocus = true;

        [SerializeField, Tooltip("Сколько кадров подождать перед выделением кнопки. Нужно, чтобы EventSystem и UI успели обновиться.")]
        private int selectAfterFrames = 1;

        [Header("Debug")]
        [SerializeField, Tooltip("Писать короткие сообщения в Console.")]
        private bool verboseLogs = false;

        private Coroutine refreshRoutine;
        private Coroutine selectRoutine;
        private bool hasSaves;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();

            if (refreshOnEnable)
                Refresh();
        }

        private void OnDisable()
        {
            StopRefreshRoutine();
            StopSelectRoutine();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!refreshOnApplicationFocus)
                return;

            if (hasFocus)
                Refresh();
        }

        public void Refresh()
        {
            StopRefreshRoutine();
            refreshRoutine = StartCoroutine(RefreshRoutine());
        }

        private IEnumerator RefreshRoutine()
        {
            yield return null;

            SaveManager manager = ResolveSaveManager();

            if (manager == null)
            {
                SetContinueState(false);

                if (verboseLogs)
                    Debug.LogWarning("MainMenuSaveButtonsUI: SaveManager не найден. Кнопка Продолжить выключена.", this);

                refreshRoutine = null;
                yield break;
            }

            var task = manager.GetSlotsMetaAsync();

            while (!task.IsCompleted)
                yield return null;

            if (task.Exception != null)
            {
                SetContinueState(false);

                if (verboseLogs)
                    Debug.LogWarning("MainMenuSaveButtonsUI: не удалось проверить сохранения.\n" + task.Exception, this);

                refreshRoutine = null;
                yield break;
            }

            IReadOnlyList<SaveSlotMeta> metas = task.Result;
            bool found = HasExistingSave(metas);

            SetContinueState(found);

            if (verboseLogs)
                Debug.Log("MainMenuSaveButtonsUI: сохранения найдены = " + found, this);

            refreshRoutine = null;
        }

        private bool HasExistingSave(IReadOnlyList<SaveSlotMeta> metas)
        {
            if (metas == null)
                return false;

            for (int i = 0; i < metas.Count; i++)
            {
                SaveSlotMeta meta = metas[i];

                if (meta != null && meta.exists)
                    return true;
            }

            return false;
        }

        private void SetContinueState(bool enabled)
        {
            hasSaves = enabled;

            if (continueButton != null && setButtonInteractable)
                continueButton.interactable = enabled;

            if (continueCanvasGroup != null)
            {
                continueCanvasGroup.alpha = enabled ? enabledAlpha : disabledAlpha;

                if (blockRaycastsWhenDisabled)
                {
                    continueCanvasGroup.blocksRaycasts = enabled;
                    continueCanvasGroup.interactable = enabled;
                }
            }

            ScheduleAutoSelection();
        }

        private void ScheduleAutoSelection()
        {
            StopSelectRoutine();
            selectRoutine = StartCoroutine(AutoSelectionRoutine());
        }

        private IEnumerator AutoSelectionRoutine()
        {
            int frames = Mathf.Max(0, selectAfterFrames);

            for (int i = 0; i < frames; i++)
                yield return null;

            ApplyAutoSelection();

            selectRoutine = null;
        }

        private void ApplyAutoSelection()
        {
            if (EventSystem.current == null)
                return;

            Button target = null;

            if (!hasSaves && selectNewGameWhenContinueDisabled)
            {
                if (newGameButton != null && newGameButton.gameObject.activeInHierarchy && newGameButton.interactable)
                    target = newGameButton;
            }
            else if (hasSaves && selectContinueWhenAvailable)
            {
                if (continueButton != null && continueButton.gameObject.activeInHierarchy && continueButton.interactable)
                    target = continueButton;
            }

            if (target == null)
                return;

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target.gameObject);

            if (verboseLogs)
                Debug.Log("MainMenuSaveButtonsUI selected: " + target.name, target);
        }

        private SaveManager ResolveSaveManager()
        {
            if (saveManager != null)
                return saveManager;

            if (SaveManager.Instance != null)
            {
                saveManager = SaveManager.Instance;
                return saveManager;
            }

            if (autoFindSaveManager)
            {
                saveManager = FindObjectOfType<SaveManager>();

                if (saveManager != null)
                    return saveManager;
            }

            return null;
        }

        private void CacheComponents()
        {
            if (continueButton != null && continueCanvasGroup == null)
            {
                continueCanvasGroup = continueButton.GetComponent<CanvasGroup>();

                if (continueCanvasGroup == null)
                    continueCanvasGroup = continueButton.gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void StopRefreshRoutine()
        {
            if (refreshRoutine == null)
                return;

            StopCoroutine(refreshRoutine);
            refreshRoutine = null;
        }

        private void StopSelectRoutine()
        {
            if (selectRoutine == null)
                return;

            StopCoroutine(selectRoutine);
            selectRoutine = null;
        }

        public bool HasSaves()
        {
            return hasSaves;
        }
    }
}