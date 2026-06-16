using System;
using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class CollectibleSaveAdapter : MonoBehaviour, ISaveable
    {
        [SerializeField, Tooltip("SaveableId предмета. Рекомендация: каждый collectible должен иметь уникальный SaveableId.")]
        private SaveableId saveableId;

        [SerializeField, Tooltip("Отключать весь объект после сбора. Рекомендация: включено для рыбок, монет, одноразовых предметов.")]
        private bool disableGameObjectWhenCollected = true;

        [SerializeField, Tooltip("Визуал предмета, если не нужно отключать весь GameObject. Рекомендация: оставить пустым, если отключается весь объект.")]
        private GameObject visualRoot;

        [SerializeField, Tooltip("Начальное состояние. Рекомендация: выключено, если предмет должен быть доступен при новой игре.")]
        private bool startsCollected = false;

        private bool collected;

        public string SaveId
        {
            get { return saveableId != null ? saveableId.Id : string.Empty; }
        }

        private void Awake()
        {
            if (saveableId == null)
                saveableId = GetComponent<SaveableId>();

            SetCollected(startsCollected, false);
        }

        public void Collect()
        {
            SetCollected(true, true);
        }

        public string CaptureStateJson()
        {
            CollectibleState state = new CollectibleState { collected = collected };
            return JsonUtility.ToJson(state);
        }

        public void RestoreStateJson(string json)
        {
            CollectibleState state = JsonUtility.FromJson<CollectibleState>(json);
            SetCollected(state.collected, false);
        }

        private void SetCollected(bool value, bool markDirty)
        {
            collected = value;

            if (disableGameObjectWhenCollected)
                gameObject.SetActive(!collected);
            else if (visualRoot != null)
                visualRoot.SetActive(!collected);

            if (markDirty && SaveManager.Instance != null)
                SaveManager.Instance.MarkDirtyAndAutosave();
        }

        [Serializable]
        private struct CollectibleState
        {
            public bool collected;
        }
    }
}
