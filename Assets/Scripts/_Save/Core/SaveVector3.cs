using System;
using UnityEngine;

namespace CatGame.SaveSystem
{
    [Serializable]
    public struct SaveVector3
    {
        public float x;
        public float y;
        public float z;

        public SaveVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static SaveVector3 FromUnity(Vector3 value)
        {
            return new SaveVector3(value.x, value.y, value.z);
        }

        public Vector3 ToUnity()
        {
            return new Vector3(x, y, z);
        }
    }
}
