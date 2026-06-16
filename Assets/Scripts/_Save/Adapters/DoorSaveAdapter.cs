using System;
using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class DoorSaveAdapter : MonoBehaviour, ISaveable
    {
        [SerializeField, Tooltip("SaveableId двери. Рекомендация: повесить SaveableId на этот же объект и назначить сюда.")]
        private SaveableId saveableId;

        [SerializeField, Tooltip("Визуал закрытой двери. Рекомендация: объект со спрайтом/анимацией двери, который надо скрыть при открытии.")]
        private GameObject closedDoorVisual;

        [SerializeField, Tooltip("Коллайдер двери. Рекомендация: назначить Collider2D, который блокирует проход, чтобы отключать его при открытии.")]
        private Collider2D blockingCollider;

        [SerializeField, Tooltip("Начальное состояние двери, если сейва ещё нет. Рекомендация: выключено для закрытых дверей.")]
        private bool startsOpen = false;

        private bool isOpen;

        public string SaveId
        {
            get { return saveableId != null ? saveableId.Id : string.Empty; }
        }

        private void Awake()
        {
            if (saveableId == null)
                saveableId = GetComponent<SaveableId>();

            SetOpen(startsOpen, false);
        }

        public string CaptureStateJson()
        {
            DoorState state = new DoorState { isOpen = isOpen };
            return JsonUtility.ToJson(state);
        }

        public void RestoreStateJson(string json)
        {
            DoorState state = JsonUtility.FromJson<DoorState>(json);
            SetOpen(state.isOpen, false);
        }

        public void SetOpen(bool value)
        {
            SetOpen(value, true);
        }

        private void SetOpen(bool value, bool markDirty)
        {
            isOpen = value;

            if (closedDoorVisual != null)
                closedDoorVisual.SetActive(!isOpen);

            if (blockingCollider != null)
                blockingCollider.enabled = !isOpen;

            if (markDirty && SaveManager.Instance != null)
                SaveManager.Instance.MarkDirtyAndAutosave();
        }

        [Serializable]
        private struct DoorState
        {
            public bool isOpen;
        }
    }
}
