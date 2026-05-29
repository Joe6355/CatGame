using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MenuOptionHighlightUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Main UI")]
    [Tooltip("Главный UI-элемент настройки: Slider, TMP_Dropdown, Dropdown, Button, Toggle и т.д.")]
    public Selectable selectable;

    [Tooltip("Если это Slider, можешь сюда перетащить его. Нужно только для отображения процентов.")]
    public Slider slider;

    [Tooltip("Название настройки.")]
    public TMP_Text title;

    [Tooltip("Текст значения справа. Для Slider показывает проценты. Можно оставить пустым.")]
    public TMP_Text valueText;

    [Header("Selected Highlight Image")]
    [Tooltip("Image-объект, который подсвечивает выбранную настройку.")]
    public Image selectionImage;

    [Tooltip("Включать подсветку при наведении мышкой.")]
    public bool highlightOnHover = true;

    [Tooltip("Альфа подсветки. Значение из 255. Например 100 = 100/255.")]
    [Range(0, 255)]
    public int activeAlpha255 = 100;

    private ColorBlock colors;
    private bool hover;

    private void Reset()
    {
        selectable = GetComponent<Selectable>();

        if (slider == null)
            slider = GetComponent<Slider>();

        if (selectable == null)
            selectable = GetComponentInChildren<Selectable>();

        if (slider == null)
            slider = GetComponentInChildren<Slider>();
    }

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        if (selectable != null)
            colors = selectable.colors;

        SetSelectionImageAlpha255(0);
    }

    private void OnEnable()
    {
        hover = false;
        SetSelectionImageAlpha255(0);
    }

    private void OnDisable()
    {
        hover = false;
        SetSelectionImageAlpha255(0);
    }

    private void Update()
    {
        if (selectable == null)
            return;

        UpdateValueText();
        UpdateTextColor();
        UpdateSelectionImage();
    }

    private void AutoFindReferences()
    {
        if (selectable == null)
            selectable = GetComponent<Selectable>();

        if (selectable == null)
            selectable = GetComponentInChildren<Selectable>();

        if (slider == null)
            slider = GetComponent<Slider>();

        if (slider == null)
            slider = GetComponentInChildren<Slider>();
    }

    private void UpdateValueText()
    {
        if (valueText == null)
            return;

        if (slider != null)
        {
            valueText.text = (slider.value * 100f).ToString("0") + "%";
        }
    }

    private void UpdateTextColor()
    {
        Color col =
            !selectable.interactable ? colors.disabledColor :
            Input.GetMouseButton(0) && hover ? colors.pressedColor :
            hover ? colors.highlightedColor :
            IsSelectedNow() ? colors.selectedColor :
            colors.normalColor;

        if (title != null)
            title.color = col;

        if (valueText != null)
            valueText.color = col;
    }

    private void UpdateSelectionImage()
    {
        if (selectionImage == null)
            return;

        bool active = selectable.interactable && (IsSelectedNow() || (highlightOnHover && hover));

        if (active)
            SetSelectionImageAlpha255(activeAlpha255);
        else
            SetSelectionImageAlpha255(0);
    }

    private bool IsSelectedNow()
    {
        if (EventSystem.current == null)
            return false;

        GameObject selected = EventSystem.current.currentSelectedGameObject;

        if (selected == null)
            return false;

        if (selected == gameObject)
            return true;

        if (selectable != null && selected == selectable.gameObject)
            return true;

        if (selected.transform.IsChildOf(transform))
            return true;

        return false;
    }

    private void SetSelectionImageAlpha255(float alpha255)
    {
        if (selectionImage == null)
            return;

        Color col = selectionImage.color;
        col.a = Mathf.Clamp(alpha255, 0f, 255f) / 255f;
        selectionImage.color = col;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        hover = true;
    }

    public void OnPointerExit(PointerEventData e)
    {
        hover = false;
    }
}