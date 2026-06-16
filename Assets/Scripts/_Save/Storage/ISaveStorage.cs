using System.Collections.Generic;
using System.Threading.Tasks;

namespace CatGame.SaveSystem
{
    public interface ISaveStorage
    {
        Task<bool> ExistsAsync(string slotId);
        Task<IReadOnlyList<SaveSlotMeta>> GetSlotsMetaAsync();
        Task<SaveData> LoadAsync(string slotId);
        Task SaveAsync(string slotId, SaveData data);
        Task DeleteAsync(string slotId);
    }
}
