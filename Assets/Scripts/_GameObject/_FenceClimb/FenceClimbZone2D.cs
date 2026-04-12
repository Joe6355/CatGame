using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FenceClimbZone2D : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField, Tooltip("Trigger-коллайдер зоны. Если не задан, берётся Collider2D с этого объекта.")]
    private Collider2D triggerZone;

    [Header("Ограничения движения внутри зоны")]
    [SerializeField, Tooltip("Если ВЫКЛ — движение по X внутри зоны блокируется и игрок остаётся по центру.")]
    private bool allowHorizontalMovement = true;

    [SerializeField, Tooltip("Если ВЫКЛ — движение по Y внутри зоны блокируется и игрок остаётся по центру.")]
    private bool allowVerticalMovement = true;

    [Header("TMP-подсказка взаимодействия")]
    [SerializeField, Tooltip("Если ВКЛ — при подходе к лестнице/забору будет показываться TMP-подсказка.")]
    private bool showInteractionPrompt = true;

    [SerializeField, Tooltip("TMP_Text, куда выводить подсказку.")]
    private TMP_Text interactionPromptText;

    [SerializeField, Tooltip("Текст подсказки.")]
    private string interactionPromptMessage = "нажмите f/x для взаимодействия";

    [SerializeField, Min(0f), Tooltip("Сколько секунд держать подсказку на экране.")]
    private float promptShowDuration = 2f;

    [SerializeField, Min(0f), Tooltip("Если игрок после выхода отошёл от зоны не меньше чем на это расстояние, подсказка сможет показаться снова.")]
    private float promptReshowDistance = 5f;

    [Header("Отладка")]
    [SerializeField, Tooltip("Если ВКЛ — в Scene view рисуется рамка зоны.")]
    private bool drawGizmo = true;

    [SerializeField, Tooltip("Если ВКЛ — gizmo рисуется всегда. Если ВЫКЛ — только когда объект выделен.")]
    private bool drawGizmoAlways = false;

    private readonly HashSet<Collider2D> playerCollidersInside = new HashSet<Collider2D>();

    private Transform trackedPlayerTransform = null;
    private bool playerInside = false;

    private bool promptShownForCurrentApproach = false;
    private float promptHideAt = -999f;

    private void Reset()
    {
        CacheComponents();
        EnsureTrigger();
    }

    private void Awake()
    {
        CacheComponents();
        EnsureTrigger();
        HidePromptImmediate();
    }

    private void OnValidate()
    {
        CacheComponents();
        EnsureTrigger();

        promptShowDuration = Mathf.Max(0f, promptShowDuration);
        promptReshowDistance = Mathf.Max(0f, promptReshowDistance);
    }

    private void Update()
    {
        if (interactionPromptText != null && interactionPromptText.gameObject.activeSelf)
        {
            if (Time.unscaledTime >= promptHideAt)
                HidePromptImmediate();
        }

        if (!playerInside && promptShownForCurrentApproach && trackedPlayerTransform != null)
        {
            Vector2 from = trackedPlayerTransform.position;
            Vector2 to = GetReferencePoint();

            if (Vector2.Distance(from, to) >= promptReshowDistance)
                promptShownForCurrentApproach = false;
        }

        if (trackedPlayerTransform == null && !playerInside)
            promptShownForCurrentApproach = false;
    }

    public Vector2 GetReferencePoint()
    {
        if (triggerZone == null)
            return transform.position;

        return triggerZone.bounds.center;
    }

    public Vector2 ClampPlayerPosition(Vector2 desiredWorldPosition, Vector2 actorExtents, float extraPadding)
    {
        if (triggerZone == null)
            return desiredWorldPosition;

        Bounds bounds = triggerZone.bounds;

        float padX = Mathf.Max(0f, actorExtents.x + extraPadding);
        float padY = Mathf.Max(0f, actorExtents.y + extraPadding);

        float minX = bounds.min.x + padX;
        float maxX = bounds.max.x - padX;
        float minY = bounds.min.y + padY;
        float maxY = bounds.max.y - padY;

        if (!allowHorizontalMovement || minX > maxX)
            desiredWorldPosition.x = bounds.center.x;
        else
            desiredWorldPosition.x = Mathf.Clamp(desiredWorldPosition.x, minX, maxX);

        if (!allowVerticalMovement || minY > maxY)
            desiredWorldPosition.y = bounds.center.y;
        else
            desiredWorldPosition.y = Mathf.Clamp(desiredWorldPosition.y, minY, maxY);

        return desiredWorldPosition;
    }

    public void HidePromptImmediate()
    {
        promptHideAt = -999f;

        if (interactionPromptText != null)
            interactionPromptText.gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandlePlayerEnterOrStay(other, true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandlePlayerEnterOrStay(other, false);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerFenceClimbModule module = other.GetComponentInParent<PlayerFenceClimbModule>();
        if (module == null)
            return;

        if (playerCollidersInside.Contains(other))
            playerCollidersInside.Remove(other);

        playerInside = playerCollidersInside.Count > 0;

        if (!playerInside)
            HidePromptImmediate();

        module.ReportZoneExit(this);
    }

    private void HandlePlayerEnterOrStay(Collider2D other, bool isEnter)
    {
        PlayerFenceClimbModule module = other.GetComponentInParent<PlayerFenceClimbModule>();
        if (module == null)
            return;

        trackedPlayerTransform = module.transform;

        if (isEnter)
            playerCollidersInside.Add(other);
        else if (!playerCollidersInside.Contains(other))
            playerCollidersInside.Add(other);

        playerInside = playerCollidersInside.Count > 0;

        module.ReportZoneStay(this);
        TryShowPrompt(module);
    }

    private void TryShowPrompt(PlayerFenceClimbModule module)
    {
        if (!showInteractionPrompt)
            return;

        if (interactionPromptText == null)
            return;

        if (module == null || module.IsActive)
            return;

        if (promptShownForCurrentApproach)
            return;

        interactionPromptText.text = interactionPromptMessage;
        interactionPromptText.gameObject.SetActive(true);
        promptHideAt = Time.unscaledTime + Mathf.Max(0f, promptShowDuration);
        promptShownForCurrentApproach = true;
    }

    private void CacheComponents()
    {
        if (triggerZone == null)
            triggerZone = GetComponent<Collider2D>();
    }

    private void EnsureTrigger()
    {
        if (triggerZone != null)
            triggerZone.isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        if (drawGizmo && drawGizmoAlways)
            DrawZoneGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawGizmo && !drawGizmoAlways)
            DrawZoneGizmo();
    }

    private void DrawZoneGizmo()
    {
        CacheComponents();
        if (triggerZone == null)
            return;

        Bounds bounds = triggerZone.bounds;

        Gizmos.color = new Color(0.2f, 0.9f, 0.6f, 0.85f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        if (!allowHorizontalMovement || !allowVerticalMovement)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            Gizmos.DrawSphere(bounds.center, 0.06f);
        }
    }
}