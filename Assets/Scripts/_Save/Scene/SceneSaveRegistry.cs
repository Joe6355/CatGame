using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class SceneSaveRegistry : MonoBehaviour
    {
        [Header("Scene Identity")]
        [Tooltip("ID крупной локации. Рекомендация: trash_chute, sorting_hall, incinerator. Используется в UI слота и аналитике прогресса.")]
        public string locationId = "location_id";

        [Tooltip("ID комнаты/зоны внутри локации. Рекомендация: room_01_entry, room_02_parkour. Показывается в меню сохранений.")]
        public string roomId = "room_id";

        [Tooltip("Человекочитаемое название зоны. Рекомендация: 'Мусоросток — входная шахта'. Можно использовать в UI слота вместо технического ID.")]
        public string displayName = "Location Name";
    }
}
