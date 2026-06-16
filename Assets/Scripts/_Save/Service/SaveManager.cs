using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Header("Storage")]
        [SerializeField, Tooltip("Количество ручных слотов, которые будут показаны в меню. Рекомендация на старт: 2, как в текущей заготовке меню. Позже можно увеличить без переписывания сохранений.")]
        private int manualSlotsCount = SaveConstants.DefaultManualSlotsCount;

        [Header("New Game")]
        [SerializeField, Tooltip("Имя стартовой сцены для новой игры. Рекомендация: указать первую игровую сцену с котом, не Main Menu.")]
        private string defaultStartSceneName = "SampleScene";

        [SerializeField, Tooltip("ID стартового SpawnPoint для новой игры. Рекомендация: start. На стартовом объекте SpawnPoint укажи такой же spawnId.")]
        private string defaultStartSpawnId = "start";

        [Header("Autosave")]
        [SerializeField, Tooltip("Минимальная пауза между автосейвами в секундах. Рекомендация: 5-10 секунд, чтобы не дергать диск при каждом триггере.")]
        private float autosaveCooldownSeconds = 5f;

        [SerializeField, Tooltip("Писать автосейв сразу при значимом изменении. Рекомендация: включено. Cooldown все равно защищает от частой записи.")]
        private bool saveAutosaveAfterDirtyState = true;

        [Header("Debug")]
        [SerializeField, Tooltip("Писать подробные сообщения в Console. Рекомендация: включить на этапе подключения, выключить перед билдом.")]
        private bool verboseLogs = true;

        private ISaveStorage storage;
        private SaveData currentData;
        private bool hasDirtyState;
        private bool isSaving;
        private float lastAutosaveRealtime = -999f;

        public SaveData CurrentData
        {
            get { return currentData; }
        }

        public int ManualSlotsCount
        {
            get { return Mathf.Max(1, manualSlotsCount); }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            storage = new LocalJsonSaveStorage(ManualSlotsCount);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            if (currentData != null)
                currentData.playTimeSeconds += Time.unscaledDeltaTime;
        }

        public Task<IReadOnlyList<SaveSlotMeta>> GetSlotsMetaAsync()
        {
            return storage.GetSlotsMetaAsync();
        }

        public async void NewGameFromInspectorButton()
        {
            await NewGameAsync(defaultStartSceneName, defaultStartSpawnId);
        }

        public async Task NewGameAsync(string startSceneName, string spawnId)
        {
            currentData = SaveDataFactory.CreateNew(startSceneName);
            currentData.currentSceneName = startSceneName;

            await LoadSceneAsync(startSceneName);

            SpawnPoint spawnPoint = SpawnPoint.FindById(spawnId);
            PlayerSaveAdapter player = FindObjectOfType<PlayerSaveAdapter>();

            if (player != null && spawnPoint != null)
                player.RestoreToPosition(spawnPoint.transform.position, spawnPoint.facingRight);

            CaptureSceneState(currentData);
            StampSave(currentData, SaveConstants.AutoSaveSlotId);
            await SaveToSlotAsync(SaveConstants.AutoSaveSlotId, true);
        }

        public async Task<bool> ContinueLatestAsync()
        {
            IReadOnlyList<SaveSlotMeta> metas = await storage.GetSlotsMetaAsync();
            SaveSlotMeta best = null;

            for (int i = 0; i < metas.Count; i++)
            {
                SaveSlotMeta meta = metas[i];
                if (meta == null || !meta.exists)
                    continue;

                if (best == null || meta.savedUnixTime > best.savedUnixTime)
                    best = meta;
            }

            if (best == null)
                return false;

            await LoadSlotAsync(best.slotId);
            return true;
        }

        public async Task SaveToSlotAsync(string slotId, bool force = false)
        {
            if (isSaving)
                return;

            if (currentData == null)
                currentData = SaveDataFactory.CreateNew(SceneManager.GetActiveScene().name);

            isSaving = true;

            try
            {
                CaptureSceneState(currentData);
                StampSave(currentData, slotId);
                await storage.SaveAsync(slotId, currentData);
                hasDirtyState = false;

                if (verboseLogs)
                    Debug.Log("Saved slot: " + slotId);

                SaveEvents.RaiseSaved(slotId);
            }
            catch (Exception ex)
            {
                Debug.LogError("Save failed for slot " + slotId + ": " + ex);
                throw;
            }
            finally
            {
                isSaving = false;
            }
        }

        public async Task SaveAutoAsync(bool force = false)
        {
            if (!force && !saveAutosaveAfterDirtyState)
                return;

            if (!force && !hasDirtyState)
                return;

            if (!force && Time.unscaledTime - lastAutosaveRealtime < autosaveCooldownSeconds)
                return;

            lastAutosaveRealtime = Time.unscaledTime;
            await SaveToSlotAsync(SaveConstants.AutoSaveSlotId, force);
        }

        public async Task LoadSlotAsync(string slotId)
        {
            try
            {
                SaveData loaded = await storage.LoadAsync(slotId);
                currentData = loaded;
                await LoadSceneAsync(loaded.currentSceneName);
                RestoreSceneState(loaded);
                SaveEvents.RaiseLoaded(loaded);
            }
            catch (Exception ex)
            {
                Debug.LogError("Load failed for slot " + slotId + ": " + ex);
                SaveEvents.RaiseLoadFailed(slotId);
                throw;
            }
        }

        public async Task DeleteSlotAsync(string slotId)
        {
            await storage.DeleteAsync(slotId);
            SaveEvents.RaiseSlotDeleted(slotId);
        }

        public void MarkDirty()
        {
            hasDirtyState = true;
        }

        public async void MarkDirtyAndAutosave()
        {
            MarkDirty();
            await SaveAutoAsync(false);
        }

        public async void SetCheckpoint(string checkpointId, Vector3 position, bool facingRight)
        {
            if (currentData == null)
                currentData = SaveDataFactory.CreateNew(SceneManager.GetActiveScene().name);

            currentData.lastCheckpointId = checkpointId;
            currentData.player.position = SaveVector3.FromUnity(position);
            currentData.player.facingRight = facingRight;

            if (!currentData.passedCheckpointIds.Contains(checkpointId))
                currentData.passedCheckpointIds.Add(checkpointId);

            MarkDirty();
            SaveEvents.RaiseCheckpointChanged(checkpointId);
            await SaveAutoAsync(false);
        }

        public void RegisterDeathAndRespawn()
        {
            if (currentData == null)
                return;

            currentData.deathsCount++;
            RespawnAtLastCheckpoint();
            MarkDirtyAndAutosave();
        }

        public void RespawnAtLastCheckpoint()
        {
            if (currentData == null)
                return;

            PlayerSaveAdapter player = FindObjectOfType<PlayerSaveAdapter>();
            if (player != null)
                player.RestoreFromData(currentData.player);
        }

        private void CaptureSceneState(SaveData data)
        {
            if (data == null)
                return;

            data.EnsureLists();
            data.currentSceneName = SceneManager.GetActiveScene().name;

            SceneSaveRegistry sceneRegistry = FindObjectOfType<SceneSaveRegistry>();
            if (sceneRegistry != null)
            {
                data.currentLocationId = sceneRegistry.locationId;
                data.currentRoomId = sceneRegistry.roomId;
            }

            PlayerSaveAdapter player = FindObjectOfType<PlayerSaveAdapter>();
            if (player != null)
                player.CaptureTo(data.player);

            SaveRegistry.RefreshSceneRegistry(verboseLogs);
            data.sceneObjects.Clear();

            foreach (ISaveable saveable in SaveRegistry.All)
            {
                SaveObjectState state = new SaveObjectState();
                state.saveId = saveable.SaveId;
                state.type = saveable.GetType().Name;
                state.json = saveable.CaptureStateJson();
                data.sceneObjects.Add(state);
            }
        }

        private void RestoreSceneState(SaveData data)
        {
            if (data == null)
                return;

            data.EnsureLists();
            SaveRegistry.RefreshSceneRegistry(verboseLogs);

            PlayerSaveAdapter player = FindObjectOfType<PlayerSaveAdapter>();
            if (player != null)
                player.RestoreFromData(data.player);

            for (int i = 0; i < data.sceneObjects.Count; i++)
            {
                SaveObjectState state = data.sceneObjects[i];
                if (state == null || string.IsNullOrWhiteSpace(state.saveId))
                    continue;

                ISaveable saveable;
                if (SaveRegistry.TryGet(state.saveId, out saveable))
                    saveable.RestoreStateJson(state.json);
                else if (verboseLogs)
                    Debug.LogWarning("Saved object not found in scene: " + state.saveId);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SaveRegistry.RefreshSceneRegistry(false);
        }

        private static async Task LoadSceneAsync(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            while (operation != null && !operation.isDone)
                await Task.Yield();
        }

        private static void StampSave(SaveData data, string slotId)
        {
            data.slotId = slotId;
            data.saveVersion = SaveConstants.CurrentSaveVersion;
            data.platform = Application.platform.ToString();
            data.buildVersion = Application.version;
            data.savedUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            data.savedLocalTimeText = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        }
    }
}
