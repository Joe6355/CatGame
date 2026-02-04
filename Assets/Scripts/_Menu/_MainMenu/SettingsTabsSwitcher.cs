using UnityEngine;
using UnityEngine.UI;

public class SettingsTabsSwitcher : MonoBehaviour
{
    [Header("Кнопки вкладок")]
    [SerializeField] private Button audioBtn;
    [SerializeField] private Button controlsBtn;
    [SerializeField] private Button gameplayBtn;
    [SerializeField] private Button videoBtn;

    [Header("Панели вкладок")]
    [SerializeField] private GameObject tabAudio;
    [SerializeField] private GameObject tabControls;
    [SerializeField] private GameObject tabGameplay;
    [SerializeField] private GameObject tabVideo;

    [Header("Gameplay Tooltip (Упрощение)")]
    [SerializeField] private Button assistInfoBtn;          // кнопка "?"
    [SerializeField] private GameObject assistTooltipPanel; // панель подсказки
    [SerializeField] private bool hideTooltipOnTabChange = true;

    [Header("Стартовое поведение")]
    [SerializeField] private bool closeAllTabsOnEnable = true; // при открытии Settings_Panel всё закрывать

    private void Awake()
    {
        // Подписка на кнопки вкладок
        if (audioBtn != null) audioBtn.onClick.AddListener(() => ShowTab(tabAudio));
        if (controlsBtn != null) controlsBtn.onClick.AddListener(() => ShowTab(tabControls));
        if (gameplayBtn != null) gameplayBtn.onClick.AddListener(() => ShowTab(tabGameplay));
        if (videoBtn != null) videoBtn.onClick.AddListener(() => ShowTab(tabVideo));

        // Tooltip
        if (assistTooltipPanel != null)
            assistTooltipPanel.SetActive(false);

        if (assistInfoBtn != null)
            assistInfoBtn.onClick.AddListener(ToggleAssistTooltip);

        // ВАЖНО: НИЧЕГО не открываем по умолчанию
        CloseAllTabs();
    }

    private void OnEnable()
    {
        // Если этот скрипт висит на Settings_Panel (который выключен в начале),
        // то при открытии настроек вкладки будут закрываться каждый раз.
        if (closeAllTabsOnEnable)
            CloseAllTabs();
    }

    private void ShowTab(GameObject active)
    {
        if (tabAudio != null) tabAudio.SetActive(active == tabAudio);
        if (tabControls != null) tabControls.SetActive(active == tabControls);
        if (tabGameplay != null) tabGameplay.SetActive(active == tabGameplay);
        if (tabVideo != null) tabVideo.SetActive(active == tabVideo);

        if (hideTooltipOnTabChange && assistTooltipPanel != null)
            assistTooltipPanel.SetActive(false);
    }

    public void CloseAllTabs()
    {
        if (tabAudio != null) tabAudio.SetActive(false);
        if (tabControls != null) tabControls.SetActive(false);
        if (tabGameplay != null) tabGameplay.SetActive(false);
        if (tabVideo != null) tabVideo.SetActive(false);

        if (assistTooltipPanel != null)
            assistTooltipPanel.SetActive(false);
    }

    public void ToggleAssistTooltip()
    {
        if (assistTooltipPanel == null) return;

        // Подсказка показывается только на вкладке Gameplay
        if (tabGameplay != null && !tabGameplay.activeSelf)
            return;

        assistTooltipPanel.SetActive(!assistTooltipPanel.activeSelf);
    }
}
