using UnityEngine;
using UnityEngine.UI;

public class ContinuePanelOpener : MonoBehaviour
{
    [Header("ѕанели")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject continuePanel;

    [Header(" нопки")]
    [SerializeField] private Button openContinueButton; // Btn_Continue
    [SerializeField] private Button backButton;         // Btn_Back внутри Continue_Panel

    private void Awake()
    {
        if (openContinueButton != null)
            openContinueButton.onClick.AddListener(OpenContinue);

        if (backButton != null)
            backButton.onClick.AddListener(BackToMain);

        // на старте обычно показываем главное меню
        if (mainPanel != null) mainPanel.SetActive(true);
        if (continuePanel != null) continuePanel.SetActive(false);
    }

    public void OpenContinue()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (continuePanel != null) continuePanel.SetActive(true);
    }

    public void BackToMain()
    {
        if (continuePanel != null) continuePanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }
}
