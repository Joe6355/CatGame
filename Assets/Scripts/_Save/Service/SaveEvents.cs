using System;

namespace CatGame.SaveSystem
{
    public static class SaveEvents
    {
        public static event Action<string> Saved;
        public static event Action<string> SlotDeleted;
        public static event Action<string> LoadFailed;
        public static event Action<SaveData> Loaded;
        public static event Action<string> CheckpointChanged;

        public static void RaiseSaved(string slotId)
        {
            if (Saved != null) Saved(slotId);
        }

        public static void RaiseSlotDeleted(string slotId)
        {
            if (SlotDeleted != null) SlotDeleted(slotId);
        }

        public static void RaiseLoadFailed(string slotId)
        {
            if (LoadFailed != null) LoadFailed(slotId);
        }

        public static void RaiseLoaded(SaveData data)
        {
            if (Loaded != null) Loaded(data);
        }

        public static void RaiseCheckpointChanged(string checkpointId)
        {
            if (CheckpointChanged != null) CheckpointChanged(checkpointId);
        }
    }
}
