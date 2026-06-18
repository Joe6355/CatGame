using System;
using System.Collections;
using CatGame.SaveSystem;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DoorLock : MonoBehaviour, ISaveable
{
    [Header("Save")]
    [SerializeField, Tooltip("SaveableId двери. Повесь SaveableId на этот же объект и назначь сюда.")]
    private SaveableId saveableId;

    [SerializeField, Tooltip("Если включено — дверь сохраняет состояние открыта/закрыта.")]
    private bool saveDoorState = true;

    [SerializeField, Tooltip("Начальное состояние двери, если сейва ещё нет.")]
    private bool startsOpened = false;

    [SerializeField, Tooltip("Если включено — после открытия двери сразу помечает сейв как изменённый и запускает автосейв.")]
    private bool markDirtyOnOpen = true;

    [Header("ID замка")]
    [SerializeField] private int doorId = 1;

    [Header("Слот для ключа")]
    [Tooltip("Точка, куда должен прилететь ключ перед открытием.")]
    [SerializeField] private Transform keySlotPoint;

    [Header("Что открыть")]
    [Tooltip("Что исчезнет, когда дверь откроется. Обычно это child _Door, а НЕ root _DoorLock.")]
    [SerializeField] private GameObject doorRootToDisable;

    [Header("Вход игрока")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Сколько ждать после прилёта ключа до открытия.")]
    [SerializeField] private float openDelay = 0.1f;

    [Header("Повторное использование")]
    [SerializeField] private bool oneShot = true;

    [Header("Аниматор")]
    [Tooltip("Animator для проигрывания анимаций двери. Обычно Animator лежит на child _Door.")]
    [SerializeField] private Animator doorAnimator;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = false;

    private bool opened = false;
    private Collider2D triggerCol;
    private Coroutine openRoutine;

    public bool IsOpened => opened;

    public string SaveId
    {
        get
        {
            if (!saveDoorState)
                return string.Empty;

            return saveableId != null ? saveableId.Id : string.Empty;
        }
    }

    private void Reset()
    {
        CacheRefs();

        if (triggerCol != null)
            triggerCol.isTrigger = true;
    }

    private void Awake()
    {
        CacheRefs();

        if (triggerCol != null)
            triggerCol.isTrigger = true;

        ApplyOpenedState(startsOpened, false);
    }

    private void OnValidate()
    {
        CacheRefs();

        openDelay = Mathf.Max(0f, openDelay);
    }

    private void OnDisable()
    {
        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (opened && oneShot)
            return;

        if (other == null)
            return;

        if (!other.CompareTag(playerTag))
            return;

        PlayerKeyRing ring = other.GetComponent<PlayerKeyRing>() ?? other.GetComponentInParent<PlayerKeyRing>();

        if (ring == null)
            return;

        if (!ring.HasKey(doorId))
            return;

        if (keySlotPoint == null)
        {
            ring.GiveKeyToDoor(doorId, other.transform, null);
            OpenDoorInstant();
            return;
        }

        ring.GiveKeyToDoor(doorId, keySlotPoint, StartOpenAfterDelay);
    }

    public string CaptureStateJson()
    {
        DoorLockState state = new DoorLockState
        {
            opened = opened
        };

        return JsonUtility.ToJson(state);
    }

    public void RestoreStateJson(string json)
    {
        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            ApplyOpenedState(startsOpened, false);
            return;
        }

        DoorLockState state = JsonUtility.FromJson<DoorLockState>(json);
        ApplyOpenedState(state.opened, false);
    }

    public void OpenDoorInstant()
    {
        if (opened && oneShot)
            return;

        ApplyOpenedState(true, markDirtyOnOpen);
    }

    private void StartOpenAfterDelay()
    {
        if (!gameObject.activeInHierarchy)
        {
            OpenDoorInstant();
            return;
        }

        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(OpenAfterDelay());
    }

    private IEnumerator OpenAfterDelay()
    {
        if (doorAnimator != null)
            doorAnimator.SetTrigger("OpenDoor");

        if (openDelay > 0f)
            yield return new WaitForSeconds(openDelay);

        openRoutine = null;
        OpenDoorInstant();
    }

    private void ApplyOpenedState(bool value, bool markDirty)
    {
        opened = value;

        if (doorRootToDisable != null)
            doorRootToDisable.SetActive(!opened);

        if (triggerCol != null)
            triggerCol.enabled = !(opened && oneShot);

        if (markDirty && SaveManager.Instance != null)
            SaveManager.Instance.MarkDirtyAndAutosave();

        if (verboseLogs)
            Debug.Log("[DoorLock] " + name + " opened=" + opened, this);
    }

    private void CacheRefs()
    {
        if (saveableId == null)
            saveableId = GetComponent<SaveableId>();

        if (triggerCol == null)
            triggerCol = GetComponent<Collider2D>();

        if (doorRootToDisable == null)
        {
            Transform childDoor = transform.Find("_Door");

            if (childDoor != null)
                doorRootToDisable = childDoor.gameObject;
            else
                doorRootToDisable = gameObject;
        }

        if (doorAnimator == null)
        {
            if (doorRootToDisable != null)
                doorAnimator = doorRootToDisable.GetComponent<Animator>();

            if (doorAnimator == null)
                doorAnimator = GetComponent<Animator>();
        }
    }

    [Serializable]
    private struct DoorLockState
    {
        public bool opened;
    }
}