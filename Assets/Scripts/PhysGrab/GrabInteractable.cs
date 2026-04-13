using UnityEngine;
using Sol.Actions;

namespace Sol.Grab
{
    /// <summary>
    /// Wraps GrabManager as an IInteractable so grabbing goes through the unified interaction pipeline.
    /// Attach alongside GrabbableComponent on objects that should be grabbable via the interaction system.
    /// </summary>
    [RequireComponent(typeof(GrabbableComponent))]
    public class GrabInteractable : MonoBehaviour, IInteractable
    {
        private GrabbableComponent _grabbable;

        private void Awake()
        {
            _grabbable = GetComponent<GrabbableComponent>();
        }

        public string InteractionPrompt => "Grab";

        public bool CanInteract(Interactor interactor)
        {
            // Only players can grab (NPC hands are handled by equipment).
            return interactor.IsPlayer && !_grabbable.IsGrabbed;
        }

        public GameAction GetInteraction(Interactor interactor)
        {
            if (!interactor.IsPlayer) return null;
            return new StartGrabAction();
        }
    }
}
