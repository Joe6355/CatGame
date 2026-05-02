using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
public class MenuDiagnosticsConsole : MonoBehaviour
{
    public static MenuDiagnosticsConsole I { get; private set; }

    private enum Severity
    {
        Info,
        Ok,
        Warning
    }

    [Header("Console Output")]
    [SerializeField, Tooltip("Если выключено — MenuDiagnosticsConsole не будет писать сообщения в Unity Console. Лог останется только во внутренней мини-консоли.")]
    private bool enableUnityConsoleOutput = true;

    [SerializeField, Tooltip("Если включено — успешные [OK] строки тоже будут писаться в Unity Console. Иначе туда пойдут только [WARN] и [INFO].")]
    private bool writeOkLinesToUnityConsole = false;

    [Header("Singleton / Lifetime")]
    [SerializeField, Tooltip("Если включено — в игре будет только один такой объект. Дубликаты уничтожаются.")]
    private bool useSingleton = true;

    [SerializeField, Tooltip("Если включено — менеджер переживает смену сцен.")]
    private bool useDontDestroyOnLoad = true;

    [Header("Persistent Roots")]
    [SerializeField, Tooltip("Необязательный root для менеджера. Если пусто — будет использоваться root текущего объекта со скриптом.")]
    private GameObject managerPersistentRootOverride;

    [SerializeField, Tooltip("Root UI-консоли, который тоже нужно переносить между сценами. Лучше указывать отдельный Canvas/Root только для консоли.")]
    private GameObject consoleUiPersistentRoot;

    [Header("Version")]
    [SerializeField, Tooltip("Текущая версия/патч. Редактируется прямо в инспекторе.")]
    private string patchVersion = "0.0.1";

    [SerializeField, Tooltip("Префикс перед версией.")]
    private string versionPrefix = "PatchVersion:";

    [SerializeField, Tooltip("Если ссылка на TMP в главном меню уже есть — укажи её здесь.")]
    private TMP_Text directMainMenuVersionText;

    [SerializeField, Tooltip("Если directMainMenuVersionText пустой — пытаться найти TMP в главном меню по имени объекта.")]
    private bool findMainMenuVersionTextByObjectName = true;

    [SerializeField, Tooltip("Имя сцены главного меню, где нужно обновлять текст версии.")]
    private string mainMenuSceneName = "_MainMenu";

    [SerializeField, Tooltip("Точное имя TMP-объекта в главном меню, куда писать версию.")]
    private string mainMenuVersionObjectName = "PatchVersion";

    [Header("Mini Console UI")]
    [SerializeField, Tooltip("Корневой объект мини-консоли. Он будет показываться/скрываться по кнопке `.")]
    private GameObject consoleRoot;

    [SerializeField, Tooltip("TMP-текст, куда будет выводиться диагностика.")]
    private TMP_Text consoleText;

    [SerializeField, Tooltip("ScrollRect мини-консоли.")]
    private ScrollRect consoleScrollRect;

    [SerializeField, Tooltip("RectTransform Content внутри Scroll View.")]
    private RectTransform consoleContent;


    [Header("Developer Commands")]
    [SerializeField, Tooltip("TMP_InputField для ввода команд. Добавь его внутрь ConsolePanel под Scroll_View_Console.")]
    private TMP_InputField commandInput;

    [SerializeField, Tooltip("Если ВКЛ — при открытии консоли курсор сразу ставится в поле ввода команд.")]
    private bool focusCommandInputOnConsoleOpen = true;

    [SerializeField, Tooltip("Если ВКЛ — после ввода команды поле автоматически очищается.")]
    private bool clearCommandInputAfterSubmit = true;

    [SerializeField, Tooltip("Если ВКЛ — после команды поле ввода снова получает фокус.")]
    private bool refocusCommandInputAfterSubmit = true;

    [SerializeField, Tooltip("Если ВКЛ — в консоль будет добавлена стартовая подсказка про команду info.")]
    private bool printCommandHintOnStart = true;

    [Header("Developer Commands: Player")]
    [SerializeField, Tooltip("Корневой объект игрока. Если пусто — консоль попробует найти игрока по тегу или по PlayerController.")]
    private Transform commandPlayerRoot;

    [SerializeField, Tooltip("Rigidbody2D игрока. Если пусто — будет найден на Command Player Root.")]
    private Rigidbody2D commandPlayerRigidbody;

    [SerializeField, Tooltip("Теги, по которым консоль будет искать игрока, если ссылка не назначена вручную.")]
    private string[] commandPlayerTags = { "Player", "player" };

    [Header("Command: fly")]
    [SerializeField, Min(0f), Tooltip("Скорость полёта игрока после команды fly.")]
    private float commandFlySpeed = 8f;

    [SerializeField, Tooltip("Если ВКЛ — при включении/выключении fly скорость Rigidbody2D будет сбрасываться в 0.")]
    private bool resetPlayerVelocityOnFlyToggle = true;

    [SerializeField, Tooltip("Если ВКЛ — если список ниже пустой, консоль сама попробует отключить PlayerController на время fly.")]
    private bool autoDisablePlayerControllerWhileFlying = true;

    [SerializeField, Tooltip("Скрипты, которые надо выключать на время fly. Обычно достаточно PlayerController.")]
    private MonoBehaviour[] scriptsToDisableWhileFlying;

    [Header("Command: speed")]
    [SerializeField, Min(0.01f), Tooltip("Множитель скорости для команды speed без числа.")]
    private float defaultSpeedMultiplier = 2f;

    [SerializeField, Tooltip("Скрипты, в которых команда speed будет искать поля скорости. Если пусто — ищет во всех MonoBehaviour на игроке и его детях.")]
    private MonoBehaviour[] speedTargetScripts;

    [SerializeField, Tooltip("Имена float/int полей скорости, которые команда speed имеет право умножать.")]
    private string[] speedFieldNames =
    {
        "moveSpeed",
        "movementSpeed",
        "walkSpeed",
        "runSpeed",
        "sprintSpeed",
        "maxSpeed",
        "maxMoveSpeed",
        "maxGroundSpeed",
        "groundSpeed",
        "groundMoveSpeed",
        "baseMoveSpeed",
        "normalMoveSpeed",
        "airSpeed",
        "airControlSpeed",
        "climbSpeed",
        "climbMoveSpeed",
        "fenceClimbSpeed",
        "pounceSpeed",
        "wallSlideSpeed"
    };

    [SerializeField, Tooltip("Открывать консоль сразу при старте.")]
    private bool openConsoleOnStart = false;

    [SerializeField, Tooltip("Максимум строк, которые храним в мини-консоли.")]
    private int maxConsoleLines = 120;

    [Header("Manual Scroll Layout")]
    [SerializeField, Tooltip("Внутренний отступ текста слева/справа внутри Content.")]
    private float textHorizontalPadding = 12f;

    [SerializeField, Tooltip("Внутренний отступ текста сверху/снизу внутри Content.")]
    private float textVerticalPadding = 8f;

    [SerializeField, Tooltip("Минимальная высота Content, даже если строк мало.")]
    private float minContentHeight = 40f;

    [SerializeField, Tooltip("Если true — при обновлении текста скролл прыгает в самый низ.")]
    private bool stickToBottomOnRefresh = true;

    [Header("Hotkeys")]
    [SerializeField, Tooltip("Кнопка открытия/закрытия мини-консоли.")]
    private KeyCode toggleConsoleKey = KeyCode.BackQuote;

    [SerializeField, Tooltip("Кнопка ручного запуска диагностики.")]
    private KeyCode runDiagnosticsKey = KeyCode.F8;

    [SerializeField, Tooltip("Если консоль открыта и нажать эту кнопку — консоль очистится. При следующем открытии диагностика соберётся заново.")]
    private KeyCode clearConsoleKey = KeyCode.C;

    [Header("Diagnostics Run")]
    [SerializeField, Tooltip("Запускать диагностику в Awake.")]
    private bool runDiagnosticsOnAwake = true;

    [SerializeField, Tooltip("Запускать диагностику после загрузки каждой новой сцены.")]
    private bool runDiagnosticsOnSceneLoaded = true;

    [SerializeField, Tooltip("Если открыть мини-консоль — автоматически прогнать диагностику ещё раз.")]
    private bool rerunDiagnosticsWhenConsoleOpens = true;

    [SerializeField, Tooltip("Очищать текст мини-консоли при загрузке новой сцены перед новым отчётом.")]
    private bool clearConsoleOnSceneLoaded = true;

    [Header("General Checks")]
    [SerializeField, Tooltip("Проверять количество EventSystem в текущей сцене.")]
    private bool checkEventSystems = true;

    [SerializeField, Tooltip("Проверять Selectable в сцене и ругаться на Navigation=None.")]
    private bool checkSelectableNavigation = false;

    [SerializeField, Tooltip("Проверять и неактивные объекты сцены тоже.")]
    private bool includeInactiveSceneObjects = true;

    [Header("Script Checks")]
    [SerializeField] private bool checkMainMenuPanelsUI = true;
    [SerializeField] private bool checkPauseMenuUI = true;
    [SerializeField] private bool checkSettingsTabsSwitcher = true;
    [SerializeField] private bool checkLegacyKeycodeRebind = true;

    [Header("Ignored fields: MainMenuPanelsUI")]
    [SerializeField, Tooltip("Поля, которые считать допустимо пустыми для MainMenuPanelsUI.")]
    private List<string> ignoreFieldsMainMenuPanelsUI = new List<string>
    {
        "settingsTabsSwitcher",
        "mainPanelCanvasGroup",
        "newGameButton",
        "newGameSceneName",
        "continueFirstSelected",
        "controlFirstSelected",
        "settingsFirstSelected",
        "exitConfirmFirstSelected",
        "keyboardResetConfirmDialog",
        "keyboardResetYesButton",
        "keyboardResetNoButton",
        "keyboardResetReturnButton",
        "keyboardResetConfirmFirstSelected",
        "gamepadResetConfirmDialog",
        "gamepadResetYesButton",
        "gamepadResetNoButton",
        "gamepadResetReturnButton",
        "gamepadResetConfirmFirstSelected"
    };

    [Header("Ignored fields: PauseMenuUI")]
    [SerializeField, Tooltip("Поля, которые считать допустимо пустыми для PauseMenuUI.")]
    private List<string> ignoreFieldsPauseMenuUI = new List<string>
    {
        "settingsTabsSwitcher",
        "openPauseButton",
        "pauseMenuFirstSelected",
        "settingsFirstSelected",
        "controlsFirstSelected",
        "exitConfirmFirstSelected",
        "restartConfirmFirstSelected",
        "exitConfirmReturnButton",
        "restartConfirmReturnButton",
        "playerController"
    };

    [Header("Ignored fields: SettingsTabsSwitcher")]
    [SerializeField, Tooltip("Поля, которые считать допустимо пустыми для SettingsTabsSwitcher.")]
    private List<string> ignoreFieldsSettingsTabsSwitcher = new List<string>
    {
        "assistInfoBtn",
        "assistTooltipPanel",
        "settingsRootFirstSelected",
        "audioFirstSelected",
        "controlsFirstSelected",
        "gameplayFirstSelected",
        "videoFirstSelected",
        "keyboardFirstSelected",
        "gamepadFirstSelected"
    };

    [Header("Ignored fields: LegacyKeycodeRebind")]
    [SerializeField, Tooltip("Поля, которые считать допустимо пустыми для LegacyKeycodeRebind.")]
    private List<string> ignoreFieldsLegacyKeycodeRebind = new List<string>
    {
        "waitingOverlay",
        "waitingText",
        "resetButton"
    };

    private readonly List<string> _consoleLines = new List<string>(256);
    private readonly StringBuilder _builder = new StringBuilder(4096);

    private readonly Dictionary<string, Action<string[]>> _developerCommands = new Dictionary<string, Action<string[]>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<FieldKey, object> _originalSpeedValues = new Dictionary<FieldKey, object>();
    private readonly List<MonoBehaviour> _scriptsDisabledByFly = new List<MonoBehaviour>();

    private bool _flyEnabled;
    private bool _speedModified;
    private bool _hasOriginalGravityScale;
    private float _originalGravityScale = 1f;
    private float _lastCommandTimeScale = 1f;


    private bool _consoleVisible;
    private bool _forceDiagnosticsOnNextConsoleOpen;
    private Coroutine _scrollRoutine;

    private GameObject _managerRuntimeRoot;
    private GameObject _consoleRuntimeRoot;
    private bool _hasExplicitConsolePersistentRoot;


    private struct FieldKey : IEquatable<FieldKey>
    {
        public readonly MonoBehaviour Target;
        public readonly FieldInfo Field;

        public FieldKey(MonoBehaviour target, FieldInfo field)
        {
            Target = target;
            Field = field;
        }

        public bool Equals(FieldKey other)
        {
            return Target == other.Target && Field == other.Field;
        }

        public override bool Equals(object obj)
        {
            return obj is FieldKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Target != null ? Target.GetHashCode() : 0) * 397) ^ (Field != null ? Field.GetHashCode() : 0);
            }
        }
    }

    private void Awake()
    {
        _managerRuntimeRoot = ResolveManagerPersistentRoot();
        _consoleRuntimeRoot = ResolveConsolePersistentRoot();
        _hasExplicitConsolePersistentRoot = consoleUiPersistentRoot != null;

        if (useSingleton)
        {
            if (I != null && I != this)
            {
                DestroyDuplicateInstanceRoots();
                return;
            }

            I = this;
        }

        if (useDontDestroyOnLoad)
        {
            PersistRuntimeRoots();
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        RegisterDeveloperCommands();
        RegisterCommandInput();
        ResolveCommandPlayer();

        ApplyVersionToMainMenuText();
        SetConsoleVisible(openConsoleOnStart, false);

        if (printCommandHintOnStart)
            AddLine(Severity.Info, "Commands: type info in the input field for command list.", false);

        if (runDiagnosticsOnAwake)
            RunDiagnostics("Awake");
    }

    private void Start()
    {
        RefreshConsoleText();
    }

    private void OnDestroy()
    {
        if (I == this)
            I = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnregisterCommandInput();
        DisableFly(false);
        ResetPlayerSpeed(false);
    }

    private void OnValidate()
    {
        if (maxConsoleLines < 10)
            maxConsoleLines = 10;

        if (textHorizontalPadding < 0f)
            textHorizontalPadding = 0f;

        if (textVerticalPadding < 0f)
            textVerticalPadding = 0f;

        if (minContentHeight < 0f)
            minContentHeight = 0f;

        if (commandFlySpeed < 0f)
            commandFlySpeed = 0f;

        if (defaultSpeedMultiplier < 0.01f)
            defaultSpeedMultiplier = 0.01f;
    }

    private void Update()
    {
        bool typingCommand = IsCommandInputFocused();

        if (_consoleVisible && !typingCommand && Input.GetKeyDown(clearConsoleKey))
        {
            ClearConsoleAndMarkDirty();
            return;
        }

        if (Input.GetKeyDown(toggleConsoleKey))
        {
            bool wasVisible = _consoleVisible;
            ToggleConsole();

            if (!wasVisible && _consoleVisible)
            {
                if (_forceDiagnosticsOnNextConsoleOpen || rerunDiagnosticsWhenConsoleOpens)
                {
                    _forceDiagnosticsOnNextConsoleOpen = false;
                    RunDiagnostics("Console opened");
                }
            }
        }

        if (!typingCommand && Input.GetKeyDown(runDiagnosticsKey))
        {
            _forceDiagnosticsOnNextConsoleOpen = false;
            RunDiagnostics("Hotkey");
        }

        if (_flyEnabled)
            UpdateFlyMovement();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyVersionToMainMenuText();
        ResolveCommandPlayer();

        if (clearConsoleOnSceneLoaded)
            ClearConsoleText();

        _forceDiagnosticsOnNextConsoleOpen = false;

        if (runDiagnosticsOnSceneLoaded)
            RunDiagnostics("Scene loaded: " + scene.name);
    }

    [ContextMenu("Run Diagnostics Now")]
    public void RunDiagnosticsFromContextMenu()
    {
        RunDiagnostics("Context menu");
    }

    public void RunDiagnosticsNow()
    {
        _forceDiagnosticsOnNextConsoleOpen = false;
        RunDiagnostics("Public call");
    }

    public void ToggleConsole()
    {
        SetConsoleVisible(!_consoleVisible, true);
    }

    public void SetConsoleVisible(bool visible)
    {
        SetConsoleVisible(visible, true);
    }

    public void ClearConsoleNow()
    {
        ClearConsoleAndMarkDirty();
    }

    private void SetConsoleVisible(bool visible, bool refreshImmediately)
    {
        _consoleVisible = visible;

        if (consoleRoot != null)
            consoleRoot.SetActive(visible);

        if (refreshImmediately)
            RefreshConsoleText();

        if (visible && focusCommandInputOnConsoleOpen)
            FocusCommandInput();
    }

    private void RegisterCommandInput()
    {
        if (commandInput == null)
            return;

        commandInput.onSubmit.RemoveListener(SubmitDeveloperCommand);
        commandInput.onSubmit.AddListener(SubmitDeveloperCommand);
    }

    private void UnregisterCommandInput()
    {
        if (commandInput == null)
            return;

        commandInput.onSubmit.RemoveListener(SubmitDeveloperCommand);
    }

    private void FocusCommandInput()
    {
        if (!_consoleVisible || commandInput == null || !commandInput.gameObject.activeInHierarchy)
            return;

        commandInput.ActivateInputField();
        commandInput.Select();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(commandInput.gameObject);
    }

    private bool IsCommandInputFocused()
    {
        if (commandInput == null)
            return false;

        if (commandInput.isFocused)
            return true;

        return EventSystem.current != null && EventSystem.current.currentSelectedGameObject == commandInput.gameObject;
    }


    private void RegisterDeveloperCommands()
    {
        _developerCommands.Clear();

        _developerCommands["clear"] = CmdClear;
        _developerCommands["cls"] = CmdClear;

        _developerCommands["info"] = CmdInfo;
        _developerCommands["help"] = CmdInfo;

        _developerCommands["diag"] = CmdDiagnostics;
        _developerCommands["diagnostics"] = CmdDiagnostics;

        _developerCommands["fly"] = CmdFly;
        _developerCommands["speed"] = CmdSpeed;

        _developerCommands["pos"] = CmdPosition;
        _developerCommands["tp"] = CmdTeleport;
        _developerCommands["teleport"] = CmdTeleport;

        _developerCommands["gravity"] = CmdGravity;
        _developerCommands["time"] = CmdTimeScale;
        _developerCommands["scene"] = CmdScene;
        _developerCommands["reload"] = CmdReloadScene;
    }

    private void SubmitDeveloperCommand(string rawCommand)
    {
        if (string.IsNullOrWhiteSpace(rawCommand))
        {
            if (refocusCommandInputAfterSubmit)
                FocusCommandInput();

            return;
        }

        string commandLine = rawCommand.Trim();

        if (clearCommandInputAfterSubmit && commandInput != null)
            commandInput.text = string.Empty;

        AddLine(Severity.Info, "> " + commandLine, false);

        string[] parts = commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            RefreshConsoleText();
            if (refocusCommandInputAfterSubmit)
                FocusCommandInput();
            return;
        }

        string commandName = parts[0];
        string[] args = new string[Mathf.Max(0, parts.Length - 1)];
        for (int i = 1; i < parts.Length; i++)
            args[i - 1] = parts[i];

        if (_developerCommands.TryGetValue(commandName, out Action<string[]> command))
        {
            try
            {
                command.Invoke(args);
            }
            catch (Exception e)
            {
                AddLine(Severity.Warning, "Command error: " + e.Message, true);
            }
        }
        else
        {
            AddLine(Severity.Warning, "Unknown command: " + commandName + ". Type info.", false);
        }

        RefreshConsoleText();

        if (refocusCommandInputAfterSubmit)
            FocusCommandInput();
    }

    private void CmdClear(string[] args)
    {
        ClearConsoleText();
    }

    private void CmdInfo(string[] args)
    {
        AddLine(Severity.Info, "Available commands:", false);
        AddLine(Severity.Info, "clear / cls — очистить консоль", false);
        AddLine(Severity.Info, "info / help — показать список команд", false);
        AddLine(Severity.Info, "diag — запустить диагностику меню", false);
        AddLine(Severity.Info, "fly — включить/выключить полёт игрока", false);
        AddLine(Severity.Info, "speed — включить/выключить ускорение игрока x" + FormatFloat(defaultSpeedMultiplier), false);
        AddLine(Severity.Info, "speed 3 — поставить множитель скорости x3", false);
        AddLine(Severity.Info, "speed reset / speed off — вернуть обычную скорость", false);
        AddLine(Severity.Info, "pos — показать позицию игрока", false);
        AddLine(Severity.Info, "tp x y — телепортировать игрока. Пример: tp 10 4", false);
        AddLine(Severity.Info, "gravity 0 — изменить gravityScale игрока", false);
        AddLine(Severity.Info, "gravity reset — вернуть исходную гравитацию", false);
        AddLine(Severity.Info, "time 0.5 — изменить Time.timeScale", false);
        AddLine(Severity.Info, "time reset — вернуть Time.timeScale = 1", false);
        AddLine(Severity.Info, "scene — показать имя текущей сцены", false);
        AddLine(Severity.Info, "reload — перезагрузить текущую сцену", false);
    }

    private void CmdDiagnostics(string[] args)
    {
        RunDiagnostics("Command: diag");
    }

    private void CmdFly(string[] args)
    {
        if (_flyEnabled)
            DisableFly(true);
        else
            EnableFly();
    }

    private void EnableFly()
    {
        ResolveCommandPlayer();

        if (commandPlayerRigidbody == null)
        {
            AddLine(Severity.Warning, "fly: Rigidbody2D игрока не найден.", false);
            return;
        }

        if (!_hasOriginalGravityScale)
        {
            _originalGravityScale = commandPlayerRigidbody.gravityScale;
            _hasOriginalGravityScale = true;
        }

        _flyEnabled = true;
        commandPlayerRigidbody.gravityScale = 0f;

        if (resetPlayerVelocityOnFlyToggle)
            commandPlayerRigidbody.velocity = Vector2.zero;

        DisableMovementScriptsForFly();

        AddLine(Severity.Ok, "fly: ON", false);
    }

    private void DisableFly(bool print)
    {
        if (!_flyEnabled)
            return;

        _flyEnabled = false;

        if (commandPlayerRigidbody != null)
        {
            if (_hasOriginalGravityScale)
                commandPlayerRigidbody.gravityScale = _originalGravityScale;

            if (resetPlayerVelocityOnFlyToggle)
                commandPlayerRigidbody.velocity = Vector2.zero;
        }

        RestoreScriptsDisabledByFly();

        if (print)
            AddLine(Severity.Ok, "fly: OFF", false);
    }

    private void UpdateFlyMovement()
    {
        if (commandPlayerRigidbody == null)
        {
            ResolveCommandPlayer();
            if (commandPlayerRigidbody == null)
                return;
        }

        float x = 0f;
        float y = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            x += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Space))
            y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            y -= 1f;

        Vector2 move = new Vector2(x, y);
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        commandPlayerRigidbody.velocity = move * commandFlySpeed;
    }

    private void DisableMovementScriptsForFly()
    {
        _scriptsDisabledByFly.Clear();

        if (scriptsToDisableWhileFlying != null && scriptsToDisableWhileFlying.Length > 0)
        {
            for (int i = 0; i < scriptsToDisableWhileFlying.Length; i++)
                DisableScriptForFly(scriptsToDisableWhileFlying[i]);

            return;
        }

        if (!autoDisablePlayerControllerWhileFlying)
            return;

        ResolveCommandPlayer();

        if (commandPlayerRoot == null)
            return;

        PlayerController controller = commandPlayerRoot.GetComponent<PlayerController>();
        if (controller == null)
            controller = commandPlayerRoot.GetComponentInChildren<PlayerController>(true);

        DisableScriptForFly(controller);
    }

    private void DisableScriptForFly(MonoBehaviour script)
    {
        if (script == null)
            return;

        if (script == this)
            return;

        if (!script.enabled)
            return;

        script.enabled = false;
        _scriptsDisabledByFly.Add(script);
    }

    private void RestoreScriptsDisabledByFly()
    {
        for (int i = 0; i < _scriptsDisabledByFly.Count; i++)
        {
            MonoBehaviour script = _scriptsDisabledByFly[i];
            if (script != null)
                script.enabled = true;
        }

        _scriptsDisabledByFly.Clear();
    }

    private void CmdSpeed(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            if (_speedModified)
                ResetPlayerSpeed(true);
            else
                ApplyPlayerSpeed(defaultSpeedMultiplier);

            return;
        }

        string first = args[0].ToLowerInvariant();

        if (first == "reset" || first == "off" || first == "normal")
        {
            ResetPlayerSpeed(true);
            return;
        }

        if (!TryParseFloat(args[0], out float multiplier) || multiplier <= 0f)
        {
            AddLine(Severity.Warning, "speed: неверное значение. Пример: speed 2 или speed reset", false);
            return;
        }

        ApplyPlayerSpeed(multiplier);
    }

    private void ApplyPlayerSpeed(float multiplier)
    {
        ResolveCommandPlayer();

        MonoBehaviour[] targets = GetSpeedTargets();
        if (targets == null || targets.Length == 0)
        {
            AddLine(Severity.Warning, "speed: скрипты скорости не найдены. Заполни Speed Target Scripts или Command Player Root.", false);
            return;
        }

        if (_speedModified)
            ResetPlayerSpeed(false);

        int changed = 0;

        for (int i = 0; i < targets.Length; i++)
        {
            MonoBehaviour target = targets[i];
            if (target == null)
                continue;

            Type type = target.GetType();

            for (int n = 0; n < speedFieldNames.Length; n++)
            {
                string fieldName = speedFieldNames[n];
                if (string.IsNullOrWhiteSpace(fieldName))
                    continue;

                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null)
                    continue;

                if (field.FieldType != typeof(float) && field.FieldType != typeof(int))
                    continue;

                FieldKey key = new FieldKey(target, field);
                if (!_originalSpeedValues.ContainsKey(key))
                    _originalSpeedValues.Add(key, field.GetValue(target));

                object original = _originalSpeedValues[key];

                if (field.FieldType == typeof(float))
                {
                    field.SetValue(target, (float)original * multiplier);
                    changed++;
                }
                else if (field.FieldType == typeof(int))
                {
                    field.SetValue(target, Mathf.RoundToInt((int)original * multiplier));
                    changed++;
                }
            }
        }

        if (changed <= 0)
        {
            AddLine(Severity.Warning, "speed: подходящие поля скорости не найдены. Добавь имена полей в Speed Field Names.", false);
            return;
        }

        _speedModified = true;
        AddLine(Severity.Ok, "speed: ON x" + FormatFloat(multiplier) + " | changed fields: " + changed, false);
    }

    private void ResetPlayerSpeed(bool print)
    {
        foreach (KeyValuePair<FieldKey, object> pair in _originalSpeedValues)
        {
            if (pair.Key.Target == null || pair.Key.Field == null)
                continue;

            pair.Key.Field.SetValue(pair.Key.Target, pair.Value);
        }

        _speedModified = false;

        if (print)
            AddLine(Severity.Ok, "speed: OFF", false);
    }

    private MonoBehaviour[] GetSpeedTargets()
    {
        if (speedTargetScripts != null && speedTargetScripts.Length > 0)
            return speedTargetScripts;

        ResolveCommandPlayer();

        if (commandPlayerRoot == null)
            return Array.Empty<MonoBehaviour>();

        return commandPlayerRoot.GetComponentsInChildren<MonoBehaviour>(true);
    }

    private void CmdPosition(string[] args)
    {
        ResolveCommandPlayer();

        if (commandPlayerRoot == null)
        {
            AddLine(Severity.Warning, "pos: игрок не найден.", false);
            return;
        }

        Vector3 p = commandPlayerRoot.position;
        AddLine(Severity.Info, "player position: x=" + FormatFloat(p.x) + " y=" + FormatFloat(p.y) + " z=" + FormatFloat(p.z), false);
    }

    private void CmdTeleport(string[] args)
    {
        ResolveCommandPlayer();

        if (commandPlayerRoot == null)
        {
            AddLine(Severity.Warning, "tp: игрок не найден.", false);
            return;
        }

        if (args == null || args.Length < 2)
        {
            AddLine(Severity.Warning, "tp: нужно указать x y. Пример: tp 10 4", false);
            return;
        }

        if (!TryParseFloat(args[0], out float x) || !TryParseFloat(args[1], out float y))
        {
            AddLine(Severity.Warning, "tp: неверные координаты. Пример: tp 10 4", false);
            return;
        }

        Vector3 p = commandPlayerRoot.position;
        p.x = x;
        p.y = y;
        commandPlayerRoot.position = p;

        if (commandPlayerRigidbody != null)
            commandPlayerRigidbody.velocity = Vector2.zero;

        AddLine(Severity.Ok, "tp: player moved to x=" + FormatFloat(x) + " y=" + FormatFloat(y), false);
    }

    private void CmdGravity(string[] args)
    {
        ResolveCommandPlayer();

        if (commandPlayerRigidbody == null)
        {
            AddLine(Severity.Warning, "gravity: Rigidbody2D игрока не найден.", false);
            return;
        }

        if (!_hasOriginalGravityScale)
        {
            _originalGravityScale = commandPlayerRigidbody.gravityScale;
            _hasOriginalGravityScale = true;
        }

        if (args == null || args.Length == 0)
        {
            AddLine(Severity.Info, "gravity: current = " + FormatFloat(commandPlayerRigidbody.gravityScale), false);
            return;
        }

        string first = args[0].ToLowerInvariant();
        if (first == "reset" || first == "normal")
        {
            commandPlayerRigidbody.gravityScale = _originalGravityScale;
            AddLine(Severity.Ok, "gravity: reset to " + FormatFloat(_originalGravityScale), false);
            return;
        }

        if (!TryParseFloat(args[0], out float value))
        {
            AddLine(Severity.Warning, "gravity: неверное значение. Пример: gravity 0 или gravity reset", false);
            return;
        }

        commandPlayerRigidbody.gravityScale = value;
        AddLine(Severity.Ok, "gravity: " + FormatFloat(value), false);
    }

    private void CmdTimeScale(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            AddLine(Severity.Info, "time: current Time.timeScale = " + FormatFloat(Time.timeScale), false);
            return;
        }

        string first = args[0].ToLowerInvariant();
        if (first == "reset" || first == "normal")
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            AddLine(Severity.Ok, "time: reset to 1", false);
            return;
        }

        if (!TryParseFloat(args[0], out float value))
        {
            AddLine(Severity.Warning, "time: неверное значение. Пример: time 0.5 или time reset", false);
            return;
        }

        value = Mathf.Clamp(value, 0f, 10f);
        _lastCommandTimeScale = Time.timeScale;
        Time.timeScale = value;
        Time.fixedDeltaTime = 0.02f * Mathf.Max(value, 0.0001f);

        AddLine(Severity.Ok, "time: " + FormatFloat(value) + " | previous: " + FormatFloat(_lastCommandTimeScale), false);
    }

    private void CmdScene(string[] args)
    {
        Scene scene = SceneManager.GetActiveScene();
        AddLine(Severity.Info, "scene: " + scene.name + " | buildIndex: " + scene.buildIndex, false);
    }

    private void CmdReloadScene(string[] args)
    {
        DisableFly(false);
        ResetPlayerSpeed(false);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        Scene scene = SceneManager.GetActiveScene();
        AddLine(Severity.Info, "reload: " + scene.name, false);
        RefreshConsoleText();

        SceneManager.LoadScene(scene.buildIndex);
    }

    private void ResolveCommandPlayer()
    {
        if (commandPlayerRoot != null)
        {
            if (commandPlayerRigidbody == null)
                commandPlayerRigidbody = commandPlayerRoot.GetComponent<Rigidbody2D>();

            return;
        }

        if (commandPlayerTags != null)
        {
            for (int i = 0; i < commandPlayerTags.Length; i++)
            {
                string tagName = commandPlayerTags[i];
                if (string.IsNullOrWhiteSpace(tagName))
                    continue;

                GameObject found = null;

                try
                {
                    found = GameObject.FindGameObjectWithTag(tagName);
                }
                catch
                {
                    // Tag may not exist in the project. Ignore.
                }

                if (found == null)
                    continue;

                commandPlayerRoot = found.transform;
                commandPlayerRigidbody = found.GetComponent<Rigidbody2D>();
                return;
            }
        }

        List<PlayerController> players = FindSceneComponents<PlayerController>(SceneManager.GetActiveScene(), true);
        if (players.Count > 0 && players[0] != null)
        {
            commandPlayerRoot = players[0].transform;
            commandPlayerRigidbody = commandPlayerRoot.GetComponent<Rigidbody2D>();
        }
    }

    private static bool TryParseFloat(string raw, out float value)
    {
        value = 0f;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        raw = raw.Replace(',', '.');
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private GameObject ResolveManagerPersistentRoot()
    {
        GameObject source = managerPersistentRootOverride != null
            ? managerPersistentRootOverride
            : gameObject;

        return ResolveRootObject(source);
    }

    private GameObject ResolveConsolePersistentRoot()
    {
        if (consoleUiPersistentRoot != null)
            return ResolveRootObject(consoleUiPersistentRoot);

        if (consoleRoot != null)
            return ResolveRootObject(consoleRoot);

        return null;
    }

    private static GameObject ResolveRootObject(GameObject go)
    {
        if (go == null)
            return null;

        Transform t = go.transform;
        while (t.parent != null)
            t = t.parent;

        return t.gameObject;
    }

    private void PersistRuntimeRoots()
    {
        if (_managerRuntimeRoot != null)
            DontDestroyOnLoad(_managerRuntimeRoot);

        if (_consoleRuntimeRoot != null && _consoleRuntimeRoot != _managerRuntimeRoot)
            DontDestroyOnLoad(_consoleRuntimeRoot);
    }

    private void DestroyDuplicateInstanceRoots()
    {
        GameObject duplicateManagerRoot = _managerRuntimeRoot != null
            ? _managerRuntimeRoot
            : ResolveManagerPersistentRoot();

        GameObject duplicateConsoleRoot = _consoleRuntimeRoot != null
            ? _consoleRuntimeRoot
            : ResolveConsolePersistentRoot();

        if (_hasExplicitConsolePersistentRoot &&
            duplicateConsoleRoot != null &&
            duplicateConsoleRoot != duplicateManagerRoot)
        {
            Destroy(duplicateConsoleRoot);
        }

        if (duplicateManagerRoot != null)
            Destroy(duplicateManagerRoot);
        else
            Destroy(gameObject);
    }

    private void ApplyVersionToMainMenuText()
    {
        TMP_Text target = ResolveMainMenuVersionText();
        if (target == null)
            return;

        target.text = GetFullVersionString();
    }

    private TMP_Text ResolveMainMenuVersionText()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (directMainMenuVersionText != null &&
            directMainMenuVersionText.gameObject != null &&
            directMainMenuVersionText.gameObject.scene.IsValid() &&
            directMainMenuVersionText.gameObject.scene.handle == activeScene.handle)
        {
            return directMainMenuVersionText;
        }

        if (!findMainMenuVersionTextByObjectName)
            return null;

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName) &&
            activeScene.name != mainMenuSceneName)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(mainMenuVersionObjectName))
            return null;

        List<TMP_Text> texts = FindSceneComponents<TMP_Text>(activeScene, includeInactiveSceneObjects);
        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text text = texts[i];
            if (text == null) continue;

            if (text.gameObject.name == mainMenuVersionObjectName)
                return text;
        }

        return null;
    }

    private string GetFullVersionString()
    {
        return $"{versionPrefix}{patchVersion}";
    }

    private void RunDiagnostics(string trigger)
    {
        Scene scene = SceneManager.GetActiveScene();

        int okCount = 0;
        int warningCount = 0;

        AddLine(Severity.Info, "==================================================", false);
        AddLine(Severity.Info, $"Diagnostics trigger: {trigger}", false);
        AddLine(Severity.Info, $"Scene: {scene.name}", false);
        AddLine(Severity.Info, $"Version: {GetFullVersionString()}", false);
        AddLine(Severity.Info, $"Time: {DateTime.Now:HH:mm:ss}", false);

        if (checkEventSystems)
            CheckEventSystem(scene, ref okCount, ref warningCount);

        if (checkSelectableNavigation)
            CheckSceneSelectableNavigation(scene, ref okCount, ref warningCount);

        if (checkMainMenuPanelsUI)
        {
            CheckComponentsInScene<MainMenuPanelsUI>(
                scene,
                "MainMenuPanelsUI",
                ignoreFieldsMainMenuPanelsUI,
                ref okCount,
                ref warningCount);
        }

        if (checkPauseMenuUI)
        {
            CheckComponentsInScene<PauseMenuUI>(
                scene,
                "PauseMenuUI",
                ignoreFieldsPauseMenuUI,
                ref okCount,
                ref warningCount);
        }

        if (checkSettingsTabsSwitcher)
        {
            CheckComponentsInScene<SettingsTabsSwitcher>(
                scene,
                "SettingsTabsSwitcher",
                ignoreFieldsSettingsTabsSwitcher,
                ref okCount,
                ref warningCount);
        }

        if (checkLegacyKeycodeRebind)
        {
            CheckComponentsInScene<LegacyKeycodeRebind>(
                scene,
                "LegacyKeycodeRebind",
                ignoreFieldsLegacyKeycodeRebind,
                ref okCount,
                ref warningCount);

            CheckLegacyRows(scene, ref okCount, ref warningCount);
        }

        CheckVersionLabel(scene, ref okCount, ref warningCount);

        AddLine(
            warningCount > 0 ? Severity.Warning : Severity.Ok,
            $"SUMMARY -> OK: {okCount} | WARN: {warningCount}",
            true);

        RefreshConsoleText();
    }

    private void CheckEventSystem(Scene scene, ref int okCount, ref int warningCount)
    {
        List<EventSystem> systems = FindSceneComponents<EventSystem>(scene, includeInactiveSceneObjects);

        if (systems.Count == 0)
        {
            AddLine(Severity.Warning, "EventSystem: в сцене нет ни одного EventSystem.", true);
            warningCount++;
            return;
        }

        if (systems.Count > 1)
        {
            AddLine(Severity.Warning, $"EventSystem: найдено несколько EventSystem ({systems.Count}).", true);
            warningCount++;
            return;
        }

        AddLine(Severity.Ok, "EventSystem: найден ровно один.", false);
        okCount++;
    }

    private void CheckVersionLabel(Scene scene, ref int okCount, ref int warningCount)
    {
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && scene.name != mainMenuSceneName)
        {
            AddLine(Severity.Info, "Version label: текущая сцена не MainMenu, проверка пропущена.", false);
            return;
        }

        TMP_Text versionText = ResolveMainMenuVersionText();
        if (versionText == null)
        {
            AddLine(Severity.Warning, "Version label: TMP для версии не найден.", true);
            warningCount++;
            return;
        }

        string expected = GetFullVersionString();

        if (versionText.text != expected)
            versionText.text = expected;

        AddLine(Severity.Ok, $"Version label: найден и обновлён -> {expected}", false);
        okCount++;
    }

    private void CheckSceneSelectableNavigation(Scene scene, ref int okCount, ref int warningCount)
    {
        List<Selectable> selectables = FindSceneComponents<Selectable>(scene, includeInactiveSceneObjects);

        if (selectables.Count == 0)
        {
            AddLine(Severity.Warning, "Selectable: в сцене не найдено ни одного Selectable.", true);
            warningCount++;
            return;
        }

        int issues = 0;

        for (int i = 0; i < selectables.Count; i++)
        {
            Selectable selectable = selectables[i];
            if (selectable == null) continue;
            if (!selectable.interactable) continue;

            Navigation nav = selectable.navigation;
            if (nav.mode == Navigation.Mode.None)
            {
                AddLine(
                    Severity.Warning,
                    $"Navigation=None -> {BuildHierarchyPath(selectable.transform)} ({selectable.GetType().Name})",
                    true);
                issues++;
            }
        }

        if (issues == 0)
        {
            AddLine(Severity.Ok, "Selectable navigation: явных проблем не найдено.", false);
            okCount++;
        }
        else
        {
            warningCount += issues;
        }
    }

    private void CheckLegacyRows(Scene scene, ref int okCount, ref int warningCount)
    {
        List<LegacyKeycodeRebind> list = FindSceneComponents<LegacyKeycodeRebind>(scene, includeInactiveSceneObjects);
        for (int i = 0; i < list.Count; i++)
        {
            LegacyKeycodeRebind rebind = list[i];
            if (rebind == null) continue;

            FieldInfo rowsField = typeof(LegacyKeycodeRebind).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic);
            if (rowsField == null)
                continue;

            object value = rowsField.GetValue(rebind);
            IList rows = value as IList;

            if (rows == null || rows.Count == 0)
            {
                AddLine(
                    Severity.Warning,
                    $"LegacyKeycodeRebind [{BuildHierarchyPath(rebind.transform)}]: список rows пустой.",
                    true);
                warningCount++;
            }
            else
            {
                AddLine(
                    Severity.Ok,
                    $"LegacyKeycodeRebind [{BuildHierarchyPath(rebind.transform)}]: rows = {rows.Count}",
                    false);
                okCount++;
            }
        }
    }

    private void CheckComponentsInScene<T>(
        Scene scene,
        string label,
        List<string> ignoreFields,
        ref int okCount,
        ref int warningCount) where T : Component
    {
        List<T> components = FindSceneComponents<T>(scene, includeInactiveSceneObjects);

        if (components.Count == 0)
        {
            AddLine(Severity.Info, $"{label}: в этой сцене компонент не найден.", false);
            return;
        }

        HashSet<string> ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < ignoreFields.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(ignoreFields[i]))
                ignore.Add(ignoreFields[i].Trim());
        }

        for (int i = 0; i < components.Count; i++)
        {
            T component = components[i];
            if (component == null) continue;

            int localWarnings = 0;

            InspectSerializedFieldsRecursive(
                ownerComponent: component,
                currentObject: component,
                currentType: component.GetType(),
                pathPrefix: component.GetType().Name,
                ignoreFields: ignore,
                ref localWarnings);

            CheckSpecialStringFields(component, ref localWarnings);

            if (localWarnings == 0)
            {
                AddLine(
                    Severity.Ok,
                    $"{label}: OK -> {BuildHierarchyPath(component.transform)}",
                    false);
                okCount++;
            }
            else
            {
                warningCount += localWarnings;
            }
        }
    }

    private void CheckSpecialStringFields(Component component, ref int localWarnings)
    {
        if (component is MainMenuPanelsUI)
        {
            Button newGameButton = GetPrivateFieldValue<Button>(component, "newGameButton");
            string newGameSceneName = GetPrivateFieldValue<string>(component, "newGameSceneName");

            if (newGameButton != null && string.IsNullOrWhiteSpace(newGameSceneName))
            {
                AddLine(
                    Severity.Warning,
                    $"MainMenuPanelsUI [{BuildHierarchyPath(component.transform)}]: newGameButton задан, но newGameSceneName пустой.",
                    true);
                localWarnings++;
            }
        }

        if (component is PauseMenuUI)
        {
            Button exitToMenuButton = GetPrivateFieldValue<Button>(component, "exitToMenuButton");
            string mainMenuScene = GetPrivateFieldValue<string>(component, "mainMenuSceneName");

            if (exitToMenuButton != null && string.IsNullOrWhiteSpace(mainMenuScene))
            {
                AddLine(
                    Severity.Warning,
                    $"PauseMenuUI [{BuildHierarchyPath(component.transform)}]: exitToMenuButton задан, но mainMenuSceneName пустой.",
                    true);
                localWarnings++;
            }
        }
    }

    private void InspectSerializedFieldsRecursive(
        Component ownerComponent,
        object currentObject,
        Type currentType,
        string pathPrefix,
        HashSet<string> ignoreFields,
        ref int localWarnings)
    {
        if (currentObject == null || currentType == null)
            return;

        FieldInfo[] fields = currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!ShouldInspectSerializedField(field))
                continue;

            string fieldPath = $"{pathPrefix}.{field.Name}";
            if (IsFieldIgnored(field.Name, fieldPath, ignoreFields))
                continue;

            object value = field.GetValue(currentObject);
            Type fieldType = field.FieldType;

            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                UnityEngine.Object unityObject = value as UnityEngine.Object;
                if (unityObject == null)
                {
                    AddLine(
                        Severity.Warning,
                        $"{ownerComponent.GetType().Name} [{BuildHierarchyPath(ownerComponent.transform)}]: NULL -> {fieldPath}",
                        true);
                    localWarnings++;
                }

                continue;
            }

            if (typeof(IList).IsAssignableFrom(fieldType))
            {
                IList list = value as IList;
                if (list == null)
                    continue;

                for (int listIndex = 0; listIndex < list.Count; listIndex++)
                {
                    object element = list[listIndex];
                    if (element == null)
                    {
                        AddLine(
                            Severity.Warning,
                            $"{ownerComponent.GetType().Name} [{BuildHierarchyPath(ownerComponent.transform)}]: NULL ELEMENT -> {fieldPath}[{listIndex}]",
                            true);
                        localWarnings++;
                        continue;
                    }

                    Type elementType = element.GetType();

                    if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
                    {
                        UnityEngine.Object unityElement = element as UnityEngine.Object;
                        if (unityElement == null)
                        {
                            AddLine(
                                Severity.Warning,
                                $"{ownerComponent.GetType().Name} [{BuildHierarchyPath(ownerComponent.transform)}]: NULL ELEMENT -> {fieldPath}[{listIndex}]",
                                true);
                            localWarnings++;
                        }
                    }
                    else if (ShouldRecurseIntoType(elementType))
                    {
                        InspectSerializedFieldsRecursive(
                            ownerComponent,
                            element,
                            elementType,
                            $"{fieldPath}[{listIndex}]",
                            ignoreFields,
                            ref localWarnings);
                    }
                }

                continue;
            }

            if (ShouldRecurseIntoType(fieldType) && value != null)
            {
                InspectSerializedFieldsRecursive(
                    ownerComponent,
                    value,
                    fieldType,
                    fieldPath,
                    ignoreFields,
                    ref localWarnings);
            }
        }
    }

    private static bool ShouldInspectSerializedField(FieldInfo field)
    {
        if (field.IsStatic)
            return false;

        if (Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
            return false;

        if (field.IsPublic)
            return true;

        return Attribute.IsDefined(field, typeof(SerializeField));
    }

    private static bool ShouldRecurseIntoType(Type type)
    {
        if (type == null)
            return false;

        if (type.IsPrimitive || type.IsEnum)
            return false;

        if (type == typeof(string) || type == typeof(decimal))
            return false;

        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            return false;

        if (typeof(IList).IsAssignableFrom(type))
            return false;

        string ns = type.Namespace ?? string.Empty;

        if (ns.StartsWith("System", StringComparison.Ordinal))
            return false;

        if (ns.StartsWith("UnityEngine", StringComparison.Ordinal))
            return false;

        if (ns.StartsWith("TMPro", StringComparison.Ordinal))
            return false;

        return true;
    }

    private static bool IsFieldIgnored(string fieldName, string fieldPath, HashSet<string> ignoreFields)
    {
        if (ignoreFields == null || ignoreFields.Count == 0)
            return false;

        if (ignoreFields.Contains(fieldName))
            return true;

        if (ignoreFields.Contains(fieldPath))
            return true;

        foreach (string item in ignoreFields)
        {
            if (string.IsNullOrWhiteSpace(item))
                continue;

            if (fieldPath.EndsWith("." + item, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static TField GetPrivateFieldValue<TField>(Component component, string fieldName)
    {
        if (component == null || string.IsNullOrWhiteSpace(fieldName))
            return default;

        FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
            return default;

        object value = field.GetValue(component);
        if (value == null)
            return default;

        if (value is TField typed)
            return typed;

        return default;
    }

    private static List<T> FindSceneComponents<T>(Scene scene, bool includeInactive) where T : Component
    {
        T[] all = Resources.FindObjectsOfTypeAll<T>();
        List<T> result = new List<T>(all.Length);

        for (int i = 0; i < all.Length; i++)
        {
            T item = all[i];
            if (item == null) continue;

            GameObject go = item.gameObject;
            if (go == null) continue;
            if (!go.scene.IsValid()) continue;
            if (go.scene.handle != scene.handle) continue;
            if (!includeInactive && !go.activeInHierarchy) continue;

            result.Add(item);
        }

        return result;
    }

    private void AddLine(Severity severity, string message, bool sendToUnityConsole)
    {
        string prefix =
            severity == Severity.Ok ? "[OK] " :
            severity == Severity.Warning ? "[WARN] " :
            "[INFO] ";

        string final = prefix + message;

        _consoleLines.Add(final);

        while (_consoleLines.Count > maxConsoleLines)
            _consoleLines.RemoveAt(0);

        if (!enableUnityConsoleOutput || !sendToUnityConsole)
            return;

        if (severity == Severity.Warning)
        {
            Debug.LogWarning(final, this);
        }
        else if (severity == Severity.Ok)
        {
            if (writeOkLinesToUnityConsole)
                Debug.Log(final, this);
        }
        else
        {
            Debug.Log(final, this);
        }
    }

    private void RefreshConsoleText()
    {
        if (consoleText == null)
            return;

        _builder.Clear();

        for (int i = 0; i < _consoleLines.Count; i++)
            _builder.AppendLine(_consoleLines[i]);

        consoleText.text = _builder.ToString();

        RebuildConsoleGeometry();

        if (_scrollRoutine != null)
            StopCoroutine(_scrollRoutine);

        _scrollRoutine = StartCoroutine(ApplyScrollNextFrames(stickToBottomOnRefresh));
    }

    private void RebuildConsoleGeometry()
    {
        if (consoleText == null || consoleContent == null)
            return;

        RectTransform textRt = consoleText.rectTransform;
        RectTransform contentRt = consoleContent;

        float contentWidth = contentRt.rect.width;
        if (contentWidth <= 1f)
            contentWidth = GetSafeWidthFromParents(contentRt);

        if (contentWidth <= 1f)
            contentWidth = 600f;

        float usableTextWidth = Mathf.Max(10f, contentWidth - textHorizontalPadding * 2f);

        consoleText.enableWordWrapping = true;
        consoleText.overflowMode = TextOverflowModes.Overflow;
        consoleText.ForceMeshUpdate();

        Vector2 preferred = consoleText.GetPreferredValues(consoleText.text, usableTextWidth, 0f);
        float textHeight = Mathf.Max(preferred.y, consoleText.fontSize + 8f);
        float finalContentHeight = Mathf.Max(minContentHeight, textHeight + textVerticalPadding * 2f);

        ConfigureTopStretchRect(contentRt, 0f, 0f, 0f, 0f);
        ConfigureTopStretchRect(
            textRt,
            textHorizontalPadding,
            -textHorizontalPadding,
            -textVerticalPadding,
            -(textVerticalPadding + textHeight));

        textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
        contentRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalContentHeight);

        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator ApplyScrollNextFrames(bool scrollToBottom)
    {
        yield return null;
        RebuildConsoleGeometry();

        yield return null;
        RebuildConsoleGeometry();

        if (consoleScrollRect != null)
        {
            consoleScrollRect.StopMovement();

            if (scrollToBottom)
                consoleScrollRect.verticalNormalizedPosition = 0f;
        }

        _scrollRoutine = null;
    }

    private void ClearConsoleAndMarkDirty()
    {
        ClearConsoleText();
        _forceDiagnosticsOnNextConsoleOpen = true;
    }

    private void ClearConsoleText()
    {
        _consoleLines.Clear();

        if (consoleText != null)
            consoleText.text = string.Empty;

        if (_scrollRoutine != null)
        {
            StopCoroutine(_scrollRoutine);
            _scrollRoutine = null;
        }

        RebuildConsoleGeometry();

        if (_consoleVisible)
            RefreshConsoleText();
    }

    private static void ConfigureTopStretchRect(
        RectTransform rt,
        float left,
        float right,
        float top,
        float bottom)
    {
        if (rt == null)
            return;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    private static float GetSafeWidthFromParents(RectTransform rt)
    {
        if (rt == null)
            return 0f;

        RectTransform current = rt;
        for (int i = 0; i < 8 && current != null; i++)
        {
            if (current.rect.width > 1f)
                return current.rect.width;

            current = current.parent as RectTransform;
        }

        return 0f;
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
            return "<null>";

        StringBuilder sb = new StringBuilder(target.name);
        Transform current = target.parent;

        while (current != null)
        {
            sb.Insert(0, current.name + "/");
            current = current.parent;
        }

        return sb.ToString();
    }
}