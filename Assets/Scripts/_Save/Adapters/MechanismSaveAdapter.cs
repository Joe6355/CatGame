using System;
using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class MechanismSaveAdapter : MonoBehaviour, ISaveable
    {
        [SerializeField, Tooltip("SaveableId механизма. Рекомендация: использовать для рычагов, вентиляций, лифтов, коротких путей.")]
        private SaveableId saveableId;

        [SerializeField, Tooltip("Начальное состояние активности. Рекомендация: выключено для рычагов, которые игрок ещё не трогал.")]
        private bool startsActivated = false;

        [SerializeField, Tooltip("Связанные объекты, которые включаются при активации. Рекомендация: путь открыт, лампа горит, лифт включен.")]
        private GameObject[] enableWhenActivated;

        [SerializeField, Tooltip("Связанные объекты, которые выключаются при активации. Рекомендация: заглушка прохода, блокирующий визуал, старая подсказка.")]
        private GameObject[] disableWhenActivated;

        private bool activated;

        public string SaveId
        {
            get { return saveableId != null ? saveableId.Id : string.Empty; }
        }

        private void Awake()
        {
            if (saveableId == null)
                saveableId = GetComponent<SaveableId>();

            SetActivated(startsActivated, false);
        }

        public void Activate()
        {
            SetActivated(true, true);
        }

        public void Deactivate()
        {
            SetActivated(false, true);
        }

        public string CaptureStateJson()
        {
            MechanismState state = new MechanismState { activated = activated };
            return JsonUtility.ToJson(state);
        }

        public void RestoreStateJson(string json)
        {
            MechanismState state = JsonUtility.FromJson<MechanismState>(json);
            SetActivated(state.activated, false);
        }

        private void SetActivated(bool value, bool markDirty)
        {
            activated = value;

            ApplyActiveArray(enableWhenActivated, activated);
            ApplyActiveArray(disableWhenActivated, !activated);

            if (markDirty && SaveManager.Instance != null)
                SaveManager.Instance.MarkDirtyAndAutosave();
        }

        private static void ApplyActiveArray(GameObject[] objects, bool active)
        {
            if (objects == null)
                return;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                    objects[i].SetActive(active);
            }
        }

        [Serializable]
        private struct MechanismState
        {
            public bool activated;
        }
    }
}
