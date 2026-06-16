using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CheckpointSaveTrigger : MonoBehaviour
    {
        [Header("Checkpoint")]
        [SerializeField, Tooltip("Уникальный ID чекпоинта. Рекомендация: checkpoint_location_room_01, например checkpoint_trash_chute_parkour_start.")]
        private string checkpointId = "checkpoint_id";

        [SerializeField, Tooltip("Точка, куда ставить кота после смерти. Рекомендация: отдельный Transform чуть выше пола, не внутри коллайдера.")]
        private Transform respawnPoint;

        [SerializeField, Tooltip("Направление кота после респавна. Рекомендация: поставить по направлению движения игрока.")]
        private bool facingRightAfterRespawn = true;

        [Header("Behaviour")]
        [SerializeField, Tooltip("Срабатывать только один раз за жизнь сцены. Рекомендация: включено для обычных чекпоинтов, выключено для входа в комнату, если игрок может возвращаться.")]
        private bool triggerOnlyOncePerScene = true;

        [SerializeField, Tooltip("Показывать маленькую надпись/иконку 'Сохранено'. Рекомендация: включено, чтобы игрок понимал, что прогресс зафиксирован.")]
        private bool showSaveIcon = true;

        private bool used;

        private void Reset()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null)
                trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (used && triggerOnlyOncePerScene)
                return;

            PlayerSaveAdapter player = other.GetComponentInParent<PlayerSaveAdapter>();
            if (player == null)
                return;

            used = true;
            Vector3 position = respawnPoint != null ? respawnPoint.position : player.transform.position;

            if (SaveManager.Instance != null)
                SaveManager.Instance.SetCheckpoint(checkpointId, position, facingRightAfterRespawn);

            if (showSaveIcon)
                SaveIconUI.ShowSmallSaved();
        }
    }
}
