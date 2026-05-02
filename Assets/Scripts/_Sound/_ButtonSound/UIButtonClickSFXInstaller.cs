using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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

    private readonly Dictionary<Button, UnityAction> wiredButtons = new Dictionary<Button, UnityAction>();

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

        UninstallFromButtons();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallToButtons();
    }

    private IEnumerator RescanRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(rescanInterval);

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

            if (wiredButtons.ContainsKey(button))
                continue;

            UnityAction action = () => PlayClick(button);

            button.onClick.AddListener(action);
            wiredButtons.Add(button, action);

            added++;
        }

        CleanupDestroyedButtons();

        if (debugLogs && added > 0)
            Debug.Log($"UIButtonClickSFXInstaller: подключено новых кнопок: {added}. Всего: {wiredButtons.Count}", this);
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

    private void PlayClick(Button button)
    {
        if (buttonClickClip == null)
            return;

        if (button == null)
            return;

        if (ignoreNotInteractableButtons && !button.interactable)
            return;

        if (SoundFXManager.instance != null)
        {
            SoundFXManager.instance.PlaySoundFXClip(buttonClickClip, button.transform, clickVolume);
            return;
        }

        Debug.LogWarning("UIButtonClickSFXInstaller: SoundFXManager.instance не найден. Звук кнопки не проигран.", this);
    }

    private void CleanupDestroyedButtons()
    {
        if (wiredButtons.Count == 0)
            return;

        List<Button> destroyedButtons = null;

        foreach (KeyValuePair<Button, UnityAction> pair in wiredButtons)
        {
            if (pair.Key == null)
            {
                destroyedButtons ??= new List<Button>();
                destroyedButtons.Add(pair.Key);
            }
        }

        if (destroyedButtons == null)
            return;

        for (int i = 0; i < destroyedButtons.Count; i++)
            wiredButtons.Remove(destroyedButtons[i]);
    }

    private void UninstallFromButtons()
    {
        foreach (KeyValuePair<Button, UnityAction> pair in wiredButtons)
        {
            if (pair.Key == null)
                continue;

            pair.Key.onClick.RemoveListener(pair.Value);
        }

        wiredButtons.Clear();
    }
}