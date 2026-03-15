using System;
using System.Collections;
using System.Collections.Generic;
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

    private bool _consoleVisible;
    private bool _forceDiagnosticsOnNextConsoleOpen;
    private Coroutine _scrollRoutine;

    private GameObject _managerRuntimeRoot;
    private GameObject _consoleRuntimeRoot;
    private bool _hasExplicitConsolePersistentRoot;

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

        ApplyVersionToMainMenuText();
        SetConsoleVisible(openConsoleOnStart, false);

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
    }

    private void Update()
    {
        if (_consoleVisible && Input.GetKeyDown(clearConsoleKey))
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

        if (Input.GetKeyDown(runDiagnosticsKey))
        {
            _forceDiagnosticsOnNextConsoleOpen = false;
            RunDiagnostics("Hotkey");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyVersionToMainMenuText();

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