using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class KeyPickup : MonoBehaviour
{
    public enum State { Idle, Following, FlyingToDoor, Consumed }

    [Header("Идентификатор")]
    [SerializeField] private int keyId = 1;

    [Header("Подбор")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool autoDisableColliderOnPickup = true;

    [Header("Орбита вокруг игрока")]
    [SerializeField] private float orbitRadius = 0.8f;
    [SerializeField] private float orbitAngularSpeed = 180f; // град/с
    [SerializeField] private Vector2 orbitOffset = new Vector2(0f, 0.6f);

    [Header("Полёт к двери")]
    [SerializeField] private float flySpeed = 8f;
    [SerializeField] private float arriveDistance = 0.05f;
    [SerializeField] private AnimationCurve flyEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public int Id => keyId;
    public State CurrentState { get; private set; } = State.Idle;

    Transform carrier;              // кого крутим вокруг (игрок)
    float angle;                    // текущий угол орбиты (градусы)
    Collider2D col;
    Coroutine flyCo;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true; // удобнее как триггер
        angle = UnityEngine.Random.Range(0f, 360f);
    }

    void Update()
    {
        if (CurrentState == State.Following && carrier)
        {
            angle += orbitAngularSpeed * Time.deltaTime;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 center = (Vector2)carrier.position + orbitOffset;
            Vector2 pos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0, 0, angle); // немного живости
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (CurrentState != State.Idle) return;
        if (!other.CompareTag(playerTag)) return;

        var ring = other.GetComponent<PlayerKeyRing>() ?? other.GetComponentInParent<PlayerKeyRing>();
        if (!ring) return;

        ring.AddKey(this);
        carrier = ring.FollowAnchor ? ring.FollowAnchor : other.transform;
        CurrentState = State.Following;
        if (autoDisableColliderOnPickup && col) col.enabled = false;
    }

    /// <summary>Дверь просит ключ полететь к слоту. Вызывает дверь, передавая точку и колбэк.</summary>
    public void LaunchToDoor(Transform slotPoint, Action onArrivedAndConsumed)
    {
        if (CurrentState == State.Consumed) return;
        if (flyCo != null) StopCoroutine(flyCo);
        flyCo = StartCoroutine(FlyToDoorRoutine(slotPoint, onArrivedAndConsumed));
    }

    IEnumerator FlyToDoorRoutine(Transform slot, Action onArrived)
    {
        CurrentState = State.FlyingToDoor;
        carrier = null;

        Vector3 start = transform.position;
        float dist = Vector3.Distance(start, slot.position);
        float t = 0f;
        float dur = Mathf.Max(0.05f, dist / Mathf.Max(0.01f, flySpeed)); // приблизительное время полёта

        while (true)
        {
            t += Time.deltaTime / dur;
            float k = flyEase.Evaluate(Mathf.Clamp01(t));
            transform.position = Vector3.Lerp(start, slot.position, k);

            if (Vector3.SqrMagnitude(transform.position - slot.position) <= arriveDistance * arriveDistance)
                break;

            yield return null;
        }

        // столкнулись/прилетели в точку — потребляем ключ
        CurrentState = State.Consumed;
        onArrived?.Invoke();
        Destroy(gameObject);
    }
}
