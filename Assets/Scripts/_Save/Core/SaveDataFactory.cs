using System;
using UnityEngine;

namespace CatGame.SaveSystem
{
    public static class SaveDataFactory
    {
        public static SaveData CreateNew(string startSceneName)
        {
            SaveData data = new SaveData();
            data.saveVersion = SaveConstants.CurrentSaveVersion;
            data.slotId = SaveConstants.AutoSaveSlotId;
            data.currentSceneName = startSceneName;
            data.platform = Application.platform.ToString();
            data.buildVersion = Application.version;
            data.savedUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            data.savedLocalTimeText = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            data.playTimeSeconds = 0f;
            data.EnsureLists();
            return data;
        }
    }
}
