namespace Sol.Actions
{
    /// <summary>
    /// Lightweight failure reasons for interaction actions.
    /// Used by AI state routing/cooldown logic to avoid heuristic retries.
    /// </summary>
    public enum InteractionFailureReason
    {
        None = 0,
        PreconditionsFailed,
        InteractorUnavailable,
        InteractionUnavailable,
        Occupied,
        BeginRejected,
        NoResolvedAction,
        DispatchRejected,
        OwnershipLost,
        Cancelled
    }

    /// <summary>
    /// Base for actions that invoke world interactions (IInteractable).
    /// </summary>
    public abstract class InteractionAction : GameAction { }
}
