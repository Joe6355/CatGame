using System;
using System.Collections;
using CatGame.SaveSystem;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class KeyPickup : MonoBehaviour
{
    public enum State
    {
        Idle,
        Following,
        FlyingToDoor,
        Consumed
    }

    [Header("Идентификатор")]
    [SerializeField] private int keyId = 1;

    [Header("Подбор")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool autoDisableColliderOnPickup = true;

    [Header("Орбита вокруг игрока")]
    [SerializeField] private float orbitRadius = 0.8f;
    [SerializeField] private float orbitAngularSpeed = 180f;
    [SerializeField] private Vector2 orbitOffset = new Vector2(0f, 0.6f);

    [Header("Полёт к двери")]
    [SerializeField] private float flySpeed = 8f;
    [SerializeField] private float arriveDistance = 0.05f;
    [SerializeField] private AnimationCurve flyEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("После использования")]
    [SerializeField, Tooltip("Когда ключ использован дверью — выключать GameObject вместо Destroy. Важно для RatController, который сохраняет состояние runtime-ключа.")]
    private bool disableObjectWhenConsumed = true;

    [Header("Save dirty")]
    [SerializeField, Tooltip("После подбора/полёта/использования ключа помечать сейв как изменённый и запускать autosave.")]
    private bool markDirtyOnStateChange = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = false;

    public int Id => keyId;
    public State CurrentState { get; private set; } = State.Idle;

    private Transform carrier;
    private float angle;
    private Collider2D col;
    private Coroutine flyCo;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private void Reset()
    {
        CacheRefs();

        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        CacheRefs();

        if (col != null)
            col.isTrigger = true;

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        angle = UnityEngine.Random.Range(0f, 360f);
    }

    private void OnValidate()
    {
        CacheRefs();

        orbitRadius = Mathf.Max(0f, orbitRadius);
        flySpeed = Mathf.Max(0.01f, flySpeed);
        arriveDistance = Mathf.Max(0.001f, arriveDistance);
    }

    private void Update()
    {
        if (CurrentState == State.Following && carrier != null)
        {
            angle += orbitAngularSpeed * Time.deltaTime;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 center = (Vector2)carrier.position + orbitOffset;
            Vector2 pos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
            transform.position = pos;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (CurrentState != State.Idle)
            return;

        if (other == null || !other.CompareTag(playerTag))
            return;

        PlayerKeyRing ring = other.GetComponent<PlayerKeyRing>() ?? other.GetComponentInParent<PlayerKeyRing>();

        if (ring == null)
            return;

        AttachToRing(ring, true);
        MarkDirty();
    }

    public void LaunchToDoor(Transform slotPoint, Action onArrivedAndConsumed)
    {
        if (CurrentState == State.Consumed)
            return;

        StopFlyRoutine();

        if (slotPoint == null)
        {
            ConsumeKey(onArrivedAndConsumed);
            return;
        }

        flyCo = StartCoroutine(FlyToDoorRoutine(slotPoint, onArrivedAndConsumed));
    }

    public void ForceRestoreHeldByRat(Transform parentPoint)
    {
        StopFlyRoutine();

        gameObject.SetActive(true);
        CurrentState = State.Idle;
        carrier = null;

        if (parentPoint != null)
        {
            transform.SetParent(parentPoint, true);
            transform.position = parentPoint.position;
            transform.rotation = parentPoint.rotation;
        }

        if (col != null)
            col.enabled = true;
    }

    public void ForceRestoreIdle(Vector3 position, Quaternion rotation)
    {
        StopFlyRoutine();

        gameObject.SetActive(true);
        CurrentState = State.Idle;
        carrier = null;
        transform.SetParent(null, true);
        transform.position = position;
        transform.rotation = rotation;

        if (col != null)
            col.enabled = true;
    }

    public void ForceRestoreInitialIdle()
    {
        ForceRestoreIdle(initialPosition, initialRotation);
    }

    public void ForceRestoreFollowing(PlayerKeyRing ring)
    {
        StopFlyRoutine();

        gameObject.SetActive(true);

        if (ring == null)
        {
            ForceRestoreInitialIdle();
            return;
        }

        AttachToRing(ring, true);
    }

    public void ForceRestoreConsumed(bool deactivateObject)
    {
        StopFlyRoutine();

        CurrentState = State.Consumed;
        carrier = null;

        if (col != null)
            col.enabled = false;

        gameObject.SetActive(!deactivateObject);
    }

    private IEnumerator FlyToDoorRoutine(Transform slot, Action onArrived)
    {
        CurrentState = State.FlyingToDoor;
        carrier = null;
        MarkDirty();

        Vector3 start = transform.position;
        float dist = slot != null ? Vector3.Distance(start, slot.position) : 0f;
        float t = 0f;
        float dur = Mathf.Max(0.05f, dist / Mathf.Max(0.01f, flySpeed));

        while (slot != null)
        {
            t += Time.deltaTime / dur;
            float k = flyEase != null ? flyEase.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
            transform.position = Vector3.Lerp(start, slot.position, k);

            if (Vector3.SqrMagnitude(transform.position - slot.position) <= arriveDistance * arriveDistance)
                break;

            yield return null;
        }

        flyCo = null;
        ConsumeKey(onArrived);
    }

    private void ConsumeKey(Action onArrived)
    {
        CurrentState = State.Consumed;
        carrier = null;

        if (col != null)
            col.enabled = false;

        onArrived?.Invoke();
        MarkDirty();

        if (verboseLogs)
            Debug.Log("KeyPickup consumed: " + name, this);

        if (disableObjectWhenConsumed)
            gameObject.SetActive(false);
    }

    private void AttachToRing(PlayerKeyRing ring, bool callAddKey)
    {
        if (ring == null)
            return;

        if (callAddKey)
            ring.AddKey(this);

        carrier = ring.FollowAnchor != null ? ring.FollowAnchor : ring.transform;
        CurrentState = State.Following;

        if (autoDisableColliderOnPickup && col != null)
            col.enabled = false;
    }

    private void StopFlyRoutine()
    {
        if (flyCo == null)
            return;

        StopCoroutine(flyCo);
        flyCo = null;
    }

    private void CacheRefs()
    {
        if (col == null)
            col = GetComponent<Collider2D>();
    }

    private void MarkDirty()
    {
        if (!markDirtyOnStateChange)
            return;

        if (SaveManager.Instance != null)
            SaveManager.Instance.MarkDirtyAndAutosave();
    }
}
