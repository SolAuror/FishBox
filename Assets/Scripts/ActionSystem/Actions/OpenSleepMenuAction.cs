using UnityEngine;

namespace Sol.Actions
{
    /// <summary>
    /// Opens the bed sleep radial menu for the interacting player.
    /// Completes immediately because the rest flow is handled by UI and the interactable.
    /// </summary>
    public sealed class OpenSleepMenuAction : InteractionAction
    {
        private readonly BedSleepInteractable _bed;
        private readonly Interactor _interactor;

        public bool Succeeded { get; private set; }

        public OpenSleepMenuAction(BedSleepInteractable bed, Interactor interactor)
        {
            _bed = bed;
            _interactor = interactor;
        }

        public override bool CanExecute()
        {
            return _bed != null && _interactor != null && _bed.CanInteract(_interactor);
        }

        public override void OnStart()
        {
            Succeeded = _bed != null && _bed.TryOpenSleepMenu(_interactor);
            if (!Succeeded)
            {
                Cancel();
                return;
            }

            Complete();
        }
    }
}
