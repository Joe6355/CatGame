using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CatGame.SaveSystem
{
    public sealed class UnityCloudSaveStorage : ISaveStorage
    {
        public Task<bool> ExistsAsync(string slotId)
        {
            throw new NotImplementedException("UnityCloudSaveStorage is a future adapter. SaveData JSON should remain the same.");
        }

        public Task<IReadOnlyList<SaveSlotMeta>> GetSlotsMetaAsync()
        {
            throw new NotImplementedException("UnityCloudSaveStorage is a future adapter.");
        }

        public Task<SaveData> LoadAsync(string slotId)
        {
            throw new NotImplementedException("UnityCloudSaveStorage is a future adapter.");
        }

        public Task SaveAsync(string slotId, SaveData data)
        {
            throw new NotImplementedException("UnityCloudSaveStorage is a future adapter.");
        }

        public Task DeleteAsync(string slotId)
        {
            throw new NotImplementedException("UnityCloudSaveStorage is a future adapter.");
        }
    }
}
