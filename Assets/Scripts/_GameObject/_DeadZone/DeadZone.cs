using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class DeadZone : MonoBehaviour
{
    [Header("Фильтр")]
    [SerializeField, Tooltip("Если включено — перезапускать сцену только когда в триггер вошел объект с нужным тегом.")]
    private bool requirePlayerTag = true;

    [SerializeField, Tooltip("Тег объекта, который может вызвать рестарт сцены.")]
    private string playerTag = "Player";

    [SerializeField, Tooltip("Если включено — рестарт сработает только один раз, чтобы не было повторных вызовов.")]
    private bool restartOnlyOnce = true;

    private bool isRestarting;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (restartOnlyOnce && isRestarting)
            return;

        if (requirePlayerTag && !other.CompareTag(playerTag))
            return;

        RestartScene();
    }

    private void RestartScene()
    {
        isRestarting = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
