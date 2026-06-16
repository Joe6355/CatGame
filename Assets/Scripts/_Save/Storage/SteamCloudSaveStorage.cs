using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CatGame.SaveSystem
{
    public sealed class SteamCloudSaveStorage : ISaveStorage
    {
        public Task<bool> ExistsAsync(string slotId)
        {
            throw new NotImplementedException("SteamCloudSaveStorage is a future adapter. Keep SaveData format unchanged and implement Steam file sync here later.");
        }

        public Task<IReadOnlyList<SaveSlotMeta>> GetSlotsMetaAsync()
        {
            throw new NotImplementedException("SteamCloudSaveStorage is a future adapter.");
        }

        public Task<SaveData> LoadAsync(string slotId)
        {
            throw new NotImplementedException("SteamCloudSaveStorage is a future adapter.");
        }

        public Task SaveAsync(string slotId, SaveData data)
        {
            throw new NotImplementedException("SteamCloudSaveStorage is a future adapter.");
        }

        public Task DeleteAsync(string slotId)
        {
            throw new NotImplementedException("SteamCloudSaveStorage is a future adapter.");
        }
    }
}
