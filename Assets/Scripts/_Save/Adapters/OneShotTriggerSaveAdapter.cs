using System;
using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class OneShotTriggerSaveAdapter : MonoBehaviour, ISaveable
    {
        [SerializeField, Tooltip("SaveableId одноразового триггера. Рекомендация: использовать для кат-сцен, подсказок, одноразовых ловушек.")]
        private SaveableId saveableId;

        [SerializeField, Tooltip("Collider2D триггера, который нужно отключить после срабатывания. Рекомендация: назначить trigger collider этого объекта.")]
        private Collider2D triggerCollider;

        [SerializeField, Tooltip("Отключать весь GameObject после срабатывания. Рекомендация: включено для полностью одноразовых триггеров.")]
        private bool disableGameObjectWhenTriggered = false;

        [SerializeField, Tooltip("Начальное состояние. Рекомендация: выключено, чтобы кат-сцена/триггер сработали в новой игре.")]
        private bool startsTriggered = false;

        private bool triggered;

        public string SaveId
        {
            get { return saveableId != null ? saveableId.Id : string.Empty; }
        }

        private void Awake()
        {
            if (saveableId == null)
                saveableId = GetComponent<SaveableId>();

            if (triggerCollider == null)
                triggerCollider = GetComponent<Collider2D>();

            SetTriggered(startsTriggered, false);
        }

        public void MarkTriggered()
        {
            SetTriggered(true, true);
        }

        public string CaptureStateJson()
        {
            OneShotState state = new OneShotState { triggered = triggered };
            return JsonUtility.ToJson(state);
        }

        public void RestoreStateJson(string json)
        {
            OneShotState state = JsonUtility.FromJson<OneShotState>(json);
            SetTriggered(state.triggered, false);
        }

        private void SetTriggered(bool value, bool markDirty)
        {
            triggered = value;

            if (triggerCollider != null)
                triggerCollider.enabled = !triggered;

            if (disableGameObjectWhenTriggered)
                gameObject.SetActive(!triggered);

            if (markDirty && SaveManager.Instance != null)
                SaveManager.Instance.MarkDirtyAndAutosave();
        }

        [Serializable]
        private struct OneShotState
        {
            public bool triggered;
        }
    }
}
