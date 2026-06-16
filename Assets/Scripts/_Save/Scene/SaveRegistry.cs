using System.Collections.Generic;
using UnityEngine;

namespace CatGame.SaveSystem
{
    public static class SaveRegistry
    {
        private static readonly Dictionary<string, ISaveable> saveables = new Dictionary<string, ISaveable>();

        public static IEnumerable<ISaveable> All
        {
            get { return saveables.Values; }
        }

        public static void RefreshSceneRegistry(bool verboseLogs)
        {
            saveables.Clear();
            MonoBehaviour[] behaviours = Object.FindObjectsOfType<MonoBehaviour>(true);

            for (int i = 0; i < behaviours.Length; i++)
            {
                ISaveable saveable = behaviours[i] as ISaveable;
                if (saveable == null)
                    continue;

                if (string.IsNullOrWhiteSpace(saveable.SaveId))
                {
                    Debug.LogError("Saveable without SaveId: " + behaviours[i].name, behaviours[i]);
                    continue;
                }

                if (saveables.ContainsKey(saveable.SaveId))
                {
                    Debug.LogError("Duplicate SaveId: " + saveable.SaveId, behaviours[i]);
                    continue;
                }

                saveables.Add(saveable.SaveId, saveable);
            }

            if (verboseLogs)
                Debug.Log("SaveRegistry refreshed. Saveables: " + saveables.Count);
        }

        public static bool TryGet(string id, out ISaveable saveable)
        {
            return saveables.TryGetValue(id, out saveable);
        }
    }
}
