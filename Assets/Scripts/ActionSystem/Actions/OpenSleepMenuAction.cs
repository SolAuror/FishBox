using UnityEngine;

namespace Sol.Actions
{
    /// <summary>
    /// Runs a staged bed sleep interaction. The radial menu opens only after
    /// the bed InteractionPoint reaches its animation-ready phase.
    /// </summary>
    public sealed class OpenSleepMenuAction : InteractionAction
    {
        private readonly SleepInteractable _bed;
        private readonly Interactor _interactor;
        private bool _started;

        public bool Succeeded { get; private set; }

        public OpenSleepMenuAction(SleepInteractable bed, Interactor interactor)
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
            _started = true;
            if (_bed == null || !_bed.BeginSleepInteraction(_interactor))
            {
                Succeeded = false;
                Cancel();
            }
        }

        public override void OnUpdate()
        {
            if (!_started || IsComplete || IsCancelled || _bed == null)
                return;

            _bed.TickSleepInteraction();
            if (!_bed.SleepInteractionFinished)
                return;

            Succeeded = _bed.SleepInteractionSucceeded;
            if (Succeeded)
                Complete();
            else
                Cancel();
        }

        public override void OnCancel()
        {
            if (_started && _bed != null && !_bed.SleepInteractionFinished)
                _bed.CancelSleepInteraction();
        }
    }
}
