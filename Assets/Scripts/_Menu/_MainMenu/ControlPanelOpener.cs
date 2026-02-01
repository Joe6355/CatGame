using UnityEngine;
using UnityEngine.UI;

public class ControlPanelOpener : MonoBehaviour
{
    [Header("Панели")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject controlPanel;

    [Header("Кнопки")]
    [SerializeField] private Button openControlButton; // Btn_HelpCredits или Btn_Controls
    [SerializeField] private Button backButton;        // Btn_Back внутри Control_Panel

    private void Awake()
    {
        if (openControlButton != null)
            openControlButton.onClick.AddListener(OpenControl);

        if (backButton != null)
            backButton.onClick.AddListener(BackToMain);

        if (controlPanel != null) controlPanel.SetActive(false);
    }

    public void OpenControl()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (controlPanel != null) controlPanel.SetActive(true);
    }

    public void BackToMain()
    {
        if (controlPanel != null) controlPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }
}
