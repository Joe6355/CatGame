using UnityEngine;
using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class TouchButton2D : MonoBehaviour
{
    [Header("Кого считаем игроком")]
    public LayerMask playerMask = ~0;
    public string playerTag = "Player";

    [Header("Поведение кнопки")]
    public bool openOnce = false;
    public bool closeOnExit = true;
    public float closeDelay = 0f;

    [Header("Дверь (работает без правок её скрипта)")]
    [Tooltip("Корневой объект двери/контроллера двери. Можно указывать неактивный объект.")]
    public GameObject doorTarget;

    [Tooltip("Имена МЕТОДОВ, которые попробуем вызвать на компонентах doorTarget и детей (в т.ч. отключённых). Поддерживаются сигнатуры void M() и void M(bool).")]
    public string[] candidateMethodNames = new[]
    {
        "Open", "Close", "SetOpen", "SetOpened", "SetState", "ApplyImmediate", "Apply"
    };

    [Tooltip("Для методов без параметров: какое имя считать «открыть», какое — «закрыть».")]
    public string openMethodNameHint = "Open";
    public string closeMethodNameHint = "Close";

    [Header("Animator (если есть)")]
    public bool useAnimator = false;
    public Animator doorAnimator;
    public string animBoolIsOpen = "IsOpen"; // если пусто — триггеры
    public string animTriggerOpen = "Open";
    public string animTriggerClose = "Close";

    [Header("Запасной план: просто включить/выключить объект")]
    public GameObject doorRootForSetActive;
    public bool whenOpen_SetActive = true;

    [Header("Визуал (опц.)")]
    public SpriteRenderer spriteRenderer;
    public Color idleColor = Color.white;
    public Color pressedColor = Color.green;

    [Header("Отладка")]
    public bool debugLogs = true;

    private Collider2D col;
    private bool fired = false;
    private int insideCount = 0;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true; // делаем кнопку триггером по умолчанию
        SetVisual(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryEnter(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        TryExit(other);
    }

    private void TryEnter(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        insideCount++;

        if (fired && openOnce) return;

        if (debugLogs) Debug.Log("[TouchButton2D] Player ENTER button.", this);
        SetVisual(true);
        SetDoorOpen(true);

        if (openOnce) fired = true;
    }

    private void TryExit(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        insideCount = Mathf.Max(insideCount - 1, 0);

        if (openOnce) return;
        if (!closeOnExit) return;

        if (insideCount == 0)
        {
            if (closeDelay <= 0f)
            {
                if (debugLogs) Debug.Log("[TouchButton2D] Player EXIT → close now.", this);
                SetDoorOpen(false);
                SetVisual(false);
            }
            else
            {
                if (debugLogs) Debug.Log($"[TouchButton2D] Player EXIT → close in {closeDelay}s.", this);
                StartCoroutine(CloseAfterDelay(closeDelay));
            }
        }
    }

    IEnumerator CloseAfterDelay(float t)
    {
        yield return new WaitForSeconds(t);
        if (insideCount > 0) yield break;
        SetDoorOpen(false);
        SetVisual(false);
    }

    // ======= Главное действие: открыть/закрыть дверь =======
    private void SetDoorOpen(bool open)
    {
        bool done = false;

        // 1) Попробовать вызвать методы на скриптах двери (в т.ч. с bool-параметром)
        if (doorTarget != null)
        {
            done = TryInvokeDoorMethods(doorTarget, open);
            if (debugLogs) Debug.Log($"[TouchButton2D] InvokeByReflection => {(done ? "OK" : "no match")}", this);
        }

        // 2) Animator (если включён)
        if (!done && useAnimator && doorAnimator != null)
        {
            if (!string.IsNullOrEmpty(animBoolIsOpen))
            {
                doorAnimator.SetBool(animBoolIsOpen, open);
            }
            else
            {
                if (open && !string.IsNullOrEmpty(animTriggerOpen)) doorAnimator.SetTrigger(animTriggerOpen);
                if (!open && !string.IsNullOrEmpty(animTriggerClose)) doorAnimator.SetTrigger(animTriggerClose);
            }
            if (debugLogs) Debug.Log("[TouchButton2D] Animator control used.", this);
            done = true;
        }

        // 3) Просто SetActive
        if (!done && doorRootForSetActive != null)
        {
            doorRootForSetActive.SetActive(open ? whenOpen_SetActive : !whenOpen_SetActive);
            if (debugLogs) Debug.Log("[TouchButton2D] Fallback SetActive used.", this);
            done = true;
        }

        if (!done && debugLogs)
        {
            Debug.LogWarning("[TouchButton2D] Не смог открыть/закрыть дверь. Проверь 'doorTarget' и имена методов/параметры.", this);
        }
    }

    // Попытка найти метод через Reflection и вызвать его на любом компоненте цели/детей (включая неактивные/disabled)
    private bool TryInvokeDoorMethods(GameObject targetRoot, bool open)
    {
        // Посмотрим на всех компонентах корня и детей, включая неактивных
        var comps = targetRoot.GetComponentsInChildren<Component>(true);
        if (comps == null || comps.Length == 0) return false;

        // подберём список имён: если открываем — сначала openHint, потом candidateMethodNames; если закрываем — наоборот
        var names = new List<string>();
        if (open && !string.IsNullOrEmpty(openMethodNameHint)) names.Add(openMethodNameHint);
        if (!open && !string.IsNullOrEmpty(closeMethodNameHint)) names.Add(closeMethodNameHint);
        if (candidateMethodNames != null) names.AddRange(candidateMethodNames);

        foreach (var comp in comps)
        {
            if (comp == null) continue;
            var t = comp.GetType();
            foreach (var nm in names)
            {
                if (string.IsNullOrEmpty(nm)) continue;

                // 1) Поиск void M(bool)
                var mBool = t.GetMethod(nm, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                        binder: null,
                                        types: new[] { typeof(bool) },
                                        modifiers: null);
                if (mBool != null)
                {
                    try
                    {
                        mBool.Invoke(comp, new object[] { open });
                        if (debugLogs) Debug.Log($"[TouchButton2D] {t.Name}.{nm}(bool) -> {open}", comp);
                        return true;
                    }
                    catch (Exception e)
                    {
                        if (debugLogs) Debug.LogWarning($"[TouchButton2D] Invoke {t.Name}.{nm}(bool) failed: {e.Message}", comp);
                    }
                }

                // 2) Поиск void M()
                var mNo = t.GetMethod(nm, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                      binder: null,
                                      types: Type.EmptyTypes,
                                      modifiers: null);
                if (mNo != null)
                {
                    // вызываем только если имя «соответствует» действию (Open при open=true, Close при open=false)
                    bool nameMatches =
                        (open && string.Equals(nm, openMethodNameHint, StringComparison.OrdinalIgnoreCase)) ||
                        (!open && string.Equals(nm, closeMethodNameHint, StringComparison.OrdinalIgnoreCase));

                    if (nameMatches)
                    {
                        try
                        {
                            mNo.Invoke(comp, null);
                            if (debugLogs) Debug.Log($"[TouchButton2D] {t.Name}.{nm}()", comp);
                            return true;
                        }
                        catch (Exception e)
                        {
                            if (debugLogs) Debug.LogWarning($"[TouchButton2D] Invoke {t.Name}.{nm}() failed: {e.Message}", comp);
                        }
                    }
                }
            }
        }

        return false;
    }

    private bool IsPlayer(Collider2D other)
    {
        bool byLayer = (playerMask.value & (1 << other.gameObject.layer)) != 0;
        bool byTag = string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag);
        return byLayer && byTag;
    }

    private void SetVisual(bool pressed)
    {
        if (!spriteRenderer) return;
        spriteRenderer.color = pressed ? pressedColor : idleColor;
    }
}
