using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(10000)]
public class PauseMenuUI : MonoBehaviour
{
    [Header("Root panels")]
    [SerializeField] private GameObject pauseRoot;        // затемнение + всё pause-меню
    [SerializeField] private GameObject menuPanel;        // главное меню паузы
    [SerializeField] private GameObject settingsPanel;    // панель настроек
    [SerializeField] private GameObject controlsPanel;    // отдельная панель управления (НЕ вкладка Settings)

    [Header("Settings tabs")]
    [SerializeField, Tooltip("Скрипт вкладок внутри Settings_Panel. Если не задан — попробует найти внутри settingsPanel.")]
    private SettingsTabsSwitcher settingsTabsSwitcher;

    [SerializeField, Tooltip("Если settingsTabsSwitcher не задан вручную — попробовать найти его внутри settingsPanel.")]
    private bool autoFindSettingsTabsSwitcher = true;

    [Header("Confirm panels")]
    [SerializeField] private GameObject exitConfirmPanel;
    [SerializeField] private GameObject restartConfirmPanel;

    [Header("Main menu buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button exitToMenuButton;

    [Header("Confirm buttons: Exit")]
    [SerializeField] private Button exitYesButton;
    [SerializeField] private Button exitNoButton;

    [Header("Confirm buttons: Restart")]
    [SerializeField] private Button restartYesButton;
    [SerializeField] private Button restartNoButton;

    [Header("Confirm return buttons")]
    [SerializeField, Tooltip("На какую кнопку возвращать выделение после закрытия подтверждения выхода. Если пусто — вернётся на кнопку, с которой окно открыли, иначе на Exit To Menu.")]
    private Button exitConfirmReturnButton;

    [SerializeField, Tooltip("На какую кнопку возвращать выделение после закрытия подтверждения рестарта. Если пусто — вернётся на кнопку, с которой окно открыли, иначе на Restart.")]
    private Button restartConfirmReturnButton;

    [Header("Back buttons (optional)")]
    [SerializeField] private Button settingsBackButton;   // Back внутри Settings_Panel
    [SerializeField] private Button controlsBackButton;   // Back внутри отдельного ControlsPanel

    [Header("Optional Pause Button (mobile/UI)")]
    [SerializeField] private Button openPauseButton;      // кнопка паузы на экране

    [Header("Navigation (keyboard / gamepad)")]
    [SerializeField] private bool enableUiNavigation = true;

    [Tooltip("Кнопка 'Назад/Отмена' на геймпаде. Обычно B / Circle = JoystickButton1.")]
    [SerializeField] private KeyCode gamepadBackKey = KeyCode.JoystickButton1;

    [Tooltip("Какой элемент выделять при открытии главного меню паузы. Если пусто — Continue.")]
    [SerializeField] private Button pauseMenuFirstSelected;

    [Tooltip("Какой элемент выделять внутри Settings_Panel. Обычно сюда лучше поставить первую кнопку вкладок (например Audio), а не Back.")]
    [SerializeField] private Button settingsFirstSelected;

    [Tooltip("Какой элемент выделять внутри отдельного ControlsPanel.")]
    [SerializeField] private Button controlsFirstSelected;

    [Tooltip("Какая кнопка выделяется в подтверждении выхода (обычно No).")]
    [SerializeField] private Button exitConfirmFirstSelected;

    [Tooltip("Какая кнопка выделяется в подтверждении рестарта (обычно No).")]
    [SerializeField] private Button restartConfirmFirstSelected;

    [Tooltip("Если выделение потерялось — восстановить автоматически.")]
    [SerializeField] private bool restoreSelectionIfLost = true;

    [Header("Mouse hover sync")]
    [SerializeField, Tooltip("Если включено — hovered кнопка становится current selected, чтобы не было двойной подсветки.")]
    private bool selectHoveredButtonWithMouse = true;

    [SerializeField, Tooltip("Обновлять hover -> selected только когда мышь реально двигается.")]
    private bool syncMouseOnlyWhenMoved = true;

    [SerializeField, Tooltip("Когда игрок начал пользоваться клавиатурой/геймпадом — hover мыши временно отключается, пока мышь снова не сдвинется.")]
    private bool disableMouseHoverWhileUsingNavigation = true;

    [Header("Pause input")]
    [SerializeField] private KeyCode keyboardPauseKey = KeyCode.Escape;

    [Tooltip("Обычно Start / Options / Menu на геймпаде. Часто это JoystickButton7.")]
    [SerializeField] private KeyCode gamepadPauseKey = KeyCode.JoystickButton7;

    [Tooltip("Дополнительная кнопка паузы. Можно оставить None.")]
    [SerializeField] private KeyCode gamepadPauseAltKey = KeyCode.None;

    [Header("Legacy Rebind (KeyCode)")]
    [SerializeField, Tooltip("Если включено и в сцене есть LegacyKeycodeRebind — Pause/Back читаются из него.")]
    private bool useLegacyKeycodeRebind = true;

    [Header("Gameplay input block")]
    [SerializeField, Tooltip("Ссылка на PlayerController, у которого выключаем игровой ввод на время паузы.")]
    private PlayerController playerController;

    [SerializeField, Tooltip("Если ссылка на PlayerController не задана — попробовать найти автоматически.")]
    private bool autoFindPlayerController = true;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Cursor")]
    [SerializeField] private bool showCursorWhenPaused = true;
    [SerializeField] private bool hideCursorWhenPlaying = true;
    [SerializeField] private CursorLockMode playLockMode = CursorLockMode.None;

    private bool _paused;
    private Coroutine _selectRoutine;

    private readonly List<RaycastResult> _mouseRaycastResults = new List<RaycastResult>(16);

    private bool _pauseButtonNavCached;
    private Navigation _pauseButtonOriginalNavigation;

    private Vector3 _lastMousePosition;
    private bool _mouseMovedThisFrame;
    private bool _mouseInputActive = true;
    private Button _lastHoveredButton;
    private float _prevHorizontalAxis;
    private float _prevVerticalAxis;

    private bool _prevExitConfirmActive;
    private bool _prevRestartConfirmActive;

    private Button _selectedBeforeExitConfirm;
    private Button _selectedBeforeRestartConfirm;

    public bool IsPaused => _paused;

    private void Awake()
    {
        CacheSettingsTabsIfNeeded();
        CacheOpenPauseButtonNavigationIfNeeded();
        WireButtons();
        SetupConfirmNavigation();
        CacheModalStates();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        _lastMousePosition = Input.mousePosition;
        _mouseInputActive = Input.mousePresent;

        CachePlayerControllerIfNeeded();
        CacheSettingsTabsIfNeeded();
        ResumeGame(true);
    }

    private void OnEnable()
    {
        CacheModalStates();
    }

    private void Update()
    {
        UpdateConfirmSelectionState();

        if (IsRebindBlockingUi())
            return;

        UpdateInputModeState();

        if (!_paused)
        {
            if (IsPausePressed())
                PauseGame();

            return;
        }

        if (!IsPausePressed() && !IsBackPressed())
            return;

        if (exitConfirmPanel && exitConfirmPanel.activeSelf)
        {
            CancelExitToMainMenu();
            return;
        }

        if (restartConfirmPanel && restartConfirmPanel.activeSelf)
        {
            CancelRestartLevel();
            return;
        }

        if (controlsPanel && controlsPanel.activeSelf)
        {
            BackToPauseMenu();
            return;
        }

        if (settingsPanel && settingsPanel.activeSelf)
        {
            if (HasAnySettingsContentOpen())
            {
                CloseSettingsContentToRoot();
                return;
            }

            BackToPauseMenu();
            return;
        }

        ResumeGame();
    }

    private void LateUpdate()
    {
        if (!_paused || pauseRoot == null || !pauseRoot.activeSelf)
            return;

        ApplyPauseCursorState();
        UpdateConfirmSelectionState();

        if (IsRebindBlockingUi())
            return;

        if (enableUiNavigation &&
            selectHoveredButtonWithMouse &&
            (!disableMouseHoverWhileUsingNavigation || _mouseInputActive))
        {
            SyncMouseHoverSelection();
        }

        if (enableUiNavigation && restoreSelectionIfLost)
            RestoreSelectionIfNeeded();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;

        if (_paused && pauseRoot != null && pauseRoot.activeSelf)
            ApplyPauseCursorState();
        else
            ApplyPlayCursorState();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) return;

        if (_paused && pauseRoot != null && pauseRoot.activeSelf)
            ApplyPauseCursorState();
        else
            ApplyPlayCursorState();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        playerController = null;
        CachePlayerControllerIfNeeded();
        CacheSettingsTabsIfNeeded();
        CacheOpenPauseButtonNavigationIfNeeded();
        CacheModalStates();

        _selectedBeforeExitConfirm = null;
        _selectedBeforeRestartConfirm = null;

        if (_paused && pauseRoot != null && pauseRoot.activeSelf)
            ApplyPauseCursorState();
        else
            ApplyPlayCursorState();
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

    private void CachePlayerControllerIfNeeded()
    {
        if (playerController == null && autoFindPlayerController)
            playerController = FindObjectOfType<PlayerController>();
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

    private void CacheOpenPauseButtonNavigationIfNeeded()
    {
        if (openPauseButton == null || _pauseButtonNavCached)
            return;

        _pauseButtonOriginalNavigation = openPauseButton.navigation;
        _pauseButtonNavCached = true;
    }

    private void SetOpenPauseButtonAvailable(bool available)
    {
        if (openPauseButton == null)
            return;

        CacheOpenPauseButtonNavigationIfNeeded();

        openPauseButton.interactable = available;

        if (available)
        {
            openPauseButton.navigation = _pauseButtonOriginalNavigation;
        }
        else
        {
            Navigation nav = openPauseButton.navigation;
            nav.mode = Navigation.Mode.None;
            nav.selectOnUp = null;
            nav.selectOnDown = null;
            nav.selectOnLeft = null;
            nav.selectOnRight = null;
            openPauseButton.navigation = nav;

            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == openPauseButton.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }

    private bool IsRebindBlockingUi()
    {
        LegacyKeycodeRebind rebind =
            (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null)
            ? LegacyKeycodeRebind.I
            : null;

        return rebind != null && rebind.IsBlockingOtherUi;
    }

    private void SetGameplayInputEnabled(bool enabled)
    {
        CachePlayerControllerIfNeeded();

        if (playerController != null)
            playerController.SetInputEnabled(enabled);
    }

    private bool IsPausePressed()
    {
        LegacyKeycodeRebind rebind =
            (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null)
            ? LegacyKeycodeRebind.I
            : null;

        if (rebind != null)
            return rebind.GetDownAny(LegacyKeycodeRebind.Action.Pause);

        if (Input.GetKeyDown(keyboardPauseKey))
            return true;

        if (Input.GetKeyDown(gamepadPauseKey))
            return true;

        if (gamepadPauseAltKey != KeyCode.None && Input.GetKeyDown(gamepadPauseAltKey))
            return true;

        return false;
    }

    private bool IsBackPressed()
    {
        LegacyKeycodeRebind rebind =
            (useLegacyKeycodeRebind && LegacyKeycodeRebind.I != null)
            ? LegacyKeycodeRebind.I
            : null;

        if (rebind != null)
            return rebind.GetDownAny(LegacyKeycodeRebind.Action.Back);

        if (Input.GetKeyDown(KeyCode.Escape))
            return true;

        return Input.GetKeyDown(gamepadBackKey);
    }

    private void WireButtons()
    {
        Bind(continueButton, ResumeGame);
        Bind(restartButton, AskRestartLevel);
        Bind(settingsButton, OpenSettings);
        Bind(controlsButton, OpenControls);
        Bind(exitToMenuButton, AskExitToMainMenu);

        Bind(exitYesButton, ConfirmExitToMainMenu);
        Bind(exitNoButton, CancelExitToMainMenu);

        Bind(restartYesButton, ConfirmRestartLevel);
        Bind(restartNoButton, CancelRestartLevel);

        Bind(settingsBackButton, BackToPauseMenu);
        Bind(controlsBackButton, BackToPauseMenu);

        Bind(openPauseButton, OpenPauseFromButton);
    }

    private static void Bind(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;

        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }

    private void SetupConfirmNavigation()
    {
        SetupTwoButtonNavigation(exitYesButton, exitNoButton);
        SetupTwoButtonNavigation(restartYesButton, restartNoButton);
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

    private void ApplyPauseCursorState()
    {
        if (!showCursorWhenPaused) return;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ApplyPlayCursorState()
    {
        if (!hideCursorWhenPlaying) return;

        Cursor.visible = false;
        Cursor.lockState = playLockMode;
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

        if (menuPanel) menuPanel.SetActive(false);
        if (controlsPanel) controlsPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
        if (exitConfirmPanel) exitConfirmPanel.SetActive(false);
        if (restartConfirmPanel) restartConfirmPanel.SetActive(false);

        SelectSettingsRoot();
    }

    private void SelectSettingsRoot()
    {
        if (settingsFirstSelected != null && settingsFirstSelected.isActiveAndEnabled && settingsFirstSelected.interactable)
        {
            SelectButtonDeferred(settingsFirstSelected);
            return;
        }

        if (settingsBackButton != null && settingsBackButton.isActiveAndEnabled && settingsBackButton.interactable)
        {
            SelectButtonDeferred(settingsBackButton);
            return;
        }
    }

    private void RestoreSelectionIfNeeded()
    {
        if (EventSystem.current == null) return;

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        if (currentSelected != null)
        {
            Selectable currentSelectable = currentSelected.GetComponent<Selectable>();

            if (currentSelectable == null || !IsSelectableAllowedInCurrentContext(currentSelectable))
                SelectCurrentContextDefault();

            return;
        }

        SelectCurrentContextDefault();
    }

    private void SelectCurrentContextDefault()
    {
        if (exitConfirmPanel && exitConfirmPanel.activeSelf)
        {
            SelectButtonDeferred(exitConfirmFirstSelected ? exitConfirmFirstSelected : exitNoButton);
            return;
        }

        if (restartConfirmPanel && restartConfirmPanel.activeSelf)
        {
            SelectButtonDeferred(restartConfirmFirstSelected ? restartConfirmFirstSelected : restartNoButton);
            return;
        }

        if (controlsPanel && controlsPanel.activeSelf)
        {
            SelectButtonDeferred(controlsFirstSelected ? controlsFirstSelected : controlsBackButton);
            return;
        }

        if (settingsPanel && settingsPanel.activeSelf)
        {
            if (HasAnySettingsContentOpen())
                SelectButtonDeferred(settingsFirstSelected ? settingsFirstSelected : settingsBackButton);
            else
                SelectSettingsRoot();

            return;
        }

        if (menuPanel && menuPanel.activeSelf)
            SelectPauseMenuDefault();
    }

    private void SelectPauseMenuDefault()
    {
        SelectButtonDeferred(pauseMenuFirstSelected ? pauseMenuFirstSelected : continueButton);
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
        if (!IsButtonAllowedInCurrentContext(btn)) yield break;

        Canvas.ForceUpdateCanvases();
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(btn.gameObject);

        _selectRoutine = null;
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

    private bool IsSelectableAllowedInCurrentContext(Selectable selectable)
    {
        if (selectable == null) return false;
        if (!selectable.isActiveAndEnabled) return false;
        if (!selectable.interactable) return false;

        return IsTransformAllowedInCurrentContext(selectable.transform);
    }

    private bool IsButtonAllowedInCurrentContext(Button btn)
    {
        if (btn == null) return false;
        return IsSelectableAllowedInCurrentContext(btn);
    }

    private bool IsTransformAllowedInCurrentContext(Transform target)
    {
        if (target == null) return false;

        if (_paused && openPauseButton != null && target == openPauseButton.transform)
            return false;

        if (exitConfirmPanel != null && exitConfirmPanel.activeSelf)
            return target.IsChildOf(exitConfirmPanel.transform);

        if (restartConfirmPanel != null && restartConfirmPanel.activeSelf)
            return target.IsChildOf(restartConfirmPanel.transform);

        if (controlsPanel != null && controlsPanel.activeSelf)
            return target.IsChildOf(controlsPanel.transform);

        if (settingsPanel != null && settingsPanel.activeSelf)
            return target.IsChildOf(settingsPanel.transform);

        if (menuPanel != null && menuPanel.activeSelf)
            return target.IsChildOf(menuPanel.transform);

        return false;
    }

    public void OpenPauseFromButton()
    {
        if (_paused) return;

        _mouseInputActive = true;
        PauseGame();
    }

    public void TogglePause()
    {
        if (_paused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        SetGameplayInputEnabled(false);
        SetOpenPauseButtonAvailable(false);

        _paused = true;
        Time.timeScale = 0f;

        if (pauseRoot) pauseRoot.SetActive(true);

        ShowMainMenu();
        ApplyPauseCursorState();
        CacheModalStates();
        SelectPauseMenuDefault();
    }

    public void ResumeGame()
    {
        ResumeGame(false);
    }

    private void ResumeGame(bool isInitialStart)
    {
        _paused = false;
        Time.timeScale = 1f;

        HideAllSubPanels();
        if (pauseRoot) pauseRoot.SetActive(false);

        if (_selectRoutine != null)
        {
            StopCoroutine(_selectRoutine);
            _selectRoutine = null;
        }

        ClearMouseHoverVisual();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        ApplyPlayCursorState();
        SetOpenPauseButtonAvailable(true);
        CacheModalStates();

        _selectedBeforeExitConfirm = null;
        _selectedBeforeRestartConfirm = null;

        if (!isInitialStart)
            SetGameplayInputEnabled(true);
    }

    public void AskRestartLevel()
    {
        _selectedBeforeRestartConfirm = GetCurrentlySelectedButton();

        HideAllSubPanels();
        if (restartConfirmPanel) restartConfirmPanel.SetActive(true);

        SelectButtonDeferred(restartConfirmFirstSelected ? restartConfirmFirstSelected : restartNoButton);
    }

    public void ConfirmRestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void CancelRestartLevel()
    {
        if (restartConfirmPanel) restartConfirmPanel.SetActive(false);
        ShowMainMenu();
        SelectButtonDeferred(GetRestartConfirmReturnButton());
    }

    public void AskExitToMainMenu()
    {
        _selectedBeforeExitConfirm = GetCurrentlySelectedButton();

        HideAllSubPanels();
        if (exitConfirmPanel) exitConfirmPanel.SetActive(true);

        SelectButtonDeferred(exitConfirmFirstSelected ? exitConfirmFirstSelected : exitNoButton);
    }

    public void ConfirmExitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void CancelExitToMainMenu()
    {
        if (exitConfirmPanel) exitConfirmPanel.SetActive(false);
        ShowMainMenu();
        SelectButtonDeferred(GetExitConfirmReturnButton());
    }

    public void OpenSettings()
    {
        CacheSettingsTabsIfNeeded();

        HideAllSubPanels();

        if (settingsPanel) settingsPanel.SetActive(true);
        if (controlsPanel) controlsPanel.SetActive(false);

        if (settingsTabsSwitcher != null)
            settingsTabsSwitcher.CloseToSettingsRoot();

        SelectSettingsRoot();
    }

    public void OpenControls()
    {
        HideAllSubPanels();

        if (controlsPanel) controlsPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);

        SelectButtonDeferred(controlsFirstSelected ? controlsFirstSelected : controlsBackButton);
    }

    public void BackToPauseMenu()
    {
        CacheSettingsTabsIfNeeded();

        if (settingsTabsSwitcher != null)
            settingsTabsSwitcher.CloseToSettingsRoot();

        if (settingsPanel) settingsPanel.SetActive(false);
        if (controlsPanel) controlsPanel.SetActive(false);
        if (exitConfirmPanel) exitConfirmPanel.SetActive(false);
        if (restartConfirmPanel) restartConfirmPanel.SetActive(false);

        ShowMainMenu();
        SelectPauseMenuDefault();
    }

    private void HideAllSubPanels()
    {
        if (menuPanel) menuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (controlsPanel) controlsPanel.SetActive(false);
        if (exitConfirmPanel) exitConfirmPanel.SetActive(false);
        if (restartConfirmPanel) restartConfirmPanel.SetActive(false);
    }

    private void ShowMainMenu()
    {
        if (menuPanel) menuPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (controlsPanel) controlsPanel.SetActive(false);
        if (exitConfirmPanel) exitConfirmPanel.SetActive(false);
        if (restartConfirmPanel) restartConfirmPanel.SetActive(false);
    }

    private Button GetExitConfirmReturnButton()
    {
        if (exitConfirmReturnButton != null && exitConfirmReturnButton.isActiveAndEnabled && exitConfirmReturnButton.interactable)
            return exitConfirmReturnButton;

        if (_selectedBeforeExitConfirm != null && _selectedBeforeExitConfirm.isActiveAndEnabled && _selectedBeforeExitConfirm.interactable)
            return _selectedBeforeExitConfirm;

        if (exitToMenuButton != null && exitToMenuButton.isActiveAndEnabled && exitToMenuButton.interactable)
            return exitToMenuButton;

        return pauseMenuFirstSelected != null ? pauseMenuFirstSelected : continueButton;
    }

    private Button GetRestartConfirmReturnButton()
    {
        if (restartConfirmReturnButton != null && restartConfirmReturnButton.isActiveAndEnabled && restartConfirmReturnButton.interactable)
            return restartConfirmReturnButton;

        if (_selectedBeforeRestartConfirm != null && _selectedBeforeRestartConfirm.isActiveAndEnabled && _selectedBeforeRestartConfirm.interactable)
            return _selectedBeforeRestartConfirm;

        if (restartButton != null && restartButton.isActiveAndEnabled && restartButton.interactable)
            return restartButton;

        return pauseMenuFirstSelected != null ? pauseMenuFirstSelected : continueButton;
    }

    private void UpdateConfirmSelectionState()
    {
        bool exitActive = exitConfirmPanel != null && exitConfirmPanel.activeSelf;
        bool restartActive = restartConfirmPanel != null && restartConfirmPanel.activeSelf;

        bool exitJustOpened = exitActive && !_prevExitConfirmActive;
        bool exitJustClosed = !exitActive && _prevExitConfirmActive;
        bool restartJustOpened = restartActive && !_prevRestartConfirmActive;
        bool restartJustClosed = !restartActive && _prevRestartConfirmActive;

        _prevExitConfirmActive = exitActive;
        _prevRestartConfirmActive = restartActive;

        if (exitJustOpened)
        {
            SelectButtonDeferred(exitConfirmFirstSelected ? exitConfirmFirstSelected : exitNoButton);
            return;
        }

        if (exitJustClosed)
        {
            SelectButtonDeferred(GetExitConfirmReturnButton());
            return;
        }

        if (restartJustOpened)
        {
            SelectButtonDeferred(restartConfirmFirstSelected ? restartConfirmFirstSelected : restartNoButton);
            return;
        }

        if (restartJustClosed)
        {
            SelectButtonDeferred(GetRestartConfirmReturnButton());
        }
    }

    private void CacheModalStates()
    {
        _prevExitConfirmActive = exitConfirmPanel != null && exitConfirmPanel.activeSelf;
        _prevRestartConfirmActive = restartConfirmPanel != null && restartConfirmPanel.activeSelf;
    }

    private Button GetCurrentlySelectedButton()
    {
        if (EventSystem.current == null)
            return null;

        GameObject current = EventSystem.current.currentSelectedGameObject;
        if (current == null)
            return null;

        return current.GetComponent<Button>();
    }
}
