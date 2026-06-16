namespace CatGame.SaveSystem
{
    public static class SaveSlotMetaFactory
    {
        public static SaveSlotMeta FromSaveData(SaveData data)
        {
            SaveSlotMeta meta = new SaveSlotMeta();
            meta.exists = data != null;

            if (data == null)
                return meta;

            meta.slotId = data.slotId;
            meta.saveVersion = data.saveVersion;
            meta.buildVersion = data.buildVersion;
            meta.platform = data.platform;
            meta.savedUnixTime = data.savedUnixTime;
            meta.savedLocalTimeText = data.savedLocalTimeText;
            meta.playTimeSeconds = data.playTimeSeconds;
            meta.sceneName = data.currentSceneName;
            meta.locationId = data.currentLocationId;
            meta.roomId = data.currentRoomId;
            meta.previewTitle = string.IsNullOrWhiteSpace(data.currentRoomId) ? data.currentSceneName : data.currentRoomId;
            meta.previewIconId = data.currentLocationId;
            return meta;
        }
    }
}
