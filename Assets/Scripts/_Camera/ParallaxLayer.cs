using UnityEngine;

[ExecuteAlways]
public class ParallaxLayer : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;

    [Header("Parallax Strength")]
    [Tooltip("0 = не двигается, 1 = двигается как камера")]
    public Vector2 parallaxMultiplier = new Vector2(0.5f, 0.5f);

    [Header("Pixel Perfect")]
    public bool snapToPixels = true;
    public float pixelsPerUnit = 16f;

    private Vector3 lastCameraPosition;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        lastCameraPosition = targetCamera.transform.position;
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 delta = targetCamera.transform.position - lastCameraPosition;

        Vector3 move = new Vector3(
            delta.x * parallaxMultiplier.x,
            delta.y * parallaxMultiplier.y,
            0f
        );

        transform.position += move;

        if (snapToPixels)
        {
            transform.position = Snap(transform.position);
        }

        lastCameraPosition = targetCamera.transform.position;
    }

    Vector3 Snap(Vector3 pos)
    {
        float snapValue = 1f / pixelsPerUnit;

        pos.x = Mathf.Round(pos.x / snapValue) * snapValue;
        pos.y = Mathf.Round(pos.y / snapValue) * snapValue;

        return pos;
    }
}
