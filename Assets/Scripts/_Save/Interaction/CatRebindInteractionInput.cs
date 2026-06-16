using System;
using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class CatRebindInteractionInput : MonoBehaviour
    {
        [Header("Legacy Rebind Integration")]
        [SerializeField, Tooltip("PlayerPrefs ключ текущей системы ребинда. Рекомендация: оставить LEGACY_INPUT_BINDINGS_V4_GAMEPAD_AXES, он совпадает с LegacyKeycodeRebind в проекте.")]
        private string legacyPlayerPrefsKey = "LEGACY_INPUT_BINDINGS_V4_GAMEPAD_AXES";

        [SerializeField, Tooltip("Как часто перечитывать PlayerPrefs ребинда, чтобы подхватить изменения в настройках. Рекомендация: 0.5 секунды.")]
        private float reloadBindingsInterval = 0.5f;

        [Header("Fallback Bindings")]
        [SerializeField, Tooltip("Клавиша взаимодействия, если ребинд ещё не сохранён. В текущем проекте дефолт Interact = F.")]
        private KeyCode keyboardFallback = KeyCode.F;

        [SerializeField, Tooltip("Кнопка геймпада для взаимодействия, если ребинд ещё не сохранён. Рекомендация: JoystickButton2 = X/Square.")]
        private KeyCode gamepadFallback = KeyCode.JoystickButton2;

        [SerializeField, Tooltip("Показывать в Console предупреждение, если JSON ребинда не удалось прочитать. Рекомендация: включить на этапе настройки.")]
        private bool logBindingReadErrors = false;

        private LegacyInputBinding keyboardInteract;
        private LegacyInputBinding gamepadInteract;
        private string cachedJson = "";
        private float nextReloadTime;
        private bool keyboardAxisWasActive;
        private bool gamepadAxisWasActive;

        private void Awake()
        {
            keyboardInteract = LegacyInputBinding.Button(keyboardFallback);
            gamepadInteract = LegacyInputBinding.Button(gamepadFallback);
            ReloadBindingsIfNeeded(true);
        }

        public bool WasInteractPressedThisFrame()
        {
            ReloadBindingsIfNeeded(false);

            bool keyboardDown = GetBindingDown(keyboardInteract, ref keyboardAxisWasActive);
            bool gamepadDown = GetBindingDown(gamepadInteract, ref gamepadAxisWasActive);
            return keyboardDown || gamepadDown;
        }

        public string GetInteractBindingDisplayName()
        {
            ReloadBindingsIfNeeded(false);
            return BindingToText(keyboardInteract, keyboardFallback) + " / " + BindingToText(gamepadInteract, gamepadFallback);
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

            keyboardInteract = LegacyInputBinding.Button(keyboardFallback);
            gamepadInteract = LegacyInputBinding.Button(gamepadFallback);

            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                LegacyRebindSaveData data = JsonUtility.FromJson<LegacyRebindSaveData>(json);
                if (data != null)
                {
                    if (data.keyboard != null && data.keyboard.interact != null)
                        keyboardInteract = data.keyboard.interact;

                    if (data.gamepad != null && data.gamepad.interact != null)
                        gamepadInteract = data.gamepad.interact;
                }
            }
            catch (Exception ex)
            {
                if (logBindingReadErrors)
                    Debug.LogWarning("Failed to read legacy rebind JSON. Fallback bindings will be used. " + ex.Message);
            }
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

        private static string BindingToText(LegacyInputBinding binding, KeyCode fallback)
        {
            if (binding == null)
                return fallback.ToString();

            if (binding.kind == LegacyBindingKind.Button)
                return binding.key == KeyCode.None ? fallback.ToString() : binding.key.ToString();

            if (binding.kind == LegacyBindingKind.AxisPositive)
                return binding.axisName + "+";

            if (binding.kind == LegacyBindingKind.AxisNegative)
                return binding.axisName + "-";

            return fallback.ToString();
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
            public LegacyInputBinding interact;
        }

        [Serializable]
        private sealed class LegacyGamepadBinds
        {
            public LegacyInputBinding interact;
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
