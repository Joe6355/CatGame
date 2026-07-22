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
        [SerializeField, Tooltip("Название строки: Автосейв, Файл 01, Файл 02.")]
        private TextMeshProUGUI titleText;

        [SerializeField, Tooltip("Дата и реальное время сохранения.")]
        private TextMeshProUGUI dateTimeText;

        [SerializeField, Tooltip("Название сцены/локации/комнаты.")]
        private TextMeshProUGUI locationText;

        [SerializeField, Tooltip("Игровое время прохождения.")]
        private TextMeshProUGUI playTimeText;

        [Header("Preview")]
        [SerializeField, Tooltip("Image строки, в который будет подставляться preview.png этого слота.")]
        private Image previewImage;

        [SerializeField, Tooltip("Картинка-заглушка для пустого или старого слота. Если поле пустое, используется Sprite, который уже стоит в Preview Image.")]
        private Sprite fallbackPreviewSprite;

        [SerializeField, Tooltip("Сохранять пропорции игрового скриншота внутри Image.")]
        private bool preservePreviewAspect = true;

        [Header("Buttons")]
        [SerializeField, Tooltip("Кнопка загрузки слота.")]
        private Button loadButton;

        [SerializeField, Tooltip("Кнопка удаления слота.")]
        private Button deleteButton;

        [SerializeField, Tooltip("Кнопка перезаписи/создания ручного слота.")]
        private Button overwriteButton;

        [SerializeField, Tooltip("Текст кнопки перезаписи.")]
        private TextMeshProUGUI overwriteButtonText;

        [Header("Navigation")]
        [SerializeField, Tooltip("Автоматически включать Navigation = Automatic у кнопок строки.")]
        private bool forceAutomaticNavigation = true;

        private string slotId;
        private Action<string> onLoad;
        private Action<string> onDelete;
        private Action<string> onOverwrite;

        private Sprite initialPreviewSprite;
        private Color initialPreviewColor = Color.white;
        private Sprite runtimePreviewSprite;
        private Texture2D runtimePreviewTexture;
        private int previewRequestVersion;

        public Button LoadButton => loadButton;
        public Button DeleteButton => deleteButton;
        public Button OverwriteButton => overwriteButton;

        private void Awake()
        {
            if (previewImage == null)
                return;

            initialPreviewSprite = previewImage.sprite;
            initialPreviewColor = previewImage.color;
            previewImage.preserveAspect = preservePreviewAspect;

            if (fallbackPreviewSprite == null)
                fallbackPreviewSprite = initialPreviewSprite;
        }

        private void OnDestroy()
        {
            previewRequestVersion++;
            DestroyRuntimePreview();
        }

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

            // Верхний текст
            if (dateTimeText != null)
                dateTimeText.text = exists ? meta.savedLocalTimeText : "";

            // Центральный текст
            if (locationText != null)
                locationText.text = exists ? BuildLocationText(meta) : "НЕТ ДАННЫХ";

            // Нижний текст
            if (playTimeText != null)
                playTimeText.text = exists ? FormatPlayTime(meta.playTimeSeconds) : "";

            SetButton(loadButton, exists && allowLoad, OnLoadButtonClicked);
            SetButton(deleteButton, exists && allowDelete, OnDeleteButtonClicked);
            SetButton(overwriteButton, allowOverwrite, OnOverwriteButtonClicked);

            if (overwriteButtonText != null)
                overwriteButtonText.text = exists ? "Перезаписать" : "Сохранить";

            RefreshPreviewAsync(slotId, exists);
        }

        public Selectable GetPreferredSelectable()
        {
            if (loadButton != null &&
                loadButton.gameObject.activeInHierarchy &&
                loadButton.interactable)
            {
                return loadButton;
            }

            if (overwriteButton != null &&
                overwriteButton.gameObject.activeInHierarchy &&
                overwriteButton.interactable)
            {
                return overwriteButton;
            }

            if (deleteButton != null &&
                deleteButton.gameObject.activeInHierarchy &&
                deleteButton.interactable)
            {
                return deleteButton;
            }

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

        private async void RefreshPreviewAsync(string requestedSlotId, bool slotExists)
        {
            int requestVersion = ++previewRequestVersion;

            if (!slotExists ||
                previewImage == null ||
                SaveManager.Instance == null)
            {
                ApplyFallbackPreview();
                return;
            }

            byte[] pngBytes =
                await SaveManager.Instance.LoadSlotPreviewAsync(requestedSlotId);

            if (this == null)
                return;

            if (requestVersion != previewRequestVersion)
                return;

            if (!string.Equals(slotId, requestedSlotId, StringComparison.Ordinal))
                return;

            if (pngBytes == null || pngBytes.Length == 0)
            {
                ApplyFallbackPreview();
                return;
            }

            ApplyPreviewBytes(pngBytes);
        }

        private void ApplyPreviewBytes(byte[] pngBytes)
        {
            DestroyRuntimePreview();

            Texture2D texture = new Texture2D(
                2,
                2,
                TextureFormat.RGB24,
                false,
                false
            );

            texture.name = "SavePreview_" + slotId;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            bool loaded = ImageConversion.LoadImage(texture, pngBytes, false);

            if (!loaded)
            {
                Destroy(texture);
                ApplyFallbackPreview();
                return;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );

            sprite.name = "SavePreviewSprite_" + slotId;

            runtimePreviewTexture = texture;
            runtimePreviewSprite = sprite;

            previewImage.sprite = runtimePreviewSprite;
            previewImage.color = Color.white;
            previewImage.preserveAspect = preservePreviewAspect;
            previewImage.enabled = true;
        }

        private void ApplyFallbackPreview()
        {
            DestroyRuntimePreview();

            if (previewImage == null)
                return;

            previewImage.sprite =
                fallbackPreviewSprite != null
                    ? fallbackPreviewSprite
                    : initialPreviewSprite;

            previewImage.color = initialPreviewColor;
            previewImage.preserveAspect = preservePreviewAspect;
            previewImage.enabled = true;
        }

        private void DestroyRuntimePreview()
        {
            if (runtimePreviewSprite != null)
            {
                Destroy(runtimePreviewSprite);
                runtimePreviewSprite = null;
            }

            if (runtimePreviewTexture != null)
            {
                Destroy(runtimePreviewTexture);
                runtimePreviewTexture = null;
            }
        }

        private void SetButton(
            Button button,
            bool interactable,
            UnityEngine.Events.UnityAction action)
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
            onLoad?.Invoke(slotId);
        }

        private void OnDeleteButtonClicked()
        {
            onDelete?.Invoke(slotId);
        }

        private void OnOverwriteButtonClicked()
        {
            onOverwrite?.Invoke(slotId);
        }

        private static string BuildLocationText(SaveSlotMeta meta)
        {
            if (!string.IsNullOrWhiteSpace(meta.roomId))
                return meta.roomId;

            if (!string.IsNullOrWhiteSpace(meta.locationId))
                return meta.locationId;

            return string.IsNullOrWhiteSpace(meta.sceneName)
                ? "Неизвестная сцена"
                : meta.sceneName;
        }

        private static string FormatPlayTime(float seconds)
        {
            if (seconds < 0f)
                seconds = 0f;

            TimeSpan span = TimeSpan.FromSeconds(seconds);

            return string.Format(
                "{0:00}:{1:00}:{2:00}",
                (int)span.TotalHours,
                span.Minutes,
                span.Seconds
            );
        }
    }
}