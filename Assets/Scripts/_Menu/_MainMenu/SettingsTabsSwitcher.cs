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

    [Header("Controls: подпункты (ребинд)")]
    [SerializeField] private Button keyboardBtn;      // Кнопка "Клавиатура"
    [SerializeField] private Button gamepadBtn;       // Кнопка "Геймпад"

    [SerializeField] private GameObject keyboardPanel; // Панель ребинда клавиатуры
    [SerializeField] private GameObject gamepadPanel;  // Панель ребинда геймпада

    [SerializeField] private bool closeControlsSubPanelsOnControlsTabOpen = false;
    [SerializeField] private bool closeControlsSubPanelsWhenLeaveControlsTab = true;

    [Header("Gameplay Tooltip (Упрощение)")]
    [SerializeField] private Button assistInfoBtn;          // кнопка "?"
    [SerializeField] private GameObject assistTooltipPanel; // панель подсказки
    [SerializeField] private bool hideTooltipOnTabChange = true;

    [Header("Стартовое поведение")]
    [SerializeField] private bool closeAllTabsOnEnable = true; // при открытии Settings_Panel всё закрывать

    private void Awake()
    {
        // Подписка на кнопки вкладок
        if (audioBtn != null) audioBtn.onClick.AddListener(() => ShowTab(tabAudio));
        if (controlsBtn != null) controlsBtn.onClick.AddListener(OnControlsTabClicked);
        if (gameplayBtn != null) gameplayBtn.onClick.AddListener(() => ShowTab(tabGameplay));
        if (videoBtn != null) videoBtn.onClick.AddListener(() => ShowTab(tabVideo));

        // Подписка на кнопки подпунктов Controls
        if (keyboardBtn != null) keyboardBtn.onClick.AddListener(OpenKeyboardPanel);
        if (gamepadBtn != null) gamepadBtn.onClick.AddListener(OpenGamepadPanel);

        // Tooltip
        if (assistTooltipPanel != null)
            assistTooltipPanel.SetActive(false);

        if (assistInfoBtn != null)
            assistInfoBtn.onClick.AddListener(ToggleAssistTooltip);

        // Ничего не открываем по умолчанию
        CloseAllTabs();
        CloseControlsSubPanels();
    }

    private void OnEnable()
    {
        // Если этот скрипт висит на Settings_Panel (который выключен в начале),
        // то при открытии настроек вкладки будут закрываться каждый раз.
        if (closeAllTabsOnEnable)
            CloseAllTabs();

        // Чтобы не оставались "залипшие" подпанели после повторного открытия настроек
        CloseControlsSubPanels();

        if (assistTooltipPanel != null)
            assistTooltipPanel.SetActive(false);
    }

    /// <summary>
    /// Отдельный обработчик для кнопки Controls, чтобы при необходимости
    /// можно было закрывать подпанели при входе во вкладку Controls.
    /// </summary>
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

        // Если ушли с вкладки Controls — закрываем подпанели (по желанию)
        if (closeControlsSubPanelsWhenLeaveControlsTab && active != tabControls)
            CloseControlsSubPanels();

        if (hideTooltipOnTabChange && assistTooltipPanel != null)
            assistTooltipPanel.SetActive(false);
    }

    // ======== Controls: подпанели (клавиатура / геймпад) ========

    public void OpenKeyboardPanel()
    {
        // Если вкладка Controls закрыта — откроем её автоматически
        if (tabControls != null && !tabControls.activeSelf)
            ShowTab(tabControls);

        ShowControlsSubPanel(keyboardPanel);
    }

    public void OpenGamepadPanel()
    {
        // Если вкладка Controls закрыта — откроем её автоматически
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

    // ======== Общие методы ========

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

        // Подсказка показывается только на вкладке Gameplay
        if (tabGameplay != null && !tabGameplay.activeSelf)
            return;

        assistTooltipPanel.SetActive(!assistTooltipPanel.activeSelf);
    }

    // (Необязательно) Вызов из других скриптов/кнопок:
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