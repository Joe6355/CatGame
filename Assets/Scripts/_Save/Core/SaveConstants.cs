namespace CatGame.SaveSystem
{
    public static class SaveConstants
    {
        public const int CurrentSaveVersion = 1;
        public const string AutoSaveSlotId = "autosave";
        public const int DefaultManualSlotsCount = 2;
        public const string GameFolderName = "CatGame";
        public const string SaveFolderName = "Saves";

        public static bool IsAutoSaveSlot(string slotId)
        {
            return string.Equals(slotId, AutoSaveSlotId, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
