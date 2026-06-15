using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MenuExternalLinksUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button telegramButton;
    [SerializeField] private Button twitterButton;
    [SerializeField] private Button bugReportButton;
    [SerializeField] private Button clownButton;

    [Header("Panels")]
    [SerializeField] private GameObject developersPanel;

    [Tooltip("Если включено, панель разработчиков будет автоматически скрыта при запуске сцены.")]
    [SerializeField] private bool hideDevelopersPanelOnAwake = true;

    [Tooltip("Если эти панели станут активными, панель разработчиков автоматически закроется. Сюда можно назначить SettingsPanel, ContinuePanel, ControlPanel и другие окна меню.")]
    [SerializeField] private GameObject[] panelsThatCloseDevelopersPanel;

    [Header("External URLs")]
    [SerializeField] private string telegramUrl;
    [SerializeField] private string twitterUrl;
    [SerializeField] private string bugReportUrl;

    [Header("Close Behaviour")]
    [Tooltip("Если включено, кнопка Clown будет не только открывать, но и закрывать панель при повторном нажатии.")]
    [SerializeField] private bool clownButtonTogglesPanel = true;

    [Tooltip("Если включено, панель разработчиков закрывается при открытии внешней ссылки.")]
    [SerializeField] private bool closeDevelopersPanelWhenOpeningExternalUrl = true;

    [Tooltip("Если включено, панель разработчиков закрывается при нажатии на любую другую кнопку меню.")]
    [SerializeField] private bool closeDevelopersPanelOnAnyOtherButtonClick = true;

    [Tooltip("Корень, внутри которого искать кнопки для автозакрытия панели. Если пусто, скрипт сам попробует взять Canvas от назначенных кнопок.")]
    [SerializeField] private Transform autoCloseButtonsRoot;

    [Tooltip("Если включено, панель разработчиков закрывается по клавише Escape.")]
    [SerializeField] private bool closeDevelopersPanelOnCancelKey = true;

    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

    [Header("Debug")]
    [SerializeField] private bool warnAboutMissingButtons = true;
    [SerializeField] private bool warnAboutMissingPanel = true;
    [SerializeField] private bool logOpenedUrls = false;

    private readonly List<Button> autoCloseButtons = new List<Button>();
    private readonly UnityAction closeDevelopersPanelActionCache = null;

    private UnityAction closeDevelopersPanelAction;

    private void Awake()
    {
        closeDevelopersPanelAction = CloseDevelopersPanelSilently;

        ValidateInspectorReferences();

        if (hideDevelopersPanelOnAwake && developersPanel != null)
        {
            developersPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        RegisterMainButtonListeners();
        RegisterAutoCloseButtonListeners();
    }

    private void OnDisable()
    {
        UnregisterMainButtonListeners();
        UnregisterAutoCloseButtonListeners();
    }

    private void Update()
    {
        if (!IsDevelopersPanelOpen())
            return;

        if (closeDevelopersPanelOnCancelKey && Input.GetKeyDown(cancelKey))
        {
            CloseClownPanel();
            return;
        }

        CloseDevelopersPanelIfWatchedPanelOpened();
    }

    public void OpenTelegram()
    {
        OpenUrl(telegramUrl, nameof(telegramUrl));
    }

    public void OpenTwitter()
    {
        OpenUrl(twitterUrl, nameof(twitterUrl));
    }

    public void OpenBugReport()
    {
        OpenUrl(bugReportUrl, nameof(bugReportUrl));
    }

    public void OpenClownPanel()
    {
        if (developersPanel == null)
        {
            Debug.LogWarning($"{nameof(MenuExternalLinksUI)}: developersPanel не назначена в Inspector.", this);
            return;
        }

        if (clownButtonTogglesPanel && developersPanel.activeSelf)
        {
            developersPanel.SetActive(false);
            return;
        }

        developersPanel.SetActive(true);
    }

    public void ToggleClownPanel()
    {
        OpenClownPanel();
    }

    public void CloseClownPanel()
    {
        if (developersPanel == null)
        {
            Debug.LogWarning($"{nameof(MenuExternalLinksUI)}: developersPanel не назначена в Inspector.", this);
            return;
        }

        developersPanel.SetActive(false);
    }

    private void CloseDevelopersPanelSilently()
    {
        if (developersPanel == null)
            return;

        if (!developersPanel.activeSelf)
            return;

        developersPanel.SetActive(false);
    }

    private void RegisterMainButtonListeners()
    {
        BindButton(telegramButton, OpenTelegram);
        BindButton(twitterButton, OpenTwitter);
        BindButton(bugReportButton, OpenBugReport);
        BindButton(clownButton, OpenClownPanel);
    }

    private void UnregisterMainButtonListeners()
    {
        UnbindButton(telegramButton, OpenTelegram);
        UnbindButton(twitterButton, OpenTwitter);
        UnbindButton(bugReportButton, OpenBugReport);
        UnbindButton(clownButton, OpenClownPanel);
    }

    private void RegisterAutoCloseButtonListeners()
    {
        if (!closeDevelopersPanelOnAnyOtherButtonClick)
            return;

        if (closeDevelopersPanelAction == null)
            closeDevelopersPanelAction = CloseDevelopersPanelSilently;

        Transform root = ResolveAutoCloseButtonsRoot();

        if (root == null)
            return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);

        autoCloseButtons.Clear();

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null)
                continue;

            if (button == clownButton)
                continue;

            button.onClick.RemoveListener(closeDevelopersPanelAction);
            button.onClick.AddListener(closeDevelopersPanelAction);

            autoCloseButtons.Add(button);
        }
    }

    private void UnregisterAutoCloseButtonListeners()
    {
        if (closeDevelopersPanelAction == null)
            return;

        for (int i = 0; i < autoCloseButtons.Count; i++)
        {
            Button button = autoCloseButtons[i];

            if (button == null)
                continue;

            button.onClick.RemoveListener(closeDevelopersPanelAction);
        }

        autoCloseButtons.Clear();
    }

    private Transform ResolveAutoCloseButtonsRoot()
    {
        if (autoCloseButtonsRoot != null)
            return autoCloseButtonsRoot;

        Canvas canvas = null;

        if (telegramButton != null)
            canvas = telegramButton.GetComponentInParent<Canvas>(true);

        if (canvas == null && twitterButton != null)
            canvas = twitterButton.GetComponentInParent<Canvas>(true);

        if (canvas == null && bugReportButton != null)
            canvas = bugReportButton.GetComponentInParent<Canvas>(true);

        if (canvas == null && clownButton != null)
            canvas = clownButton.GetComponentInParent<Canvas>(true);

        if (canvas != null)
            return canvas.transform;

        return transform.root;
    }

    private void BindButton(Button button, UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void UnbindButton(Button button, UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
    }

    private void OpenUrl(string rawUrl, string fieldName)
    {
        if (closeDevelopersPanelWhenOpeningExternalUrl)
        {
            CloseDevelopersPanelSilently();
        }

        string cleanUrl = NormalizeUrl(rawUrl);

        if (string.IsNullOrEmpty(cleanUrl))
        {
            Debug.LogWarning($"{nameof(MenuExternalLinksUI)}: ссылка {fieldName} пустая. Заполни её в Inspector.", this);
            return;
        }

        if (logOpenedUrls)
        {
            Debug.Log($"{nameof(MenuExternalLinksUI)}: open URL -> {cleanUrl}", this);
        }

        Application.OpenURL(cleanUrl);
    }

    private string NormalizeUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return string.Empty;

        string url = rawUrl.Trim();

        if (url.StartsWith("@"))
            return "https://t.me/" + url.Substring(1);

        if (url.StartsWith("t.me/", System.StringComparison.OrdinalIgnoreCase))
            return "https://" + url;

        if (url.StartsWith("telegram.me/", System.StringComparison.OrdinalIgnoreCase))
            return "https://" + url;

        if (url.StartsWith("twitter.com/", System.StringComparison.OrdinalIgnoreCase))
            return "https://" + url;

        if (url.StartsWith("x.com/", System.StringComparison.OrdinalIgnoreCase))
            return "https://" + url;

        if (url.StartsWith("docs.google.com/", System.StringComparison.OrdinalIgnoreCase))
            return "https://" + url;

        if (url.StartsWith("forms.gle/", System.StringComparison.OrdinalIgnoreCase))
            return "https://" + url;

        if (url.Contains("://"))
            return url;

        return "https://" + url;
    }

    private void CloseDevelopersPanelIfWatchedPanelOpened()
    {
        if (developersPanel == null)
            return;

        if (panelsThatCloseDevelopersPanel == null)
            return;

        for (int i = 0; i < panelsThatCloseDevelopersPanel.Length; i++)
        {
            GameObject panel = panelsThatCloseDevelopersPanel[i];

            if (panel == null)
                continue;

            if (panel == developersPanel)
                continue;

            if (!panel.activeInHierarchy)
                continue;

            developersPanel.SetActive(false);
            return;
        }
    }

    private bool IsDevelopersPanelOpen()
    {
        return developersPanel != null && developersPanel.activeInHierarchy;
    }

    private void ValidateInspectorReferences()
    {
        if (warnAboutMissingButtons)
        {
            WarnIfButtonMissing(telegramButton, nameof(telegramButton));
            WarnIfButtonMissing(twitterButton, nameof(twitterButton));
            WarnIfButtonMissing(bugReportButton, nameof(bugReportButton));
            WarnIfButtonMissing(clownButton, nameof(clownButton));
        }

        if (warnAboutMissingPanel && developersPanel == null)
        {
            Debug.LogWarning($"{nameof(MenuExternalLinksUI)}: developersPanel не назначена в Inspector.", this);
        }
    }

    private void WarnIfButtonMissing(Button button, string fieldName)
    {
        if (button != null)
            return;

        Debug.LogWarning($"{nameof(MenuExternalLinksUI)}: кнопка {fieldName} не назначена в Inspector.", this);
    }
}