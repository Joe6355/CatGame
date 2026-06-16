using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public class GlobalSavePoint : MonoBehaviour, IInteractable
    {
        [Header("Save Point")]
        [SerializeField, Tooltip("ID глобальной точки сохранения. Рекомендация: bed_home_01, safe_bench_sorting_01. Сохраняется в SaveData.lastGlobalSavePointId.")]
        private string savePointId = "global_save_point_id";

        [SerializeField, Tooltip("Текст действия в подсказке. Рекомендация: 'Сохраниться', 'Отдохнуть', 'Лечь в лежанку'.")]
        private string interactionText = "Сохраниться";

        [SerializeField, Tooltip("Меню слотов сохранений. Рекомендация: назначить SaveSlotsPanel с компонентом SaveSlotsMenuUI.")]
        private SaveSlotsMenuUI saveSlotsMenu;

        public bool CanInteract(Component interactor)
        {
            return saveSlotsMenu != null && SaveManager.Instance != null;
        }

        public string GetInteractionText(Component interactor)
        {
            return interactionText;
        }

        public void Interact(Component interactor)
        {
            if (!CanInteract(interactor))
                return;

            SaveData data = SaveManager.Instance.CurrentData;
            if (data != null)
                data.lastGlobalSavePointId = savePointId;

            saveSlotsMenu.OpenForManualSave();
        }
    }
}
