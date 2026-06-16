using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class SaveConfirmationDialog : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField, Tooltip("Корневой объект панели подтверждения. Рекомендация: отдельная modal panel поверх меню. Можно указать этот же GameObject.")]
        private GameObject panelRoot;

        [Header("Texts")]
        [SerializeField, Tooltip("Заголовок диалога. Рекомендация: коротко: 'Удалить сохранение?', 'Загрузить сохранение?', 'Перезаписать сохранение?'.")]
        private TextMeshProUGUI titleText;

        [SerializeField, Tooltip("Основное сообщение диалога. Рекомендация: объяснять последствие действия.")]
        private TextMeshProUGUI messageText;

        [Header("Buttons")]
        [SerializeField, Tooltip("Кнопка подтверждения. Рекомендация: текст 'Да' или 'Подтвердить'.")]
        private Button confirmButton;

        [SerializeField, Tooltip("Кнопка отмены. Рекомендация: текст 'Нет' или 'Отмена'.")]
        private Button cancelButton;

        [SerializeField, Tooltip("Что выделить при открытии диалога. Рекомендация: Btn_Cancel, чтобы случайно не подтвердить удаление/перезапись.")]
        private Selectable defaultSelected;

        [Header("Navigation")]
        [SerializeField, Tooltip("Принудительно настроить навигацию между кнопками Да/Нет. Рекомендация: включено.")]
        private bool forceTwoButtonNavigation = true;

        [SerializeField, Tooltip("Выставлять выбранную кнопку на следующий кадр. Рекомендация: включено.")]
        private bool selectDefaultNextFrame = true;

        [SerializeField, Tooltip("Возвращать выделение на кнопку, с которой открыли подтверждение. Рекомендация: включено.")]
        private bool returnSelectionAfterClose = true;

        [Header("Close Hotkeys")]
        [SerializeField, Tooltip("Закрывать подтверждение по Escape. Рекомендация: включено.")]
        private bool closeOnEscape = true;

        [SerializeField, Tooltip("Закрывать подтверждение по B/Circle на геймпаде. Рекомендация: JoystickButton1.")]
        private KeyCode gamepadCancelKey = KeyCode.JoystickButton1;

        [Header("Input Guard")]
        [SerializeField, Tooltip("После открытия диалога коротко игнорировать Submit/Cancel, чтобы тот же Enter/A/Space, которым открыли окно, не нажал его сразу. Рекомендация: 0.08.")]
        private float openInputGuardSeconds = 0.08f;

        [Header("Debug")]
        [SerializeField, Tooltip("Писать технические сообщения в Console. Рекомендация: выключено.")]
        private bool verboseLogs = false;

        private Action confirmAction;
        private Coroutine selectRoutine;
        private GameObject previousSelectedObject;
        private float ignoreInputUntilUnscaledTime;
        private bool isOpen;
        private bool initialized;

        private GameObject RootObject
        {
            get
            {
                return panelRoot != null ? panelRoot : gameObject;
            }
        }

        private void Awake()
        {
            InitializeIfNeeded();

            /*
             * ВАЖНО:
             * Не вызываем Hide() / SetActive(false) в Awake.
             * Если объект изначально выключен в сцене, первый Show() активирует его,
             * Unity вызовет Awake(), и старый код тут же выключал объект обратно.
             */
        }

        private void OnEnable()
        {
            InitializeIfNeeded();
        }

        private void Update()
        {
            if (!isOpen)
                return;

            if (Time.unscaledTime < ignoreInputUntilUnscaledTime)
                return;

            if (closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            if (gamepadCancelKey != KeyCode.None && Input.GetKeyDown(gamepadCancelKey))
            {
                Cancel();
                return;
            }

            RestoreSelectionIfLost();
        }

        public void Show(string title, string message, Action onConfirm)
        {
            InitializeIfNeeded();

            confirmAction = onConfirm;
            isOpen = true;
            ignoreInputUntilUnscaledTime = Time.unscaledTime + Mathf.Max(0f, openInputGuardSeconds);

            previousSelectedObject = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            if (titleText != null)
                titleText.text = title;

            if (messageText != null)
                messageText.text = message;

            RootObject.SetActive(true);

            SetupNavigation();
            ScheduleSelectDefault();

            if (verboseLogs)
                Debug.Log("SaveConfirmationDialog opened.", this);
        }

        public void Hide()
        {
            HideImmediate(true);
        }

        private void HideImmediate(bool restoreSelection)
        {
            StopSelectRoutine();

            isOpen = false;
            confirmAction = null;

            if (RootObject != null)
                RootObject.SetActive(false);

            if (restoreSelection && returnSelectionAfterClose)
                RestorePreviousSelection();
        }

        private void Confirm()
        {
            if (!isOpen)
                return;

            if (Time.unscaledTime < ignoreInputUntilUnscaledTime)
                return;

            Action action = confirmAction;

            StopSelectRoutine();

            isOpen = false;
            confirmAction = null;

            if (RootObject != null)
                RootObject.SetActive(false);

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            action?.Invoke();
        }

        private void Cancel()
        {
            if (!isOpen)
                return;

            HideImmediate(true);
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
                return;

            initialized = true;

            if (panelRoot == null)
                panelRoot = gameObject;

            WireButtons();
            SetupNavigation();
        }

        private void WireButtons()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(Confirm);
                confirmButton.onClick.AddListener(Confirm);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(Cancel);
                cancelButton.onClick.AddListener(Cancel);
            }
        }

        private void SetupNavigation()
        {
            if (!forceTwoButtonNavigation)
                return;

            if (confirmButton == null || cancelButton == null)
                return;

            Navigation confirmNav = confirmButton.navigation;
            confirmNav.mode = Navigation.Mode.Explicit;
            confirmNav.selectOnLeft = cancelButton;
            confirmNav.selectOnRight = cancelButton;
            confirmNav.selectOnUp = confirmButton;
            confirmNav.selectOnDown = confirmButton;
            confirmButton.navigation = confirmNav;

            Navigation cancelNav = cancelButton.navigation;
            cancelNav.mode = Navigation.Mode.Explicit;
            cancelNav.selectOnLeft = confirmButton;
            cancelNav.selectOnRight = confirmButton;
            cancelNav.selectOnUp = cancelButton;
            cancelNav.selectOnDown = cancelButton;
            cancelButton.navigation = cancelNav;
        }

        private void ScheduleSelectDefault()
        {
            StopSelectRoutine();

            if (!selectDefaultNextFrame)
            {
                SelectDefaultNow();
                return;
            }

            if (isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                selectRoutine = StartCoroutine(SelectDefaultRoutine());
            }
            else
            {
                SelectDefaultNow();
            }
        }

        private IEnumerator SelectDefaultRoutine()
        {
            yield return null;

            Canvas.ForceUpdateCanvases();
            SelectDefaultNow();

            selectRoutine = null;
        }

        private void SelectDefaultNow()
        {
            if (!isOpen)
                return;

            if (EventSystem.current == null)
                return;

            Selectable target = GetDefaultSelectable();

            if (target == null)
                return;

            if (!target.gameObject.activeInHierarchy || !target.interactable)
                return;

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target.gameObject);

            if (verboseLogs)
                Debug.Log("SaveConfirmationDialog selected: " + target.name, target);
        }

        private Selectable GetDefaultSelectable()
        {
            if (defaultSelected != null && defaultSelected.gameObject.activeInHierarchy && defaultSelected.interactable)
                return defaultSelected;

            if (cancelButton != null && cancelButton.gameObject.activeInHierarchy && cancelButton.interactable)
                return cancelButton;

            if (confirmButton != null && confirmButton.gameObject.activeInHierarchy && confirmButton.interactable)
                return confirmButton;

            return null;
        }

        private void RestoreSelectionIfLost()
        {
            if (EventSystem.current == null)
                return;

            GameObject selected = EventSystem.current.currentSelectedGameObject;

            if (selected == null)
            {
                SelectDefaultNow();
                return;
            }

            if (confirmButton != null && selected == confirmButton.gameObject)
                return;

            if (cancelButton != null && selected == cancelButton.gameObject)
                return;

            SelectDefaultNow();
        }

        private void RestorePreviousSelection()
        {
            if (EventSystem.current == null)
                return;

            if (previousSelectedObject != null && previousSelectedObject.activeInHierarchy)
            {
                Selectable selectable = previousSelectedObject.GetComponent<Selectable>();

                if (selectable != null && selectable.interactable)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(previousSelectedObject);
                    return;
                }
            }

            EventSystem.current.SetSelectedGameObject(null);
        }

        private void StopSelectRoutine()
        {
            if (selectRoutine == null)
                return;

            StopCoroutine(selectRoutine);
            selectRoutine = null;
        }
    }
}