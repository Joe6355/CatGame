using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class PointerHoldHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public event Action OnDown;
    public event Action OnUp;

    private bool isPressed = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        OnDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ReleaseIfNeeded();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ReleaseIfNeeded();
    }

    private void OnDisable()
    {
        ReleaseIfNeeded();
    }

    private void ReleaseIfNeeded()
    {
        if (!isPressed)
            return;

        isPressed = false;
        OnUp?.Invoke();
    }
}