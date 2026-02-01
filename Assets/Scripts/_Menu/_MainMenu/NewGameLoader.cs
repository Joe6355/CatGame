using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NewGameLoader : MonoBehaviour
{
    [Header(" нопка")]
    [SerializeField] private Button newGameButton;

    [Header("—цена, которую грузим")]
    [SerializeField] private string sceneName;

    private void Awake()
    {
        if (newGameButton != null)
            newGameButton.onClick.AddListener(LoadScene);
    }

    public void LoadScene()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("NewGameLoader: sceneName пустой. ”кажи им€ сцены в инспекторе.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
