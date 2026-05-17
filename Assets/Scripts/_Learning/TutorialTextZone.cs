using System;
using System.Reflection;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class TutorialTextZone : MonoBehaviour
{
    public enum TutorialType
    {
        Jump,
        WallBounce,
        LedgeHook,
        Run,
        SprintJump,
        Pounce,
        Custom
    }

    [Header("Tutorial")]
    [SerializeField] private TutorialType tutorialType = TutorialType.Jump;

    [SerializeField, TextArea(2, 6), Tooltip(
        "Текст подсказки. Можно использовать плейсхолдеры:\n" +
        "{key} — главная кнопка для выбранного Tutorial Type\n" +
        "{jump} — прыжок\n" +
        "{left} — влево\n" +
        "{right} — вправо\n" +
        "{up} — вверх / зацеп\n" +
        "{down} — вниз / паунс\n" +
        "{interact} — взаимодействие")]
    private string tutorialText = "Нажми {key}";

    [Header("UI")]
    [SerializeField, Tooltip("TMP-текст, который будет показывать подсказку.")]
    private TMP_Text targetText;

    [SerializeField, Tooltip("Если указан CanvasGroup, скрипт будет плавно показывать/скрывать подсказку.")]
    private CanvasGroup canvasGroup;

    [SerializeField, Tooltip("Если ВКЛ — GameObject текста будет включаться/выключаться.")]
    private bool toggleTextObject = true;

    [SerializeField, Min(0f), Tooltip("Время плавного появления/исчезновения.")]
    private float fadeTime = 0.12f;

    [Header("Player Detection")]
    [SerializeField, Tooltip("Тег игрока.")]
    private string playerTag = "Player";

    [SerializeField, Tooltip("Если ВКЛ — входить в зону может объект без тега Player, если у него есть PlayerController в родителях.")]
    private bool allowPlayerControllerFallback = true;

    [Header("Input Binding Display")]
    [SerializeField, Tooltip("Если ВКЛ — скрипт попробует получить актуальные бинды из LegacyKeycodeRebind через reflection.")]
    private bool useLegacyKeycodeRebind = true;

    [SerializeField, Tooltip("Какое устройство показывать в подсказке.")]
    private BindingDevice preferredDevice = BindingDevice.Keyboard;

    [SerializeField, Tooltip("Если ВКЛ — рядом с клавиатурной кнопкой будет добавляться геймпадная кнопка, если её удалось получить.")]
    private bool showGamepadAlternative = true;

    [Header("Fallback Keyboard Labels")]
    [SerializeField] private string fallbackJump = "Space";
    [SerializeField] private string fallbackMoveLeft = "A";
    [SerializeField] private string fallbackMoveRight = "D";
    [SerializeField] private string fallbackUp = "W";
    [SerializeField] private string fallbackDown = "S";
    [SerializeField] private string fallbackInteract = "F";

    [Header("Fallback Gamepad Labels")]
    [SerializeField] private string fallbackGamepadJump = "A";
    [SerializeField] private string fallbackGamepadMoveLeft = "Left Stick ←";
    [SerializeField] private string fallbackGamepadMoveRight = "Left Stick →";
    [SerializeField] private string fallbackGamepadUp = "Left Stick ↑";
    [SerializeField] private string fallbackGamepadDown = "Left Stick ↓";
    [SerializeField] private string fallbackGamepadInteract = "X";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    public enum BindingDevice
    {
        Keyboard,
        Gamepad
    }

    private Collider2D triggerCollider;
    private bool playerInside = false;
    private float fadeVelocity = 0f;
    private float targetAlpha = 0f;

    private void Reset()
    {
        CacheRefs();
        EnsureTrigger();
    }

    private void Awake()
    {
        CacheRefs();
        EnsureTrigger();
        HideInstant();
    }

    private void OnValidate()
    {
        CacheRefs();
        EnsureTrigger();

        fadeTime = Mathf.Max(0f, fadeTime);
    }

    private void Update()
    {
        if (canvasGroup == null)
            return;

        float current = canvasGroup.alpha;
        float next;

        if (fadeTime <= 0f)
        {
            next = targetAlpha;
        }
        else
        {
            next = Mathf.SmoothDamp(
                current,
                targetAlpha,
                ref fadeVelocity,
                fadeTime);
        }

        canvasGroup.alpha = next;
        canvasGroup.interactable = targetAlpha > 0.5f;
        canvasGroup.blocksRaycasts = false;

        if (toggleTextObject && targetText != null)
        {
            if (targetAlpha > 0f && !targetText.gameObject.activeSelf)
                targetText.gameObject.SetActive(true);

            if (targetAlpha <= 0f && canvasGroup.alpha <= 0.01f && targetText.gameObject.activeSelf)
                targetText.gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        playerInside = true;
        Show();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        playerInside = false;
        Hide();
    }

    private void Show()
    {
        if (targetText == null)
            return;

        targetText.text = BuildText();

        if (toggleTextObject)
            targetText.gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            targetAlpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            targetText.enabled = true;
        }

        if (debugLogs)
            Debug.Log($"[TutorialTextZone] Show: {targetText.text}", this);
    }

    private void Hide()
    {
        if (targetText == null)
            return;

        if (canvasGroup != null)
        {
            targetAlpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            targetText.enabled = false;

            if (toggleTextObject)
                targetText.gameObject.SetActive(false);
        }

        if (debugLogs)
            Debug.Log("[TutorialTextZone] Hide", this);
    }

    private void HideInstant()
    {
        targetAlpha = 0f;
        fadeVelocity = 0f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (targetText != null)
        {
            targetText.enabled = true;

            if (toggleTextObject)
                targetText.gameObject.SetActive(false);
        }
    }

    private string BuildText()
    {
        string result = string.IsNullOrWhiteSpace(tutorialText)
            ? GetDefaultTextForType()
            : tutorialText;

        result = result.Replace("{key}", GetMainKeyForTutorialType(tutorialType));
        result = result.Replace("{jump}", GetBindingLabel("Jump", fallbackJump, fallbackGamepadJump));
        result = result.Replace("{left}", GetBindingLabel("MoveLeft", fallbackMoveLeft, fallbackGamepadMoveLeft));
        result = result.Replace("{right}", GetBindingLabel("MoveRight", fallbackMoveRight, fallbackGamepadMoveRight));
        result = result.Replace("{up}", GetBindingLabel("UpAction", fallbackUp, fallbackGamepadUp));
        result = result.Replace("{down}", GetBindingLabel("DownAction", fallbackDown, fallbackGamepadDown));
        result = result.Replace("{interact}", GetBindingLabel("Interact", fallbackInteract, fallbackGamepadInteract));

        return result;
    }

    private string GetDefaultTextForType()
    {
        switch (tutorialType)
        {
            case TutorialType.Jump:
                return "Нажми {jump}. Чем дольше держишь, тем выше прыжок.";

            case TutorialType.WallBounce:
                return "Прыгни на стену и нажми {jump}, чтобы оттолкнуться.";

            case TutorialType.LedgeHook:
                return "Подойди к краю платформы и нажми {up}, чтобы забраться.";

            case TutorialType.Run:
                return "Удерживай {left} или {right}, чтобы разбежаться.";

            case TutorialType.SprintJump:
                return "Разбегись и нажми {jump}, чтобы сделать усиленный прыжок.";

            case TutorialType.Pounce:
                return "В воздухе нажми {down}, чтобы выполнить паунс.";

            case TutorialType.Custom:
            default:
                return "{key}";
        }
    }

    private string GetMainKeyForTutorialType(TutorialType type)
    {
        switch (type)
        {
            case TutorialType.Jump:
            case TutorialType.WallBounce:
            case TutorialType.SprintJump:
                return GetBindingLabel("Jump", fallbackJump, fallbackGamepadJump);

            case TutorialType.LedgeHook:
                return GetBindingLabel("UpAction", fallbackUp, fallbackGamepadUp);

            case TutorialType.Run:
                return $"{GetBindingLabel("MoveLeft", fallbackMoveLeft, fallbackGamepadMoveLeft)} / {GetBindingLabel("MoveRight", fallbackMoveRight, fallbackGamepadMoveRight)}";

            case TutorialType.Pounce:
                return GetBindingLabel("DownAction", fallbackDown, fallbackGamepadDown);

            case TutorialType.Custom:
            default:
                return GetBindingLabel("Jump", fallbackJump, fallbackGamepadJump);
        }
    }

    private string GetBindingLabel(string actionName, string keyboardFallback, string gamepadFallback)
    {
        string keyboard = keyboardFallback;
        string gamepad = gamepadFallback;

        if (useLegacyKeycodeRebind)
        {
            string reflectedKeyboard = TryGetLegacyBindingLabel("Keyboard", actionName);
            string reflectedGamepad = TryGetLegacyBindingLabel("Gamepad", actionName);

            if (!string.IsNullOrWhiteSpace(reflectedKeyboard))
                keyboard = reflectedKeyboard;

            if (!string.IsNullOrWhiteSpace(reflectedGamepad))
                gamepad = reflectedGamepad;
        }

        if (preferredDevice == BindingDevice.Gamepad)
        {
            if (showGamepadAlternative && !string.IsNullOrWhiteSpace(keyboard))
                return $"{gamepad} / {keyboard}";

            return gamepad;
        }

        if (showGamepadAlternative && !string.IsNullOrWhiteSpace(gamepad))
            return $"{keyboard} / {gamepad}";

        return keyboard;
    }

    private string TryGetLegacyBindingLabel(string deviceName, string actionName)
    {
        /*
         * Скрипт не жёстко зависит от конкретного имени метода получения бинда.
         * Он пробует найти один из частых методов в LegacyKeycodeRebind:
         *
         * GetDisplayNameStatic(Device, Action)
         * GetBindingDisplayNameStatic(Device, Action)
         * GetKeyDisplayNameStatic(Device, Action)
         * GetKeyNameStatic(Device, Action)
         * GetBindingStatic(Device, Action)
         * GetKeyStatic(Device, Action)
         * GetKeyCodeStatic(Device, Action)
         *
         * Если в твоём LegacyKeycodeRebind такого публичного метода нет,
         * будет показан fallback из инспектора.
         */

        Type rebindType = Type.GetType("LegacyKeycodeRebind");

        if (rebindType == null)
            rebindType = FindTypeByName("LegacyKeycodeRebind");

        if (rebindType == null)
            return null;

        if (!IsLegacyRebindRuntimeReady(rebindType))
            return null;

        Type deviceType = FindNestedType(rebindType, "Device");
        Type actionType = FindNestedType(rebindType, "Action");

        if (deviceType == null || actionType == null)
            return null;

        object deviceValue;
        object actionValue;

        try
        {
            deviceValue = Enum.Parse(deviceType, deviceName);
            actionValue = Enum.Parse(actionType, actionName);
        }
        catch
        {
            return null;
        }

        string[] methodNames =
        {
            "GetDisplayNameStatic",
            "GetBindingDisplayNameStatic",
            "GetKeyDisplayNameStatic",
            "GetKeyNameStatic",
            "GetBindingStatic",
            "GetKeyStatic",
            "GetKeyCodeStatic"
        };

        for (int i = 0; i < methodNames.Length; i++)
        {
            string label = TryCallLegacyMethod(rebindType, methodNames[i], deviceType, actionType, deviceValue, actionValue);

            if (!string.IsNullOrWhiteSpace(label))
                return NicifyKeyName(label);
        }

        return null;
    }

    private string TryCallLegacyMethod(
        Type rebindType,
        string methodName,
        Type deviceType,
        Type actionType,
        object deviceValue,
        object actionValue)
    {
        MethodInfo method = rebindType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { deviceType, actionType },
            null);

        if (method == null)
            return null;

        try
        {
            object value = method.Invoke(null, new[] { deviceValue, actionValue });

            if (value == null)
                return null;

            if (value is KeyCode keyCode)
                return KeyCodeToDisplayName(keyCode);

            return value.ToString();
        }
        catch
        {
            return null;
        }
    }

    private bool IsLegacyRebindRuntimeReady(Type rebindType)
    {
        PropertyInfo prop = rebindType.GetProperty(
            "RuntimeReady",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (prop == null)
            return true;

        try
        {
            object value = prop.GetValue(null, null);
            return value is bool b && b;
        }
        catch
        {
            return true;
        }
    }

    private static Type FindTypeByName(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(typeName);
            if (type != null)
                return type;

            Type[] types;

            try
            {
                types = assemblies[i].GetTypes();
            }
            catch
            {
                continue;
            }

            for (int j = 0; j < types.Length; j++)
            {
                if (types[j].Name == typeName)
                    return types[j];
            }
        }

        return null;
    }

    private static Type FindNestedType(Type parent, string nestedName)
    {
        Type[] nestedTypes = parent.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);

        for (int i = 0; i < nestedTypes.Length; i++)
        {
            if (nestedTypes[i].Name == nestedName)
                return nestedTypes[i];
        }

        return null;
    }

    private string KeyCodeToDisplayName(KeyCode key)
    {
        if (key == KeyCode.None)
            return "";

        switch (key)
        {
            case KeyCode.Space: return "Space";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";

            case KeyCode.JoystickButton0: return "A";
            case KeyCode.JoystickButton1: return "B";
            case KeyCode.JoystickButton2: return "X";
            case KeyCode.JoystickButton3: return "Y";
            case KeyCode.JoystickButton4: return "LB";
            case KeyCode.JoystickButton5: return "RB";
            case KeyCode.JoystickButton6: return "Back";
            case KeyCode.JoystickButton7: return "Start";
            case KeyCode.JoystickButton8: return "L3";
            case KeyCode.JoystickButton9: return "R3";

            default:
                return NicifyKeyName(key.ToString());
        }
    }

    private string NicifyKeyName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        string value = raw;

        value = value.Replace("Alpha", "");
        value = value.Replace("Keypad", "Num ");
        value = value.Replace("JoystickButton0", "A");
        value = value.Replace("JoystickButton1", "B");
        value = value.Replace("JoystickButton2", "X");
        value = value.Replace("JoystickButton3", "Y");
        value = value.Replace("JoystickButton4", "LB");
        value = value.Replace("JoystickButton5", "RB");
        value = value.Replace("JoystickButton6", "Back");
        value = value.Replace("JoystickButton7", "Start");
        value = value.Replace("JoystickButton8", "L3");
        value = value.Replace("JoystickButton9", "R3");

        return value;
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;

        if (!string.IsNullOrWhiteSpace(playerTag) && other.CompareTag(playerTag))
            return true;

        if (allowPlayerControllerFallback && other.GetComponentInParent<PlayerController>() != null)
            return true;

        return false;
    }

    private void CacheRefs()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider2D>();

        if (targetText == null)
            targetText = GetComponentInChildren<TMP_Text>(true);

        if (canvasGroup == null && targetText != null)
            canvasGroup = targetText.GetComponentInParent<CanvasGroup>();
    }

    private void EnsureTrigger()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider2D>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }
}