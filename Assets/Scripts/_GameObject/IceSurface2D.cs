// IceSurface2D.cs
using UnityEngine;

/// <summary>
/// ������ �� ����� ������� �����������. ����������� ������� ������� Tag = "Ice".
/// ������ (�� �������) �������� ����������� �������� � ������� �������/�����������.
/// PlayerController �� ���� "Ice" ������� ���������� (������� ���������/����������).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IceSurface2D : MonoBehaviour
{
    [Header("�������� ���� (�������������)")]
    [Tooltip("���� ������� � �������� � ����� Collider2D. ����� �������� runtime-�������� � Friction=0, Bounciness=0.")]
    [SerializeField] private PhysicsMaterial2D iceMaterial;

    [Tooltip("��������� �������� ���� �� ���� ��������� ��� ������.")]
    [SerializeField] private bool applyMaterialToSelf = true;

    private Collider2D col;
    private PhysicsMaterial2D runtimeMat;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (string.Compare(tag, "Ice", true) != 0)
            Debug.LogWarning("[IceSurface2D] �������� Tag = \"Ice\" �� ������� � �����.");

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
