using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DoorLock : MonoBehaviour
{
    [Header("ID замка")]
    [SerializeField] private int doorId = 1;

    [Header("Слот для ключа")]
    [Tooltip("Точка, куда должен прилететь ключ перед открытием.")]
    [SerializeField] private Transform keySlotPoint;

    [Header("Что открыть")]
    [Tooltip("Что «исчезнет», когда дверь откроется (сама дверь/спрайт/коллайдеры).")]
    [SerializeField] private GameObject doorRootToDisable;

    [Header("Вход игрока")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Сколько ждать после прилёта ключа до открытия (для анимаций).")]
    [SerializeField] private float openDelay = 0.1f;

    [Header("Повторное использование")]
    [SerializeField] private bool oneShot = true; // открыть один раз и больше не закрывать

    [Header("Аниматор")]
    [Tooltip("Animator для проигрывания анимаций двери.")]
    [SerializeField] private Animator doorAnimator;

    private bool opened = false;
    Collider2D triggerCol;

    void Awake()
    {
        triggerCol = GetComponent<Collider2D>();
        if (triggerCol) triggerCol.isTrigger = true;
        if (!doorRootToDisable) doorRootToDisable = gameObject; // по умолчанию выключаем сам объект двери

        // Если аниматор не назначен, попробуем найти его на этом объекте
        if (!doorAnimator) doorAnimator = GetComponent<Animator>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (opened && oneShot) return;
        if (!other.CompareTag(playerTag)) return;

        var ring = other.GetComponent<PlayerKeyRing>() ?? other.GetComponentInParent<PlayerKeyRing>();
        if (!ring) return;

        if (!ring.HasKey(doorId)) return;

        // отправляем ключ в слот; по прилёту — открываем дверь
        if (!keySlotPoint) { OpenDoorInstant(); ring.GiveKeyToDoor(doorId, other.transform, () => { }); return; }

        ring.GiveKeyToDoor(doorId, keySlotPoint, () => { StartCoroutine(OpenAfterDelay()); });
    }

    System.Collections.IEnumerator OpenAfterDelay()
    {
        // 1) Запускаем анимацию открытия
        if (doorAnimator)
        {
            doorAnimator.SetTrigger("OpenDoor");
        }

        // 2) Ждем указанную задержку перед полным открытием
        if (openDelay > 0f) yield return new WaitForSeconds(openDelay);

        // 3) Завершаем открытие
        OpenDoorInstant();
    }

    void OpenDoorInstant()
    {
        if (opened && oneShot) return;
        opened = true;

        // 1) проиграть звук/частицы (заглушка)
        // PlaySound(); 

        // 2) анимация открытия уже запущена в OpenAfterDelay

        // 3) по окончании — выключить дверь
        if (doorRootToDisable) doorRootToDisable.SetActive(false);

        // если дверь одноразовая — можно выключить триггер
        if (oneShot && triggerCol) triggerCol.enabled = false;
    }
}