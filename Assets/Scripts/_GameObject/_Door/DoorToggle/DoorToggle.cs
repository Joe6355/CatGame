using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorToggle : MonoBehaviour
{
    public enum HideStrategy
    {
        DisableComponents,   // ✅ РЕКОМЕНДУЮ: выключаем Renderer/Collider, скрипт остаётся активным
        DisableRootObject    // SetActive(false) на корне (может ломать корутины)
    }

    [Header("Что скрывать/показывать")]
    [Tooltip("Корень визуала/коллайдеров двери. Если пусто — возьмётся сам объект.")]
    public GameObject doorRoot;

    [Tooltip("Стратегия скрытия двери.")]
    public HideStrategy hideStrategy = HideStrategy.DisableComponents;

    [Header("Поведение")]
    [Tooltip("Инвертировать логику: ON = дверь открыта, когда кнопка НЕ нажата.")]
    public bool invert;
    [Tooltip("Задержка перед открытием (сек).")]
    public float openDelay = 0f;
    [Tooltip("Задержка перед закрытием (сек).")]
    public float closeDelay = 0f;

    [Header("Анимация (опц.)")]
    public Animator animator;
    public string triggerOpen = "Open";
    public string triggerClose = "Close";

    // runtime
    private Coroutine co;
    private bool isOpen;

    // кэш для DisableComponents
    private List<Renderer> renderers = new();
    private List<Collider2D> colliders2D = new();
    private List<Collider> colliders3D = new();

    void Awake()
    {
        if (!doorRoot) doorRoot = gameObject;
        CacheComponents();
        // по умолчанию — закрыто (или инверт)
        ApplyImmediate(false ^ invert); // <-- позиционный аргумент
    }

    void CacheComponents()
    {
        renderers.Clear(); colliders2D.Clear(); colliders3D.Clear();
        if (doorRoot)
        {
            renderers.AddRange(doorRoot.GetComponentsInChildren<Renderer>(true));
            colliders2D.AddRange(doorRoot.GetComponentsInChildren<Collider2D>(true));
            colliders3D.AddRange(doorRoot.GetComponentsInChildren<Collider>(true));
        }
    }

    public void SetOpen(bool pressedFromButton)
    {
        bool wantOpen = pressedFromButton ^ invert;

        if (!isActiveAndEnabled)
        {
            ApplyImmediate(wantOpen); // <-- позиционный аргумент
            return;
        }

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(DoSetOpen(wantOpen));
    }

    IEnumerator DoSetOpen(bool wantOpen)
    {
        if (wantOpen == isOpen) yield break;

        float delay = wantOpen ? openDelay : closeDelay;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (animator)
        {
            if (wantOpen && !string.IsNullOrEmpty(triggerOpen)) animator.SetTrigger(triggerOpen);
            if (!wantOpen && !string.IsNullOrEmpty(triggerClose)) animator.SetTrigger(triggerClose);
        }

        ApplyImmediate(wantOpen); // <-- позиционный аргумент
        co = null;
    }

    private void ApplyImmediate(bool open)
    {
        isOpen = open;

        if (hideStrategy == HideStrategy.DisableRootObject)
        {
            // ВНИМАНИЕ: если doorRoot == объект с этим скриптом, SetActive(false) отключит корутины.
            if (doorRoot) doorRoot.SetActive(!open); // открыто = «исчезла»
            return;
        }

        // DisableComponents —— рекомендуемый режим
        bool visible = !open; // открыто = «исчезла»
        foreach (var r in renderers) if (r) r.enabled = visible;
        foreach (var c in colliders2D) if (c) c.enabled = visible;
        foreach (var c in colliders3D) if (c) c.enabled = visible;
    }
}
