using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelOpener : MonoBehaviour
{
    [Header("Панели")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Кнопки")]
    [SerializeField] private Button openSettingsButton; // Btn_Settings на главном меню
    [SerializeField] private Button backButton;         // Btn_Back внутри Settings_Panel

    private void Awake()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (openSettingsButton != null)
            openSettingsButton.onClick.AddListener(OpenSettings);

        if (backButton != null)
            backButton.onClick.AddListener(BackToMain);
    }

    public void OpenSettings()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void BackToMain()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }
}
