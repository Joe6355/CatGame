using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool _paused;

    private void Awake()
    {
        WireButtons();
    }

    private void Start()
    {
        ResumeGame(); // стартуем без паузы
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (!_paused)
        {
            PauseGame();
            return;
        }

        // ESC закрывает верхние окна по приоритету
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

        if (settingsPanel && settingsPanel.activeSelf)
        {
            BackToPauseMenu();
            return;
        }

        if (controlsPanel && controlsPanel.activeSelf)
        {
            BackToPauseMenu();
            return;
        }

        ResumeGame();
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
        btn.onClick.RemoveListener(action); // чтобы не накапливалось при домене/повторах
        btn.onClick.AddListener(action);
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

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ResumeGame()
    {
        _paused = false;
        Time.timeScale = 1f;

        HideAllSubPanels();
        if (pauseRoot) pauseRoot.SetActive(false);

        Cursor.visible = false; // если курсор в игре не нужен
        Cursor.lockState = CursorLockMode.None;
    }

    // ===== Restart confirm =====

    public void AskRestartLevel()
    {
        HideAllSubPanels();
        if (restartConfirmPanel) restartConfirmPanel.SetActive(true);
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
    }

    // ===== Exit confirm =====

    public void AskExitToMainMenu()
    {
        HideAllSubPanels();
        if (exitConfirmPanel) exitConfirmPanel.SetActive(true);
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
    }

    // ===== Sub menus =====

    public void OpenSettings()
    {
        HideAllSubPanels();
        if (settingsPanel) settingsPanel.SetActive(true);
    }

    public void OpenControls()
    {
        HideAllSubPanels();
        if (controlsPanel) controlsPanel.SetActive(true);
    }

    public void BackToPauseMenu()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (controlsPanel) controlsPanel.SetActive(false);
        if (exitConfirmPanel) exitConfirmPanel.SetActive(false);
        if (restartConfirmPanel) restartConfirmPanel.SetActive(false);

        ShowMainMenu();
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
}
