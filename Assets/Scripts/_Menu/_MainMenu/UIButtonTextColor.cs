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
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    private static readonly HashSet<UIButtonTextColor> All = new HashSet<UIButtonTextColor>();

    private static bool suppressPointerHoverVisuals;

    public static void SetSuppressPointerHoverVisuals(bool value)
    {
        suppressPointerHoverVisuals = value;
        RefreshAll();
    }

    private static void RefreshAll()
    {
        foreach (UIButtonTextColor item in All)
        {
            if (item != null && item.isActiveAndEnabled)
                item.RefreshColor();
        }
    }

    [Header("Target")]
    [Tooltip("TMP текст, который нужно перекрашивать.")]
    public TMP_Text targetText;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color highlightedColor = Color.yellow;
    public Color pressedColor = Color.gray;
    public Color selectedColor = Color.cyan;

    [Header("Effects Toggle")]
    public bool enableEffects = true;

    [Header("Selected State")]
    public bool useSelectedColor = true;

    [Header("Mouse / Keyboard Conflict Fix")]
    [Tooltip("Если ВКЛ — hover мыши не будет подсвечивать вторую кнопку при управлении клавиатурой/геймпадом.")]
    public bool ignoreMouseHoverWhenKeyboardNavigation = true;

    [Tooltip("Если ВКЛ — при наведении мышью кнопка сразу становится selected в EventSystem.")]
    public bool selectButtonOnMouseHover = true;

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
    private RectTransform targetRect;

    private bool isPointerInside;
    private bool isPointerPressed;

    private Coroutine bounceCoroutine;
    private Vector2 bounceBasePosition;
    private bool bounceChangedPosition;

    private void Reset()
    {
        targetText = GetComponentInChildren<TMP_Text>(true);
        ownerButton = GetComponentInParent<Button>();
        targetRect = GetComponent<RectTransform>();
        bounceTarget = targetRect;
    }

    private void Awake()
    {
        ownerButton = GetComponentInParent<Button>();

        if (targetText == null)
            targetText = GetComponentInChildren<TMP_Text>(true);

        targetRect = GetComponent<RectTransform>();

        if (bounceTarget == null)
            bounceTarget = targetRect;
    }

    private void OnEnable()
    {
        All.Add(this);

        isPointerInside = false;
        isPointerPressed = false;

        StopBounceAndRestore();

        if (resetColorOnEnable)
            SetColor(normalColor);

        RefreshAll();
    }

    private void OnDisable()
    {
        isPointerInside = false;
        isPointerPressed = false;

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
        All.Remove(this);
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

    private bool CanUsePointerVisuals()
    {
        if (!ignoreMouseHoverWhenKeyboardNavigation)
            return true;

        return !suppressPointerHoverVisuals;
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

        return true;
    }

    private void SelectOwnerButton()
    {
        if (!CanOwnerButtonBeSelected())
            return;

        GameObject ownerObject = GetOwnerSelectedObject();

        if (EventSystem.current.currentSelectedGameObject == ownerObject)
            return;

        EventSystem.current.SetSelectedGameObject(null);
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

        bool ownerIsSelected = IsOwnerSelectedByEventSystem();
        bool canUsePointerVisuals = CanUsePointerVisuals();

        if (ownerIsSelected && canUsePointerVisuals && isPointerPressed)
        {
            SetColor(pressedColor);
            return;
        }

        if (ownerIsSelected && canUsePointerVisuals && isPointerInside)
        {
            SetColor(highlightedColor);
            return;
        }

        if (ownerIsSelected && useSelectedColor)
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

    public void ForceResetVisualState()
    {
        isPointerInside = false;
        isPointerPressed = false;

        StopBounceAndRestore();

        SetColor(normalColor);
        ClearSelectionIfNeeded();

        RefreshAll();
    }

    public void ForceClearPointerStateKeepSelection()
    {
        isPointerInside = false;
        isPointerPressed = false;

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

        if (!CanUsePointerVisuals())
        {
            isPointerInside = false;
            isPointerPressed = false;
            RefreshAll();
            return;
        }

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

        suppressPointerHoverVisuals = false;

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
        isPointerInside = IsRaycastInsideOwner(eventData);

        RefreshAll();
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