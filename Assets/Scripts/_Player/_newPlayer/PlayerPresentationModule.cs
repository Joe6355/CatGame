using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerPresentationModule : MonoBehaviour
{
    [Header("UI шкалы прыжка")]
    [SerializeField, Tooltip("Заполняемая часть шкалы заряда прыжка (Image Fill).")]
    private Image jumpBarFill;

    [SerializeField, Tooltip("Фон шкалы заряда прыжка.")]
    private Image jumpBarBG;

    [SerializeField, Tooltip("UI-изображение усталости.")]
    private Image fatigueImage;

    [SerializeField, Tooltip("Камера, по которой считаем позицию UI.")]
    private Camera mainCamera;

    [SerializeField, Tooltip("Canvas, в котором лежит UI шкалы.")]
    private Canvas uiCanvas;

    [SerializeField, Tooltip("Смещение шкалы от позиции игрока в мире.")]
    private Vector3 barOffset = new Vector3(0f, 2f, 0f);

    private void Awake()
    {
        ResolveCamera();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        ResolveCamera();
    }

    public void RefreshPresentation(
        bool isFatigued,
        bool isJumpHoldActive,
        bool isChargingJump,
        float jumpBarNormalized,
        bool isApexThrowAvailable)
    {
        // isApexThrowAvailable оставлен в сигнатуре только для совместимости с PlayerController.
        // Специально нигде не используется: перекраска персонажа отключена.
        UpdateJumpBarVisual(isJumpHoldActive, isChargingJump, jumpBarNormalized);
        UpdateFatigueUI(isFatigued);
        UpdateJumpBarPosition();
    }

    public void ForceRefreshPositionOnly()
    {
        UpdateJumpBarPosition();
    }

    private void ResolveCamera()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void UpdateJumpBarVisual(bool isJumpHoldActive, bool isChargingJump, float normalized)
    {
        float clamped = Mathf.Clamp01(normalized);

        if (jumpBarFill != null)
            jumpBarFill.fillAmount = clamped;

        bool show = clamped > 0f || isChargingJump;

        if (jumpBarFill != null)
            jumpBarFill.enabled = show;

        if (jumpBarBG != null)
            jumpBarBG.enabled = show;
    }

    private void UpdateJumpBarPosition()
    {
        if (jumpBarFill == null && jumpBarBG == null)
            return;

        ResolveCamera();

        if (mainCamera == null || uiCanvas == null)
            return;

        Vector3 worldPos = transform.position + barOffset;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        RectTransform canvasRect = uiCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
            out localPoint);

        if (jumpBarFill != null)
            jumpBarFill.rectTransform.anchoredPosition = localPoint;

        if (jumpBarBG != null)
            jumpBarBG.rectTransform.anchoredPosition = localPoint;
    }

    private void UpdateFatigueUI(bool isFatigued)
    {
        if (fatigueImage == null)
            return;

        float alpha = isFatigued ? 1f : 0f;
        Color c = fatigueImage.color;
        c.a = alpha;
        fatigueImage.color = c;
    }
}