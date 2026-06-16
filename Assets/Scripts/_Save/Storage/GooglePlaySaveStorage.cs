using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CatGame.SaveSystem
{
    public sealed class GooglePlaySaveStorage : ISaveStorage
    {
        public Task<bool> ExistsAsync(string slotId)
        {
            throw new NotImplementedException("GooglePlaySaveStorage is a future adapter. It should send the same SaveData JSON as bytes/string to Google Play Saved Games later.");
        }

        public Task<IReadOnlyList<SaveSlotMeta>> GetSlotsMetaAsync()
        {
            throw new NotImplementedException("GooglePlaySaveStorage is a future adapter.");
        }

        public Task<SaveData> LoadAsync(string slotId)
        {
            throw new NotImplementedException("GooglePlaySaveStorage is a future adapter.");
        }

        public Task SaveAsync(string slotId, SaveData data)
        {
            throw new NotImplementedException("GooglePlaySaveStorage is a future adapter.");
        }

        public Task DeleteAsync(string slotId)
        {
            throw new NotImplementedException("GooglePlaySaveStorage is a future adapter.");
        }
    }
}
