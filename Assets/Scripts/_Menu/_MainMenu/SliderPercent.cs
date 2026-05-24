using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class SliderUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Slider slider;
    public TMP_Text title, percent;

    ColorBlock c;
    bool hover;

    void Start() => c = slider.colors;

    void Update()
    {
        percent.text = (slider.value * 100).ToString("0") + "%";

        Color col =
            !slider.interactable ? c.disabledColor :
            Input.GetMouseButton(0) && hover ? c.pressedColor :
            hover ? c.highlightedColor :
            EventSystem.current.currentSelectedGameObject == gameObject ? c.selectedColor :
            c.normalColor;

        title.color = col;
        percent.color = col;
    }

    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) => hover = false;
}