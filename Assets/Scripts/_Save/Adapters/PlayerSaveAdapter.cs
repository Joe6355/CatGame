using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerSaveAdapter : MonoBehaviour
    {
        [Header("Position")]
        [SerializeField, Tooltip("Сохранять и восстанавливать позицию кота. Рекомендация: включено.")]
        private bool restorePosition = true;

        [SerializeField, Tooltip("Поднять кота чуть выше сохранённой позиции при восстановлении. Рекомендация: 0-0.05, если кот иногда цепляет пол/коллайдер.")]
        private Vector2 restorePositionOffset = Vector2.zero;

        [Header("Facing")]
        [SerializeField, Tooltip("Сохранять направление взгляда кота.")]
        private bool saveFacing = true;

        [SerializeField, Tooltip("Восстанавливать направление кота при загрузке/респавне. Теперь делается через PlayerMovementModule, а не через прямой scale.")]
        private bool restoreFacingOnLoad = true;

        [SerializeField, Tooltip("PlayerMovementModule кота. Если пусто — найдётся автоматически.")]
        private PlayerMovementModule movementModule;

        [SerializeField, Tooltip("Если PlayerMovementModule не найден, использовать запасной флип через localScale. Рекомендация: выключено, если всё настроено нормально.")]
        private bool fallbackScaleFlipIfMovementMissing = false;

        [SerializeField, Tooltip("Визуальный/корневой объект для fallback scale flip. Обычно сам Player.")]
        private Transform fallbackScaleRoot;

        [SerializeField, Tooltip("Какой знак localScale.x считается взглядом вправо для fallback scale flip.")]
        private bool positiveScaleMeansFacingRight = true;

        [Header("Physics")]
        [SerializeField, Tooltip("Сбрасывать скорость Rigidbody2D при загрузке/респавне. Рекомендация: включено.")]
        private bool resetVelocityOnRestore = true;

        private Rigidbody2D rb;

        private void Reset()
        {
            CacheRefs();
        }

        private void Awake()
        {
            CacheRefs();
        }

        private void OnValidate()
        {
            CacheRefs();
        }

        public void CaptureTo(PlayerSaveData data)
        {
            if (data == null)
                return;

            data.position = SaveVector3.FromUnity(transform.position);

            if (saveFacing)
                data.facingRight = IsFacingRight();
        }

        public void RestoreFromData(PlayerSaveData data)
        {
            if (data == null)
                return;

            RestoreToPosition(data.position.ToUnity(), data.facingRight);
        }

        public void RestoreToPosition(Vector3 position, bool facingRight)
        {
            if (restorePosition)
                transform.position = position + new Vector3(restorePositionOffset.x, restorePositionOffset.y, 0f);

            if (restoreFacingOnLoad)
                ApplyFacing(facingRight);

            if (resetVelocityOnRestore && rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        public bool IsFacingRight()
        {
            CacheRefs();

            if (movementModule != null)
                return movementModule.IsFacingRight;

            Transform target = fallbackScaleRoot != null ? fallbackScaleRoot : transform;
            bool positive = target.localScale.x >= 0f;

            return positiveScaleMeansFacingRight ? positive : !positive;
        }

        private void ApplyFacing(bool facingRight)
        {
            CacheRefs();

            if (movementModule != null)
            {
                movementModule.ForceFacing(facingRight);
                return;
            }

            if (!fallbackScaleFlipIfMovementMissing)
                return;

            Transform target = fallbackScaleRoot != null ? fallbackScaleRoot : transform;

            Vector3 scale = target.localScale;
            float absX = Mathf.Abs(scale.x);

            bool shouldBePositive = positiveScaleMeansFacingRight ? facingRight : !facingRight;

            scale.x = shouldBePositive ? absX : -absX;
            target.localScale = scale;
        }

        private void CacheRefs()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody2D>();

            if (movementModule == null)
                movementModule = GetComponent<PlayerMovementModule>();

            if (fallbackScaleRoot == null)
                fallbackScaleRoot = transform;
        }
    }
}
