using UnityEngine;
using UnityEngine.UI;

public class SettingsTabsSwitcher : MonoBehaviour
{
    [Header("Кнопки вкладок")]
    [SerializeField] private Button audioBtn;
    [SerializeField] private Button controlsBtn;
    [SerializeField] private Button gameplayBtn;
    [SerializeField] private Button videoBtn;

    [Header("Панели вкладок")]
    [SerializeField] private GameObject tabAudio;
    [SerializeField] private GameObject tabControls;
    [SerializeField] private GameObject tabGameplay;
    [SerializeField] private GameObject tabVideo;

    [Header("Controls: подпункты")]
    [SerializeField] private Button keyboardBtn;
    [SerializeField] private Button gamepadBtn;

    [SerializeField] private GameObject keyboardPanel;
    [SerializeField] private GameObject gamepadPanel;

    [SerializeField] private bool closeControlsSubPanelsOnControlsTabOpen = false;
    [SerializeField] private bool closeControlsSubPanelsWhenLeaveControlsTab = true;

    [Header("Gameplay Tooltip")]
    [SerializeField] private Button assistInfoBtn;
    [SerializeField] private GameObject assistTooltipPanel;
    [SerializeField] private bool hideTooltipOnTabChange = true;

    [Header("Стартовое поведение")]
    [SerializeField] private bool closeAllTabsOnEnable = true;

    private void Awake()
    {
        if (audioBtn != null)
        {
            audioBtn.onClick.RemoveAllListeners();
            audioBtn.onClick.AddListener(() => ShowTab(tabAudio));
        }

        if (controlsBtn != null)
        {
            controlsBtn.onClick.RemoveAllListeners();
            controlsBtn.onClick.AddListener(OnControlsTabClicked);
        }

        if (gameplayBtn != null)
        {
            gameplayBtn.onClick.RemoveAllListeners();
            gameplayBtn.onClick.AddListener(() => ShowTab(tabGameplay));
        }

        if (videoBtn != null)
        {
            videoBtn.onClick.RemoveAllListeners();
            videoBtn.onClick.AddListener(() => ShowTab(tabVideo));
        }

        if (keyboardBtn != null)
        {
            keyboardBtn.onClick.RemoveAllListeners();
            keyboardBtn.onClick.AddListener(OpenKeyboardPanel);
        }

        if (gamepadBtn != null)
        {
            gamepadBtn.onClick.RemoveAllListeners();
            gamepadBtn.onClick.AddListener(OpenGamepadPanel);
        }

        if (assistInfoBtn != null)
        {
            assistInfoBtn.onClick.RemoveAllListeners();
            assistInfoBtn.onClick.AddListener(ToggleAssistTooltip);
        }

        if (assistTooltipPanel != null)
            assistTooltipPanel.SetActive(false);

        CloseAllTabs();
    }

    private void OnEnable()
    {
        if (closeAllTabsOnEnable)
            CloseAllTabs();
        else
            CloseControlsSubPanels();

        if (assistTooltipPanel != null)
            assistTooltipPanel.SetActive(false);
    }

    private void OnControlsTabClicked()
    {
        ShowTab(tabControls);

        if (closeControlsSubPanelsOnControlsTabOpen)
            CloseControlsSubPanels();
    }

    private void ShowTab(GameObject active)
    {
        if (tabAudio != null) tabAudio.SetActive(active == tabAudio);
        if (tabControls != null) tabControls.SetActive(active == tabControls);
        if (tabGameplay != null) tabGameplay.SetActive(active == tabGameplay);
        if (tabVideo != null) tabVideo.SetActive(active == tabVideo);

        if (closeControlsSubPanelsWhenLeaveControlsTab && active != tabControls)
            CloseControlsSubPanels();

        if (hideTooltipOnTabChange && assistTooltipPanel != null)
            assistTooltipPanel.SetActive(false);
    }

    public void OpenKeyboardPanel()
    {
        if (tabControls != null && !tabControls.activeSelf)
            ShowTab(tabControls);

        ShowControlsSubPanel(keyboardPanel);
    }

    public void OpenGamepadPanel()
    {
        if (tabControls != null && !tabControls.activeSelf)
            ShowTab(tabControls);

        ShowControlsSubPanel(gamepadPanel);
    }

    private void ShowControlsSubPanel(GameObject activeSubPanel)
    {
        if (keyboardPanel != null)
            keyboardPanel.SetActive(activeSubPanel == keyboardPanel);

        if (gamepadPanel != null)
            gamepadPanel.SetActive(activeSubPanel == gamepadPanel);
    }

    public void CloseControlsSubPanels()
    {
        if (keyboardPanel != null) keyboardPanel.SetActive(false);
        if (gamepadPanel != null) gamepadPanel.SetActive(false);
    }

    public bool HasAnyOpenView()
    {
        return HasAnyOpenTab() || IsAnyControlsSubPanelOpen();
    }

    public bool HasAnyOpenTab()
    {
        return (tabAudio != null && tabAudio.activeSelf) ||
               (tabControls != null && tabControls.activeSelf) ||
               (tabGameplay != null && tabGameplay.activeSelf) ||
               (tabVideo != null && tabVideo.activeSelf);
    }

    public bool IsAnyControlsSubPanelOpen()
    {
        return (keyboardPanel != null && keyboardPanel.activeSelf) ||
               (gamepadPanel != null && gamepadPanel.activeSelf);
    }

    public void CloseToSettingsRoot()
    {
        CloseAllTabs();
    }

    public void CloseAllTabs()
    {
        if (tabAudio != null) tabAudio.SetActive(false);
        if (tabControls != null) tabControls.SetActive(false);
        if (tabGameplay != null) tabGameplay.SetActive(false);
        if (tabVideo != null) tabVideo.SetActive(false);

        CloseControlsSubPanels();

        if (assistTooltipPanel != null)
            assistTooltipPanel.SetActive(false);
    }

    public void ToggleAssistTooltip()
    {
        if (assistTooltipPanel == null) return;

        if (tabGameplay != null && !tabGameplay.activeSelf)
            return;

        assistTooltipPanel.SetActive(!assistTooltipPanel.activeSelf);
    }

    public void OpenControlsKeyboard()
    {
        ShowTab(tabControls);
        OpenKeyboardPanel();
    }

    public void OpenControlsGamepad()
    {
        ShowTab(tabControls);
        OpenGamepadPanel();
    }
}