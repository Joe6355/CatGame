using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingsTopTabsAutoOpenOnSelect : MonoBehaviour
{
    [Header("Верхние кнопки вкладок")]
    [SerializeField] private Button audioButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button gameplayButton;
    [SerializeField] private Button videoButton;

    [Header("Кнопки внутри Controls")]
    [SerializeField] private Button keyboardButton;
    [SerializeField] private Button gamepadButton;

    [Header("Поведение")]
    [SerializeField, Tooltip("Если ВКЛ — кнопка срабатывает сразу, когда выбрана клавиатурой или геймпадом.")]
    private bool autoOpenOnKeyboardOrGamepadSelect = true;

    [SerializeField, Tooltip("Сколько секунд после ввода с клавиатуры/геймпада считать выбор кнопки навигационным.")]
    private float navigationInputMemoryTime = 0.25f;

    [SerializeField, Range(0.1f, 0.95f), Tooltip("Порог стика/крестовины для определения навигации геймпадом.")]
    private float axisThreshold = 0.45f;

    [SerializeField, Tooltip("Если ВКЛ — учитывает твой LegacyKeycodeRebind: A/D, стрелки, геймпад и переназначенные кнопки.")]
    private bool useLegacyKeycodeRebind = true;

    [SerializeField, Tooltip("Если ВКЛ — дополнительно проверяет стандартные Unity axes: Horizontal/Vertical.")]
    private bool useUnityNavigationAxes = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private GameObject _lastSelectedObject;
    private Vector3 _lastMousePosition;
    private bool _hasMousePosition;
    private float _lastNavigationInputTime = -999f;

    private void OnEnable()
    {
        _lastSelectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        _lastMousePosition = Input.mousePosition;
        _hasMousePosition = true;
        _lastNavigationInputTime = -999f;
    }

    private void Update()
    {
        if (!autoOpenOnKeyboardOrGamepadSelect)
            return;

        UpdateInputSource();
        TryAutoInvokeSelectedButton();
    }

    private void UpdateInputSource()
    {
        if (MouseWasUsedThisFrame())
            _lastNavigationInputTime = -999f;

        if (KeyboardOrGamepadNavigationIsActive())
            _lastNavigationInputTime = Time.unscaledTime;
    }

    private bool MouseWasUsedThisFrame()
    {
        bool mouseButton =
            Input.GetMouseButtonDown(0) ||
            Input.GetMouseButtonDown(1) ||
            Input.GetMouseButtonDown(2);

        if (!_hasMousePosition)
        {
            _lastMousePosition = Input.mousePosition;
            _hasMousePosition = true;
            return mouseButton;
        }

        Vector3 currentMousePosition = Input.mousePosition;
        bool mouseMoved = (currentMousePosition - _lastMousePosition).sqrMagnitude > 0.01f;
        _lastMousePosition = currentMousePosition;

        return mouseButton || mouseMoved;
    }

    private bool KeyboardOrGamepadNavigationIsActive()
    {
        if (Input.GetKey(KeyCode.LeftArrow) ||
            Input.GetKey(KeyCode.RightArrow) ||
            Input.GetKey(KeyCode.UpArrow) ||
            Input.GetKey(KeyCode.DownArrow) ||
            Input.GetKey(KeyCode.A) ||
            Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.W) ||
            Input.GetKey(KeyCode.S))
        {
            return true;
        }

        if (useLegacyKeycodeRebind)
        {
            if (LegacyKeycodeRebind.GetHeldAnyStatic(LegacyKeycodeRebind.Action.MoveLeft) ||
                LegacyKeycodeRebind.GetHeldAnyStatic(LegacyKeycodeRebind.Action.MoveRight) ||
                LegacyKeycodeRebind.GetHeldAnyStatic(LegacyKeycodeRebind.Action.UpAction) ||
                LegacyKeycodeRebind.GetHeldAnyStatic(LegacyKeycodeRebind.Action.DownAction))
            {
                return true;
            }
        }

        if (useUnityNavigationAxes)
        {
            if (Mathf.Abs(SafeGetAxisRaw("Horizontal")) >= axisThreshold)
                return true;

            if (Mathf.Abs(SafeGetAxisRaw("Vertical")) >= axisThreshold)
                return true;
        }

        if (Mathf.Abs(SafeGetAxisRaw("GamepadHorizontal")) >= axisThreshold)
            return true;

        if (Mathf.Abs(SafeGetAxisRaw("GamepadVertical")) >= axisThreshold)
            return true;

        if (Mathf.Abs(SafeGetAxisRaw("DPadX")) >= axisThreshold)
            return true;

        if (Mathf.Abs(SafeGetAxisRaw("DPadY")) >= axisThreshold)
            return true;

        return false;
    }

    private void TryAutoInvokeSelectedButton()
    {
        if (EventSystem.current == null)
            return;

        if (LegacyKeycodeRebind.I != null && LegacyKeycodeRebind.I.IsBlockingOtherUi)
            return;

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

        if (selectedObject == null)
        {
            _lastSelectedObject = null;
            return;
        }

        if (selectedObject == _lastSelectedObject)
            return;

        _lastSelectedObject = selectedObject;

        Button selectedButton = GetAutoInvokeButton(selectedObject);

        if (selectedButton == null)
            return;

        if (!WasSelectedByKeyboardOrGamepad())
            return;

        if (!selectedButton.isActiveAndEnabled || !selectedButton.interactable)
            return;

        if (debugLogs)
            Debug.Log("[SettingsTopTabsAutoOpenOnSelect] Auto invoke: " + selectedButton.name, selectedButton);

        selectedButton.onClick.Invoke();
    }

    private bool WasSelectedByKeyboardOrGamepad()
    {
        return Time.unscaledTime - _lastNavigationInputTime <= navigationInputMemoryTime;
    }

    private Button GetAutoInvokeButton(GameObject selectedObject)
    {
        if (audioButton != null && selectedObject == audioButton.gameObject)
            return audioButton;

        if (controlsButton != null && selectedObject == controlsButton.gameObject)
            return controlsButton;

        if (gameplayButton != null && selectedObject == gameplayButton.gameObject)
            return gameplayButton;

        if (videoButton != null && selectedObject == videoButton.gameObject)
            return videoButton;

        if (keyboardButton != null && selectedObject == keyboardButton.gameObject)
            return keyboardButton;

        if (gamepadButton != null && selectedObject == gamepadButton.gameObject)
            return gamepadButton;

        return null;
    }

    private float SafeGetAxisRaw(string axisName)
    {
        if (string.IsNullOrWhiteSpace(axisName))
            return 0f;

        try
        {
            return Input.GetAxisRaw(axisName);
        }
        catch
        {
            return 0f;
        }
    }
}