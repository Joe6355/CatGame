using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class SettingsTabsSwitcher : MonoBehaviour
{
    private enum SettingsTabId
    {
        Audio,
        Controls,
        Gameplay,
        Video
    }

    private enum SettingsNavButtonId
    {
        None,
        Audio,
        Controls,
        Gameplay,
        Video,
        Keyboard,
        Gamepad
    }

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

    [Header("Подсветка текущей вкладки")]
    [SerializeField, Tooltip("Если ВКЛ — активная вкладка держит визуал Selected/Highlighted даже когда фокус ушёл на Toggle/Slider/Dropdown.")]
    private bool keepActiveSettingsButtonHighlighted = true;

    [SerializeField, Tooltip("Если ВКЛ — активная кнопка берёт Selected Color из Button. Если ВЫКЛ — Highlighted Color.")]
    private bool useSelectedColorForActiveSettingsButton = true;

    [SerializeField, Tooltip("Если ВКЛ — у активной кнопки Normal/Highlighted/Selected временно становятся одним активным цветом, чтобы подсветка не сбрасывалась.")]
    private bool forceActiveColorForAllButtonStates = true;

    [Header("Gameplay Tooltip Universal")]
    [SerializeField, Tooltip("Одна общая панель подсказки. Твой Panel_AssistTooltip.")]
    private GameObject assistTooltipPanel;

    [SerializeField, Tooltip("TMP-текст внутри Panel_AssistTooltip. Можно оставить пустым — скрипт найдёт сам.")]
    private TMP_Text assistTooltipText;

    [SerializeField, Tooltip("Если ВКЛ — повторное нажатие на ту же кнопку ? закрывает подсказку.")]
    private bool toggleTooltipOnSameButtonClick = true;

    [SerializeField, Tooltip("Если ВКЛ — подсказка скрывается при смене вкладки.")]
    private bool hideTooltipOnTabChange = true;

    [Header("Tooltip Element 0")]
    [SerializeField, Tooltip("Кнопка ? для первой подсказки.")]
    private Button tooltipButton0;

    [SerializeField, TextArea(2, 8), Tooltip("Текст первой подсказки.")]
    private string tooltipMessage0 =
        "Показывает предполагаемую траекторию прыжка персонажа. Помогает заранее увидеть направление и дальность прыжка.";

    [Header("Tooltip Element 1")]
    [SerializeField, Tooltip("Кнопка ? для второй подсказки.")]
    private Button tooltipButton1;

    [SerializeField, TextArea(2, 8), Tooltip("Текст второй подсказки.")]
    private string tooltipMessage1 =
        "Включает визуальный эффект старого экрана: зерно, хроматические искажения, виньетку, свечение и лёгкое искривление изображения.";

    [Header("Tooltip Element 2")]
    [SerializeField, Tooltip("Кнопка ? для третьей подсказки.")]
    private Button tooltipButton2;

    [SerializeField, TextArea(2, 8), Tooltip("Текст третьей подсказки.")]
    private string tooltipMessage2 =
        "Включает или выключает вибрацию геймпада при жёстком приземлении. Чем сильнее падение, тем заметнее отклик геймпада.";

    [Header("Gameplay: траектория прыжка")]
    [SerializeField, Tooltip("Toggle из Gameplay-вкладки.")]
    private Toggle jumpTrajectoryToggle;

    [SerializeField, Tooltip("Обычно оставь None. Используется только если Background сделан отдельной Button-кнопкой.")]
    private Button jumpTrajectoryBackgroundButton;

    [SerializeField, Tooltip("Ссылка на JumpTrajectory2D на игроке. Можно оставить пустым, если включён Auto Find.")]
    private JumpTrajectory2D jumpTrajectory;

    [SerializeField, Tooltip("Если ВКЛ — SettingsTabsSwitcher сам попробует найти JumpTrajectory2D в сцене.")]
    private bool autoFindJumpTrajectoryInScene = true;

    [SerializeField, Tooltip("Если ВКЛ — состояние галочки сохраняется в PlayerPrefs.")]
    private bool saveJumpTrajectorySetting = true;

    [SerializeField, Tooltip("Ключ сохранения. Такой же ключ должен быть в JumpTrajectory2D.")]
    private string jumpTrajectoryPrefsKey = "Settings.ShowJumpTrajectory";

    [SerializeField, Tooltip("Значение по умолчанию, если сохранения ещё нет.")]
    private bool defaultJumpTrajectoryVisible = true;

    [SerializeField, Tooltip("Если ВКЛ — пишет в Console, когда настройка меняется.")]
    private bool debugJumpTrajectoryToggle = false;

    [Header("Gameplay: VHS / CRT / Post Processing")]
    [SerializeField, Tooltip("Toggle, который включает/выключает VHS/CRT/PostFX эффект.")]
    private Toggle postFxToggle;

    [SerializeField, Tooltip("Обычно оставь None. Используется только если Background сделан отдельной Button-кнопкой.")]
    private Button postFxBackgroundButton;

    [SerializeField, Tooltip("Global Volume из сцены. Лучше перетащить вручную.")]
    private Volume postFxVolume;

    [SerializeField, Tooltip("Если ВКЛ — скрипт сам попробует найти Global Volume в сцене.")]
    private bool autoFindPostFxInScene = true;

    [SerializeField, Tooltip("Если ВКЛ — эффект выключается через Volume.weight = 0. Если ВЫКЛ — выключается сам компонент Volume.")]
    private bool controlPostFxByWeight = true;

    [SerializeField, Range(0f, 1f), Tooltip("Какой Weight ставить, когда эффект включён.")]
    private float postFxEnabledWeight = 1f;

    [SerializeField, Tooltip("Если ВКЛ — состояние сохраняется в PlayerPrefs.")]
    private bool savePostFxSetting = true;

    [SerializeField, Tooltip("Ключ сохранения VHS/PostFX.")]
    private string postFxPrefsKey = "Settings.CRTPostFx";

    [SerializeField, Tooltip("Значение по умолчанию, если сохранения ещё нет.")]
    private bool defaultPostFxVisible = true;

    [SerializeField, Tooltip("Если ВКЛ — пишет в Console, когда настройка меняется.")]
    private bool debugPostFxToggle = false;

    [Header("Gameplay: вибрация геймпада")]
    [SerializeField, Tooltip("Toggle, который включает/выключает вибрацию геймпада при жёстком приземлении.")]
    private Toggle gamepadRumbleToggle;

    [SerializeField, Tooltip("Обычно оставь None. Используется только если Background сделан отдельной Button-кнопкой.")]
    private Button gamepadRumbleBackgroundButton;

    [SerializeField, Tooltip("PlayerController из игровой сцены. Можно оставить пустым, если включён Auto Find.")]
    private PlayerController playerController;

    [SerializeField, Tooltip("Если ВКЛ — скрипт сам попробует найти PlayerController в сцене.")]
    private bool autoFindPlayerControllerInScene = true;

    [SerializeField, Tooltip("Если ВКЛ — состояние сохраняется в PlayerPrefs.")]
    private bool saveGamepadRumbleSetting = true;

    [SerializeField, Tooltip("Ключ сохранения вибрации. Такой же ключ должен быть в PlayerController.")]
    private string gamepadRumblePrefsKey = "Settings.GamepadRumble";

    [SerializeField, Tooltip("Значение по умолчанию, если сохранения ещё нет.")]
    private bool defaultGamepadRumbleEnabled = true;

    [SerializeField, Tooltip("Если ВКЛ — пишет в Console, когда настройка меняется.")]
    private bool debugGamepadRumbleToggle = false;

    [Header("Стартовое поведение")]
    [SerializeField] private bool closeAllTabsOnEnable = true;

    [Header("Запоминание последней вкладки")]
    [SerializeField, Tooltip("Если ВКЛ — при открытии настроек будет открываться последняя вкладка, на которой игрок вышел.")]
    private bool reopenLastTabOnEnable = true;

    [SerializeField, Tooltip("Какая вкладка откроется в первый раз, если игрок ещё никуда не заходил.")]
    private SettingsTabId defaultTabOnFirstOpen = SettingsTabId.Audio;

    [SerializeField, Tooltip("Если ВКЛ — после восстановления вкладки выделяется её верхняя кнопка.")]
    private bool selectRestoredTabButton = true;

    [Header("UI selection defaults")]
    [SerializeField] private Button settingsRootFirstSelected;
    [SerializeField] private Button audioFirstSelected;
    [SerializeField] private Button controlsFirstSelected;
    [SerializeField] private Button gameplayFirstSelected;
    [SerializeField] private Button videoFirstSelected;
    [SerializeField] private Button keyboardFirstSelected;
    [SerializeField] private Button gamepadFirstSelected;

    private static bool s_hasRememberedTab;
    private static SettingsTabId s_rememberedTab = SettingsTabId.Audio;

    private GameObject _currentTab;
    private GameObject _currentControlsSubPanel;

    private bool _ignoreJumpTrajectoryToggleCallback;
    private bool _ignorePostFxToggleCallback;
    private bool _ignoreGamepadRumbleToggleCallback;

    private Button _currentTooltipButton;

    private readonly Dictionary<Button, ColorBlock> _originalSettingsButtonColors = new Dictionary<Button, ColorBlock>();
    private SettingsNavButtonId _currentHighlightedButtonId = SettingsNavButtonId.None;

    private void Awake()
    {
        CacheOriginalSettingsButtonColors();

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

        SetupTooltipButtons();
        SetupJumpTrajectoryToggle();
        SetupPostFxToggle();
        SetupGamepadRumbleToggle();

        HideSharedTooltip();
        CloseAllTabs();
    }

    private void OnEnable()
    {
        CacheOriginalSettingsButtonColors();

        if (reopenLastTabOnEnable)
        {
            RestoreRememberedTab();
        }
        else
        {
            if (closeAllTabsOnEnable)
                CloseAllTabs();
            else
                CloseControlsSubPanels();
        }

        HideSharedTooltip();

        SyncJumpTrajectoryToggleWithSavedValue();
        SyncPostFxToggleWithSavedValue();
        SyncGamepadRumbleToggleWithSavedValue();

        RefreshActiveSettingsButtonHighlight();
    }

    private void OnDisable()
    {
        RestoreAllSettingsButtonColors();
        _currentHighlightedButtonId = SettingsNavButtonId.None;
    }

    public void OpenAudioTab()
    {
        RememberTab(SettingsTabId.Audio);
        ShowTab(tabAudio);
    }

    private void OnControlsTabClicked()
    {
        RememberTab(SettingsTabId.Controls);
        ShowTab(tabControls);

        if (closeControlsSubPanelsOnControlsTabOpen)
            CloseControlsSubPanels();
    }

    public void OpenGameplayTab()
    {
        RememberTab(SettingsTabId.Gameplay);
        ShowTab(tabGameplay);
    }

    public void OpenVideoTab()
    {
        RememberTab(SettingsTabId.Video);
        ShowTab(tabVideo);
    }

    private void RememberTab(SettingsTabId tabId)
    {
        s_rememberedTab = tabId;
        s_hasRememberedTab = true;
    }

    private void RestoreRememberedTab()
    {
        SettingsTabId tabToOpen = s_hasRememberedTab
            ? s_rememberedTab
            : defaultTabOnFirstOpen;

        switch (tabToOpen)
        {
            case SettingsTabId.Audio:
                ShowTab(tabAudio);
                SelectButton(audioBtn);
                break;

            case SettingsTabId.Controls:
                ShowTab(tabControls);

                if (closeControlsSubPanelsOnControlsTabOpen)
                    CloseControlsSubPanels();

                SelectButton(controlsBtn);
                break;

            case SettingsTabId.Gameplay:
                ShowTab(tabGameplay);
                SelectButton(gameplayBtn);
                break;

            case SettingsTabId.Video:
                ShowTab(tabVideo);
                SelectButton(videoBtn);
                break;
        }
    }

    private void SelectButton(Button button)
    {
        if (!selectRestoredTabButton)
            return;

        if (button == null)
            return;

        if (!button.gameObject.activeInHierarchy)
            return;

        if (!button.interactable)
            return;

        if (EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(button.gameObject);
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

        if (hideTooltipOnTabChange)
            HideSharedTooltip();

        RefreshActiveSettingsButtonHighlight();
    }

    public void OpenKeyboardPanel()
    {
        RememberTab(SettingsTabId.Controls);

        if (tabControls != null && !tabControls.activeSelf)
            ShowTab(tabControls);

        ShowControlsSubPanel(keyboardPanel);
    }

    public void OpenGamepadPanel()
    {
        RememberTab(SettingsTabId.Controls);

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

        RefreshActiveSettingsButtonHighlight();
    }

    public void CloseControlsSubPanels()
    {
        _currentControlsSubPanel = null;

        if (keyboardPanel != null) keyboardPanel.SetActive(false);
        if (gamepadPanel != null) gamepadPanel.SetActive(false);

        RefreshActiveSettingsButtonHighlight();
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
        HideSharedTooltip();
        RefreshActiveSettingsButtonHighlight();
    }

    public void OpenControlsKeyboard()
    {
        RememberTab(SettingsTabId.Controls);
        ShowTab(tabControls);
        OpenKeyboardPanel();
    }

    public void OpenControlsGamepad()
    {
        RememberTab(SettingsTabId.Controls);
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

    // =========================================================
    // Active Settings Button Highlight
    // =========================================================

    private void CacheOriginalSettingsButtonColors()
    {
        CacheOriginalButtonColors(audioBtn);
        CacheOriginalButtonColors(controlsBtn);
        CacheOriginalButtonColors(gameplayBtn);
        CacheOriginalButtonColors(videoBtn);
        CacheOriginalButtonColors(keyboardBtn);
        CacheOriginalButtonColors(gamepadBtn);
    }

    private void CacheOriginalButtonColors(Button button)
    {
        if (button == null)
            return;

        if (_originalSettingsButtonColors.ContainsKey(button))
            return;

        _originalSettingsButtonColors.Add(button, button.colors);
    }

    private void RefreshActiveSettingsButtonHighlight()
    {
        CacheOriginalSettingsButtonColors();

        if (!keepActiveSettingsButtonHighlighted)
        {
            RestoreAllSettingsButtonColors();
            _currentHighlightedButtonId = SettingsNavButtonId.None;
            return;
        }

        SettingsNavButtonId targetId = GetCurrentSettingsNavButtonId();

        RestoreAllSettingsButtonColors();

        Button targetButton = GetSettingsNavButton(targetId);

        if (targetButton != null)
            ApplyActiveSettingsButtonColor(targetButton);

        _currentHighlightedButtonId = targetId;
    }

    private SettingsNavButtonId GetCurrentSettingsNavButtonId()
    {
        bool audioOpen = tabAudio != null && tabAudio.activeSelf;
        bool controlsOpen = tabControls != null && tabControls.activeSelf;
        bool gameplayOpen = tabGameplay != null && tabGameplay.activeSelf;
        bool videoOpen = tabVideo != null && tabVideo.activeSelf;

        if (controlsOpen)
        {
            if (keyboardPanel != null && keyboardPanel.activeSelf)
                return SettingsNavButtonId.Keyboard;

            if (gamepadPanel != null && gamepadPanel.activeSelf)
                return SettingsNavButtonId.Gamepad;

            return SettingsNavButtonId.Controls;
        }

        if (audioOpen)
            return SettingsNavButtonId.Audio;

        if (gameplayOpen)
            return SettingsNavButtonId.Gameplay;

        if (videoOpen)
            return SettingsNavButtonId.Video;

        return SettingsNavButtonId.None;
    }

    private Button GetSettingsNavButton(SettingsNavButtonId buttonId)
    {
        switch (buttonId)
        {
            case SettingsNavButtonId.Audio:
                return audioBtn;

            case SettingsNavButtonId.Controls:
                return controlsBtn;

            case SettingsNavButtonId.Gameplay:
                return gameplayBtn;

            case SettingsNavButtonId.Video:
                return videoBtn;

            case SettingsNavButtonId.Keyboard:
                return keyboardBtn;

            case SettingsNavButtonId.Gamepad:
                return gamepadBtn;

            default:
                return null;
        }
    }

    private void ApplyActiveSettingsButtonColor(Button button)
    {
        if (button == null)
            return;

        ColorBlock originalColors;

        if (!_originalSettingsButtonColors.TryGetValue(button, out originalColors))
        {
            originalColors = button.colors;
            _originalSettingsButtonColors[button] = originalColors;
        }

        Color activeColor = useSelectedColorForActiveSettingsButton
            ? originalColors.selectedColor
            : originalColors.highlightedColor;

        if (activeColor.a <= 0.001f)
            activeColor = originalColors.highlightedColor;

        ColorBlock activeColors = originalColors;
        activeColors.normalColor = activeColor;

        if (forceActiveColorForAllButtonStates)
        {
            activeColors.highlightedColor = activeColor;
            activeColors.selectedColor = activeColor;
        }

        button.colors = activeColors;
    }

    private void RestoreAllSettingsButtonColors()
    {
        RestoreButtonColors(audioBtn);
        RestoreButtonColors(controlsBtn);
        RestoreButtonColors(gameplayBtn);
        RestoreButtonColors(videoBtn);
        RestoreButtonColors(keyboardBtn);
        RestoreButtonColors(gamepadBtn);
    }

    private void RestoreButtonColors(Button button)
    {
        if (button == null)
            return;

        ColorBlock originalColors;

        if (_originalSettingsButtonColors.TryGetValue(button, out originalColors))
            button.colors = originalColors;
    }

    // =========================================================
    // Universal Tooltip
    // =========================================================

    private void SetupTooltipButtons()
    {
        SetupTooltipButton(tooltipButton0, tooltipMessage0);
        SetupTooltipButton(tooltipButton1, tooltipMessage1);
        SetupTooltipButton(tooltipButton2, tooltipMessage2);

        ResolveTooltipText();
    }

    private void SetupTooltipButton(Button button, string message)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();

        Button capturedButton = button;
        string capturedMessage = message;

        button.onClick.AddListener(() =>
        {
            ToggleSharedTooltip(capturedButton, capturedMessage);
        });
    }

    private void ResolveTooltipText()
    {
        if (assistTooltipText != null)
            return;

        if (assistTooltipPanel == null)
            return;

        assistTooltipText = assistTooltipPanel.GetComponentInChildren<TMP_Text>(true);
    }

    private void ToggleSharedTooltip(Button sourceButton, string message)
    {
        if (assistTooltipPanel == null)
            return;

        if (tabGameplay != null && !tabGameplay.activeSelf)
            return;

        ResolveTooltipText();

        bool sameButton = _currentTooltipButton == sourceButton;
        bool panelIsOpen = assistTooltipPanel.activeSelf;

        if (toggleTooltipOnSameButtonClick && sameButton && panelIsOpen)
        {
            HideSharedTooltip();
            return;
        }

        _currentTooltipButton = sourceButton;

        if (assistTooltipText != null)
            assistTooltipText.text = message;

        assistTooltipPanel.SetActive(true);
    }

    public void HideSharedTooltip()
    {
        _currentTooltipButton = null;

        if (assistTooltipPanel != null)
            assistTooltipPanel.SetActive(false);
    }

    public void ToggleAssistTooltip()
    {
        if (assistTooltipPanel == null)
            return;

        if (tabGameplay != null && !tabGameplay.activeSelf)
            return;

        if (assistTooltipPanel.activeSelf)
        {
            HideSharedTooltip();
            return;
        }

        ResolveTooltipText();

        if (assistTooltipText != null && string.IsNullOrWhiteSpace(assistTooltipText.text))
            assistTooltipText.text = "Подсказка для выбранной настройки.";

        assistTooltipPanel.SetActive(true);
    }

    // =========================================================
    // Jump Trajectory
    // =========================================================

    private void SetupJumpTrajectoryToggle()
    {
        ResolveJumpTrajectoryReference();

        if (jumpTrajectoryToggle != null)
        {
            jumpTrajectoryToggle.onValueChanged.RemoveListener(OnJumpTrajectoryToggleChanged);
            jumpTrajectoryToggle.onValueChanged.AddListener(OnJumpTrajectoryToggleChanged);
        }

        if (jumpTrajectoryBackgroundButton != null)
        {
            jumpTrajectoryBackgroundButton.onClick.RemoveListener(ToggleJumpTrajectoryFromBackgroundButton);
            jumpTrajectoryBackgroundButton.onClick.AddListener(ToggleJumpTrajectoryFromBackgroundButton);
        }

        SyncJumpTrajectoryToggleWithSavedValue();
    }

    private void SyncJumpTrajectoryToggleWithSavedValue()
    {
        bool visible = ReadSavedJumpTrajectoryVisible();
        ApplyJumpTrajectoryVisible(visible, false, false);
    }

    private void OnJumpTrajectoryToggleChanged(bool isOn)
    {
        if (_ignoreJumpTrajectoryToggleCallback)
            return;

        ApplyJumpTrajectoryVisible(isOn, true, true);
    }

    private void ToggleJumpTrajectoryFromBackgroundButton()
    {
        if (jumpTrajectoryToggle != null)
        {
            jumpTrajectoryToggle.isOn = !jumpTrajectoryToggle.isOn;
            return;
        }

        bool current = ReadSavedJumpTrajectoryVisible();
        ApplyJumpTrajectoryVisible(!current, true, true);
    }

    public void SetJumpTrajectoryVisibleFromUI(bool visible)
    {
        ApplyJumpTrajectoryVisible(visible, true, true);
    }

    public void ToggleJumpTrajectoryVisibleFromUI()
    {
        bool current = ReadSavedJumpTrajectoryVisible();
        ApplyJumpTrajectoryVisible(!current, true, true);
    }

    private void ApplyJumpTrajectoryVisible(bool visible, bool save, bool log)
    {
        ResolveJumpTrajectoryReference();

        if (jumpTrajectory != null)
            jumpTrajectory.SetTrajectoryEnabled(visible);

        if (save && saveJumpTrajectorySetting && !string.IsNullOrEmpty(jumpTrajectoryPrefsKey))
        {
            PlayerPrefs.SetInt(jumpTrajectoryPrefsKey, visible ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (jumpTrajectoryToggle != null && jumpTrajectoryToggle.isOn != visible)
        {
            _ignoreJumpTrajectoryToggleCallback = true;
            jumpTrajectoryToggle.SetIsOnWithoutNotify(visible);
            _ignoreJumpTrajectoryToggleCallback = false;
        }

        if (debugJumpTrajectoryToggle && log)
            Debug.Log("[SettingsTabsSwitcher] Jump trajectory visible = " + visible);
    }

    private bool ReadSavedJumpTrajectoryVisible()
    {
        if (saveJumpTrajectorySetting &&
            !string.IsNullOrEmpty(jumpTrajectoryPrefsKey) &&
            PlayerPrefs.HasKey(jumpTrajectoryPrefsKey))
        {
            return PlayerPrefs.GetInt(jumpTrajectoryPrefsKey, defaultJumpTrajectoryVisible ? 1 : 0) != 0;
        }

        return defaultJumpTrajectoryVisible;
    }

    private void ResolveJumpTrajectoryReference()
    {
        if (jumpTrajectory != null)
            return;

        if (!autoFindJumpTrajectoryInScene)
            return;

        JumpTrajectory2D[] found = Resources.FindObjectsOfTypeAll<JumpTrajectory2D>();

        for (int i = 0; i < found.Length; i++)
        {
            JumpTrajectory2D candidate = found[i];

            if (candidate == null)
                continue;

            if (!candidate.gameObject.scene.IsValid())
                continue;

            jumpTrajectory = candidate;
            return;
        }
    }

    // =========================================================
    // VHS / CRT / PostFX
    // =========================================================

    private void SetupPostFxToggle()
    {
        ResolvePostFxReference();

        if (postFxToggle != null)
        {
            postFxToggle.onValueChanged.RemoveListener(OnPostFxToggleChanged);
            postFxToggle.onValueChanged.AddListener(OnPostFxToggleChanged);
        }

        if (postFxBackgroundButton != null)
        {
            postFxBackgroundButton.onClick.RemoveListener(TogglePostFxFromBackgroundButton);
            postFxBackgroundButton.onClick.AddListener(TogglePostFxFromBackgroundButton);
        }

        SyncPostFxToggleWithSavedValue();
    }

    private void SyncPostFxToggleWithSavedValue()
    {
        bool visible = ReadSavedPostFxVisible();
        ApplyPostFxVisible(visible, false, false);
    }

    private void OnPostFxToggleChanged(bool isOn)
    {
        if (_ignorePostFxToggleCallback)
            return;

        ApplyPostFxVisible(isOn, true, true);
    }

    private void TogglePostFxFromBackgroundButton()
    {
        if (postFxToggle != null)
        {
            postFxToggle.isOn = !postFxToggle.isOn;
            return;
        }

        bool current = ReadSavedPostFxVisible();
        ApplyPostFxVisible(!current, true, true);
    }

    public void SetPostFxVisibleFromUI(bool visible)
    {
        ApplyPostFxVisible(visible, true, true);
    }

    public void TogglePostFxVisibleFromUI()
    {
        bool current = ReadSavedPostFxVisible();
        ApplyPostFxVisible(!current, true, true);
    }

    private void ApplyPostFxVisible(bool visible, bool save, bool log)
    {
        ResolvePostFxReference();

        if (postFxVolume != null)
            ApplyPostFxToVolume(postFxVolume, visible);

        if (save && savePostFxSetting && !string.IsNullOrEmpty(postFxPrefsKey))
        {
            PlayerPrefs.SetInt(postFxPrefsKey, visible ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (postFxToggle != null && postFxToggle.isOn != visible)
        {
            _ignorePostFxToggleCallback = true;
            postFxToggle.SetIsOnWithoutNotify(visible);
            _ignorePostFxToggleCallback = false;
        }

        if (debugPostFxToggle && log)
            Debug.Log("[SettingsTabsSwitcher] VHS/PostFX visible = " + visible);
    }

    private void ApplyPostFxToVolume(Volume volume, bool visible)
    {
        if (volume == null)
            return;

        if (controlPostFxByWeight)
        {
            volume.enabled = true;
            volume.weight = visible ? postFxEnabledWeight : 0f;
        }
        else
        {
            volume.enabled = visible;
        }
    }

    private bool ReadSavedPostFxVisible()
    {
        if (savePostFxSetting &&
            !string.IsNullOrEmpty(postFxPrefsKey) &&
            PlayerPrefs.HasKey(postFxPrefsKey))
        {
            return PlayerPrefs.GetInt(postFxPrefsKey, defaultPostFxVisible ? 1 : 0) != 0;
        }

        return defaultPostFxVisible;
    }

    private void ResolvePostFxReference()
    {
        if (postFxVolume != null)
            return;

        if (!autoFindPostFxInScene)
            return;

        Volume[] found = Resources.FindObjectsOfTypeAll<Volume>();

        for (int i = 0; i < found.Length; i++)
        {
            Volume candidate = found[i];

            if (candidate == null)
                continue;

            if (!candidate.gameObject.scene.IsValid())
                continue;

            if (!candidate.isGlobal)
                continue;

            postFxVolume = candidate;
            return;
        }
    }

    // =========================================================
    // Gamepad Rumble
    // =========================================================

    private void SetupGamepadRumbleToggle()
    {
        ResolvePlayerControllerReference();

        if (gamepadRumbleToggle != null)
        {
            gamepadRumbleToggle.onValueChanged.RemoveListener(OnGamepadRumbleToggleChanged);
            gamepadRumbleToggle.onValueChanged.AddListener(OnGamepadRumbleToggleChanged);
        }

        if (gamepadRumbleBackgroundButton != null)
        {
            gamepadRumbleBackgroundButton.onClick.RemoveListener(ToggleGamepadRumbleFromBackgroundButton);
            gamepadRumbleBackgroundButton.onClick.AddListener(ToggleGamepadRumbleFromBackgroundButton);
        }

        SyncGamepadRumbleToggleWithSavedValue();
    }

    private void SyncGamepadRumbleToggleWithSavedValue()
    {
        bool enabled = ReadSavedGamepadRumbleEnabled();
        ApplyGamepadRumbleEnabled(enabled, false, false);
    }

    private void OnGamepadRumbleToggleChanged(bool isOn)
    {
        if (_ignoreGamepadRumbleToggleCallback)
            return;

        ApplyGamepadRumbleEnabled(isOn, true, true);
    }

    private void ToggleGamepadRumbleFromBackgroundButton()
    {
        if (gamepadRumbleToggle != null)
        {
            gamepadRumbleToggle.isOn = !gamepadRumbleToggle.isOn;
            return;
        }

        bool current = ReadSavedGamepadRumbleEnabled();
        ApplyGamepadRumbleEnabled(!current, true, true);
    }

    public void SetGamepadRumbleEnabledFromUI(bool enabled)
    {
        ApplyGamepadRumbleEnabled(enabled, true, true);
    }

    public void ToggleGamepadRumbleEnabledFromUI()
    {
        bool current = ReadSavedGamepadRumbleEnabled();
        ApplyGamepadRumbleEnabled(!current, true, true);
    }

    private void ApplyGamepadRumbleEnabled(bool enabled, bool save, bool log)
    {
        ResolvePlayerControllerReference();

        if (playerController != null)
            playerController.SetLandingGamepadRumbleEnabled(enabled);

        if (save && saveGamepadRumbleSetting && !string.IsNullOrEmpty(gamepadRumblePrefsKey))
        {
            PlayerPrefs.SetInt(gamepadRumblePrefsKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (gamepadRumbleToggle != null && gamepadRumbleToggle.isOn != enabled)
        {
            _ignoreGamepadRumbleToggleCallback = true;
            gamepadRumbleToggle.SetIsOnWithoutNotify(enabled);
            _ignoreGamepadRumbleToggleCallback = false;
        }

        if (debugGamepadRumbleToggle && log)
            Debug.Log("[SettingsTabsSwitcher] Gamepad rumble enabled = " + enabled);
    }

    private bool ReadSavedGamepadRumbleEnabled()
    {
        if (saveGamepadRumbleSetting &&
            !string.IsNullOrEmpty(gamepadRumblePrefsKey) &&
            PlayerPrefs.HasKey(gamepadRumblePrefsKey))
        {
            return PlayerPrefs.GetInt(gamepadRumblePrefsKey, defaultGamepadRumbleEnabled ? 1 : 0) != 0;
        }

        return defaultGamepadRumbleEnabled;
    }

    private void ResolvePlayerControllerReference()
    {
        if (playerController != null)
            return;

        if (!autoFindPlayerControllerInScene)
            return;

        PlayerController[] found = Resources.FindObjectsOfTypeAll<PlayerController>();

        for (int i = 0; i < found.Length; i++)
        {
            PlayerController candidate = found[i];

            if (candidate == null)
                continue;

            if (!candidate.gameObject.scene.IsValid())
                continue;

            playerController = candidate;
            return;
        }
    }
}