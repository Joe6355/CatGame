using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class SpawnPoint : MonoBehaviour
    {
        [Tooltip("ID точки появления. Рекомендация: start для новой игры, checkpoint_xxx для контрольных точек.")]
        public string spawnId = "start";

        [Tooltip("Куда должен смотреть кот после появления. Рекомендация: включено, если старт направлен вправо.")]
        public bool facingRight = true;

        public static SpawnPoint FindById(string id)
        {
            SpawnPoint[] points = Object.FindObjectsOfType<SpawnPoint>(true);
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].spawnId == id)
                    return points[i];
            }

            return null;
        }
    }
}
