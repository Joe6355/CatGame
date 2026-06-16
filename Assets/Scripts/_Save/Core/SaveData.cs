using System;
using System.Collections.Generic;

namespace CatGame.SaveSystem
{
    [Serializable]
    public sealed class SaveData
    {
        public int saveVersion = SaveConstants.CurrentSaveVersion;

        public string slotId = "";
        public string buildVersion = "";
        public string platform = "";

        public long savedUnixTime;
        public string savedLocalTimeText = "";
        public float playTimeSeconds;

        public string currentSceneName = "";
        public string currentLocationId = "";
        public string currentRoomId = "";

        public string lastCheckpointId = "";
        public string lastGlobalSavePointId = "";

        public PlayerSaveData player = new PlayerSaveData();

        public int deathsCount;

        public List<string> passedCheckpointIds = new List<string>();
        public List<string> collectedItemIds = new List<string>();
        public List<string> collectedCollectibleIds = new List<string>();
        public List<string> openedDoorIds = new List<string>();
        public List<string> activatedMechanismIds = new List<string>();
        public List<string> watchedCutsceneIds = new List<string>();
        public List<string> disabledOneShotTriggerIds = new List<string>();
        public List<string> openedShortcutIds = new List<string>();
        public List<string> unlockedAbilityIds = new List<string>();

        public List<QuestStateData> quests = new List<QuestStateData>();
        public List<SaveObjectState> sceneObjects = new List<SaveObjectState>();
        public List<NpcStateData> npcStates = new List<NpcStateData>();
        public List<DialogueStateData> dialogueStates = new List<DialogueStateData>();

        public void EnsureLists()
        {
            if (player == null) player = new PlayerSaveData();
            if (passedCheckpointIds == null) passedCheckpointIds = new List<string>();
            if (collectedItemIds == null) collectedItemIds = new List<string>();
            if (collectedCollectibleIds == null) collectedCollectibleIds = new List<string>();
            if (openedDoorIds == null) openedDoorIds = new List<string>();
            if (activatedMechanismIds == null) activatedMechanismIds = new List<string>();
            if (watchedCutsceneIds == null) watchedCutsceneIds = new List<string>();
            if (disabledOneShotTriggerIds == null) disabledOneShotTriggerIds = new List<string>();
            if (openedShortcutIds == null) openedShortcutIds = new List<string>();
            if (unlockedAbilityIds == null) unlockedAbilityIds = new List<string>();
            if (quests == null) quests = new List<QuestStateData>();
            if (sceneObjects == null) sceneObjects = new List<SaveObjectState>();
            if (npcStates == null) npcStates = new List<NpcStateData>();
            if (dialogueStates == null) dialogueStates = new List<DialogueStateData>();
        }
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public SaveVector3 position;
        public bool facingRight = true;

        public bool canWallJump;
        public bool canLedgeClimb;
        public bool canFenceClimb;
        public bool canDoubleJump;
        public bool canDash;
    }

    [Serializable]
    public sealed class QuestStateData
    {
        public string questId = "";
        public string state = "";
        public int stage;
    }

    [Serializable]
    public sealed class NpcStateData
    {
        public string npcId = "";
        public string stateJson = "";
    }

    [Serializable]
    public sealed class DialogueStateData
    {
        public string dialogueId = "";
        public string stateJson = "";
    }
}
