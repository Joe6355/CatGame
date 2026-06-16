using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class SaveSlotRowUI : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField, Tooltip("Название строки: Автосейв, Сохранение 1, Сохранение 2. Рекомендация: TextMeshProUGUI.")]
        private TextMeshProUGUI titleText;

        [SerializeField, Tooltip("Дата и реальное время сохранения. Рекомендация: отдельный TMP-текст в колонке 'Дата'.")]
        private TextMeshProUGUI dateTimeText;

        [SerializeField, Tooltip("Название сцены/локации/комнаты. В твоём текущем UI это можно использовать как колонку 'Время'.")]
        private TextMeshProUGUI locationText;

        [SerializeField, Tooltip("Игровое время прохождения. Рекомендация: формат ЧЧ:ММ:СС.")]
        private TextMeshProUGUI playTimeText;

        [Header("Buttons")]
        [SerializeField, Tooltip("Кнопка загрузки слота. Рекомендация: блокировать, если слот пустой.")]
        private Button loadButton;

        [SerializeField, Tooltip("Кнопка удаления слота. Рекомендация: блокировать, если слот пустой; всегда показывать подтверждение.")]
        private Button deleteButton;

        [SerializeField, Tooltip("Кнопка перезаписи/создания ручного слота. Рекомендация: доступна только в режиме ручного сохранения у лежанки.")]
        private Button overwriteButton;

        [SerializeField, Tooltip("Текст кнопки перезаписи. Рекомендация: менять на 'Сохранить' для пустого слота и 'Перезаписать' для занятого.")]
        private TextMeshProUGUI overwriteButtonText;

        [Header("Empty Slot")]
        [SerializeField, Tooltip("Текст для пустого слота. Рекомендация: 'Пустой слот'.")]
        private string emptySlotText = "Пустой слот";

        [Header("Navigation")]
        [SerializeField, Tooltip("Автоматически включать Navigation = Automatic у кнопок строки, чтобы по ним можно было ходить клавиатурой/геймпадом. Рекомендация: включено.")]
        private bool forceAutomaticNavigation = true;

        private string slotId;
        private Action<string> onLoad;
        private Action<string> onDelete;
        private Action<string> onOverwrite;

        public Button LoadButton => loadButton;
        public Button DeleteButton => deleteButton;
        public Button OverwriteButton => overwriteButton;

        public void Bind(
            string slotId,
            string title,
            SaveSlotMeta meta,
            bool allowLoad,
            bool allowDelete,
            bool allowOverwrite,
            Action<string> onLoad,
            Action<string> onDelete,
            Action<string> onOverwrite)
        {
            this.slotId = slotId;
            this.onLoad = onLoad;
            this.onDelete = onDelete;
            this.onOverwrite = onOverwrite;

            bool exists = meta != null && meta.exists;

            if (titleText != null)
                titleText.text = title;

            if (dateTimeText != null)
                dateTimeText.text = exists ? meta.savedLocalTimeText : emptySlotText;

            if (locationText != null)
                locationText.text = exists ? BuildLocationText(meta) : "—";

            if (playTimeText != null)
                playTimeText.text = exists ? FormatPlayTime(meta.playTimeSeconds) : "—";

            SetButton(loadButton, exists && allowLoad, OnLoadButtonClicked);
            SetButton(deleteButton, exists && allowDelete, OnDeleteButtonClicked);
            SetButton(overwriteButton, allowOverwrite, OnOverwriteButtonClicked);

            if (overwriteButtonText != null)
                overwriteButtonText.text = exists ? "Перезаписать" : "Сохранить";
        }

        public Selectable GetPreferredSelectable()
        {
            if (loadButton != null && loadButton.gameObject.activeInHierarchy && loadButton.interactable)
                return loadButton;

            if (overwriteButton != null && overwriteButton.gameObject.activeInHierarchy && overwriteButton.interactable)
                return overwriteButton;

            if (deleteButton != null && deleteButton.gameObject.activeInHierarchy && deleteButton.interactable)
                return deleteButton;

            return null;
        }

        public bool ContainsSelectable(GameObject selectedObject)
        {
            if (selectedObject == null)
                return false;

            if (loadButton != null && selectedObject == loadButton.gameObject)
                return true;

            if (deleteButton != null && selectedObject == deleteButton.gameObject)
                return true;

            if (overwriteButton != null && selectedObject == overwriteButton.gameObject)
                return true;

            return false;
        }

        private void SetButton(Button button, bool interactable, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
            button.interactable = interactable;

            if (forceAutomaticNavigation)
            {
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.Automatic;
                button.navigation = navigation;
            }
        }

        private void OnLoadButtonClicked()
        {
            if (onLoad != null)
                onLoad(slotId);
        }

        private void OnDeleteButtonClicked()
        {
            if (onDelete != null)
                onDelete(slotId);
        }

        private void OnOverwriteButtonClicked()
        {
            if (onOverwrite != null)
                onOverwrite(slotId);
        }

        private static string BuildLocationText(SaveSlotMeta meta)
        {
            if (!string.IsNullOrWhiteSpace(meta.roomId))
                return meta.roomId;

            if (!string.IsNullOrWhiteSpace(meta.locationId))
                return meta.locationId;

            return string.IsNullOrWhiteSpace(meta.sceneName) ? "Неизвестная сцена" : meta.sceneName;
        }

        private static string FormatPlayTime(float seconds)
        {
            if (seconds < 0f)
                seconds = 0f;

            TimeSpan span = TimeSpan.FromSeconds(seconds);
            return string.Format("{0:00}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds);
        }
    }
}