using UnityEngine;

namespace Sol.Actions
{
    public class UseInteractionPointAction : InteractionAction
    {
        private readonly InteractionPoint _interactionPoint;
        private readonly Interactor _interactor;
        private float _remainingTime;
        private bool _startedUse;
        private bool _endedUse;
        private bool _holdUntilCancelled;
        private bool _waitForReadyBeforeDuration;
        private bool _readyDurationStarted;
        private bool _completionRequested;

        public bool Succeeded { get; private set; }
        public bool Failed => IsCancelled || (IsComplete && !Succeeded);
        public bool HasResolved => IsComplete || IsCancelled;
        public InteractionFailureReason FailureReason { get; private set; } = InteractionFailureReason.None;
        public bool IsHoldUntilCancelled => _holdUntilCancelled;
        public InteractionPoint InteractionPoint => _interactionPoint;
        public bool CanCancelFromInput => _startedUse && !_endedUse && !IsComplete && !IsCancelled;
        public bool CanRequestCompletion =>
            _startedUse && !_endedUse && _holdUntilCancelled && !IsComplete && !IsCancelled;

        public UseInteractionPointAction(InteractionPoint interactionPoint, Interactor interactor)
        {
            _interactionPoint = interactionPoint;
            _interactor = interactor;
        }

        public override bool CanExecute()
        {
            return IsInteractorAvailable()
                && _interactionPoint != null
                && _interactionPoint.isActiveAndEnabled
                && _interactionPoint.CanInteract(_interactor);
        }

        public override void OnStart()
        {
            if (_interactionPoint == null || !_interactionPoint.BeginUse(_interactor))
            {
                FailureReason = InteractionFailureReason.BeginRejected;
                Cancel();
                return;
            }

            _startedUse = true;
            _holdUntilCancelled = _interactionPoint.ShouldHoldUntilCancelled(_interactor);
            _waitForReadyBeforeDuration = _interactionPoint.ShouldWaitForReadyBeforeDuration(_interactor);
            _readyDurationStarted = !_waitForReadyBeforeDuration;
            _completionRequested = false;
            _remainingTime = _interactionPoint.GetUseDuration(_interactor);

            if (!_holdUntilCancelled && !_waitForReadyBeforeDuration && _remainingTime <= 0f)
                CompleteInteraction();
        }

        public override void OnUpdate()
        {
            if (IsComplete || IsCancelled || !_startedUse || _endedUse)
                return;

            if (!IsInteractorAvailable() || !IsInteractionStillActive())
            {
                FailureReason = ResolveInactiveFailureReason();
                Cancel();
                return;
            }

            if (_waitForReadyBeforeDuration && !_readyDurationStarted)
            {
                if (_interactionPoint.ActiveSession == null || !_interactionPoint.ActiveSession.IsReady)
                    return;

                _readyDurationStarted = true;
                _remainingTime = _interactionPoint.GetUseDuration(_interactor);
                if (!_holdUntilCancelled && _remainingTime <= 0f)
                {
                    CompleteInteraction();
                    return;
                }
            }

            if (_holdUntilCancelled)
            {
                if (_completionRequested || (_interactionPoint.ActiveSession != null && _interactionPoint.ActiveSession.CompletionRequested))
                    CompleteInteraction();
                return;
            }

            _remainingTime -= Time.deltaTime;
            if (_remainingTime <= 0f)
                CompleteInteraction();
        }

        public override void OnCancel()
        {
            if (_startedUse && !_endedUse && _interactionPoint != null && _interactionPoint.IsInUseBy(_interactor))
                _interactionPoint.EndUse(completed: false);

            _endedUse = true;
            Succeeded = false;

            if (FailureReason == InteractionFailureReason.None)
                FailureReason = _startedUse ? ResolveInactiveFailureReason() : ResolvePreconditionFailureReason();
        }

        public void RequestCompletion()
        {
            if (CanRequestCompletion)
            {
                _completionRequested = true;
                _interactionPoint?.RequestActiveCompletion();
            }
        }

        public void RequestCancelFromInput()
        {
            if (CanCancelFromInput)
                Cancel();
        }

        private void CompleteInteraction()
        {
            if (_endedUse)
            {
                Complete();
                return;
            }

            if (!IsInteractionStillActive())
            {
                FailureReason = ResolveInactiveFailureReason();
                Cancel();
                return;
            }

            _interactionPoint.EndUse(completed: true);
            _endedUse = true;
            Succeeded = true;
            FailureReason = InteractionFailureReason.None;
            Complete();
        }

        private bool IsInteractorAvailable()
        {
            return _interactor != null
                && _interactor.Owner != null
                && _interactor.Owner.activeInHierarchy;
        }

        private bool IsInteractionStillActive()
        {
            return _interactionPoint != null
                && _interactionPoint.isActiveAndEnabled
                && _interactionPoint.IsInUseBy(_interactor);
        }

        private InteractionFailureReason ResolvePreconditionFailureReason()
        {
            if (!IsInteractorAvailable())
                return InteractionFailureReason.InteractorUnavailable;

            if (_interactionPoint == null || !_interactionPoint.isActiveAndEnabled)
                return InteractionFailureReason.InteractionUnavailable;

            if (!_interactionPoint.CanInteract(_interactor))
                return InteractionFailureReason.Occupied;

            return InteractionFailureReason.PreconditionsFailed;
        }

        private InteractionFailureReason ResolveInactiveFailureReason()
        {
            if (!IsInteractorAvailable())
                return InteractionFailureReason.InteractorUnavailable;

            if (_interactionPoint == null || !_interactionPoint.isActiveAndEnabled)
                return InteractionFailureReason.InteractionUnavailable;

            if (!_interactionPoint.IsInUseBy(_interactor))
                return InteractionFailureReason.OwnershipLost;

            return InteractionFailureReason.Cancelled;
        }
    }
}
