namespace CatGame.SaveSystem
{
    public enum SaveConflictChoice
    {
        None,
        UseLocal,
        UseCloud,
        AskPlayer
    }

    public sealed class SaveConflictResult
    {
        public SaveConflictChoice recommendedChoice;
        public SaveSlotMeta localMeta;
        public SaveSlotMeta cloudMeta;
        public string reason;
    }

    public static class SaveConflictResolver
    {
        public static SaveConflictResult Resolve(SaveSlotMeta local, SaveSlotMeta cloud)
        {
            SaveConflictResult result = new SaveConflictResult();
            result.localMeta = local;
            result.cloudMeta = cloud;

            if (local == null || !local.exists)
            {
                result.recommendedChoice = SaveConflictChoice.UseCloud;
                result.reason = "Local save is missing.";
                return result;
            }

            if (cloud == null || !cloud.exists)
            {
                result.recommendedChoice = SaveConflictChoice.UseLocal;
                result.reason = "Cloud save is missing.";
                return result;
            }

            if (local.savedUnixTime > cloud.savedUnixTime)
            {
                result.recommendedChoice = SaveConflictChoice.AskPlayer;
                result.reason = "Local save is newer.";
                return result;
            }

            if (cloud.savedUnixTime > local.savedUnixTime)
            {
                result.recommendedChoice = SaveConflictChoice.AskPlayer;
                result.reason = "Cloud save is newer.";
                return result;
            }

            result.recommendedChoice = SaveConflictChoice.UseLocal;
            result.reason = "Both saves have the same timestamp.";
            return result;
        }
    }
}
