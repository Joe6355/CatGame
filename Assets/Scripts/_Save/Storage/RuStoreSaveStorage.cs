using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CatGame.SaveSystem
{
    public sealed class RuStoreSaveStorage : ISaveStorage
    {
        public Task<bool> ExistsAsync(string slotId)
        {
            throw new NotImplementedException("RuStoreSaveStorage is a future adapter. Do not mix RuStore SDK calls into SaveManager.");
        }

        public Task<IReadOnlyList<SaveSlotMeta>> GetSlotsMetaAsync()
        {
            throw new NotImplementedException("RuStoreSaveStorage is a future adapter.");
        }

        public Task<SaveData> LoadAsync(string slotId)
        {
            throw new NotImplementedException("RuStoreSaveStorage is a future adapter.");
        }

        public Task SaveAsync(string slotId, SaveData data)
        {
            throw new NotImplementedException("RuStoreSaveStorage is a future adapter.");
        }

        public Task DeleteAsync(string slotId)
        {
            throw new NotImplementedException("RuStoreSaveStorage is a future adapter.");
        }
    }
}
