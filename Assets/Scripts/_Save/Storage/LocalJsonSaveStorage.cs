using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace CatGame.SaveSystem
{
    public sealed class LocalJsonSaveStorage : ISaveStorage
    {
        private const string ProjectSavesFolderName = "_CatGame_Saves";

        private const string SaveFileName = "save.json";
        private const string InfoFileName = "info.json";
        private const string BackupFileName = "backup.json";
        private const string SaveTempFileName = "save.tmp";
        private const string InfoTempFileName = "info.tmp";

        private readonly string savesDirectory;
        private readonly string legacySavesDirectory;
        private readonly int manualSlotsCount;

        public LocalJsonSaveStorage(int manualSlotsCount)
        {
            this.manualSlotsCount = Mathf.Max(1, manualSlotsCount);

            savesDirectory = ResolveProjectSavesDirectory();

            legacySavesDirectory = Path.Combine(
                Application.persistentDataPath,
                SaveConstants.GameFolderName,
                SaveConstants.SaveFolderName
            );

            Directory.CreateDirectory(savesDirectory);

            TryMigrateFlatProjectSavesToSlotFolders();
            TryCopyLegacySavesToProjectFolder();

            Debug.Log("[LocalJsonSaveStorage] Saves folder: " + savesDirectory);
        }

        public Task<bool> ExistsAsync(string slotId)
        {
            bool exists =
                !string.IsNullOrEmpty(GetExistingSavePath(slotId)) ||
                !string.IsNullOrEmpty(GetExistingBackupPath(slotId));

            return Task.FromResult(exists);
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
            string path = GetExistingSavePath(slotId);
            string backupPath = GetExistingBackupPath(slotId);

            if (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(backupPath))
                throw new FileNotFoundException("Save slot not found: " + slotId, GetSavePath(slotId));

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    SaveData data = ReadSaveFile(path);
                    return Task.FromResult(SaveMigrationService.MigrateIfNeeded(data));
                }
                catch (Exception firstException)
                {
                    Debug.LogError("Save load failed for slot " + slotId + ". Trying backup. " + firstException);

                    if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
                        throw;
                }
            }

            SaveData backupData = ReadSaveFile(backupPath);
            return Task.FromResult(SaveMigrationService.MigrateIfNeeded(backupData));
        }

        public Task SaveAsync(string slotId, SaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Directory.CreateDirectory(savesDirectory);
            Directory.CreateDirectory(GetSlotDirectory(slotId));

            data.EnsureLists();
            data.slotId = slotId;

            string path = GetSavePath(slotId);
            string tmpPath = GetTempPath(slotId);
            string backupPath = GetBackupPath(slotId);

            string metaPath = GetMetaPath(slotId);
            string metaTmpPath = GetMetaTempPath(slotId);

            DeleteIfExists(tmpPath);
            DeleteIfExists(metaTmpPath);

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(tmpPath, json);

            string existingSavePath = GetExistingSavePath(slotId);
            if (!string.IsNullOrEmpty(existingSavePath) && File.Exists(existingSavePath))
            {
                if (!IsSamePath(existingSavePath, backupPath))
                    File.Copy(existingSavePath, backupPath, true);
            }

            ReplaceFile(tmpPath, path);

            SaveSlotMeta meta = SaveSlotMetaFactory.FromSaveData(data);
            meta.slotId = slotId;
            meta.exists = true;

            string metaJson = JsonUtility.ToJson(meta, true);
            File.WriteAllText(metaTmpPath, metaJson);

            ReplaceFile(metaTmpPath, metaPath);

            DeleteFlatProjectSlotFiles(slotId);

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string slotId)
        {
            DeleteIfExists(GetSavePath(slotId));
            DeleteIfExists(GetMetaPath(slotId));
            DeleteIfExists(GetBackupPath(slotId));
            DeleteIfExists(GetTempPath(slotId));
            DeleteIfExists(GetMetaTempPath(slotId));

            TryDeleteSlotDirectoryIfEmpty(slotId);

            DeleteFlatProjectSlotFiles(slotId);

            DeleteIfExists(GetLegacySavePath(slotId));
            DeleteIfExists(GetLegacyMetaPath(slotId));
            DeleteIfExists(GetLegacyBackupPath(slotId));
            DeleteIfExists(GetLegacyTempPath(slotId));

            return Task.CompletedTask;
        }

        private SaveData ReadSaveFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new FileNotFoundException("Save path is empty.");

            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            if (data == null)
                throw new InvalidDataException("Save JSON produced null SaveData: " + path);

            data.EnsureLists();
            return data;
        }

        private SaveSlotMeta ReadMetaOrEmpty(string slotId)
        {
            string savePath = GetExistingSavePath(slotId);

            if (string.IsNullOrEmpty(savePath) || !File.Exists(savePath))
                return new SaveSlotMeta { slotId = slotId, exists = false };

            string metaPath = GetExistingMetaPath(slotId);

            if (!string.IsNullOrEmpty(metaPath) && File.Exists(metaPath))
            {
                try
                {
                    string json = File.ReadAllText(metaPath);
                    SaveSlotMeta meta = JsonUtility.FromJson<SaveSlotMeta>(json);

                    if (meta == null)
                        meta = new SaveSlotMeta();

                    meta.slotId = slotId;
                    meta.exists = true;

                    return meta;
                }
                catch
                {
                    return BuildMetaFromSaveOrFallback(slotId, savePath);
                }
            }

            return BuildMetaFromSaveOrFallback(slotId, savePath);
        }

        private SaveSlotMeta BuildMetaFromSaveOrFallback(string slotId, string savePath)
        {
            try
            {
                SaveData data = ReadSaveFile(savePath);
                SaveSlotMeta meta = SaveSlotMetaFactory.FromSaveData(data);

                meta.slotId = slotId;
                meta.exists = true;

                return meta;
            }
            catch
            {
                return new SaveSlotMeta
                {
                    slotId = slotId,
                    exists = true
                };
            }
        }

        private static string ResolveProjectSavesDirectory()
        {
            string projectOrBuildRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectOrBuildRoot, ProjectSavesFolderName);
        }

        private void TryMigrateFlatProjectSavesToSlotFolders()
        {
            TryMigrateFlatProjectSlotToFolder(SaveConstants.AutoSaveSlotId);

            for (int i = 1; i <= manualSlotsCount; i++)
                TryMigrateFlatProjectSlotToFolder("slot_" + i);
        }

        private void TryMigrateFlatProjectSlotToFolder(string slotId)
        {
            Directory.CreateDirectory(GetSlotDirectory(slotId));

            TryMoveFileIfTargetMissing(GetFlatProjectSavePath(slotId), GetSavePath(slotId));
            TryMoveFileIfTargetMissing(GetFlatProjectMetaPath(slotId), GetMetaPath(slotId));
            TryMoveFileIfTargetMissing(GetFlatProjectBackupPath(slotId), GetBackupPath(slotId));

            DeleteIfExists(GetFlatProjectTempPath(slotId));
            DeleteIfExists(GetFlatProjectMetaTempPath(slotId));
        }

        private void TryCopyLegacySavesToProjectFolder()
        {
            if (!Directory.Exists(legacySavesDirectory))
                return;

            CopyLegacySlotIfNeeded(SaveConstants.AutoSaveSlotId);

            for (int i = 1; i <= manualSlotsCount; i++)
                CopyLegacySlotIfNeeded("slot_" + i);
        }

        private void CopyLegacySlotIfNeeded(string slotId)
        {
            Directory.CreateDirectory(GetSlotDirectory(slotId));

            CopyFileIfTargetMissing(GetLegacySavePath(slotId), GetSavePath(slotId));
            CopyFileIfTargetMissing(GetLegacyMetaPath(slotId), GetMetaPath(slotId));
            CopyFileIfTargetMissing(GetLegacyBackupPath(slotId), GetBackupPath(slotId));
        }

        private static void TryMoveFileIfTargetMissing(string sourcePath, string targetPath)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath))
                return;

            if (!File.Exists(sourcePath))
                return;

            if (File.Exists(targetPath))
                return;

            string dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            try
            {
                File.Move(sourcePath, targetPath);
            }
            catch (Exception moveException)
            {
                Debug.LogWarning(
                    "[LocalJsonSaveStorage] Could not move old save file. Trying copy. Source: " +
                    sourcePath +
                    " Target: " +
                    targetPath +
                    " Error: " +
                    moveException
                );

                try
                {
                    File.Copy(sourcePath, targetPath, false);
                    File.Delete(sourcePath);
                }
                catch (Exception copyException)
                {
                    Debug.LogError(
                        "[LocalJsonSaveStorage] Could not migrate old save file. Source: " +
                        sourcePath +
                        " Target: " +
                        targetPath +
                        " Error: " +
                        copyException
                    );
                }
            }
        }

        private static void CopyFileIfTargetMissing(string sourcePath, string targetPath)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath))
                return;

            if (!File.Exists(sourcePath))
                return;

            if (File.Exists(targetPath))
                return;

            string dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.Copy(sourcePath, targetPath, false);
        }

        private string GetExistingSavePath(string slotId)
        {
            string folderPath = GetSavePath(slotId);
            if (File.Exists(folderPath))
                return folderPath;

            string flatProjectPath = GetFlatProjectSavePath(slotId);
            if (File.Exists(flatProjectPath))
                return flatProjectPath;

            string legacyPath = GetLegacySavePath(slotId);
            if (File.Exists(legacyPath))
                return legacyPath;

            return string.Empty;
        }

        private string GetExistingMetaPath(string slotId)
        {
            string folderPath = GetMetaPath(slotId);
            if (File.Exists(folderPath))
                return folderPath;

            string flatProjectPath = GetFlatProjectMetaPath(slotId);
            if (File.Exists(flatProjectPath))
                return flatProjectPath;

            string legacyPath = GetLegacyMetaPath(slotId);
            if (File.Exists(legacyPath))
                return legacyPath;

            return string.Empty;
        }

        private string GetExistingBackupPath(string slotId)
        {
            string folderPath = GetBackupPath(slotId);
            if (File.Exists(folderPath))
                return folderPath;

            string flatProjectPath = GetFlatProjectBackupPath(slotId);
            if (File.Exists(flatProjectPath))
                return flatProjectPath;

            string legacyPath = GetLegacyBackupPath(slotId);
            if (File.Exists(legacyPath))
                return legacyPath;

            return string.Empty;
        }

        private string GetSlotDirectory(string slotId)
        {
            return Path.Combine(savesDirectory, GetReadableSlotName(slotId));
        }

        private string GetSavePath(string slotId)
        {
            return Path.Combine(GetSlotDirectory(slotId), SaveFileName);
        }

        private string GetMetaPath(string slotId)
        {
            return Path.Combine(GetSlotDirectory(slotId), InfoFileName);
        }

        private string GetBackupPath(string slotId)
        {
            return Path.Combine(GetSlotDirectory(slotId), BackupFileName);
        }

        private string GetTempPath(string slotId)
        {
            return Path.Combine(GetSlotDirectory(slotId), SaveTempFileName);
        }

        private string GetMetaTempPath(string slotId)
        {
            return Path.Combine(GetSlotDirectory(slotId), InfoTempFileName);
        }

        private string GetFlatProjectSavePath(string slotId)
        {
            return Path.Combine(savesDirectory, GetReadableSlotName(slotId) + ".json");
        }

        private string GetFlatProjectMetaPath(string slotId)
        {
            return Path.Combine(savesDirectory, GetReadableSlotName(slotId) + ".info.json");
        }

        private string GetFlatProjectBackupPath(string slotId)
        {
            return Path.Combine(savesDirectory, GetReadableSlotName(slotId) + ".backup.json");
        }

        private string GetFlatProjectTempPath(string slotId)
        {
            return Path.Combine(savesDirectory, GetReadableSlotName(slotId) + ".tmp");
        }

        private string GetFlatProjectMetaTempPath(string slotId)
        {
            return GetFlatProjectMetaPath(slotId) + ".tmp";
        }

        private string GetLegacySavePath(string slotId)
        {
            return Path.Combine(legacySavesDirectory, slotId + ".json");
        }

        private string GetLegacyMetaPath(string slotId)
        {
            return Path.Combine(legacySavesDirectory, slotId + ".meta.json");
        }

        private string GetLegacyBackupPath(string slotId)
        {
            return Path.Combine(legacySavesDirectory, slotId + ".bak");
        }

        private string GetLegacyTempPath(string slotId)
        {
            return Path.Combine(legacySavesDirectory, slotId + ".tmp");
        }

        private static string GetReadableSlotName(string slotId)
        {
            if (SaveConstants.IsAutoSaveSlot(slotId))
                return "Автосейв";

            if (!string.IsNullOrEmpty(slotId) && slotId.StartsWith("slot_", StringComparison.OrdinalIgnoreCase))
            {
                string numberPart = slotId.Substring("slot_".Length);

                if (int.TryParse(numberPart, out int index))
                    return "Сохранение " + index;
            }

            return MakeSafeFileName(slotId);
        }

        private static string MakeSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Сохранение";

            char[] invalid = Path.GetInvalidFileNameChars();
            string result = value;

            for (int i = 0; i < invalid.Length; i++)
                result = result.Replace(invalid[i], '_');

            return result.Trim();
        }

        private static void ReplaceFile(string sourcePath, string targetPath)
        {
            if (File.Exists(targetPath))
                File.Delete(targetPath);

            File.Move(sourcePath, targetPath);
        }

        private void DeleteFlatProjectSlotFiles(string slotId)
        {
            DeleteIfExists(GetFlatProjectSavePath(slotId));
            DeleteIfExists(GetFlatProjectMetaPath(slotId));
            DeleteIfExists(GetFlatProjectBackupPath(slotId));
            DeleteIfExists(GetFlatProjectTempPath(slotId));
            DeleteIfExists(GetFlatProjectMetaTempPath(slotId));
        }

        private void TryDeleteSlotDirectoryIfEmpty(string slotId)
        {
            string dir = GetSlotDirectory(slotId);

            if (!Directory.Exists(dir))
                return;

            try
            {
                if (Directory.GetFileSystemEntries(dir).Length == 0)
                    Directory.Delete(dir, false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LocalJsonSaveStorage] Could not delete empty slot folder: " + dir + ". " + exception);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }

        private static bool IsSamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;

            string fullA = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullB = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return string.Equals(fullA, fullB, StringComparison.OrdinalIgnoreCase);
        }
    }
}