// IceSurface2D.cs
using UnityEngine;

/// <summary>
/// Повесь на ЛЮБУЮ ледяную поверхность. ОБЯЗАТЕЛЬНО поставь объекту Tag = "Ice".
/// Скрипт (по желанию) присвоит поверхности материал с нулевым трением/прыгучестью.
/// PlayerController по тегу "Ice" включит скольжение (плавное ускорение/торможение).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IceSurface2D : MonoBehaviour
{
    [Header("Материал льда (необязательно)")]
    [Tooltip("Если указать — применим к этому Collider2D. Иначе создадим runtime-материал с Friction=0, Bounciness=0.")]
    [SerializeField] private PhysicsMaterial2D iceMaterial;

    [Tooltip("Назначить материал льда на этот коллайдер при старте.")]
    [SerializeField] private bool applyMaterialToSelf = true;

    private Collider2D col;
    private PhysicsMaterial2D runtimeMat;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (string.Compare(tag, "Ice", true) != 0)
            Debug.LogWarning("[IceSurface2D] Установи Tag = \"Ice\" на объекте с льдом.");

        if (!iceMaterial)
        {
            runtimeMat = new PhysicsMaterial2D("IceRuntime_0fr_0b");
            runtimeMat.friction = 0f;
            runtimeMat.bounciness = 0f;
        }
    }

    private void OnEnable()
    {
        if (applyMaterialToSelf && col)
            col.sharedMaterial = iceMaterial ? iceMaterial : runtimeMat;
    }
}
