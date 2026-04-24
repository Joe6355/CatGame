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
    [SerializeField] private GameObject exitConfirmDialog;

    [Header("Settings tabs")]
    [SerializeField, Tooltip("Скрипт вкладок внутри Settings_Panel. Если не задан — попробует найти его внутри settingsPanel.")]
    private SettingsTabsSwitcher settingsTabsSwitcher;

    [SerializeField, Tooltip("Если settingsTabsSwitcher не задан вручную — попробовать найти его внутри settingsPanel.")]
    private bool autoFindSettingsTabsSwitcher = true;

    [Header("Optional CanvasGroups")]
    [SerializeField, Tooltip("CanvasGroup главной панели. Используется для блокировки главных кнопок при открытом диалоге выхода.")]
    private CanvasGroup mainPanelCanvasGroup;

    [Header("Main menu buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button openContinueButton;
    [SerializeField] private Button openControlButton;
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button quitButton;

    [Header("Back buttons in panels")]
    [SerializeField] private Button continueBackButton;
    [SerializeField] private Button controlBackButton;
    [SerializeField] private Button settingsBackButton;

    [Header("Exit confirm buttons")]
    [SerializeField] private Button exitYesButton;
    [SerializeField] private Button exitNoButton;

    [Header("Reset confirm dialogs (optional)")]
    [SerializeField] private GameObject keyboardResetConfirmDialog;
    [SerializeField] private Button keyboardResetYesButton;
    [SerializeField] private Button keyboardResetNoButton;
    [SerializeField] private Button keyboardResetReturnButton;

    [SerializeField] private GameObject gamepadResetConfirmDialog;
    [SerializeField] private Button gamepadResetYesButton;
    [SerializeField] private Button gamepadResetNoButton;
    [SerializeField] private Button gamepadResetReturnButton;

    [Header("New Game")]
    [SerializeField, Tooltip("Имя сцены для кнопки New Game.")]
    private string newGameSceneName;

    [Header("Start state")]
    [SerializeField, Tooltip("На старте показать mainPanel и скрыть остальные панели/диалоги.")]
    private bool setupInitialStateOnAwake = true;

    [Header("UI Navigation")]
    [SerializeField] private bool enableUiNavigation = true;

    [SerializeField, Tooltip("Если выделение потерялось — восстановить его.")]
    private bool restoreSelectionIfLost = true;

    [SerializeField] private Button mainFirstSelected;
    [SerializeField] private Button continueFirstSelected;
    [SerializeField] private Button controlFirstSelected;
    [SerializeField] private Button settingsFirstSelected;
    [SerializeField] private Button exitConfirmFirstSelected;
    [SerializeField] private Button keyboardResetConfirmFirstSelected;
    [SerializeField] private Button gamepadResetConfirmFirstSelected;

    [Header("Back / Cancel input")]
    [SerializeField] private KeyCode keyboardBackKey = KeyCode.Escape;
    [SerializeField] private KeyCode gamepadBackKey = KeyCode.JoystickButton1;

    [SerializeField, Tooltip("Если нажать Back в главном меню — открыть диалог выхода.")]
    private bool openExitDialogOnBackFromMain = true;

    [Header("Legacy Rebind")]
    [SerializeField, Tooltip("Если есть LegacyKeycodeRebind — Back читается из него.")]
    private bool useLegacyKeycodeRebind = true;

    [Header("Mouse hover sync")]
    [SerializeField, Tooltip("При наведении мышью делать кнопку currentSelected.")]
    private bool selectHoveredButtonWithMouse = true;

    [SerializeField, Tooltip("Обновлять hover -> selected только когда мышь реально двигается.")]
    private bool syncMouseOnlyWhenMoved = true;

    [SerializeField, Tooltip("После навигации клавой/геймпадом временно отключать hover мыши, пока мышь снова не сдвинется.")]
    private bool disableMouseHoverWhileUsingNavigation = true;

    [Header("Button Visual Reset Fix")]
    [SerializeField, Tooltip("Перед выключением панели принудительно сбрасывать визуальное состояние кнопок.")]
    private bool resetButtonVisualStateBeforePanelSwitch = true;

    [SerializeField, Tooltip("Посылать кнопкам PointerUp/PointerExit/Deselect перед выключением панели.")]
    private bool sendPointerEventsBeforePanelSwitch = true;

    [SerializeField, Tooltip("Сбрасывать Animator на кнопках перед выключением панели. Нужно, если кнопки залипают в Pressed/Highlighted.")]
    private bool resetButtonAnimatorsBeforePanelSwitch = true;

    [SerializeField, Tooltip("Сбрасывать targetGraphic кнопки в normalColor.")]
    private bool resetButtonGraphicColorBeforePanelSwitch = true;

    [SerializeField, Tooltip("Логи сброса визуального состояния.")]
    private bool debugVisualReset = false;

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

        UIButtonTextColor.SetSuppressPointerHoverVisuals(false);

        if (enableUiNavigation)
            SelectCurrentPanelDefault();
    }

    private void OnEnable()
    {
        CacheModalStates();

        UIButtonTextColor.SetSuppressPointerHoverVisuals(false);

        if (enableUiNavigation)
            SelectCurrentPanelDefault();
    }

    private void OnDisable()
    {
        StopSelectRoutine();

        UIButtonTextColor.SetSuppressPointerHoverVisuals(false);

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
        if (!enableUiNavigation)
            return;

        UpdateModalSelectionState();

        if (IsLegacyUiBlocking())
            return;

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

                UIButtonTextColor.SetSuppressPointerHoverVisuals(false);
                RefreshAllButtonTextColors();
            }
        }

        if (!disableMouseHoverWhileUsingNavigation)
            return;

        if (HasNavigationInputThisFrame())
        {
            _mouseInputActive = false;

            UIButtonTextColor.SetSuppressPointerHoverVisuals(true);

            ClearMouseHoverVisual();
            ClearPointerStateOnAllButtonsKeepSelection();
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
                position = new Vector2(-100000f, -100000f),
                button = PointerEventData.InputButton.Left
            };

            ExecuteEvents.Execute(_lastHoveredButton.gameObject, pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(_lastHoveredButton.gameObject, pointerData, ExecuteEvents.pointerExitHandler);
        }

        _lastHoveredButton = null;
    }

    private void ClearPointerStateOnAllButtonsKeepSelection()
    {
        ClearPointerStateOnPanelKeepSelection(mainPanel);
        ClearPointerStateOnPanelKeepSelection(continuePanel);
        ClearPointerStateOnPanelKeepSelection(controlPanel);
        ClearPointerStateOnPanelKeepSelection(settingsPanel);
        ClearPointerStateOnPanelKeepSelection(exitConfirmDialog);
        ClearPointerStateOnPanelKeepSelection(keyboardResetConfirmDialog);
        ClearPointerStateOnPanelKeepSelection(gamepadResetConfirmDialog);
    }

    private void ClearPointerStateOnPanelKeepSelection(GameObject panel)
    {
        if (panel == null)
            return;

        UIButtonTextColor[] textColors = panel.GetComponentsInChildren<UIButtonTextColor>(true);

        for (int i = 0; i < textColors.Length; i++)
        {
            if (textColors[i] != null)
                textColors[i].ForceClearPointerStateKeepSelection();
        }
    }

    private void RefreshAllButtonTextColors()
    {
        RefreshButtonTextColorsInPanel(mainPanel);
        RefreshButtonTextColorsInPanel(continuePanel);
        RefreshButtonTextColorsInPanel(controlPanel);
        RefreshButtonTextColorsInPanel(settingsPanel);
        RefreshButtonTextColorsInPanel(exitConfirmDialog);
        RefreshButtonTextColorsInPanel(keyboardResetConfirmDialog);
        RefreshButtonTextColorsInPanel(gamepadResetConfirmDialog);
    }

    private void RefreshButtonTextColorsInPanel(GameObject panel)
    {
        if (panel == null)
            return;

        UIButtonTextColor[] textColors = panel.GetComponentsInChildren<UIButtonTextColor>(true);

        for (int i = 0; i < textColors.Length; i++)
        {
            if (textColors[i] != null)
                textColors[i].ForceRefreshVisualState();
        }
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

        ResetPanelVisualState(settingsPanel);

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
        if (btn == null)
            return;

        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }

    public void LoadNewGameScene()
    {
        PrepareForPanelSwitch();

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
        ResetPanelVisualState(mainPanel);

        if (exitConfirmDialog != null)
            exitConfirmDialog.SetActive(true);

        SetMainMenuInteractable(false);
        SelectButtonDeferred(exitConfirmFirstSelected ? exitConfirmFirstSelected : exitNoButton);
    }

    public void CloseExitDialog()
    {
        ResetPanelVisualState(exitConfirmDialog);

        if (exitConfirmDialog != null)
            exitConfirmDialog.SetActive(false);

        SetMainMenuInteractable(true);
        SelectCurrentPanelDefault();
    }

    public void QuitGame()
    {
        PrepareForPanelSwitch();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowMainOnly()
    {
        PrepareForPanelSwitch();

        if (mainPanel != null) mainPanel.SetActive(true);

        if (continuePanel != null) continuePanel.SetActive(false);
        if (controlPanel != null) controlPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (exitConfirmDialog != null) exitConfirmDialog.SetActive(false);

        SetMainMenuInteractable(true);
        Canvas.ForceUpdateCanvases();
    }

    private void ShowOnlySubPanel(GameObject targetPanel)
    {
        PrepareForPanelSwitch();

        if (mainPanel != null) mainPanel.SetActive(false);

        if (continuePanel != null) continuePanel.SetActive(targetPanel == continuePanel);
        if (controlPanel != null) controlPanel.SetActive(targetPanel == controlPanel);
        if (settingsPanel != null) settingsPanel.SetActive(targetPanel == settingsPanel);

        if (exitConfirmDialog != null) exitConfirmDialog.SetActive(false);

        SetMainMenuInteractable(true);
        Canvas.ForceUpdateCanvases();
    }

    private void PrepareForPanelSwitch()
    {
        StopSelectRoutine();

        UIButtonTextColor.SetSuppressPointerHoverVisuals(false);

        ClearMouseHoverVisual();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        ResetPanelVisualState(mainPanel);
        ResetPanelVisualState(continuePanel);
        ResetPanelVisualState(controlPanel);
        ResetPanelVisualState(settingsPanel);
        ResetPanelVisualState(exitConfirmDialog);
        ResetPanelVisualState(keyboardResetConfirmDialog);
        ResetPanelVisualState(gamepadResetConfirmDialog);

        _lastHoveredButton = null;
    }

    private void ResetPanelVisualState(GameObject panel)
    {
        if (!resetButtonVisualStateBeforePanelSwitch)
            return;

        if (panel == null)
            return;

        if (EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;

            if (selected != null && selected.transform.IsChildOf(panel.transform))
                EventSystem.current.SetSelectedGameObject(null);
        }

        PointerEventData pointerData = null;

        if (sendPointerEventsBeforePanelSwitch && EventSystem.current != null)
        {
            pointerData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(-100000f, -100000f),
                button = PointerEventData.InputButton.Left
            };
        }

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];

            if (btn == null)
                continue;

            if (pointerData != null)
            {
                ExecuteEvents.Execute(btn.gameObject, pointerData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(btn.gameObject, pointerData, ExecuteEvents.pointerExitHandler);
                ExecuteEvents.Execute(btn.gameObject, pointerData, ExecuteEvents.deselectHandler);
            }

            if (resetButtonGraphicColorBeforePanelSwitch)
                ResetButtonGraphicColor(btn);

            if (resetButtonAnimatorsBeforePanelSwitch)
                ResetButtonAnimators(btn);

            if (debugVisualReset)
                Debug.Log($"MainMenuPanelsUI: reset button visual -> {btn.name}", btn);
        }

        UIButtonTextColor[] textColors = panel.GetComponentsInChildren<UIButtonTextColor>(true);

        for (int i = 0; i < textColors.Length; i++)
        {
            if (textColors[i] != null)
                textColors[i].ForceResetVisualState();
        }
    }

    private static void ResetButtonGraphicColor(Button btn)
    {
        if (btn == null)
            return;

        Graphic graphic = btn.targetGraphic;

        if (graphic == null)
            return;

        Color targetColor = btn.interactable ? btn.colors.normalColor : btn.colors.disabledColor;
        graphic.CrossFadeColor(targetColor, 0f, true, true);
    }

    private static void ResetButtonAnimators(Button btn)
    {
        if (btn == null)
            return;

        Animator[] animators = btn.GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];

            if (animator == null)
                continue;

            if (!animator.gameObject.activeInHierarchy)
                continue;

            animator.Rebind();
            animator.Update(0f);
        }
    }

    private void StopSelectRoutine()
    {
        if (_selectRoutine == null)
            return;

        StopCoroutine(_selectRoutine);
        _selectRoutine = null;
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
        if (EventSystem.current == null)
            return;

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
        if (!enableUiNavigation)
            return;

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
        if (mainFirstSelected != null)
            return mainFirstSelected;

        if (newGameButton != null && newGameButton.isActiveAndEnabled && newGameButton.interactable) return newGameButton;
        if (openContinueButton != null && openContinueButton.isActiveAndEnabled && openContinueButton.interactable) return openContinueButton;
        if (openSettingsButton != null && openSettingsButton.isActiveAndEnabled && openSettingsButton.interactable) return openSettingsButton;
        if (openControlButton != null && openControlButton.isActiveAndEnabled && openControlButton.interactable) return openControlButton;
        if (quitButton != null && quitButton.isActiveAndEnabled && quitButton.interactable) return quitButton;

        return null;
    }

    private void SelectButtonDeferred(Button btn)
    {
        if (!enableUiNavigation)
            return;

        if (btn == null)
            return;

        if (!btn.isActiveAndEnabled || !btn.interactable)
            return;

        if (EventSystem.current == null)
            return;

        StopSelectRoutine();

        _selectRoutine = StartCoroutine(SelectButtonNextFrame(btn));
    }

    private IEnumerator SelectButtonNextFrame(Button btn)
    {
        yield return null;

        if (!enableUiNavigation)
            yield break;

        if (btn == null)
            yield break;

        if (EventSystem.current == null)
            yield break;

        if (!btn.isActiveAndEnabled || !btn.interactable)
            yield break;

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
        if (EventSystem.current == null)
            return;

        if (!Input.mousePresent)
            return;

        if (syncMouseOnlyWhenMoved && !_mouseMovedThisFrame)
            return;

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

            if (go == null)
                continue;

            Button btn = go.GetComponentInParent<Button>();

            if (btn == null)
                continue;

            if (!btn.isActiveAndEnabled || !btn.interactable)
                continue;

            if (!IsButtonAllowedInCurrentContext(btn))
                continue;

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

        StopSelectRoutine();

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(hoveredButton.gameObject);
    }

    private bool IsButtonAllowedInCurrentContext(Button btn)
    {
        if (btn == null)
            return false;

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
        bool keyboardResetActive = IsKeyboardResetConfirmOpen();
        bool gamepadResetActive = IsGamepadResetConfirmOpen();

        bool exitJustOpened = exitActive && !_prevExitConfirmActive;
        bool exitJustClosed = !exitActive && _prevExitConfirmActive;

        bool keyboardResetJustOpened = keyboardResetActive && !_prevKeyboardResetConfirmActive;
        bool keyboardResetJustClosed = !keyboardResetActive && _prevKeyboardResetConfirmActive;

        bool gamepadResetJustOpened = gamepadResetActive && !_prevGamepadResetConfirmActive;
        bool gamepadResetJustClosed = !gamepadResetActive && _prevGamepadResetConfirmActive;

        _prevExitConfirmActive = exitActive;
        _prevKeyboardResetConfirmActive = keyboardResetActive;
        _prevGamepadResetConfirmActive = gamepadResetActive;

        if (keyboardResetJustOpened)
        {
            SelectButtonDeferred(keyboardResetConfirmFirstSelected ? keyboardResetConfirmFirstSelected : keyboardResetNoButton);
            return;
        }

        if (gamepadResetJustOpened)
        {
            SelectButtonDeferred(gamepadResetConfirmFirstSelected ? gamepadResetConfirmFirstSelected : gamepadResetNoButton);
            return;
        }

        if (exitJustOpened)
        {
            SelectButtonDeferred(exitConfirmFirstSelected ? exitConfirmFirstSelected : exitNoButton);
            return;
        }

        if (keyboardResetJustClosed)
        {
            SelectButtonDeferred(GetKeyboardResetReturnButton());
            return;
        }

        if (gamepadResetJustClosed)
        {
            SelectButtonDeferred(GetGamepadResetReturnButton());
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
        _prevKeyboardResetConfirmActive = IsKeyboardResetConfirmOpen();
        _prevGamepadResetConfirmActive = IsGamepadResetConfirmOpen();
    }

    private Button GetKeyboardResetReturnButton()
    {
        if (keyboardResetReturnButton != null &&
            keyboardResetReturnButton.isActiveAndEnabled &&
            keyboardResetReturnButton.interactable)
        {
            return keyboardResetReturnButton;
        }

        if (settingsFirstSelected != null &&
            settingsFirstSelected.isActiveAndEnabled &&
            settingsFirstSelected.interactable)
        {
            return settingsFirstSelected;
        }

        return settingsBackButton;
    }

    private Button GetGamepadResetReturnButton()
    {
        if (gamepadResetReturnButton != null &&
            gamepadResetReturnButton.isActiveAndEnabled &&
            gamepadResetReturnButton.interactable)
        {
            return gamepadResetReturnButton;
        }

        if (settingsFirstSelected != null &&
            settingsFirstSelected.isActiveAndEnabled &&
            settingsFirstSelected.interactable)
        {
            return settingsFirstSelected;
        }

        return settingsBackButton;
    }

    private static void InvokeButton(Button btn)
    {
        if (btn == null)
            return;

        if (!btn.isActiveAndEnabled || !btn.interactable)
            return;

        btn.onClick.Invoke();
    }
}