using UnityEngine;

[DisallowMultipleComponent]
public class PlayerPresentationModule : MonoBehaviour
{
    public void RefreshPresentation(
        bool isJumpHoldActive,
        bool isChargingJump,
        float jumpBarNormalized,
        bool isApexThrowAvailable)
    {
        // Усталость и её UI убраны.
        // Метод оставлен как единая точка обновления презентации.
    }

    public void ForceRefreshPositionOnly()
    {
        // Полоса прыжка больше не используется.
    }
}