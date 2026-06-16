using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace CatGame.SaveSystem
{
    public sealed class LocalJsonSaveStorage : ISaveStorage
    {
        private readonly string savesDirectory;
        private readonly int manualSlotsCount;

        public LocalJsonSaveStorage(int manualSlotsCount)
        {
            this.manualSlotsCount = Mathf.Max(1, manualSlotsCount);
            savesDirectory = Path.Combine(Application.persistentDataPath, SaveConstants.GameFolderName, SaveConstants.SaveFolderName);
            Directory.CreateDirectory(savesDirectory);
        }

        public Task<bool> ExistsAsync(string slotId)
        {
            return Task.FromResult(File.Exists(GetSavePath(slotId)));
        }

        public Task<IReadOnlyList<SaveSlotMeta>> GetSlotsMetaAsync()
        {
            List<SaveSlotMeta> result = new List<SaveSlotMeta>();
            result.Add(ReadMetaOrEmpty(SaveConstants.AutoSaveSlotId));

            for (int i = 1; i <= manualSlotsCount; i++)
                result.Add(ReadMetaOrEmpty("slot_" + i));

            return Task.FromResult<IReadOnlyList<SaveSlotMeta>>(result);
        }

        public Task<SaveData> LoadAsync(string slotId)
        {
            string path = GetSavePath(slotId);
            string backupPath = GetBackupPath(slotId);

            if (!File.Exists(path) && !File.Exists(backupPath))
                throw new FileNotFoundException("Save slot not found: " + slotId, path);

            try
            {
                SaveData data = ReadSaveFile(path);
                return Task.FromResult(SaveMigrationService.MigrateIfNeeded(data));
            }
            catch (Exception firstException)
            {
                Debug.LogError("Save load failed for slot " + slotId + ". Trying backup. " + firstException);

                if (!File.Exists(backupPath))
                    throw;

                SaveData backupData = ReadSaveFile(backupPath);
                return Task.FromResult(SaveMigrationService.MigrateIfNeeded(backupData));
            }
        }

        public Task SaveAsync(string slotId, SaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data.EnsureLists();
            data.slotId = slotId;

            string path = GetSavePath(slotId);
            string tmpPath = GetTempPath(slotId);
            string backupPath = GetBackupPath(slotId);
            string metaPath = GetMetaPath(slotId);
            string metaTmpPath = metaPath + ".tmp";

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(tmpPath, json);

            if (File.Exists(path))
                File.Copy(path, backupPath, true);

            if (File.Exists(path))
                File.Delete(path);

            File.Move(tmpPath, path);

            SaveSlotMeta meta = SaveSlotMetaFactory.FromSaveData(data);
            string metaJson = JsonUtility.ToJson(meta, true);
            File.WriteAllText(metaTmpPath, metaJson);

            if (File.Exists(metaPath))
                File.Delete(metaPath);

            File.Move(metaTmpPath, metaPath);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string slotId)
        {
            DeleteIfExists(GetSavePath(slotId));
            DeleteIfExists(GetMetaPath(slotId));
            DeleteIfExists(GetBackupPath(slotId));
            DeleteIfExists(GetTempPath(slotId));
            return Task.CompletedTask;
        }

        private SaveData ReadSaveFile(string path)
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            if (data == null)
                throw new InvalidDataException("Save JSON produced null SaveData: " + path);

            data.EnsureLists();
            return data;
        }

        private SaveSlotMeta ReadMetaOrEmpty(string slotId)
        {
            string metaPath = GetMetaPath(slotId);
            bool saveExists = File.Exists(GetSavePath(slotId));

            if (!saveExists || !File.Exists(metaPath))
                return new SaveSlotMeta { slotId = slotId, exists = false };

            try
            {
                string json = File.ReadAllText(metaPath);
                SaveSlotMeta meta = JsonUtility.FromJson<SaveSlotMeta>(json);
                if (meta == null)
                    meta = new SaveSlotMeta();

                meta.slotId = slotId;
                meta.exists = saveExists;
                return meta;
            }
            catch
            {
                return new SaveSlotMeta { slotId = slotId, exists = false };
            }
        }

        private string GetSavePath(string slotId)
        {
            return Path.Combine(savesDirectory, slotId + ".json");
        }

        private string GetMetaPath(string slotId)
        {
            return Path.Combine(savesDirectory, slotId + ".meta.json");
        }

        private string GetBackupPath(string slotId)
        {
            return Path.Combine(savesDirectory, slotId + ".bak");
        }

        private string GetTempPath(string slotId)
        {
            return Path.Combine(savesDirectory, slotId + ".tmp");
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
