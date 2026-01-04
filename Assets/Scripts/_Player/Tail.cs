using UnityEngine;

public class Tail : MonoBehaviour
{
    [Header("Main")]
    public int length = 12;
    public LineRenderer lineRend;
    public Transform targetDir;

    [Header("Movement")]
    [Tooltip("Базовое расстояние между сегментами")]
    public float targetDist = 0.25f;

    [Tooltip("Плавность следования")]
    public float smoothSpeed = 0.05f;

    [Header("Stretch")]
    [Tooltip("Допустимое растягивание сегмента (0 = жёстко, 0.3 = +30%)")]
    [Range(0f, 1f)]
    public float maxStretch = 0.2f;

    [Header("Wiggle")]
    [Tooltip("Скорость волны")]
    public float wiggleSpeed = 6f;

    [Tooltip("Амплитуда волны")]
    public float wiggleMagnitude = 0.15f;

    [Tooltip("Сдвиг волны между сегментами")]
    public float wigglePhaseOffset = 0.5f;

    [Tooltip("Усиление покачивания к концу хвоста")]
    [Range(0f, 2f)]
    public float tailTipMultiplier = 1.3f;

    [Header("Optional Body Parts (2D, no rotation)")]
    public Transform[] bodyParts;

    private Vector3[] segmentPoses;
    private Vector3[] segmentV;

    void Start()
    {
        if (lineRend == null)
            lineRend = GetComponent<LineRenderer>();

        lineRend.positionCount = length;

        segmentPoses = new Vector3[length];
        segmentV = new Vector3[length];

        for (int i = 0; i < length; i++)
            segmentPoses[i] = targetDir.position;
    }

    void Update()
    {
        if (targetDir == null) return;

        segmentPoses[0] = targetDir.position;

        Vector3 direction = targetDir.right;
        Vector3 side = targetDir.up;

        for (int i = 1; i < segmentPoses.Length; i++)
        {
            // Волна
            float wave = Mathf.Sin(Time.time * wiggleSpeed - i * wigglePhaseOffset);

            float strength =
                wiggleMagnitude *
                wave *
                Mathf.Lerp(0.1f, tailTipMultiplier, i / (float)length);

            Vector3 wiggleOffset = side * strength;

            // Желаемая позиция
            Vector3 desiredPos =
                segmentPoses[i - 1] +
                direction * targetDist +
                wiggleOffset;

            // Плавное следование
            segmentPoses[i] = Vector3.SmoothDamp(
                segmentPoses[i],
                desiredPos,
                ref segmentV[i],
                smoothSpeed
            );

            // Контролируемое растягивание
            Vector3 delta = segmentPoses[i] - segmentPoses[i - 1];
            float dist = delta.magnitude;

            float minDist = targetDist;
            float maxDist = targetDist * (1f + maxStretch);

            if (dist > maxDist)
            {
                segmentPoses[i] =
                    segmentPoses[i - 1] + delta.normalized * maxDist;
            }
            else if (dist < minDist)
            {
                segmentPoses[i] =
                    segmentPoses[i - 1] + delta.normalized * minDist;
            }

            // Доп. части тела
            if (bodyParts != null && i - 1 < bodyParts.Length)
            {
                bodyParts[i - 1].position = segmentPoses[i];
            }
        }

        lineRend.SetPositions(segmentPoses);
    }
}
