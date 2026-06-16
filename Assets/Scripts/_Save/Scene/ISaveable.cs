namespace CatGame.SaveSystem
{
    public interface ISaveable
    {
        string SaveId { get; }
        string CaptureStateJson();
        void RestoreStateJson(string json);
    }
}
