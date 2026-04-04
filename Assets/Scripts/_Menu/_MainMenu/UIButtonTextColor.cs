using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

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

    private void Start()
    {
        SetColor(normalColor);
    }

    void SetColor(Color color)
    {
        if (targetText != null)
            targetText.color = color;
    }

    // Hover
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetColor(highlightedColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetColor(normalColor);
    }

    // Press
    public void OnPointerDown(PointerEventData eventData)
    {
        SetColor(pressedColor);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SetColor(highlightedColor);
    }

    // Selection (например через клавиатуру/геймпад)
    public void OnSelect(BaseEventData eventData)
    {
        SetColor(selectedColor);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetColor(normalColor);
    }
}