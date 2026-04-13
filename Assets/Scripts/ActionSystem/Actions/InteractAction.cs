using UnityEngine;

namespace Sol.Actions
{
    /// <summary>
    /// Meta-action: queries an IInteractable for its interaction action
    /// and dispatches it through ActionSystem. Contains zero domain logic.
    /// </summary>
    public class InteractAction : InteractionAction
    {
        private static bool InteractionTraceEnabled => false;
        private readonly IInteractable _interactable;
        private readonly Interactor _interactor;
        private bool _started;

        public bool Succeeded { get; private set; }
        public bool Failed => IsCancelled || (IsComplete && !Succeeded);
        public bool HasResolved => IsComplete || IsCancelled;
        public GameAction DispatchedAction { get; private set; }
        public InteractionFailureReason FailureReason { get; private set; } = InteractionFailureReason.None;

        public InteractAction(IInteractable interactable, Interactor interactor)
        {
            _interactable = interactable;
            _interactor = interactor;
        }

        public override bool CanExecute()
        {
            return _interactable != null
                && _interactor != null
                && _interactable.CanInteract(_interactor);
        }

        public override void OnStart()
        {
            _started = true;
            var action = _interactable.GetInteraction(_interactor);
            if (InteractionTraceEnabled && _interactor != null && _interactor.IsPlayer)
            {
                string interactableName = (_interactable as Component) != null
                    ? (_interactable as Component).name
                    : _interactable.GetType().Name;
                Debug.Log($"[InteractionTrace][InteractAction][f{Time.frameCount}] OnStart | actor='{_interactor.Owner?.name ?? "null"}' interactable='{interactableName}' resolvedAction='{action?.GetType().Name ?? "null"}'");
            }

            if (action == null || ActionSystem.Instance == null)
            {
                Succeeded = false;
                FailureReason = action == null
                    ? InteractionFailureReason.NoResolvedAction
                    : InteractionFailureReason.DispatchRejected;
                Cancel();
                return;
            }

            DispatchedAction = action;

            // Determine target: if the interactable is a Component, use its GameObject.
            GameObject target = (_interactable as Component)?.gameObject;
            bool queued = ActionSystem.Instance.Dispatch(action, _interactor.Owner, target);
            if (InteractionTraceEnabled && _interactor != null && _interactor.IsPlayer)
                Debug.Log($"[InteractionTrace][InteractAction][f{Time.frameCount}] Dispatch | queued={queued} target='{target?.name ?? "null"}'");

            Succeeded = queued;
            if (!queued)
            {
                FailureReason = InteractionFailureReason.DispatchRejected;
                Cancel();
                return;
            }

            Complete();
        }

        public override void OnCancel()
        {
            if (Succeeded || FailureReason != InteractionFailureReason.None)
                return;

            if (!_started)
                FailureReason = ResolvePreconditionFailureReason();
            else
                FailureReason = InteractionFailureReason.Cancelled;
        }

        private InteractionFailureReason ResolvePreconditionFailureReason()
        {
            if (_interactor == null || _interactor.Owner == null || !_interactor.Owner.activeInHierarchy)
                return InteractionFailureReason.InteractorUnavailable;

            if (_interactable == null)
                return InteractionFailureReason.InteractionUnavailable;

            Component interactionComponent = _interactable as Component;
            if (interactionComponent != null && !interactionComponent.gameObject.activeInHierarchy)
                return InteractionFailureReason.InteractionUnavailable;

            if (!_interactable.CanInteract(_interactor))
                return InteractionFailureReason.Occupied;

            return InteractionFailureReason.PreconditionsFailed;
        }
    }
}
