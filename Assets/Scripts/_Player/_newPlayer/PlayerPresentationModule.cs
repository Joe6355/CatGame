using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerPresentationModule : MonoBehaviour
{
    [Header("UI шкалы прыжка")]
    [SerializeField, Tooltip("Заполняемая часть шкалы заряда прыжка (Image Fill).\nРекоменд: Image с Fill Amount, можно оставить пустым если шкала не нужна.")]
    private Image jumpBarFill;

    [SerializeField, Tooltip("Фон шкалы заряда прыжка.\nРекоменд: назначить, если нужен фон (можно пусто).")]
    private Image jumpBarBG;

    [SerializeField, Tooltip("UI-изображение усталости (картинка/иконка), у которого меняется прозрачность.\nРекоменд: назначить Image на Canvas (можно оставить пустым, если не нужно).")]
    private Image fatigueImage;

    [SerializeField, Tooltip("Камера, которая рендерит мир и по которой считаем ScreenPoint.\nРекоменд: Main Camera.")]
    private Camera mainCamera;

    [SerializeField, Tooltip("Canvas, в котором лежит UI шкалы.\nРекоменд: Canvas в режиме Screen Space Overlay или Camera.")]
    private Canvas uiCanvas;

    [SerializeField, Tooltip("Смещение шкалы от позиции игрока в мире (в мировых единицах).\nНапр: (0,2,0) — над головой.\nРекоменд: Y 1.0–2.5 (зависит от размера спрайта).")]
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
        float jumpBarNormalized)
    {
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