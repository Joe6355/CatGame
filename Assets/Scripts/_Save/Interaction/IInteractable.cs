using UnityEngine;

namespace CatGame.SaveSystem
{
    public interface IInteractable
    {
        bool CanInteract(Component interactor);
        string GetInteractionText(Component interactor);
        void Interact(Component interactor);
    }
}
