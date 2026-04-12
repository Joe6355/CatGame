using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class UIButtonTextColor : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("Target")]
    public TMP_Text targetText;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color highlightedColor = Color.yellow;
    public Color pressedColor = Color.gray;
    public Color selectedColor = Color.cyan;

    [Header("Effects Toggle")]
    public bool enableEffects = true;

    [Header("Bounce Settings")]
    public float downOffset = 5f;
    public float upOffset = 8f;
    public float duration = 0.15f;

    private RectTransform targetRect;
    private Vector2 originalPos;
    private Coroutine bounceCoroutine;

    private void Awake()
    {
        // 👇 автоматически берём RectTransform с этого же объекта
        targetRect = GetComponent<RectTransform>();
        originalPos = targetRect.anchoredPosition;
    }

    private void Start()
    {
        if (enableEffects)
            SetColor(normalColor);
    }

    void SetColor(Color color)
    {
        if (targetText != null)
            targetText.color = color;
    }

    void AnimateBounce()
    {
        if (bounceCoroutine != null)
            StopCoroutine(bounceCoroutine);

        bounceCoroutine = StartCoroutine(BounceRoutine());
    }

    IEnumerator BounceRoutine()
    {
        Vector2 start = originalPos;

        Vector2 down = start + new Vector2(0, -downOffset);
        Vector2 up = start + new Vector2(0, upOffset);

        yield return Move(start, down, duration * 0.5f);
        yield return Move(down, up, duration * 0.5f);
        yield return Move(up, start, duration * 0.5f);

        bounceCoroutine = null;
    }

    IEnumerator Move(Vector2 from, Vector2 to, float time)
    {
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / time;
            targetRect.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }

        targetRect.anchoredPosition = to;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!enableEffects) return;
        SetColor(highlightedColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!enableEffects) return;
        SetColor(normalColor);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!enableEffects) return;
        SetColor(pressedColor);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!enableEffects) return;
        SetColor(highlightedColor);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!enableEffects) return;

        SetColor(selectedColor);
        AnimateBounce();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!enableEffects) return;
        SetColor(normalColor);
    }
}