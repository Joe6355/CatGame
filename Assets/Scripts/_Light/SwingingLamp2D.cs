using UnityEngine;

[DisallowMultipleComponent]
public class SwingingLamp2D : MonoBehaviour
{
    [Header("Swing")]
    [SerializeField, Tooltip("Максимальный угол качания в градусах.")]
    private float angle = 8f;

    [SerializeField, Tooltip("Скорость качания.")]
    private float speed = 1.5f;

    [SerializeField, Tooltip("Случайная фаза, чтобы несколько ламп качались не одинаково.")]
    private bool randomizePhase = true;

    [SerializeField, Tooltip("Стартовая фаза качания.")]
    private float phaseOffset = 0f;

    [Header("Optional")]
    [SerializeField, Tooltip("Если ВКЛ — лампа начнёт с текущего поворота как базового.")]
    private bool useStartRotationAsBase = true;

    private Quaternion baseRotation;

    private void Awake()
    {
        baseRotation = useStartRotationAsBase ? transform.localRotation : Quaternion.identity;

        if (randomizePhase)
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float z = Mathf.Sin((Time.time * speed) + phaseOffset) * angle;
        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, z);
    }
}