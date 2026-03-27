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

    [Header("Debug: цвет кота во время окна броска вниз")]
    [SerializeField, Tooltip("Если ВКЛ — во время доступности броска после вершины кот временно перекрашивается в debug-цвет.\nПосле выключения окна цвет автоматически возвращается.")]
    private bool debugTintWhenApexThrowAvailable = true;

    [SerializeField, Tooltip("Если ВКЛ — модуль сам найдёт все SpriteRenderer на игроке и дочерних объектах.\nЕсли ВЫКЛ — использует только массив ниже.")]
    private bool autoFindSpriteRenderersForDebugTint = true;

    [SerializeField, Tooltip("Какие SpriteRenderer перекрашивать для отладки.\nМожно оставить пустым, если включён auto-find.")]
    private SpriteRenderer[] apexThrowDebugTintRenderers;

    [SerializeField, Tooltip("В какой цвет временно красить кота, когда окно броска вниз открыто.\nДля теста удобно оставить яркий зелёный.")]
    private Color apexThrowAvailableDebugColor = new Color(0.55f, 1f, 0.55f, 1f);

    private Color[] cachedRendererColors = System.Array.Empty<Color>();
    private bool debugTintApplied = false;

    private void Awake()
    {
        ResolveCamera();
        CacheDebugTintTargets();
        RestoreApexThrowDebugTint();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        ResolveCamera();
        CacheDebugTintTargets();

        if (debugTintApplied)
            ApplyApexThrowDebugTint();
        else
            RestoreApexThrowDebugTint();
    }

    private void OnDisable()
    {
        RestoreApexThrowDebugTint();
    }

    public void RefreshPresentation(
        bool isFatigued,
        bool isJumpHoldActive,
        bool isChargingJump,
        float jumpBarNormalized,
        bool isApexThrowAvailable)
    {
        UpdateJumpBarVisual(isJumpHoldActive, isChargingJump, jumpBarNormalized);
        UpdateFatigueUI(isFatigued);
        UpdateApexThrowDebugTint(isApexThrowAvailable);
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

    private void CacheDebugTintTargets()
    {
        if (autoFindSpriteRenderersForDebugTint)
            apexThrowDebugTintRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (apexThrowDebugTintRenderers == null)
        {
            apexThrowDebugTintRenderers = System.Array.Empty<SpriteRenderer>();
            cachedRendererColors = System.Array.Empty<Color>();
            return;
        }

        cachedRendererColors = new Color[apexThrowDebugTintRenderers.Length];

        for (int i = 0; i < apexThrowDebugTintRenderers.Length; i++)
        {
            SpriteRenderer sr = apexThrowDebugTintRenderers[i];
            cachedRendererColors[i] = sr != null ? sr.color : Color.white;
        }
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

    private void UpdateApexThrowDebugTint(bool isApexThrowAvailable)
    {
        if (!debugTintWhenApexThrowAvailable)
        {
            RestoreApexThrowDebugTint();
            return;
        }

        if (isApexThrowAvailable)
            ApplyApexThrowDebugTint();
        else
            RestoreApexThrowDebugTint();
    }

    private void ApplyApexThrowDebugTint()
    {
        if (apexThrowDebugTintRenderers == null || apexThrowDebugTintRenderers.Length == 0)
            return;

        for (int i = 0; i < apexThrowDebugTintRenderers.Length; i++)
        {
            SpriteRenderer sr = apexThrowDebugTintRenderers[i];
            if (sr == null)
                continue;

            Color tint = apexThrowAvailableDebugColor;
            if (i < cachedRendererColors.Length)
                tint.a = cachedRendererColors[i].a;

            sr.color = tint;
        }

        debugTintApplied = true;
    }

    private void RestoreApexThrowDebugTint()
    {
        if (apexThrowDebugTintRenderers == null || cachedRendererColors == null)
        {
            debugTintApplied = false;
            return;
        }

        int count = Mathf.Min(apexThrowDebugTintRenderers.Length, cachedRendererColors.Length);
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer sr = apexThrowDebugTintRenderers[i];
            if (sr == null)
                continue;

            sr.color = cachedRendererColors[i];
        }

        debugTintApplied = false;
    }
}
