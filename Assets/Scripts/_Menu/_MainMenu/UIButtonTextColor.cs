using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIButtonTextColor : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerClickHandler,
    ISelectHandler,
    IDeselectHandler
{
    private static readonly HashSet<UIButtonTextColor> All = new HashSet<UIButtonTextColor>();
    private static readonly List<UIButtonTextColor> RefreshBuffer = new List<UIButtonTextColor>();

    private static bool hasMousePosition;
    private static Vector2 lastMousePosition;
    private static bool mouseMovedThisFrame;
    private static bool mouseButtonUsedThisFrame;
    private static int mouseStateFrame = -1;

    private static int lastManualClickFrame = -1000;

    public static void SetSuppressPointerHoverVisuals(bool value)
    {
        RefreshAll();
    }

    private static void RefreshAll()
    {
        RefreshBuffer.Clear();
        RefreshBuffer.AddRange(All);

        for (int i = 0; i < RefreshBuffer.Count; i++)
        {
            UIButtonTextColor item = RefreshBuffer[i];

            if (item != null && item.isActiveAndEnabled)
                item.RefreshColor();
        }
    }

    private static void UpdateGlobalMouseState(float threshold)
    {
        if (mouseStateFrame == Time.frameCount)
            return;

        Vector2 currentMousePosition = Input.mousePosition;

        if (!hasMousePosition)
        {
            hasMousePosition = true;
            lastMousePosition = currentMousePosition;
            mouseMovedThisFrame = false;
        }
        else
        {
            float sqrDistance = (currentMousePosition - lastMousePosition).sqrMagnitude;
            mouseMovedThisFrame = sqrDistance > threshold * threshold;
            lastMousePosition = currentMousePosition;
        }

        mouseButtonUsedThisFrame =
            Input.GetMouseButtonDown(0) ||
            Input.GetMouseButtonDown(1) ||
            Input.GetMouseButtonDown(2) ||
            Input.GetMouseButtonUp(0) ||
            Input.GetMouseButtonUp(1) ||
            Input.GetMouseButtonUp(2);

        mouseStateFrame = Time.frameCount;
    }

    [Header("Target")]
    [Tooltip("UI-элемент, который нужно перекрашивать. Можно указать TMP_Text, Image или любой Graphic.")]
    public Graphic targetText;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color highlightedColor = Color.yellow;
    public Color pressedColor = Color.gray;
    public Color selectedColor = Color.white;

    [Header("Effects Toggle")]
    public bool enableEffects = true;

    [Header("Selected State")]
    public bool useSelectedColor = true;

    [Header("Mouse Fix")]
    [Tooltip("Если ВКЛ — наведение мышью сразу делает кнопку selected в EventSystem.")]
    public bool selectButtonOnMouseHover = true;

    [Tooltip("Если ВКЛ — скрипт сам проверяет мышь внутри RectTransform кнопки. Нужно для текущей структуры UI.")]
    public bool useMouseRectFallback = true;

    [Tooltip("Если ВКЛ — при MouseUp скрипт сам вызовет onClick, если штатный Button.onClick не сработал.")]
    public bool manualClickFallback = true;

    [Tooltip("Если ВКЛ — клик по дочернему TMP/Text/Image будет прокинут в родительский Button.")]
    public bool forwardChildClickToOwnerButton = true;

    [Tooltip("Минимальное движение мыши, после которого мышь считается активной.")]
    public float mouseMoveThreshold = 0.05f;

    [Header("CanvasGroup Gate")]
    [Tooltip("Если ВКЛ — кнопка не реагирует, когда один из родительских CanvasGroup выключил interactable/blocksRaycasts или alpha = 0.")]
    public bool respectParentCanvasGroups = true;

    [Header("Reset")]
    public bool resetColorOnEnable = true;
    public bool resetColorOnDisable = true;
    public bool clearEventSystemSelectionOnDisable = true;

    [Header("Bounce Settings")]
    [Tooltip("Если на кнопке уже есть Animator — лучше оставить false.")]
    public bool enableBounce = false;

    [Tooltip("Что двигать при bounce. Лучше отдельный дочерний VisualRoot, а не саму кнопку.")]
    public RectTransform bounceTarget;

    public float downOffset = 5f;
    public float upOffset = 8f;
    public float duration = 0.15f;

    private Button ownerButton;
    private RectTransform ownerRect;
    private RectTransform targetRect;
    private Canvas ownerCanvas;

    private bool isPointerInside;
    private bool isPointerPressed;
    private bool fallbackMousePressedInside;

    private int lastNativePointerClickFrame = -1000;
    private int lastOwnerButtonClickFrame = -1000;

    private Coroutine bounceCoroutine;
    private Vector2 bounceBasePosition;
    private bool bounceChangedPosition;

    private void Reset()
    {
        targetText = FindDefaultTargetGraphic();

        ownerButton = GetComponentInParent<Button>();
        ownerRect = ownerButton != null ? ownerButton.GetComponent<RectTransform>() : GetComponent<RectTransform>();
        targetRect = GetComponent<RectTransform>();
        bounceTarget = targetRect;
    }

    private void Awake()
    {
        ownerButton = GetComponentInParent<Button>();
        ownerRect = ownerButton != null ? ownerButton.GetComponent<RectTransform>() : GetComponent<RectTransform>();
        ownerCanvas = GetComponentInParent<Canvas>();

        if (targetText == null)
            targetText = FindDefaultTargetGraphic();

        targetRect = GetComponent<RectTransform>();

        if (bounceTarget == null)
            bounceTarget = targetRect;
    }

    private Graphic FindDefaultTargetGraphic()
    {
        TMP_Text tmp = GetComponentInChildren<TMP_Text>(true);

        if (tmp != null)
            return tmp;

        Image image = GetComponentInChildren<Image>(true);

        if (image != null)
            return image;

        return GetComponentInChildren<Graphic>(true);
    }

    private void OnEnable()
    {
        All.Add(this);

        if (ownerButton != null)
            ownerButton.onClick.AddListener(RecordOwnerButtonClick);

        isPointerInside = false;
        isPointerPressed = false;
        fallbackMousePressedInside = false;

        StopBounceAndRestore();

        if (resetColorOnEnable)
            SetColor(normalColor);

        RefreshAll();
    }

    private void OnDisable()
    {
        if (ownerButton != null)
            ownerButton.onClick.RemoveListener(RecordOwnerButtonClick);

        isPointerInside = false;
        isPointerPressed = false;
        fallbackMousePressedInside = false;

        StopBounceAndRestore();

        if (resetColorOnDisable)
            SetColor(normalColor);

        if (clearEventSystemSelectionOnDisable)
            ClearSelectionIfNeeded();

        All.Remove(this);

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (ownerButton != null)
            ownerButton.onClick.RemoveListener(RecordOwnerButtonClick);

        All.Remove(this);
    }

    private void Update()
    {
        if (!enableEffects)
            return;

        if (!useMouseRectFallback)
            return;

        if (!CanOwnerButtonBeSelected())
            return;

        UpdateGlobalMouseState(mouseMoveThreshold);

        bool mouseInside = IsMouseInsideOwnerRect();

        if ((mouseMovedThisFrame || mouseButtonUsedThisFrame) && mouseInside)
        {
            isPointerInside = true;

            if (selectButtonOnMouseHover)
                SelectOwnerButton();

            RefreshAll();
        }

        if (mouseMovedThisFrame && !mouseInside && isPointerInside)
        {
            isPointerInside = false;
            isPointerPressed = false;
            RefreshAll();
        }

        if (Input.GetMouseButtonDown(0))
        {
            fallbackMousePressedInside = mouseInside;

            if (mouseInside)
            {
                isPointerPressed = true;

                UIButtonClickSFXTarget clickSfxTarget =
                    ownerButton.GetComponent<UIButtonClickSFXTarget>();

                if (clickSfxTarget != null)
                    clickSfxTarget.PlayFromManualPointerDown();

                if (selectButtonOnMouseHover)
                    SelectOwnerButton();

                RefreshAll();
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            bool shouldManualClick =
                manualClickFallback &&
                fallbackMousePressedInside &&
                mouseInside &&
                CanOwnerButtonBeSelected() &&
                IsOwnerSelectedByEventSystem();

            fallbackMousePressedInside = false;
            isPointerPressed = false;

            RefreshAll();

            if (shouldManualClick)
                StartCoroutine(ManualClickAtEndOfFrame(Time.frameCount));
        }
    }

    private void RecordOwnerButtonClick()
    {
        lastOwnerButtonClickFrame = Time.frameCount;
    }

    private IEnumerator ManualClickAtEndOfFrame(int capturedFrame)
    {
        yield return new WaitForEndOfFrame();

        if (!CanOwnerButtonBeSelected())
            yield break;

        if (!IsOwnerSelectedByEventSystem())
            yield break;

        if (!IsMouseInsideOwnerRect())
            yield break;

        if (lastOwnerButtonClickFrame == capturedFrame)
            yield break;

        if (lastNativePointerClickFrame == capturedFrame)
            yield break;

        if (lastManualClickFrame == capturedFrame)
            yield break;

        lastManualClickFrame = capturedFrame;

        ownerButton.onClick.Invoke();
    }

    private Camera GetEventCamera()
    {
        if (ownerCanvas == null)
            return null;

        if (ownerCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (ownerCanvas.worldCamera != null)
            return ownerCanvas.worldCamera;

        return Camera.main;
    }

    private bool IsMouseInsideOwnerRect()
    {
        if (ownerRect == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            ownerRect,
            Input.mousePosition,
            GetEventCamera()
        );
    }

    private bool IsAllowedByParentCanvasGroups()
    {
        if (!respectParentCanvasGroups)
            return true;

        if (ownerRect == null)
            return true;

        Transform current = ownerRect.transform;

        while (current != null)
        {
            CanvasGroup[] groups = current.GetComponents<CanvasGroup>();

            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];

                if (group == null || !group.enabled)
                    continue;

                if (!group.interactable)
                    return false;

                if (!group.blocksRaycasts)
                    return false;

                if (group.alpha <= 0.001f)
                    return false;

                if (group.ignoreParentGroups)
                    return true;
            }

            current = current.parent;
        }

        return true;
    }

    private void SetColor(Color color)
    {
        if (targetText != null)
            targetText.color = color;
    }

    private Transform GetOwnerRoot()
    {
        if (ownerButton != null)
            return ownerButton.transform;

        return transform;
    }

    private GameObject GetOwnerSelectedObject()
    {
        if (ownerButton != null)
            return ownerButton.gameObject;

        return gameObject;
    }

    private bool IsOwnerSelectedByEventSystem()
    {
        if (EventSystem.current == null)
            return false;

        GameObject selected = EventSystem.current.currentSelectedGameObject;

        if (selected == null)
            return false;

        Transform ownerRoot = GetOwnerRoot();

        if (selected.transform == ownerRoot)
            return true;

        return selected.transform.IsChildOf(ownerRoot);
    }

    private bool CanOwnerButtonBeSelected()
    {
        if (ownerButton == null)
            return false;

        if (!ownerButton.isActiveAndEnabled)
            return false;

        if (!ownerButton.interactable)
            return false;

        if (EventSystem.current == null)
            return false;

        if (!IsAllowedByParentCanvasGroups())
            return false;

        return true;
    }

    private void SelectOwnerButton()
    {
        if (!CanOwnerButtonBeSelected())
            return;

        GameObject ownerObject = GetOwnerSelectedObject();

        if (EventSystem.current.currentSelectedGameObject == ownerObject)
            return;

        EventSystem.current.SetSelectedGameObject(ownerObject);

        RefreshAll();
    }

    private void RefreshColor()
    {
        if (!enableEffects)
        {
            SetColor(normalColor);
            return;
        }

        if (!CanOwnerButtonBeSelected())
        {
            SetColor(normalColor);
            return;
        }

        bool ownerIsSelected = IsOwnerSelectedByEventSystem();

        if (!ownerIsSelected)
        {
            SetColor(normalColor);
            return;
        }

        if (isPointerPressed)
        {
            SetColor(pressedColor);
            return;
        }

        if (isPointerInside)
        {
            SetColor(highlightedColor);
            return;
        }

        if (useSelectedColor)
        {
            SetColor(selectedColor);
            return;
        }

        SetColor(normalColor);
    }

    private bool IsRaycastInsideOwner(PointerEventData eventData)
    {
        if (eventData == null)
            return false;

        GameObject raycastObject = eventData.pointerCurrentRaycast.gameObject;

        if (raycastObject == null)
            return false;

        Transform ownerRoot = GetOwnerRoot();

        if (raycastObject.transform == ownerRoot)
            return true;

        return raycastObject.transform.IsChildOf(ownerRoot);
    }

    private void ClearSelectionIfNeeded()
    {
        if (EventSystem.current == null)
            return;

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

        if (selectedObject == null)
            return;

        Transform ownerRoot = GetOwnerRoot();

        if (selectedObject.transform == ownerRoot || selectedObject.transform.IsChildOf(ownerRoot))
            EventSystem.current.SetSelectedGameObject(null);
    }

    private bool ShouldForwardNativeClickToOwnerButton()
    {
        if (!forwardChildClickToOwnerButton)
            return false;

        if (!CanOwnerButtonBeSelected())
            return false;

        if (ownerButton.gameObject == gameObject)
            return false;

        return true;
    }

    public void ForceResetVisualState()
    {
        isPointerInside = false;
        isPointerPressed = false;
        fallbackMousePressedInside = false;

        StopBounceAndRestore();

        SetColor(normalColor);
        ClearSelectionIfNeeded();

        RefreshAll();
    }

    public void ForceClearPointerStateKeepSelection()
    {
        isPointerInside = false;
        isPointerPressed = false;
        fallbackMousePressedInside = false;

        RefreshColor();
    }

    public void ForceRefreshVisualState()
    {
        RefreshColor();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!enableEffects)
            return;

        if (!CanOwnerButtonBeSelected())
            return;

        isPointerInside = true;
        isPointerPressed = false;

        if (selectButtonOnMouseHover)
            SelectOwnerButton();

        RefreshAll();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!enableEffects)
            return;

        if (!CanOwnerButtonBeSelected())
            return;

        isPointerInside = true;

        if (selectButtonOnMouseHover)
            SelectOwnerButton();

        RefreshAll();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!enableEffects)
            return;

        isPointerInside = false;
        isPointerPressed = false;

        RefreshAll();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!enableEffects)
            return;

        if (!CanOwnerButtonBeSelected())
            return;

        isPointerPressed = true;
        isPointerInside = true;

        SelectOwnerButton();

        RefreshAll();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!enableEffects)
            return;

        isPointerPressed = false;
        isPointerInside = IsRaycastInsideOwner(eventData) || IsMouseInsideOwnerRect();

        RefreshAll();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!enableEffects)
            return;

        if (!CanOwnerButtonBeSelected())
            return;

        lastNativePointerClickFrame = Time.frameCount;

        isPointerPressed = false;
        isPointerInside = true;

        SelectOwnerButton();
        RefreshAll();

        if (!ShouldForwardNativeClickToOwnerButton())
            return;

        if (lastManualClickFrame == Time.frameCount)
            return;

        lastManualClickFrame = Time.frameCount;

        ownerButton.onClick.Invoke();

        if (eventData != null)
            eventData.Use();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!enableEffects)
            return;

        RefreshAll();
        PlayBounce();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!enableEffects)
            return;

        isPointerPressed = false;
        RefreshAll();
    }

    private void PlayBounce()
    {
        if (!enableEffects) return;
        if (!enableBounce) return;
        if (bounceTarget == null) return;
        if (!gameObject.activeInHierarchy) return;

        StopBounceAndRestore();

        bounceBasePosition = bounceTarget.anchoredPosition;
        bounceCoroutine = StartCoroutine(BounceRoutine());
    }

    private void StopBounceAndRestore()
    {
        if (bounceCoroutine != null)
        {
            StopCoroutine(bounceCoroutine);
            bounceCoroutine = null;
        }

        if (bounceChangedPosition && bounceTarget != null)
            bounceTarget.anchoredPosition = bounceBasePosition;

        bounceChangedPosition = false;
    }

    private IEnumerator BounceRoutine()
    {
        bounceChangedPosition = true;

        Vector2 start = bounceBasePosition;
        Vector2 down = start + new Vector2(0f, -downOffset);
        Vector2 up = start + new Vector2(0f, upOffset);

        yield return Move(start, down, duration * 0.5f);
        yield return Move(down, up, duration * 0.5f);
        yield return Move(up, start, duration * 0.5f);

        if (bounceTarget != null)
            bounceTarget.anchoredPosition = start;

        bounceChangedPosition = false;
        bounceCoroutine = null;
    }

    private IEnumerator Move(Vector2 from, Vector2 to, float time)
    {
        if (bounceTarget == null)
            yield break;

        if (time <= 0f)
        {
            bounceTarget.anchoredPosition = to;
            yield break;
        }

        float timer = 0f;

        while (timer < time)
        {
            if (bounceTarget == null)
                yield break;

            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / time);

            bounceTarget.anchoredPosition = Vector2.LerpUnclamped(from, to, t);

            yield return null;
        }

        if (bounceTarget != null)
            bounceTarget.anchoredPosition = to;
    }
}
