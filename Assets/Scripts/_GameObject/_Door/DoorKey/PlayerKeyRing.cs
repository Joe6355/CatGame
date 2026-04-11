using System.Collections.Generic;
using UnityEngine;

public class PlayerKeyRing : MonoBehaviour
{
    [Header("якорь орбиты (необ€зательно)")]
    [Tooltip("≈сли пусто Ч ключ орбитит вокруг Transform игрока.")]
    public Transform FollowAnchor;

    private readonly List<KeyPickup> carried = new List<KeyPickup>();

    public void AddKey(KeyPickup key)
    {
        if (!carried.Contains(key))
            carried.Add(key);
    }

    public bool HasKey(int id) => carried.Exists(k => k && k.Id == id);

    public KeyPickup GetKey(int id) => carried.Find(k => k && k.Id == id);

    public void RemoveKey(KeyPickup key)
    {
        if (key) carried.Remove(key);
    }

    /// <summary>ќтдаЄт ключ двери: запускает полЄт к слоту и удал€ет из списка.</summary>
    public bool GiveKeyToDoor(int id, Transform slot, System.Action onArrived)
    {
        var k = GetKey(id);
        if (!k) return false;

        RemoveKey(k);
        k.LaunchToDoor(slot, onArrived);
        return true;
    }
}
