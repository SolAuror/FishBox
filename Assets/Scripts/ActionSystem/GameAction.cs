using System;
using UnityEngine;

namespace Sol.Actions
{
    /// <summary>
    /// Priority tiers for action preemption. Higher value interrupts lower.
 /// Actions at the same tier queue normally - no preemption.
    /// </summary>
    public enum ActionPriority
    {
        Low       = 0,
        Normal    = 10,
        High      = 20,
        Critical  = 30
    }

    /// <summary>
    /// Abstract base for all discrete actions in the simulation.
    /// Actions express intent and delegate to State/Execution systems through ActionContext.
    /// They must never manipulate transforms, physics, animation, or UI directly.
    /// </summary>
    public abstract class GameAction
    {
        public ActionContext Context { get; private set; }
        public GameObject Target { get; private set; }
        public Rigidbody TargetRigidbody { get; private set; }

        public bool IsComplete { get; private set; }
        public bool IsCancelled { get; private set; }
        public bool IsRunning => Context != null && !IsComplete && !IsCancelled;

        /// <summary>Priority tier for preemption. Override in subclasses to escalate.</summary>
        public virtual ActionPriority Priority => ActionPriority.Normal;

        /// <summary>
        /// Optional follow-up action. Returned from OnStart/OnComplete,
        /// enqueued automatically by ActionSystem after this action finishes.
        /// Return null for no chaining.
        /// </summary>
        public virtual GameAction Next => null;

        /// <summary>Bind this action to an actor context. Called by ActionSystem before execution.</summary>
        public void Initialize(ActionContext context, GameObject target = null)
        {
            if (IsRunning)
                throw new InvalidOperationException(
                    $"[GameAction] Cannot re-initialize {GetType().Name} while it is still running.");

            Context = context;
            Target = target;
            TargetRigidbody = target != null ? target.GetComponent<Rigidbody>() : null;
            IsComplete = false;
            IsCancelled = false;
        }

        /// <summary>
        /// Return true if this action's preconditions are satisfied.
        /// Called immediately before OnStart. Must be free of side effects.
        /// </summary>
        public abstract bool CanExecute();

        /// <summary>Called once when the action begins. Instant actions should call Complete() here.</summary>
        public virtual void OnStart() { }

        /// <summary>Called each frame while the action is running.</summary>
        public virtual void OnUpdate() { }

        /// <summary>Cleanup hook invoked after the action finishes successfully.</summary>
        public virtual void OnComplete() { }

        /// <summary>Cleanup hook invoked when the action is cancelled.</summary>
        public virtual void OnCancel() { }

        /// <summary>Signal that this action has finished successfully.</summary>
        protected void Complete() => IsComplete = true;

        /// <summary>Signal that this action should be cancelled from within.</summary>
        protected void Cancel() => IsCancelled = true;

        /// <summary>Used by ActionSystem to force-set the cancelled flag on external cancellation.</summary>
        internal void ForceCancelInternal() => IsCancelled = true;
    }
}
