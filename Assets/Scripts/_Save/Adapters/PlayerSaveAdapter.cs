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
        [SerializeField, Tooltip("Восстанавливать направление кота при загрузке. Рекомендация: ВЫКЛЮЧЕНО, пока не подключим это напрямую к PlayerMovementModule. Иначе кот может идти спиной.")]
        private bool restoreFacingOnLoad = false;

        [SerializeField, Tooltip("Визуальный корень кота, который зеркалится по X. Рекомендация: назначать только если точно знаешь, какой объект отвечает за поворот спрайта.")]
        private Transform visualRoot;

        [SerializeField, Tooltip("Использовать SpriteRenderer.flipX вместо localScale.x. Рекомендация: выключено, если твой кот поворачивается через scale.")]
        private bool useSpriteRendererFlipX = false;

        [SerializeField, Tooltip("SpriteRenderer кота, если используется flipX. Рекомендация: назначать только если useSpriteRendererFlipX включён.")]
        private SpriteRenderer spriteRenderer;

        [SerializeField, Tooltip("Какой знак localScale.x считается взглядом вправо. Рекомендация: не трогать, пока restoreFacingOnLoad выключен.")]
        private bool positiveScaleMeansFacingRight = true;

        [Header("Physics")]
        [SerializeField, Tooltip("Сбрасывать скорость Rigidbody2D при загрузке/респавне. Рекомендация: включено, чтобы кот не улетал после загрузки.")]
        private bool resetVelocityOnRestore = true;

        private Rigidbody2D rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            if (visualRoot == null)
                visualRoot = transform;

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void CaptureTo(PlayerSaveData data)
        {
            if (data == null)
                return;

            data.position = SaveVector3.FromUnity(transform.position);
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
            if (useSpriteRendererFlipX && spriteRenderer != null)
                return !spriteRenderer.flipX;

            Transform target = visualRoot != null ? visualRoot : transform;
            bool positive = target.localScale.x >= 0f;
            return positiveScaleMeansFacingRight ? positive : !positive;
        }

        private void ApplyFacing(bool facingRight)
        {
            if (useSpriteRendererFlipX && spriteRenderer != null)
            {
                spriteRenderer.flipX = !facingRight;
                return;
            }

            Transform target = visualRoot != null ? visualRoot : transform;
            Vector3 scale = target.localScale;
            float absX = Mathf.Abs(scale.x);
            bool shouldBePositive = positiveScaleMeansFacingRight ? facingRight : !facingRight;
            scale.x = shouldBePositive ? absX : -absX;
            target.localScale = scale;
        }
    }
}