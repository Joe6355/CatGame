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

    [Header("UI selection defaults (optional)")]
    [SerializeField, Tooltip("Кнопка по умолчанию в корне Settings, когда ни одна вкладка ещё не открыта. Если пусто — возьмёт первую доступную вкладку.")]
    private Button settingsRootFirstSelected;

    [SerializeField, Tooltip("Какая кнопка считается основной для вкладки Audio. Если пусто — сама кнопка вкладки Audio.")]
    private Button audioFirstSelected;

    [SerializeField, Tooltip("Какая кнопка считается основной для вкладки Controls. Если пусто — сама кнопка вкладки Controls.")]
    private Button controlsFirstSelected;

    [SerializeField, Tooltip("Какая кнопка считается основной для вкладки Gameplay. Если пусто — сама кнопка вкладки Gameplay.")]
    private Button gameplayFirstSelected;

    [SerializeField, Tooltip("Какая кнопка считается основной для вкладки Video. Если пусто — сама кнопка вкладки Video.")]
    private Button videoFirstSelected;

    [SerializeField, Tooltip("Какая кнопка выделяется внутри Keyboard-подпанели. Если пусто — Keyboard button или Controls default.")]
    private Button keyboardFirstSelected;

    [SerializeField, Tooltip("Какая кнопка выделяется внутри Gamepad-подпанели. Если пусто — Gamepad button или Controls default.")]
    private Button gamepadFirstSelected;

    private GameObject _currentTab;
    private GameObject _currentControlsSubPanel;

    private void Awake()
    {
        if (audioBtn != null)
        {
            audioBtn.onClick.RemoveAllListeners();
            audioBtn.onClick.AddListener(OpenAudioTab);
        }

        if (controlsBtn != null)
        {
            controlsBtn.onClick.RemoveAllListeners();
            controlsBtn.onClick.AddListener(OnControlsTabClicked);
        }

        if (gameplayBtn != null)
        {
            gameplayBtn.onClick.RemoveAllListeners();
            gameplayBtn.onClick.AddListener(OpenGameplayTab);
        }

        if (videoBtn != null)
        {
            videoBtn.onClick.RemoveAllListeners();
            videoBtn.onClick.AddListener(OpenVideoTab);
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

    public void OpenAudioTab()
    {
        ShowTab(tabAudio);
    }

    private void OnControlsTabClicked()
    {
        ShowTab(tabControls);

        if (closeControlsSubPanelsOnControlsTabOpen)
            CloseControlsSubPanels();
    }

    public void OpenGameplayTab()
    {
        ShowTab(tabGameplay);
    }

    public void OpenVideoTab()
    {
        ShowTab(tabVideo);
    }

    private void ShowTab(GameObject active)
    {
        _currentTab = active;

        if (tabAudio != null) tabAudio.SetActive(active == tabAudio);
        if (tabControls != null) tabControls.SetActive(active == tabControls);
        if (tabGameplay != null) tabGameplay.SetActive(active == tabGameplay);
        if (tabVideo != null) tabVideo.SetActive(active == tabVideo);

        if (closeControlsSubPanelsWhenLeaveControlsTab && active != tabControls)
        {
            CloseControlsSubPanels();
        }
        else if (active != tabControls)
        {
            _currentControlsSubPanel = null;
        }

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
        _currentControlsSubPanel = activeSubPanel;

        if (keyboardPanel != null)
            keyboardPanel.SetActive(activeSubPanel == keyboardPanel);

        if (gamepadPanel != null)
            gamepadPanel.SetActive(activeSubPanel == gamepadPanel);
    }

    public void CloseControlsSubPanels()
    {
        _currentControlsSubPanel = null;

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
        _currentTab = null;
        _currentControlsSubPanel = null;

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
        if (tabGameplay != null && !tabGameplay.activeSelf) return;

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

    public Button GetPreferredSelectedButton()
    {
        Button preferred = GetPreferredSelectedButtonInternal();
        if (IsSelectable(preferred))
            return preferred;

        return GetPreferredRootButton();
    }

    public Button GetPreferredRootButton()
    {
        if (IsSelectable(settingsRootFirstSelected)) return settingsRootFirstSelected;
        if (IsSelectable(audioBtn)) return audioBtn;
        if (IsSelectable(controlsBtn)) return controlsBtn;
        if (IsSelectable(gameplayBtn)) return gameplayBtn;
        if (IsSelectable(videoBtn)) return videoBtn;
        return null;
    }

    private Button GetPreferredSelectedButtonInternal()
    {
        if (keyboardPanel != null && keyboardPanel.activeSelf)
        {
            if (IsSelectable(keyboardFirstSelected)) return keyboardFirstSelected;
            if (IsSelectable(keyboardBtn)) return keyboardBtn;
            if (IsSelectable(controlsFirstSelected)) return controlsFirstSelected;
            if (IsSelectable(controlsBtn)) return controlsBtn;
        }

        if (gamepadPanel != null && gamepadPanel.activeSelf)
        {
            if (IsSelectable(gamepadFirstSelected)) return gamepadFirstSelected;
            if (IsSelectable(gamepadBtn)) return gamepadBtn;
            if (IsSelectable(controlsFirstSelected)) return controlsFirstSelected;
            if (IsSelectable(controlsBtn)) return controlsBtn;
        }

        if (tabAudio != null && tabAudio.activeSelf)
        {
            if (IsSelectable(audioFirstSelected)) return audioFirstSelected;
            if (IsSelectable(audioBtn)) return audioBtn;
        }

        if (tabControls != null && tabControls.activeSelf)
        {
            if (IsSelectable(controlsFirstSelected)) return controlsFirstSelected;
            if (IsSelectable(controlsBtn)) return controlsBtn;
        }

        if (tabGameplay != null && tabGameplay.activeSelf)
        {
            if (IsSelectable(gameplayFirstSelected)) return gameplayFirstSelected;
            if (IsSelectable(gameplayBtn)) return gameplayBtn;
        }

        if (tabVideo != null && tabVideo.activeSelf)
        {
            if (IsSelectable(videoFirstSelected)) return videoFirstSelected;
            if (IsSelectable(videoBtn)) return videoBtn;
        }

        if (_currentControlsSubPanel == keyboardPanel)
        {
            if (IsSelectable(keyboardFirstSelected)) return keyboardFirstSelected;
            if (IsSelectable(keyboardBtn)) return keyboardBtn;
        }

        if (_currentControlsSubPanel == gamepadPanel)
        {
            if (IsSelectable(gamepadFirstSelected)) return gamepadFirstSelected;
            if (IsSelectable(gamepadBtn)) return gamepadBtn;
        }

        if (_currentTab == tabAudio)
        {
            if (IsSelectable(audioFirstSelected)) return audioFirstSelected;
            if (IsSelectable(audioBtn)) return audioBtn;
        }

        if (_currentTab == tabControls)
        {
            if (IsSelectable(controlsFirstSelected)) return controlsFirstSelected;
            if (IsSelectable(controlsBtn)) return controlsBtn;
        }

        if (_currentTab == tabGameplay)
        {
            if (IsSelectable(gameplayFirstSelected)) return gameplayFirstSelected;
            if (IsSelectable(gameplayBtn)) return gameplayBtn;
        }

        if (_currentTab == tabVideo)
        {
            if (IsSelectable(videoFirstSelected)) return videoFirstSelected;
            if (IsSelectable(videoBtn)) return videoBtn;
        }

        return null;
    }

    private static bool IsSelectable(Button button)
    {
        return button != null && button.isActiveAndEnabled && button.interactable;
    }
}
