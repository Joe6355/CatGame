#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CatGame.SaveSystem.EditorTools
{
    [CustomEditor(typeof(SaveableId))]
    public sealed class SaveableIdEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SaveableId id = (SaveableId)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("SaveId должен быть стабильным. Не меняй его после релиза, если объект уже мог попасть в сейвы игроков.", MessageType.Warning);

            if (GUILayout.Button("Generate New Save Id"))
            {
                id.GenerateNewId();
            }
        }
    }
}
#endif
