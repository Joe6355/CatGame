using System.Threading.Tasks;

namespace CatGame.SaveSystem
{
    public interface ISavePreviewStorage
    {
        Task SavePreviewAsync(string slotId, byte[] pngBytes);
        Task<byte[]> LoadPreviewAsync(string slotId);
    }
}
