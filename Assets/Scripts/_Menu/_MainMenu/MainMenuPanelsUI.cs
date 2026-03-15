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

    [Header("Settings tabs")]
    [SerializeField, Tooltip("Скрипт вкладок внутри Settings_Panel. Если не задан — попробует найти его внутри settingsPanel.")]
    private SettingsTabsSwitcher settingsTabsSwitcher;

    [SerializeField, Tooltip("Если settingsTabsSwitcher не задан вручную — попробовать найти его внутри settingsPanel.")]
    private bool autoFindSettingsTabsSwitcher = true;

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

    [Header("Reset confirm dialogs (optional)")]
    [SerializeField, Tooltip("Панель подтверждения сброса клавиатуры.")]
    private GameObject keyboardResetConfirmDialog;

    [SerializeField, Tooltip("Кнопка Да в окне подтверждения сброса клавиатуры.")]
    private Button keyboardResetYesButton;

    [SerializeField, Tooltip("Кнопка Нет / Закрыть в окне подтверждения сброса клавиатуры.")]
    private Button keyboardResetNoButton;

    [SerializeField, Tooltip("Кнопка Reset Keyboard, на которую вернётся выделение после закрытия keyboard confirm.")]
    private Button keyboardResetReturnButton;

    [SerializeField, Tooltip("Панель подтверждения сброса геймпада.")]
    private GameObject gamepadResetConfirmDialog;

    [SerializeField, Tooltip("Кнопка Да в окне подтверждения сброса геймпада.")]
    private Button gamepadResetYesButton;

    [SerializeField, Tooltip("Кнопка Нет / Закрыть в окне подтверждения сброса геймпада.")]
    private Button gamepadResetNoButton;

    [SerializeField, Tooltip("Кнопка Reset Gamepad, на которую вернётся выделение после закрытия gamepad confirm.")]
    private Button gamepadResetReturnButton;

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

    [SerializeField, Tooltip("Кнопка, которая будет выделена в Keyboard Reset Confirm.")]
    private Button keyboardResetConfirmFirstSelected;

    [SerializeField, Tooltip("Кнопка, которая будет выделена в Gamepad Reset Confirm.")]
    private Button gamepadResetConfirmFirstSelected;

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

    private bool _prevExitConfirmActive;
    private bool _prevKeyboardResetConfirmActive;
    private bool _prevGamepadResetConfirmActive;

    private readonly List<RaycastResult> _mouseRaycastResults = new List<RaycastResult>(16);

    private void Awake()
    {
        CacheSettingsTabsIfNeeded();
        WireButtons();
        SetupExitConfirmNavigation();
        SetupResetConfirmNavigation();
        CacheModalStates();

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
        CacheModalStates();

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

        UpdateModalSelectionState();

        if (IsLegacyUiBlocking())
            return;

        UpdateInputModeState();

        if (!IsBackPressed())
            return;

        if (IsKeyboardResetConfirmOpen())
        {
            InvokeButton(keyboardResetNoButton);
            return;
        }

        if (IsGamepadResetConfirmOpen())
        {
            InvokeButton(gamepadResetNoButton);
            return;
        }

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
            if (HasAnySettingsContentOpen())
            {
                CloseSettingsContentToRoot();
                return;
            }

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

        UpdateModalSelectionState();

        if (IsLegacyUiBlocking()) return;

        if (selectHoveredButtonWithMouse &&
            (!disableMouseHoverWhileUsingNavigation || _mouseInputActive))
        {
            SyncMouseHoverSelection();
        }

        if (restoreSelectionIfLost)
            RestoreSelectionIfNeeded();
    }

    private bool IsLegacyUiBlocking()
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

    private void CacheSettingsTabsIfNeeded()
    {
        if (settingsTabsSwitcher != null)
            return;

        if (!autoFindSettingsTabsSwitcher)
            return;

        if (settingsPanel != null)
            settingsTabsSwitcher = settingsPanel.GetComponentInChildren<SettingsTabsSwitcher>(true);
    }

    private bool HasAnySettingsContentOpen()
    {
        CacheSettingsTabsIfNeeded();

        if (settingsTabsSwitcher == null)
            return false;

        return settingsTabsSwitcher.HasAnyOpenView();
    }

    private void CloseSettingsContentToRoot()
    {
        CacheSettingsTabsIfNeeded();

        if (settingsTabsSwitcher != null)
            settingsTabsSwitcher.CloseToSettingsRoot();

        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        SelectButtonDeferred(settingsFirstSelected ? settingsFirstSelected : settingsBackButton);
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

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        if (currentSelected != null)
        {
            Button selectedButton = currentSelected.GetComponent<Button>();

            if (HasAnyModalOpen() && (selectedButton == null || !IsButtonAllowedInCurrentContext(selectedButton)))
            {
                SelectCurrentPanelDefault();
            }

            return;
        }

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
        SetupTwoButtonNavigation(exitYesButton, exitNoButton);
    }

    private void SetupResetConfirmNavigation()
    {
        SetupTwoButtonNavigation(keyboardResetYesButton, keyboardResetNoButton);
        SetupTwoButtonNavigation(gamepadResetYesButton, gamepadResetNoButton);
    }

    private void SetupTwoButtonNavigation(Button leftOrYes, Button rightOrNo)
    {
        if (leftOrYes == null || rightOrNo == null)
            return;

        Navigation firstNav = leftOrYes.navigation;
        firstNav.mode = Navigation.Mode.Explicit;
        firstNav.selectOnLeft = rightOrNo;
        firstNav.selectOnRight = rightOrNo;
        firstNav.selectOnUp = leftOrYes;
        firstNav.selectOnDown = leftOrYes;
        leftOrYes.navigation = firstNav;

        Navigation secondNav = rightOrNo.navigation;
        secondNav.mode = Navigation.Mode.Explicit;
        secondNav.selectOnLeft = leftOrYes;
        secondNav.selectOnRight = leftOrYes;
        secondNav.selectOnUp = rightOrNo;
        secondNav.selectOnDown = rightOrNo;
        rightOrNo.navigation = secondNav;
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

        if (IsKeyboardResetConfirmOpen())
            return keyboardResetConfirmDialog != null && btn.transform.IsChildOf(keyboardResetConfirmDialog.transform);

        if (IsGamepadResetConfirmOpen())
            return gamepadResetConfirmDialog != null && btn.transform.IsChildOf(gamepadResetConfirmDialog.transform);

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

    private bool IsKeyboardResetConfirmOpen()
    {
        return keyboardResetConfirmDialog != null && keyboardResetConfirmDialog.activeInHierarchy;
    }

    private bool IsGamepadResetConfirmOpen()
    {
        return gamepadResetConfirmDialog != null && gamepadResetConfirmDialog.activeInHierarchy;
    }

    private bool HasAnyModalOpen()
    {
        return exitConfirmDialog != null && exitConfirmDialog.activeSelf;
    }

    private void UpdateModalSelectionState()
    {
        bool exitActive = exitConfirmDialog != null && exitConfirmDialog.activeSelf;

        bool exitJustOpened = exitActive && !_prevExitConfirmActive;
        bool exitJustClosed = !exitActive && _prevExitConfirmActive;

        _prevExitConfirmActive = exitActive;
        _prevKeyboardResetConfirmActive = IsKeyboardResetConfirmOpen();
        _prevGamepadResetConfirmActive = IsGamepadResetConfirmOpen();

        if (exitJustOpened)
        {
            SelectButtonDeferred(exitConfirmFirstSelected ? exitConfirmFirstSelected : exitNoButton);
            return;
        }

        if (exitJustClosed)
        {
            SelectCurrentPanelDefault();
        }
    }

    private void CacheModalStates()
    {
        _prevExitConfirmActive = exitConfirmDialog != null && exitConfirmDialog.activeSelf;
        _prevKeyboardResetConfirmActive = false;
        _prevGamepadResetConfirmActive = false;
    }

    private Button GetKeyboardResetReturnButton()
    {
        if (keyboardResetReturnButton != null && keyboardResetReturnButton.isActiveAndEnabled && keyboardResetReturnButton.interactable)
            return keyboardResetReturnButton;

        if (settingsFirstSelected != null && settingsFirstSelected.isActiveAndEnabled && settingsFirstSelected.interactable)
            return settingsFirstSelected;

        return settingsBackButton;
    }

    private Button GetGamepadResetReturnButton()
    {
        if (gamepadResetReturnButton != null && gamepadResetReturnButton.isActiveAndEnabled && gamepadResetReturnButton.interactable)
            return gamepadResetReturnButton;

        if (settingsFirstSelected != null && settingsFirstSelected.isActiveAndEnabled && settingsFirstSelected.interactable)
            return settingsFirstSelected;

        return settingsBackButton;
    }

    private static void InvokeButton(Button btn)
    {
        if (btn == null) return;
        if (!btn.isActiveAndEnabled || !btn.interactable) return;

        btn.onClick.Invoke();
    }
}
