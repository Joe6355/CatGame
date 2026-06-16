#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatGame.SaveSystem.EditorTools
{
    public sealed class SaveIdValidatorWindow : EditorWindow
    {
        private Vector2 scroll;
        private readonly List<string> messages = new List<string>();

        [MenuItem("Tools/CatGame/Save System/Validate Save IDs")]
        public static void Open()
        {
            SaveIdValidatorWindow window = GetWindow<SaveIdValidatorWindow>("Save ID Validator");
            window.Validate();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Проверяет пустые и дублирующиеся SaveableId в открытой сцене. Запускай перед коммитом и перед билдом.", MessageType.Info);

            if (GUILayout.Button("Validate Open Scenes"))
                Validate();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < messages.Count; i++)
                EditorGUILayout.LabelField(messages[i], EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void Validate()
        {
            messages.Clear();
            Dictionary<string, SaveableId> ids = new Dictionary<string, SaveableId>();
            SaveableId[] components = Object.FindObjectsOfType<SaveableId>(true);

            for (int i = 0; i < components.Length; i++)
            {
                SaveableId component = components[i];
                if (component == null)
                    continue;

                if (string.IsNullOrWhiteSpace(component.Id))
                {
                    messages.Add("EMPTY ID: " + GetPath(component.transform));
                    continue;
                }

                SaveableId existing;
                if (ids.TryGetValue(component.Id, out existing))
                {
                    messages.Add("DUPLICATE ID: " + component.Id + "\n  A: " + GetPath(existing.transform) + "\n  B: " + GetPath(component.transform));
                    continue;
                }

                ids.Add(component.Id, component);
            }

            if (messages.Count == 0)
                messages.Add("OK: дубликатов и пустых SaveId не найдено. Объектов проверено: " + components.Length);
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            string path = transform.name;
            Transform parent = transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
#endif
