using System;
using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class SavePanelRebindCloseHotkeys : MonoBehaviour
    {
        private static int activeSavePanelsCount;
        private static int consumedUntilFrame = -1;
        private static float consumedUntilUnscaledTime = -1f;

        private bool countedAsActive;

        [Header("Target")]
        [SerializeField, Tooltip("Меню сохранений, которое нужно закрывать горячими клавишами. Рекомендация: оставить пустым, если скрипт висит на том же объекте, где SaveSlotsMenuUI.")]
        private SaveSlotsMenuUI saveSlotsMenu;

        [Header("Legacy Rebind Integration")]
        [SerializeField, Tooltip("PlayerPrefs ключ текущей системы ребинда. Рекомендация: оставить LEGACY_INPUT_BINDINGS_V4_GAMEPAD_AXES, он совпадает с текущей системой LegacyKeycodeRebind.")]
        private string legacyPlayerPrefsKey = "LEGACY_INPUT_BINDINGS_V4_GAMEPAD_AXES";

        [SerializeField, Tooltip("Как часто перечитывать PlayerPrefs ребинда, чтобы подхватить изменения в настройках. Рекомендация: 0.5 секунды.")]
        private float reloadBindingsInterval = 0.5f;

        [Header("Close Actions")]
        [SerializeField, Tooltip("Закрывать панель по действию Back. Рекомендация: включено. Обычно это Escape на клавиатуре и B/Circle на геймпаде.")]
        private bool closeOnBack = true;

        [SerializeField, Tooltip("Закрывать панель по действию Pause. Рекомендация: включено, чтобы Escape/Start закрывали меню сохранений как обычную UI-панель.")]
        private bool closeOnPause = true;

        [Header("Fallback Bindings")]
        [SerializeField, Tooltip("Клавиша Back, если ребинд ещё не сохранён. Рекомендация: Escape.")]
        private KeyCode keyboardBackFallback = KeyCode.Escape;

        [SerializeField, Tooltip("Клавиша Pause, если ребинд ещё не сохранён. Рекомендация: Escape.")]
        private KeyCode keyboardPauseFallback = KeyCode.Escape;

        [SerializeField, Tooltip("Кнопка геймпада Back, если ребинд ещё не сохранён. Рекомендация: JoystickButton1 = B/Circle.")]
        private KeyCode gamepadBackFallback = KeyCode.JoystickButton1;

        [SerializeField, Tooltip("Кнопка геймпада Pause, если ребинд ещё не сохранён. Рекомендация: JoystickButton7 = Start/Menu.")]
        private KeyCode gamepadPauseFallback = KeyCode.JoystickButton7;

        [Header("Optional Blockers")]
        [SerializeField, Tooltip("Объекты, при активности которых панель не будет закрываться этим скриптом. Рекомендация: сюда позже добавить окно подтверждения удаления/перезаписи, чтобы сначала обрабатывалось оно.")]
        private GameObject[] doNotCloseWhenTheseObjectsAreActive;

        [Header("Input Consume Guard")]
        [SerializeField, Tooltip("На сколько кадров после открытия/закрытия панели блокировать глобальные hotkey-действия, например PauseMenuUI. Рекомендация: 2 кадра.")]
        private int consumeFrames = 2;

        [SerializeField, Tooltip("На сколько секунд после открытия/закрытия панели блокировать глобальные hotkey-действия. Рекомендация: 0.08 секунды.")]
        private float consumeSeconds = 0.08f;

        [Header("Debug")]
        [SerializeField, Tooltip("Писать в Console предупреждение, если JSON ребинда не удалось прочитать. Рекомендация: включать только при отладке.")]
        private bool logBindingReadErrors = false;

        private LegacyInputBinding keyboardBack;
        private LegacyInputBinding keyboardPause;
        private LegacyInputBinding gamepadBack;
        private LegacyInputBinding gamepadPause;

        private string cachedJson = "";
        private float nextReloadTime;

        private bool keyboardBackAxisWasActive;
        private bool keyboardPauseAxisWasActive;
        private bool gamepadBackAxisWasActive;
        private bool gamepadPauseAxisWasActive;

        public static bool ShouldBlockGlobalPauseInput()
        {
            return activeSavePanelsCount > 0 ||
                   Time.frameCount <= consumedUntilFrame ||
                   Time.unscaledTime <= consumedUntilUnscaledTime;
        }

        private static void ConsumeInputForShortMoment(int frames, float seconds)
        {
            consumedUntilFrame = Mathf.Max(consumedUntilFrame, Time.frameCount + Mathf.Max(1, frames));
            consumedUntilUnscaledTime = Mathf.Max(consumedUntilUnscaledTime, Time.unscaledTime + Mathf.Max(0.01f, seconds));
        }

        private void Awake()
        {
            if (saveSlotsMenu == null)
                saveSlotsMenu = GetComponent<SaveSlotsMenuUI>();

            ResetToFallbackBindings();
            ReloadBindingsIfNeeded(true);
        }

        private void OnEnable()
        {
            RegisterActivePanel();
            ConsumeInputForShortMoment(consumeFrames, consumeSeconds);
        }

        private void OnDisable()
        {
            UnregisterActivePanel();
            ConsumeInputForShortMoment(consumeFrames, consumeSeconds);
        }

        private void OnDestroy()
        {
            UnregisterActivePanel();
        }

        private void Update()
        {
            if (saveSlotsMenu == null)
                return;

            if (!gameObject.activeInHierarchy)
                return;

            if (HasActiveBlocker())
                return;

            ReloadBindingsIfNeeded(false);

            bool backPressed = closeOnBack && IsBackPressedThisFrame();
            bool pausePressed = closeOnPause && IsPausePressedThisFrame();

            if (backPressed || pausePressed)
            {
                ConsumeInputForShortMoment(consumeFrames, consumeSeconds);
                saveSlotsMenu.Close();
            }
        }

        private void RegisterActivePanel()
        {
            if (countedAsActive)
                return;

            countedAsActive = true;
            activeSavePanelsCount++;
        }

        private void UnregisterActivePanel()
        {
            if (!countedAsActive)
                return;

            countedAsActive = false;
            activeSavePanelsCount = Mathf.Max(0, activeSavePanelsCount - 1);
        }

        private bool IsBackPressedThisFrame()
        {
            bool keyboardDown = GetBindingDown(keyboardBack, ref keyboardBackAxisWasActive);
            bool gamepadDown = GetBindingDown(gamepadBack, ref gamepadBackAxisWasActive);
            return keyboardDown || gamepadDown;
        }

        private bool IsPausePressedThisFrame()
        {
            bool keyboardDown = GetBindingDown(keyboardPause, ref keyboardPauseAxisWasActive);
            bool gamepadDown = GetBindingDown(gamepadPause, ref gamepadPauseAxisWasActive);
            return keyboardDown || gamepadDown;
        }

        private bool HasActiveBlocker()
        {
            if (doNotCloseWhenTheseObjectsAreActive == null)
                return false;

            for (int i = 0; i < doNotCloseWhenTheseObjectsAreActive.Length; i++)
            {
                GameObject blocker = doNotCloseWhenTheseObjectsAreActive[i];

                if (blocker != null && blocker.activeInHierarchy)
                    return true;
            }

            return false;
        }

        private void ReloadBindingsIfNeeded(bool force)
        {
            if (!force && Time.unscaledTime < nextReloadTime)
                return;

            nextReloadTime = Time.unscaledTime + Mathf.Max(0.1f, reloadBindingsInterval);

            string json = PlayerPrefs.GetString(legacyPlayerPrefsKey, "");

            if (!force && json == cachedJson)
                return;

            cachedJson = json;
            ResetToFallbackBindings();

            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                LegacyRebindSaveData data = JsonUtility.FromJson<LegacyRebindSaveData>(json);

                if (data == null)
                    return;

                if (data.keyboard != null)
                {
                    if (data.keyboard.back != null)
                        keyboardBack = data.keyboard.back;

                    if (data.keyboard.pause != null)
                        keyboardPause = data.keyboard.pause;
                }

                if (data.gamepad != null)
                {
                    if (data.gamepad.back != null)
                        gamepadBack = data.gamepad.back;

                    if (data.gamepad.pause != null)
                        gamepadPause = data.gamepad.pause;
                }
            }
            catch (Exception ex)
            {
                if (logBindingReadErrors)
                    Debug.LogWarning("Failed to read legacy rebind JSON for save panel close hotkeys. Fallback bindings will be used. " + ex.Message);
            }
        }

        private void ResetToFallbackBindings()
        {
            keyboardBack = LegacyInputBinding.Button(keyboardBackFallback);
            keyboardPause = LegacyInputBinding.Button(keyboardPauseFallback);
            gamepadBack = LegacyInputBinding.Button(gamepadBackFallback);
            gamepadPause = LegacyInputBinding.Button(gamepadPauseFallback);
        }

        private static bool GetBindingDown(LegacyInputBinding binding, ref bool axisWasActive)
        {
            if (binding == null)
                return false;

            if (binding.kind == LegacyBindingKind.Button)
                return binding.key != KeyCode.None && Input.GetKeyDown(binding.key);

            if (binding.kind == LegacyBindingKind.AxisPositive || binding.kind == LegacyBindingKind.AxisNegative)
            {
                if (string.IsNullOrWhiteSpace(binding.axisName))
                    return false;

                float raw = Input.GetAxisRaw(binding.axisName);
                float signed = binding.kind == LegacyBindingKind.AxisPositive ? raw : -raw;

                bool active = signed >= Mathf.Clamp(binding.axisThreshold, 0.05f, 0.99f);
                bool down = active && !axisWasActive;

                axisWasActive = active;
                return down;
            }

            return false;
        }

        [Serializable]
        private sealed class LegacyRebindSaveData
        {
            public LegacyKeyboardBinds keyboard;
            public LegacyGamepadBinds gamepad;
        }

        [Serializable]
        private sealed class LegacyKeyboardBinds
        {
            public LegacyInputBinding back;
            public LegacyInputBinding pause;
        }

        [Serializable]
        private sealed class LegacyGamepadBinds
        {
            public LegacyInputBinding back;
            public LegacyInputBinding pause;
        }

        [Serializable]
        private sealed class LegacyInputBinding
        {
            public LegacyBindingKind kind = LegacyBindingKind.Button;
            public KeyCode key = KeyCode.None;
            public string axisName = "";
            public float axisThreshold = 0.5f;

            public static LegacyInputBinding Button(KeyCode key)
            {
                LegacyInputBinding binding = new LegacyInputBinding();
                binding.kind = LegacyBindingKind.Button;
                binding.key = key;
                binding.axisName = "";
                binding.axisThreshold = 0.5f;
                return binding;
            }
        }

        private enum LegacyBindingKind
        {
            None,
            Button,
            AxisPositive,
            AxisNegative
        }
    }
}