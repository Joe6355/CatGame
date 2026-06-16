using System;

namespace CatGame.SaveSystem
{
    [Serializable]
    public sealed class SaveSlotMeta
    {
        public string slotId = "";
        public bool exists;

        public int saveVersion;
        public string buildVersion = "";
        public string platform = "";

        public long savedUnixTime;
        public string savedLocalTimeText = "";
        public float playTimeSeconds;

        public string sceneName = "";
        public string locationId = "";
        public string roomId = "";

        public string previewTitle = "";
        public string previewIconId = "";
    }
}
