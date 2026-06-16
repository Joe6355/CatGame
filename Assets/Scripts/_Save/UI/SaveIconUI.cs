using System.Collections;
using TMPro;
using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class SaveIconUI : MonoBehaviour
    {
        public static SaveIconUI Instance { get; private set; }

        [Header("UI")]
        [SerializeField, Tooltip("CanvasGroup иконки/надписи сохранения. Рекомендация: использовать для плавного появления/исчезновения.")]
        private CanvasGroup canvasGroup;

        [SerializeField, Tooltip("Текст надписи. Рекомендация: 'Сохранено'.")]
        private TextMeshProUGUI labelText;

        [Header("Timing")]
        [SerializeField, Tooltip("Сколько секунд держать надпись видимой. Рекомендация: 1.0-1.5, чтобы не мешать управлению.")]
        private float visibleSeconds = 1.2f;

        [SerializeField, Tooltip("Скорость плавного появления/исчезновения. Рекомендация: 8-12.")]
        private float fadeSpeed = 10f;

        [SerializeField, Tooltip("Текст по умолчанию. Рекомендация: 'Сохранено'.")]
        private string defaultMessage = "Сохранено";

        private Coroutine routine;

        private void Awake()
        {
            Instance = this;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            SetAlpha(0f);
        }

        public static void ShowSmallSaved()
        {
            if (Instance != null)
                Instance.Show(null);
        }

        public void Show(string message)
        {
            if (routine != null)
                StopCoroutine(routine);

            routine = StartCoroutine(ShowRoutine(string.IsNullOrWhiteSpace(message) ? defaultMessage : message));
        }

        private IEnumerator ShowRoutine(string message)
        {
            if (labelText != null)
                labelText.text = message;

            while (canvasGroup != null && canvasGroup.alpha < 0.99f)
            {
                SetAlpha(Mathf.MoveTowards(canvasGroup.alpha, 1f, fadeSpeed * Time.unscaledDeltaTime));
                yield return null;
            }

            yield return new WaitForSecondsRealtime(visibleSeconds);

            while (canvasGroup != null && canvasGroup.alpha > 0.01f)
            {
                SetAlpha(Mathf.MoveTowards(canvasGroup.alpha, 0f, fadeSpeed * Time.unscaledDeltaTime));
                yield return null;
            }

            SetAlpha(0f);
            routine = null;
        }

        private void SetAlpha(float alpha)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = alpha;
            canvasGroup.blocksRaycasts = alpha > 0.01f;
            canvasGroup.interactable = false;
        }
    }
}
