using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class MenuOptionHighlightUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IPointerDownHandler
{
    [Header("Main UI")]
    [Tooltip("Главный UI-элемент настройки: Slider, TMP_Dropdown, Dropdown, Button, Toggle и т.д.")]
    public Selectable selectable;

    [Tooltip("Если это Slider, перетащи его сюда. Нужно только для отображения процентов.")]
    public Slider slider;

    [Tooltip("Название настройки.")]
    public TMP_Text title;

    [Tooltip("Текст значения справа. Для Slider показывает проценты. Можно оставить пустым.")]
    public TMP_Text valueText;

    [Header("Hover Hit Area")]
    [Tooltip("Прозрачный Image/Graphic, который ловит мышку по всей строке настройки. Обычно это Image на корневом объекте строки.")]
    public Graphic hoverRaycastArea;

    [Tooltip("Автоматически использовать Graphic/Image на этом же объекте как зону наведения.")]
    public bool autoUseOwnGraphicAsHoverArea = true;

    [Tooltip("Принудительно включать Raycast Target у Hover Raycast Area.")]
    public bool forceHoverAreaRaycastTarget = true;

    [Tooltip("Делать Hover Raycast Area полностью прозрачной.")]
    public bool forceHoverAreaTransparent = true;

    [Header("Selected Highlight Image")]
    [Tooltip("Image-объект, который визуально подсвечивает выбранную настройку.")]
    public Image selectionImage;

    [Tooltip("Отключать Raycast Target у картинки подсветки, чтобы она не перекрывала Slider/Button/Dropdown.")]
    public bool disableSelectionImageRaycast = true;

    [Tooltip("Включать подсветку при наведении мышкой.")]
    public bool highlightOnHover = true;

    [Tooltip("При наведении мышью делать настоящий Selectable выбранным в EventSystem.")]
    public bool selectSelectableOnHover = false;

    [Tooltip("При клике по области строки делать настоящий Selectable выбранным в EventSystem.")]
    public bool selectSelectableOnPointerDown = true;

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
            selectable = GetComponentInChildren<Selectable>(true);

        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);

        hoverRaycastArea = GetComponent<Graphic>();
    }

    private void Awake()
    {
        AutoFindReferences();
        SetupRaycastAreas();
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
        SetupRaycastAreas();
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
            selectable = GetComponentInChildren<Selectable>(true);

        if (slider == null)
            slider = GetComponent<Slider>();

        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);

        if (hoverRaycastArea == null && autoUseOwnGraphicAsHoverArea)
            hoverRaycastArea = GetComponent<Graphic>();
    }

    private void SetupRaycastAreas()
    {
        if (hoverRaycastArea != null)
        {
            if (forceHoverAreaRaycastTarget)
                hoverRaycastArea.raycastTarget = true;

            if (forceHoverAreaTransparent)
            {
                Color col = hoverRaycastArea.color;
                col.a = 0f;
                hoverRaycastArea.color = col;
            }
        }

        if (selectionImage != null && disableSelectionImageRaycast)
            selectionImage.raycastTarget = false;
    }

    private void UpdateValueText()
    {
        if (valueText == null)
            return;

        if (slider != null)
            valueText.text = (slider.value * 100f).ToString("0") + "%";
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

    private void SelectSelectable()
    {
        if (EventSystem.current == null)
            return;

        if (selectable == null)
            return;

        if (!selectable.isActiveAndEnabled)
            return;

        if (!selectable.interactable)
            return;

        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
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

        if (selectSelectableOnHover)
            SelectSelectable();
    }

    public void OnPointerMove(PointerEventData e)
    {
        hover = true;

        if (selectSelectableOnHover)
            SelectSelectable();
    }

    public void OnPointerExit(PointerEventData e)
    {
        hover = false;
    }

    public void OnPointerDown(PointerEventData e)
    {
        hover = true;

        if (selectSelectableOnPointerDown)
            SelectSelectable();
    }
}