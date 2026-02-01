using UnityEngine;
using UnityEngine.UI;

public class ExitConfirmUI : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private GameObject confirmDialog; // ConfirmExit_Dialog
    [SerializeField] private Button quitButton;        // Btn_Quit из главного меню
    [SerializeField] private Button yesButton;         // Btn_Yes
    [SerializeField] private Button noButton;          // Btn_No

    private void Awake()
    {
        // На старте всегда скрываем диалог
        if (confirmDialog != null)
            confirmDialog.SetActive(false);

        // Подписываемся на кнопки
        if (quitButton != null) quitButton.onClick.AddListener(OpenDialog);
        if (noButton != null) noButton.onClick.AddListener(CloseDialog);
        if (yesButton != null) yesButton.onClick.AddListener(QuitGame);
    }

    public void OpenDialog()
    {
        if (confirmDialog != null)
            confirmDialog.SetActive(true);
    }

    public void CloseDialog()
    {
        if (confirmDialog != null)
            confirmDialog.SetActive(false);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        // Чтобы работало в редакторе
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
