using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(10000)]
public class PauseMenuUI : MonoBehaviour
{
    [Header("Root panels")]
    [SerializeField] private GameObject pauseRoot;        // Dim + всё меню
    [SerializeField] private GameObject menuPanel;        // панель с кнопками
    [SerializeField] private GameObject settingsPanel;    // настройки
    [SerializeField] private GameObject controlsPanel;    // управление

    [Header("Confirm panels")]
    [SerializeField] private GameObject exitConfirmPanel;     // подтверждение выхода в меню
    [SerializeField] private GameObject restartConfirmPanel;  // подтверждение рестарта

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

    [Header("Back buttons (optional)")]
    [SerializeField] private Button settingsBackButton;   // кнопка "Назад" в настройках (если есть)
    [SerializeField] private Button controlsBackButton;   // кнопка "Назад" в управлении (если есть)

    [Header("Optional Pause Button (mobile/UI)")]
    [SerializeField] private Button openPauseButton;      // если есть кнопка паузы на экране

    [Header("Navigation (keyboard / gamepad)")]
    [SerializeField] private bool enableUiNavigation = true;

    [Tooltip("Кнопка 'Назад/Отмена' на геймпаде (обычно B / Circle). Часто это JoystickButton1.")]
    [SerializeField] private KeyCode gamepadBackKey = KeyCode.JoystickButton1;

    [Tooltip("Какая кнопка/элемент выделяется при открытии главного меню паузы. Если пусто — будет Continue.")]
    [SerializeField] private Button pauseMenuFirstSelected;

    [Tooltip("Какая кнопка/элемент выделяется при открытии Settings (например Audio tab button).")]
    [SerializeField] private Button settingsFirstSelected;

    [Tooltip("Какая кнопка/элемент выделяется при открытии Controls (например Back button).")]
    [SerializeField] private Button controlsFirstSelected;

    [Tooltip("Какая кнопка выделяется в подтверждении выхода (обычно No).")]
    [SerializeField] private Button exitConfirmFirstSelected;

    [Tooltip("Какая кнопка выделяется в подтверждении рестарта (обычно No).")]
    [SerializeField] private Button restartConfirmFirstSelected;

    [Tooltip("Если выделение потерялось (клик по пустому месту и т.п.), скрипт восстановит его автоматически.")]
    [SerializeField] private bool restoreSelectionIfLost = true;

    [Header("Pause input")]
    [SerializeField] private KeyCode keyboardPauseKey = KeyCode.Escape;

    [Tooltip("Обычно Start / Options / Menu на геймпаде. Часто это JoystickButton7.")]
    [SerializeField] private KeyCode gamepadPauseKey = KeyCode.JoystickButton7;

    [Tooltip("Дополнительная кнопка паузы (например Back/View), если нужна. Можно оставить None.")]
    [SerializeField] private KeyCode gamepadPauseAltKey = KeyCode.None;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Cursor")]
    [SerializeField] private bool showCursorWhenPaused = true;
    [SerializeField] private bool hideCursorWhenPlaying = true;
    [SerializeField] private CursorLockMode playLockMode = CursorLockMode.None;

    private bool _paused;
    private Coroutine _selectRoutine;

    public bool IsPaused => _paused;

    private void Awake()
    {
        WireButtons();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        ResumeGame(); // стартуем без паузы
    }

    private void Update()
    {
        // --- Если игра НЕ на паузе: открыть паузу только кнопкой паузы (Esc / Start) ---
        if (!_paused)
        {
            if (IsPausePressed())
                PauseGame();

            return;
        }

        // --- Если игра на паузе: Esc / Start / B работают как "назад/отмена" ---
        if (!IsPausePressed() && !IsBackPressed())
            return;

        // Закрываем верхние окна по приоритету
        if (exitConfirmPanel && exitConfirmPanel.activeSelf)
        {
            CancelExitToMainMenu();   // B = "Нет"
            return;
        }

        if (restartConfirmPanel && restartConfirmPanel.activeSelf)
        {
            CancelRestartLevel();     // B = "Нет"
            return;
        }

        if (settingsPanel && settingsPanel.activeSelf)
        {
            BackToPauseMenu();        // B = назад из настроек
            return;
        }

        if (controlsPanel && controlsPanel.activeSelf)
        {
            BackToPauseMenu();        // B = назад из управления
            return;
        }

        // Если мы в главном меню паузы — закрываем паузу
        ResumeGame();
    }

    private void LateUpdate()
    {
        // Держим курсор видимым каждый кадр, пока меню паузы открыто
        if (_paused && pauseRoot != null && pauseRoot.activeSelf)
        {
            ApplyPauseCursorState();

            if (enableUiNavigation && restoreSelectionIfLost)
                RestoreSelectionIfNeeded();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;

        if (_paused && pauseRoot != null && pauseRoot.activeSelf) ApplyPauseCursorState();
        else ApplyPlayCursorState();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) return;

        if (_paused && pauseRoot != null && pauseRoot.activeSelf) ApplyPauseCursorState();
        else ApplyPlayCursorState();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_paused && pauseRoot != null && pauseRoot.activeSelf)
            ApplyPauseCursorState();
        else
            ApplyPlayCursorState();
    }

    private bool IsPausePressed()
    {
        if (Input.GetKeyDown(keyboardPauseKey))
            return true;

        if (Input.GetKeyDown(gamepadPauseKey))
            return true;

        if (gamepadPauseAltKey != KeyCode.None && Input.GetKeyDown(gamepadPauseAltKey))
            return true;

        return false;
    }

    private void WireButtons()
    {
        // Главное меню
        Bind(continueButton, ResumeGame);
        Bind(restartButton, AskRestartLevel);
        Bind(settingsButton, OpenSettings);
        Bind(controlsButton, OpenControls);
        Bind(exitToMenuButton, AskExitToMainMenu);

        // Confirm: Exit
        Bind(exitYesButton, ConfirmExitToMainMenu);
        Bind(exitNoButton, CancelExitToMainMenu);

        // Confirm: Restart
        Bind(restartYesButton, ConfirmRestartLevel);
        Bind(restartNoButton, CancelRestartLevel);

        // Back (если кнопки есть)
        Bind(settingsBackButton, BackToPauseMenu);
        Bind(controlsBackButton, BackToPauseMenu);

        // UI кнопка паузы (если есть)
        Bind(openPauseButton, OpenPauseFromButton);
    }

    private static void Bind(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (!btn) return;
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }

    // ===== Cursor helpers =====

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

    // ===== UI Navigation helpers (keyboard / gamepad) =====

    private void RestoreSelectionIfNeeded()
    {
        if (EventSystem.current == null) return;

        if (EventSystem.current.currentSelectedGameObject != null) return;

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

        if (settingsPanel && settingsPanel.activeSelf)
        {
            SelectButtonDeferred(settingsFirstSelected);
            return;
        }

        if (controlsPanel && controlsPanel.activeSelf)
        {
            SelectButtonDeferred(controlsFirstSelected);
            return;
        }

        if (menuPanel && menuPanel.activeSelf)
        {
            SelectPauseMenuDefault();
        }
    }

    private void SelectPauseMenuDefault()
    {
        SelectButtonDeferred(pauseMenuFirstSelected ? pauseMenuFirstSelected : continueButton);
    }

    private void SelectButtonDeferred(Button btn)
    {
        if (!enableUiNavigation) return;
        if (!btn) return;
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

        Canvas.ForceUpdateCanvases();
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(btn.gameObject);

        _selectRoutine = null;
    }

    // ===== Public API =====

    public void OpenPauseFromButton()
    {
        if (_paused) return;
        PauseGame();
    }

    public void TogglePause()
    {
        if (_paused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        _paused = true;
        Time.timeScale = 0f;

        if (pauseRoot) pauseRoot.SetActive(true);

        ShowMainMenu();
        ApplyPauseCursorState();
        SelectPauseMenuDefault();
    }

    public void ResumeGame()
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

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        ApplyPlayCursorState();
    }

    // ===== Restart confirm =====

    public void AskRestartLevel()
    {
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
        SelectPauseMenuDefault();
    }

    // ===== Exit confirm =====

    public void AskExitToMainMenu()
    {
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
        SelectPauseMenuDefault();
    }

    // ===== Sub menus =====

    public void OpenSettings()
    {
        HideAllSubPanels();
        if (settingsPanel) settingsPanel.SetActive(true);

        SelectButtonDeferred(settingsFirstSelected);
    }

    public void OpenControls()
    {
        HideAllSubPanels();
        if (controlsPanel) controlsPanel.SetActive(true);

        SelectButtonDeferred(controlsFirstSelected);
    }

    public void BackToPauseMenu()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (controlsPanel) controlsPanel.SetActive(false);
        if (exitConfirmPanel) exitConfirmPanel.SetActive(false);
        if (restartConfirmPanel) restartConfirmPanel.SetActive(false);

        ShowMainMenu();
        SelectPauseMenuDefault();
    }

    // ===== Helpers =====

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

    private bool IsBackPressed()
    {
        return Input.GetKeyDown(gamepadBackKey);
    }
}