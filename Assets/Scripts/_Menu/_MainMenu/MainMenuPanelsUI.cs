using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(10000)]
public class MainMenuPanelsUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject continuePanel;
    [SerializeField] private GameObject controlPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject exitConfirmDialog; // ConfirmExit_Dialog

    [Header("Optional CanvasGroups (for modal blocking)")]
    [SerializeField, Tooltip("CanvasGroup главной панели. Если задан — при открытии диалога выхода mainPanel станет неинтерактивной.")]
    private CanvasGroup mainPanelCanvasGroup;

    [Header("Main menu buttons")]
    [SerializeField] private Button newGameButton;         // (опционально) Btn_NewGame
    [SerializeField] private Button openContinueButton;    // Btn_Continue
    [SerializeField] private Button openControlButton;     // Btn_HelpCredits / Btn_Controls
    [SerializeField] private Button openSettingsButton;    // Btn_Settings
    [SerializeField] private Button quitButton;            // Btn_Quit

    [Header("Back buttons in panels")]
    [SerializeField] private Button continueBackButton;    // Btn_Back внутри Continue_Panel
    [SerializeField] private Button controlBackButton;     // Btn_Back внутри Control_Panel
    [SerializeField] private Button settingsBackButton;    // Btn_Back внутри Settings_Panel

    [Header("Exit confirm buttons")]
    [SerializeField] private Button exitYesButton;         // Btn_Yes
    [SerializeField] private Button exitNoButton;          // Btn_No

    [Header("New Game (optional, from NewGameLoader)")]
    [SerializeField, Tooltip("Имя сцены для кнопки New Game. Если пусто — кнопка newGameButton ничего не загрузит.")]
    private string newGameSceneName;

    [Header("Start state")]
    [SerializeField, Tooltip("На старте показать mainPanel и скрыть остальные панели/диалог.")]
    private bool setupInitialStateOnAwake = true;

    [Header("UI Navigation (keyboard / gamepad)")]
    [SerializeField] private bool enableUiNavigation = true;

    [SerializeField, Tooltip("Если выделение потерялось (клик по пустому месту и т.п.) — восстановить.")]
    private bool restoreSelectionIfLost = true;

    [SerializeField, Tooltip("Кнопка, которая будет выделена в Main Panel при открытии.")]
    private Button mainFirstSelected;

    [SerializeField, Tooltip("Кнопка, которая будет выделена в Continue Panel.")]
    private Button continueFirstSelected;

    [SerializeField, Tooltip("Кнопка, которая будет выделена в Controls Panel.")]
    private Button controlFirstSelected;

    [SerializeField, Tooltip("Кнопка, которая будет выделена в Settings Panel.")]
    private Button settingsFirstSelected;

    [SerializeField, Tooltip("Кнопка, которая будет выделена в Exit Confirm (обычно No).")]
    private Button exitConfirmFirstSelected;

    [Header("Back / Cancel input")]
    [SerializeField, Tooltip("Клавиша назад/отмена (обычно Esc).")]
    private KeyCode keyboardBackKey = KeyCode.Escape;

    [SerializeField, Tooltip("Кнопка назад/отмена на геймпаде (обычно B / Circle). Часто JoystickButton1.")]
    private KeyCode gamepadBackKey = KeyCode.JoystickButton1;

    [SerializeField, Tooltip("Если нажать Back в главном меню — открыть диалог выхода.")]
    private bool openExitDialogOnBackFromMain = true;

    [Header("Mouse hover sync (фикс двойной подсветки)")]
    [SerializeField, Tooltip("При наведении мышью делать кнопку currentSelected, чтобы не было двойной подсветки.")]
    private bool selectHoveredButtonWithMouse = true;

    [SerializeField, Tooltip("Обновлять hover->selected только когда мышь реально двигается.")]
    private bool syncMouseOnlyWhenMoved = true;

    private Coroutine _selectRoutine;
    private Vector3 _lastMousePosition;
    private readonly List<RaycastResult> _mouseRaycastResults = new List<RaycastResult>(16);

    private void Awake()
    {
        WireButtons();
        SetupExitConfirmNavigation();

        if (setupInitialStateOnAwake)
        {
            ShowMainOnly();
        }
        else
        {
            // Минимум: диалог выхода скрываем
            if (exitConfirmDialog != null) exitConfirmDialog.SetActive(false);

            // И на всякий случай возвращаем интерактивность главного меню
            SetMainMenuInteractable(true);
        }
    }

    private void Start()
    {
        _lastMousePosition = Input.mousePosition;

        if (enableUiNavigation)
            SelectCurrentPanelDefault();
    }

    private void OnEnable()
    {
        if (enableUiNavigation)
            SelectCurrentPanelDefault();
    }

    private void OnDisable()
    {
        if (_selectRoutine != null)
        {
            StopCoroutine(_selectRoutine);
            _selectRoutine = null;
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        if (!enableUiNavigation)
            return;

        // Back / Cancel с клавы или геймпада
        if (!IsBackPressed())
            return;

        // Приоритет закрытия: сначала ExitConfirm -> потом панели -> потом выход из Main (опц.)
        if (exitConfirmDialog != null && exitConfirmDialog.activeSelf)
        {
            CloseExitDialog();
            return;
        }

        if (continuePanel != null && continuePanel.activeSelf)
        {
            BackToMain();
            return;
        }

        if (controlPanel != null && controlPanel.activeSelf)
        {
            BackToMain();
            return;
        }

        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            BackToMain();
            return;
        }

        if (mainPanel != null && mainPanel.activeSelf && openExitDialogOnBackFromMain)
        {
            OpenExitDialog();
        }
    }

    private void LateUpdate()
    {
        if (!enableUiNavigation) return;

        // Фикс двойной подсветки (мышь + selected)
        if (selectHoveredButtonWithMouse)
            SyncMouseHoverSelection();

        if (restoreSelectionIfLost)
            RestoreSelectionIfNeeded();
    }

    // =========================================================
    // Wiring (подписка на кнопки)
    // =========================================================

    private void WireButtons()
    {
        // Main menu
        Bind(newGameButton, LoadNewGameScene); // опционально
        Bind(openContinueButton, OpenContinue);
        Bind(openControlButton, OpenControl);
        Bind(openSettingsButton, OpenSettings);
        Bind(quitButton, OpenExitDialog);

        // Backs
        Bind(continueBackButton, BackToMain);
        Bind(controlBackButton, BackToMain);
        Bind(settingsBackButton, BackToMain);

        // Exit confirm
        Bind(exitNoButton, CloseExitDialog);
        Bind(exitYesButton, QuitGame);
    }

    private static void Bind(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;

        // Без дублей обработчиков
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }

    // =========================================================
    // Public API (можно вешать на OnClick в инспекторе)
    // =========================================================

    public void LoadNewGameScene()
    {
        if (string.IsNullOrWhiteSpace(newGameSceneName))
        {
            Debug.LogError("MainMenuPanelsUI: newGameSceneName пустой. Укажи имя сцены в инспекторе.");
            return;
        }

        SceneManager.LoadScene(newGameSceneName);
    }

    public void OpenContinue()
    {
        ShowOnlySubPanel(continuePanel);
        SelectButtonDeferred(continueFirstSelected ? continueFirstSelected : continueBackButton);
    }

    public void OpenControl()
    {
        ShowOnlySubPanel(controlPanel);
        SelectButtonDeferred(controlFirstSelected ? controlFirstSelected : controlBackButton);
    }

    public void OpenSettings()
    {
        ShowOnlySubPanel(settingsPanel);
        SelectButtonDeferred(settingsFirstSelected ? settingsFirstSelected : settingsBackButton);
    }

    public void BackToMain()
    {
        ShowMainOnly();
        SelectButtonDeferred(GetDefaultMainButton());
    }

    public void OpenExitDialog()
    {
        if (exitConfirmDialog != null)
            exitConfirmDialog.SetActive(true);

        // Блокируем взаимодействие с main menu, пока открыт модальный диалог
        SetMainMenuInteractable(false);

        SelectButtonDeferred(exitConfirmFirstSelected ? exitConfirmFirstSelected : exitNoButton);
    }

    public void CloseExitDialog()
    {
        if (exitConfirmDialog != null)
            exitConfirmDialog.SetActive(false);

        // Возвращаем взаимодействие с main menu
        SetMainMenuInteractable(true);

        SelectCurrentPanelDefault();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =========================================================
    // Panels helpers
    // =========================================================

    private void ShowMainOnly()
    {
        if (mainPanel != null) mainPanel.SetActive(true);

        if (continuePanel != null) continuePanel.SetActive(false);
        if (controlPanel != null) controlPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (exitConfirmDialog != null) exitConfirmDialog.SetActive(false);

        // На главной панели всё снова интерактивно
        SetMainMenuInteractable(true);
    }

    private void ShowOnlySubPanel(GameObject targetPanel)
    {
        if (mainPanel != null) mainPanel.SetActive(false);

        if (continuePanel != null) continuePanel.SetActive(targetPanel == continuePanel);
        if (controlPanel != null) controlPanel.SetActive(targetPanel == controlPanel);
        if (settingsPanel != null) settingsPanel.SetActive(targetPanel == settingsPanel);

        if (exitConfirmDialog != null) exitConfirmDialog.SetActive(false);

        // Подстраховка: если вдруг main был заблокирован модалкой — вернём интерактивность
        SetMainMenuInteractable(true);
    }

    // =========================================================
    // Modal blocking helpers (чтобы кнопки сзади не выбирались)
    // =========================================================

    private void SetMainMenuInteractable(bool value)
    {
        // Лучший вариант: CanvasGroup на Main_Panel
        if (mainPanelCanvasGroup != null)
        {
            mainPanelCanvasGroup.interactable = value;
            mainPanelCanvasGroup.blocksRaycasts = value;
        }

        // Подстраховка по кнопкам (работает даже без CanvasGroup)
        SetButtonInteractable(newGameButton, value);
        SetButtonInteractable(openContinueButton, value);
        SetButtonInteractable(openControlButton, value);
        SetButtonInteractable(openSettingsButton, value);
        SetButtonInteractable(quitButton, value);
    }

    private static void SetButtonInteractable(Button btn, bool value)
    {
        if (btn != null)
            btn.interactable = value;
    }

    // =========================================================
    // UI Navigation (keyboard / gamepad)
    // =========================================================

    private bool IsBackPressed()
    {
        if (Input.GetKeyDown(keyboardBackKey))
            return true;

        if (Input.GetKeyDown(gamepadBackKey))
            return true;

        return false;
    }

    private void RestoreSelectionIfNeeded()
    {
        if (EventSystem.current == null) return;

        if (EventSystem.current.currentSelectedGameObject != null)
            return;

        SelectCurrentPanelDefault();
    }

    private void SelectCurrentPanelDefault()
    {
        if (!enableUiNavigation) return;

        // 1) Exit dialog (если открыт)
        if (exitConfirmDialog != null && exitConfirmDialog.activeSelf)
        {
            SelectButtonDeferred(exitConfirmFirstSelected ? exitConfirmFirstSelected : exitNoButton);
            return;
        }

        // 2) Подпанели
        if (continuePanel != null && continuePanel.activeSelf)
        {
            SelectButtonDeferred(continueFirstSelected ? continueFirstSelected : continueBackButton);
            return;
        }

        if (controlPanel != null && controlPanel.activeSelf)
        {
            SelectButtonDeferred(controlFirstSelected ? controlFirstSelected : controlBackButton);
            return;
        }

        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            SelectButtonDeferred(settingsFirstSelected ? settingsFirstSelected : settingsBackButton);
            return;
        }

        // 3) Main panel
        if (mainPanel != null && mainPanel.activeSelf)
        {
            SelectButtonDeferred(GetDefaultMainButton());
        }
    }

    private Button GetDefaultMainButton()
    {
        if (mainFirstSelected != null) return mainFirstSelected;

        // Фоллбеки (если mainFirstSelected не задан)
        if (newGameButton != null && newGameButton.isActiveAndEnabled && newGameButton.interactable) return newGameButton;
        if (openContinueButton != null && openContinueButton.isActiveAndEnabled && openContinueButton.interactable) return openContinueButton;
        if (openSettingsButton != null && openSettingsButton.isActiveAndEnabled && openSettingsButton.interactable) return openSettingsButton;
        if (openControlButton != null && openControlButton.isActiveAndEnabled && openControlButton.interactable) return openControlButton;
        if (quitButton != null && quitButton.isActiveAndEnabled && quitButton.interactable) return quitButton;

        return null;
    }

    private void SelectButtonDeferred(Button btn)
    {
        if (!enableUiNavigation) return;
        if (btn == null) return;
        if (!btn.isActiveAndEnabled || !btn.interactable) return;
        if (EventSystem.current == null) return;

        if (_selectRoutine != null)
            StopCoroutine(_selectRoutine);

        _selectRoutine = StartCoroutine(SelectButtonNextFrame(btn));
    }

    private IEnumerator SelectButtonNextFrame(Button btn)
    {
        yield return null;

        if (!enableUiNavigation) yield break;
        if (btn == null) yield break;
        if (EventSystem.current == null) yield break;
        if (!btn.isActiveAndEnabled || !btn.interactable) yield break;

        Canvas.ForceUpdateCanvases();
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(btn.gameObject);

        _selectRoutine = null;
    }

    // Явная навигация для Yes/No, чтобы выбор НЕ улетал в кнопки сзади
    private void SetupExitConfirmNavigation()
    {
        if (exitYesButton == null || exitNoButton == null)
            return;

        Navigation yesNav = exitYesButton.navigation;
        yesNav.mode = Navigation.Mode.Explicit;
        yesNav.selectOnLeft = exitNoButton;
        yesNav.selectOnRight = exitNoButton;
        yesNav.selectOnUp = exitYesButton;
        yesNav.selectOnDown = exitYesButton;
        exitYesButton.navigation = yesNav;

        Navigation noNav = exitNoButton.navigation;
        noNav.mode = Navigation.Mode.Explicit;
        noNav.selectOnLeft = exitYesButton;
        noNav.selectOnRight = exitYesButton;
        noNav.selectOnUp = exitNoButton;
        noNav.selectOnDown = noButtonSelfOrFallback();
        exitNoButton.navigation = noNav;

        // локальная функция, чтобы не плодить отдельный метод
        Selectable noButtonSelfOrFallback()
        {
            return exitNoButton != null ? exitNoButton : exitYesButton;
        }
    }

    // =========================================================
    // Mouse hover -> selected sync (фикс двойной подсветки)
    // =========================================================

    private void SyncMouseHoverSelection()
    {
        if (EventSystem.current == null) return;
        if (!Input.mousePresent) return;

        Vector3 mousePos = Input.mousePosition;

        if (syncMouseOnlyWhenMoved && mousePos == _lastMousePosition)
            return;

        _lastMousePosition = mousePos;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = mousePos
        };

        _mouseRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, _mouseRaycastResults);

        Button hoveredButton = null;

        for (int i = 0; i < _mouseRaycastResults.Count; i++)
        {
            GameObject go = _mouseRaycastResults[i].gameObject;
            if (go == null) continue;

            // Рейкаст часто попадает в Text/Image внутри кнопки — поднимаемся к Button в родителе
            Button btn = go.GetComponentInParent<Button>();
            if (btn == null) continue;
            if (!btn.isActiveAndEnabled || !btn.interactable) continue;

            hoveredButton = btn;
            break;
        }

        if (hoveredButton == null) return;
        if (!hoveredButton.gameObject.activeInHierarchy) return;

        // ВАЖНО: не даём мыши перехватывать кнопки "не того" окна (например, сзади модалки)
        if (!IsButtonAllowedInCurrentContext(hoveredButton))
            return;

        if (EventSystem.current.currentSelectedGameObject == hoveredButton.gameObject)
            return;

        if (_selectRoutine != null)
        {
            StopCoroutine(_selectRoutine);
            _selectRoutine = null;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(hoveredButton.gameObject);
    }

    private bool IsButtonAllowedInCurrentContext(Button btn)
    {
        if (btn == null) return false;

        // Если открыт диалог выхода — разрешены только кнопки внутри него
        if (exitConfirmDialog != null && exitConfirmDialog.activeSelf)
            return btn.transform.IsChildOf(exitConfirmDialog.transform);

        // Если открыта одна из панелей — разрешаем только кнопки внутри неё
        if (continuePanel != null && continuePanel.activeSelf)
            return btn.transform.IsChildOf(continuePanel.transform);

        if (controlPanel != null && controlPanel.activeSelf)
            return btn.transform.IsChildOf(controlPanel.transform);

        if (settingsPanel != null && settingsPanel.activeSelf)
            return btn.transform.IsChildOf(settingsPanel.transform);

        // Иначе — главное меню
        if (mainPanel != null && mainPanel.activeSelf)
            return btn.transform.IsChildOf(mainPanel.transform);

        return true;
    }
}