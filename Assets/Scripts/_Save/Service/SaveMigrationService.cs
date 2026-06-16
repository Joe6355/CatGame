using System;

namespace CatGame.SaveSystem
{
    public static class SaveMigrationService
    {
        public static SaveData MigrateIfNeeded(SaveData data)
        {
            if (data == null)
                throw new Exception("SaveData is null.");

            if (data.saveVersion > SaveConstants.CurrentSaveVersion)
                throw new Exception("Save version is newer than this build supports: " + data.saveVersion);

            if (data.saveVersion <= 0)
                data.saveVersion = 1;

            data.EnsureLists();

            while (data.saveVersion < SaveConstants.CurrentSaveVersion)
            {
                if (data.saveVersion == 1)
                {
                    // Future example: migrate v1 to v2 here.
                    data.saveVersion = 2;
                }
                else
                {
                    throw new Exception("Unknown save version: " + data.saveVersion);
                }
            }

            return data;
        }
    }
}
