using System;

namespace CatGame.SaveSystem
{
    [Serializable]
    public sealed class SaveObjectState
    {
        public string saveId = "";
        public string type = "";
        public string json = "";
    }
}
