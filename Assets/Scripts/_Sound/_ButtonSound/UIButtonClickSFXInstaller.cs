using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIButtonClickSFXInstaller : MonoBehaviour
{
    public enum SearchMode
    {
        OnlyChildrenOfThisObject,
        WholeLoadedScene
    }

    [Header("Звук клика")]
    [SerializeField, Tooltip("Один общий звук, который будет проигрываться при нажатии на любую UI-кнопку.")]
    private AudioClip buttonClickClip;

    [SerializeField, Range(0f, 1f), Tooltip("Локальная громкость клика. Общая громкость всё равно регулируется через Row_SFX / SFX AudioMixer.")]
    private float clickVolume = 1f;

    [Header("Поиск кнопок")]
    [SerializeField, Tooltip("Где искать кнопки. Для главного Canvas обычно лучше WholeLoadedScene.")]
    private SearchMode searchMode = SearchMode.WholeLoadedScene;

    [SerializeField, Tooltip("Если включено — найдёт кнопки даже на выключенных панелях.")]
    private bool includeInactiveButtons = true;

    [SerializeField, Tooltip("Если включено — скрипт будет периодически искать новые кнопки, которые появились позже.")]
    private bool rescanPeriodically = true;

    [SerializeField, Min(0.1f), Tooltip("Как часто пересканировать кнопки, если Rescan Periodically включён.")]
    private float rescanInterval = 1f;

    [Header("Фильтры")]
    [SerializeField, Tooltip("Если включено — звук НЕ будет проигрываться у неактивных / заблокированных кнопок.")]
    private bool ignoreNotInteractableButtons = true;

    [SerializeField, Tooltip("Если включено — в консоль будет выводиться, сколько кнопок подключено.")]
    private bool debugLogs = false;

    private Coroutine rescanRoutine;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        InstallToButtons();

        if (rescanPeriodically)
            rescanRoutine = StartCoroutine(RescanRoutine());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (rescanRoutine != null)
        {
            StopCoroutine(rescanRoutine);
            rescanRoutine = null;
        }

    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallToButtons();
    }

    private IEnumerator RescanRoutine()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(rescanInterval);

        while (true)
        {
            yield return wait;
            InstallToButtons();
        }
    }

    private void InstallToButtons()
    {
        Button[] buttons = FindButtons();

        int added = 0;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null)
                continue;

            ConfigureTarget(button.gameObject, button, true, ref added);
            ConfigureChildEventHandlers(button, ref added);
        }

        if (debugLogs && added > 0)
            Debug.Log($"UIButtonClickSFXInstaller: подключено новых кнопок: {added}.", this);
    }

    private void ConfigureChildEventHandlers(Button button, ref int added)
    {
        MonoBehaviour[] behaviours = button.GetComponentsInChildren<MonoBehaviour>(includeInactiveButtons);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null || behaviour.gameObject == button.gameObject)
                continue;

            if (behaviour is UIButtonClickSFXTarget || !(behaviour is IPointerDownHandler))
                continue;

            // Не подключаем обработчики вложенной кнопки к родительской кнопке.
            if (behaviour.GetComponentInParent<Button>() != button)
                continue;

            ConfigureTarget(behaviour.gameObject, button, false, ref added);
        }
    }

    private void ConfigureTarget(GameObject targetObject, Button button, bool listenToButtonClick, ref int added)
    {
        UIButtonClickSFXTarget target = targetObject.GetComponent<UIButtonClickSFXTarget>();

        if (target == null)
        {
            target = targetObject.AddComponent<UIButtonClickSFXTarget>();
            added++;
        }

        target.Configure(this, button, listenToButtonClick);
    }

    private Button[] FindButtons()
    {
        if (searchMode == SearchMode.OnlyChildrenOfThisObject)
            return GetComponentsInChildren<Button>(includeInactiveButtons);

#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<Button>(
            includeInactiveButtons ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
#else
        return Object.FindObjectsOfType<Button>(includeInactiveButtons);
#endif
    }

    internal void PlayClick(Button button, bool validateButtonState)
    {
        if (!isActiveAndEnabled)
            return;

        if (buttonClickClip == null)
            return;

        if (button == null)
            return;

        if (validateButtonState && ignoreNotInteractableButtons &&
            (!button.IsActive() || !button.IsInteractable()))
            return;

        if (SoundFXManager.instance != null)
        {
            SoundFXManager.instance.PlaySoundFXClip(buttonClickClip, button.transform, clickVolume);
            return;
        }

        Debug.LogWarning("UIButtonClickSFXInstaller: SoundFXManager.instance не найден. Звук кнопки не проигран.", this);
    }

}

// EventSystem-обработчик не удаляется чужими Button.onClick.RemoveAllListeners().
[DisallowMultipleComponent]
public sealed class UIButtonClickSFXTarget : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
{
    private UIButtonClickSFXInstaller installer;
    private Button button;
    private bool listenToButtonClick;
    private bool suppressNextButtonClick;
    private int lastPlayedFrame = -1;
    private Coroutine clearSuppressionRoutine;

    internal void Configure(UIButtonClickSFXInstaller owner, Button targetButton, bool shouldListenToButtonClick)
    {
        if (button != null && listenToButtonClick)
            button.onClick.RemoveListener(OnButtonClick);

        installer = owner;
        button = targetButton;
        listenToButtonClick = shouldListenToButtonClick;

        if (button != null && listenToButtonClick)
        {
            // Переустанавливаем при каждом сканировании: чужой RemoveAllListeners мог удалить обработчик.
            button.onClick.RemoveListener(OnButtonClick);
            button.onClick.AddListener(OnButtonClick);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        PlayFromManualPointerDown();
    }

    internal void PlayFromManualPointerDown()
    {
        suppressNextButtonClick = true;
        TryPlay(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (clearSuppressionRoutine != null)
            StopCoroutine(clearSuppressionRoutine);

        clearSuppressionRoutine = StartCoroutine(ClearSuppressionNextFrame());
    }

    public void OnSubmit(BaseEventData eventData)
    {
        TryPlay(true);
    }

    private void OnButtonClick()
    {
        if (suppressNextButtonClick)
        {
            suppressNextButtonClick = false;
            return;
        }

        // Сам onClick уже подтверждает, что нажатие состоялось. К этому моменту
        // обработчик меню мог успеть выключить панель или загрузить другую сцену.
        TryPlay(false);
    }

    private void TryPlay(bool validateButtonState)
    {
        if (lastPlayedFrame == Time.frameCount)
            return;

        lastPlayedFrame = Time.frameCount;
        installer?.PlayClick(button, validateButtonState);
    }

    private IEnumerator ClearSuppressionNextFrame()
    {
        yield return null;
        suppressNextButtonClick = false;
        clearSuppressionRoutine = null;
    }

    private void OnDestroy()
    {
        if (button != null && listenToButtonClick)
            button.onClick.RemoveListener(OnButtonClick);
    }
}
