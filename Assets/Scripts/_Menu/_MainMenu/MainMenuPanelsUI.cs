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

    [Header("Legacy Rebind (KeyCode)")]
    [SerializeField, Tooltip("Если ВКЛ и в сцене есть LegacyKeycodeRebind — Back читается из него (с учётом ребинда и сохранения).")]
    private bool useLegacyKeycodeRebind = true;

    [Header("Mouse hover sync")]
    [SerializeField, Tooltip("При наведении мышью делать кнопку currentSelected, чтобы не было двойной подсветки.")]
    private bool selectHoveredButtonWithMouse = true;

    [SerializeField, Tooltip("Обновлять hover -> selected только когда мышь реально двигается.")]
    private bool syncMouseOnlyWhenMoved = true;

    [SerializeField, Tooltip("Когда игрок начал пользоваться клавиатурой/геймпадом — hover мыши временно отключается, пока мышь снова не сдвинется.")]
    private bool disableMouseHoverWhileUsingNavigation = true;

    private Coroutine _selectRoutine;
    private Vector3 _lastMousePosition;
    private bool _mouseMovedThisFrame;
    private bool _mouseInputActive = true;
    private Button _lastHoveredButton;
    private float _prevHorizontalAxis;
    private float _prevVerticalAxis;

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
            if (exitConfirmDialog != null)
                exitConfirmDialog.SetActive(false);

            SetMainMenuInteractable(true);
        }
    }

    private void Start()
    {
        _lastMousePosition = Input.mousePosition;
        _mouseInputActive = Input.mousePresent;

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

        ClearMouseHoverVisual();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        if (!enableUiNavigation)
            return;

        if (IsRebindBlockingUi())
            return;

        UpdateInputModeState();

        if (!IsBackPressed())
            return;

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
        if (IsRebindBlockingUi()) return;

        if (selectHoveredButtonWithMouse &&
            (!disableMouseHoverWhileUsingNavigation || _mouseInputActive))
        {
            SyncMouseHoverSelection();
        }

        if (restoreSelectionIfLost)
            RestoreSelectionIfNeeded();
    }

    private bool IsRebindBlockingUi()
    {
        return useLegacyKeycodeRebind &&
               LegacyKeycodeRebind.I != null &&
               LegacyKeycodeRebind.I.IsBlockingOtherUi;
    }

    private void UpdateInputModeState()
    {
        _mouseMovedThisFrame = false;

        if (Input.mousePresent)
        {
            Vector3 mousePos = Input.mousePosition;

            if (mousePos != _lastMousePosition)
            {
                _mouseMovedThisFrame = true;
                _mouseInputActive = true;
                _lastMousePosition = mousePos;
            }
        }

        if (!disableMouseHoverWhileUsingNavigation)
            return;

        if (HasNavigationInputThisFrame())
        {
            _mouseInputActive = false;
            ClearMouseHoverVisual();
        }
    }

    private bool HasNavigationInputThisFrame()
    {
        bool keyNav =
            Input.GetKeyDown(KeyCode.UpArrow) ||
            Input.GetKeyDown(KeyCode.DownArrow) ||
            Input.GetKeyDown(KeyCode.LeftArrow) ||
            Input.GetKeyDown(KeyCode.RightArrow) ||
            Input.GetKeyDown(KeyCode.W) ||
            Input.GetKeyDown(KeyCode.A) ||
            Input.GetKeyDown(KeyCode.S) ||
            Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.Tab) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(gamepadBackKey);

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        bool axisNav =
            (Mathf.Abs(h) > 0.5f && Mathf.Abs(_prevHorizontalAxis) <= 0.5f) ||
            (Mathf.Abs(v) > 0.5f && Mathf.Abs(_prevVerticalAxis) <= 0.5f);

        _prevHorizontalAxis = h;
        _prevVerticalAxis = v;

        return keyNav || axisNav;
    }

    private void ClearMouseHoverVisual()
    {
        if (_lastHoveredButton == null)
            return;

        if (EventSystem.current != null && _lastHoveredButton.gameObject != null)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(-100000f, -100000f)
            };

            ExecuteEvents.Execute(_lastHoveredButton.gameObject, pointerData, ExecuteEvents.pointerExitHandler);
        }

        _lastHoveredButton = null;
    }

    private void WireButtons()
    {
        Bind(newGameButton, LoadNewGameScene);
        Bind(openContinueButton, OpenContinue);
        Bind(openControlButton, OpenControl);
        Bind(openSettingsButton, OpenSettings);
        Bind(quitButton, OpenExitDialog);

        Bind(continueBackButton, BackToMain);
        Bind(controlBackButton, BackToMain);
        Bind(settingsBackButton, BackToMain);

        Bind(exitNoButton, CloseExitDialog);
        Bind(exitYesButton, QuitGame);
    }

    private static void Bind(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;

        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }

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

        SetMainMenuInteractable(false);
        SelectButtonDeferred(exitConfirmFirstSelected ? exitConfirmFirstSelected : exitNoButton);
    }

    public void CloseExitDialog()
    {
        if (exitConfirmDialog != null)
            exitConfirmDialog.SetActive(false);

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

    private void ShowMainOnly()
    {
        if (mainPanel != null) mainPanel.SetActive(true);

        if (continuePanel != null) continuePanel.SetActive(false);
        if (controlPanel != null) controlPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (exitConfirmDialog != null) exitConfirmDialog.SetActive(false);

        SetMainMenuInteractable(true);
    }

    private void ShowOnlySubPanel(GameObject targetPanel)
    {
        if (mainPanel != null) mainPanel.SetActive(false);

        if (continuePanel != null) continuePanel.SetActive(targetPanel == continuePanel);
        if (controlPanel != null) controlPanel.SetActive(targetPanel == controlPanel);
        if (settingsPanel != null) settingsPanel.SetActive(targetPanel == settingsPanel);

        if (exitConfirmDialog != null) exitConfirmDialog.SetActive(false);

        SetMainMenuInteractable(true);
    }

    private void SetMainMenuInteractable(bool value)
    {
        if (mainPanelCanvasGroup != null)
        {
            mainPanelCanvasGroup.interactable = value;
            mainPanelCanvasGroup.blocksRaycasts = value;
        }

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

    private bool IsBackPressed()
    {
        if (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null)
            return LegacyKeycodeRebind.I.GetDownAny(LegacyKeycodeRebind.Action.Back);

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

        if (exitConfirmDialog != null && exitConfirmDialog.activeSelf)
        {
            SelectButtonDeferred(exitConfirmFirstSelected ? exitConfirmFirstSelected : exitNoButton);
            return;
        }

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

        if (mainPanel != null && mainPanel.activeSelf)
        {
            SelectButtonDeferred(GetDefaultMainButton());
        }
    }

    private Button GetDefaultMainButton()
    {
        if (mainFirstSelected != null) return mainFirstSelected;

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
        noNav.selectOnDown = exitNoButton;
        exitNoButton.navigation = noNav;
    }

    private void SyncMouseHoverSelection()
    {
        if (EventSystem.current == null) return;
        if (!Input.mousePresent) return;
        if (syncMouseOnlyWhenMoved && !_mouseMovedThisFrame) return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        _mouseRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, _mouseRaycastResults);

        Button hoveredButton = null;

        for (int i = 0; i < _mouseRaycastResults.Count; i++)
        {
            GameObject go = _mouseRaycastResults[i].gameObject;
            if (go == null) continue;

            Button btn = go.GetComponentInParent<Button>();
            if (btn == null) continue;
            if (!btn.isActiveAndEnabled || !btn.interactable) continue;
            if (!IsButtonAllowedInCurrentContext(btn)) continue;

            hoveredButton = btn;
            break;
        }

        if (hoveredButton == null)
        {
            ClearMouseHoverVisual();
            return;
        }

        _lastHoveredButton = hoveredButton;

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

        if (exitConfirmDialog != null && exitConfirmDialog.activeSelf)
            return btn.transform.IsChildOf(exitConfirmDialog.transform);

        if (continuePanel != null && continuePanel.activeSelf)
            return btn.transform.IsChildOf(continuePanel.transform);

        if (controlPanel != null && controlPanel.activeSelf)
            return btn.transform.IsChildOf(controlPanel.transform);

        if (settingsPanel != null && settingsPanel.activeSelf)
            return btn.transform.IsChildOf(settingsPanel.transform);

        if (mainPanel != null && mainPanel.activeSelf)
            return btn.transform.IsChildOf(mainPanel.transform);

        return true;
    }
}