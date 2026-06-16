using UnityEngine;

namespace CatGame.SaveSystem
{
    [DisallowMultipleComponent]
    public sealed class SaveableId : MonoBehaviour
    {
        [SerializeField, Tooltip("Стабильный ID объекта для сохранений. Не должен зависеть от имени GameObject или позиции в иерархии. Рекомендация: генерировать через кнопку Generate New Save Id и не менять вручную.")]
        private string id;

        public string Id
        {
            get { return id; }
        }

#if UNITY_EDITOR
        [ContextMenu("Generate New Save Id")]
        public void GenerateNewId()
        {
            string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "Prefab";
            id = sceneName + "_" + System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void Reset()
        {
            if (string.IsNullOrWhiteSpace(id))
                GenerateNewId();
        }
#endif
    }
}
