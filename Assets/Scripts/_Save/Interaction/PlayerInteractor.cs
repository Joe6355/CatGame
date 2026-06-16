using TMPro;
using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField, Tooltip("Компонент, который читает действие Interact из текущего ребинда. Рекомендация: повесить CatRebindInteractionInput на кота и назначить сюда.")]
        private CatRebindInteractionInput input;

        [Header("Search")]
        [SerializeField, Tooltip("Радиус поиска интерактивных объектов вокруг кота. Рекомендация: 1.0-1.4 для 2D платформера.")]
        private float interactRadius = 1.2f;

        [SerializeField, Tooltip("Слои интерактивных объектов: лавочки, лежанки, рычаги, двери. Рекомендация: создать отдельный Layer Interactable.")]
        private LayerMask interactableMask = ~0;

        [SerializeField, Tooltip("Точка, откуда искать объекты. Рекомендация: центр кота или отдельный Transform у груди/головы кота.")]
        private Transform searchOrigin;

        [Header("Prompt UI")]
        [SerializeField, Tooltip("Корневой объект подсказки взаимодействия. Рекомендация: маленькая UI-панель рядом с котом или в HUD.")]
        private GameObject promptRoot;

        [SerializeField, Tooltip("Текст подсказки. Рекомендация: TextMeshProUGUI с форматом '[F / Joy2] Сохраниться'.")]
        private TextMeshProUGUI promptText;

        [SerializeField, Tooltip("Формат подсказки. {0} = кнопка из ребинда, {1} = действие объекта. Рекомендация: '[{0}] {1}'.")]
        private string promptFormat = "[{0}] {1}";

        private IInteractable currentInteractable;
        private Component currentInteractableComponent;

        private void Awake()
        {
            if (input == null)
                input = GetComponent<CatRebindInteractionInput>();

            if (searchOrigin == null)
                searchOrigin = transform;
        }

        private void Update()
        {
            FindNearestInteractable();
            RefreshPrompt();

            if (currentInteractable != null && input != null && input.WasInteractPressedThisFrame())
                currentInteractable.Interact(this);
        }

        private void FindNearestInteractable()
        {
            currentInteractable = null;
            currentInteractableComponent = null;

            Vector3 origin = searchOrigin != null ? searchOrigin.position : transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, interactRadius, interactableMask);

            float bestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                    continue;

                MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
                for (int j = 0; j < behaviours.Length; j++)
                {
                    IInteractable interactable = behaviours[j] as IInteractable;
                    if (interactable == null || !interactable.CanInteract(this))
                        continue;

                    float distance = Vector2.Distance(origin, behaviours[j].transform.position);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        currentInteractable = interactable;
                        currentInteractableComponent = behaviours[j];
                    }
                }
            }
        }

        private void RefreshPrompt()
        {
            bool hasPrompt = currentInteractable != null && input != null;

            if (promptRoot != null)
                promptRoot.SetActive(hasPrompt);

            if (!hasPrompt || promptText == null)
                return;

            string binding = input.GetInteractBindingDisplayName();
            string action = currentInteractable.GetInteractionText(this);
            promptText.text = string.Format(promptFormat, binding, action);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform originTransform = searchOrigin != null ? searchOrigin : transform;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(originTransform.position, interactRadius);
        }
#endif
    }
}
