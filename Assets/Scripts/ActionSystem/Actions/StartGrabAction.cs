using Sol.Grab;

namespace Sol.Actions
{
    /// <summary>
    /// Discrete action: enter the grab state for a target GrabbableComponent.
    /// Delegates entirely to GrabManager — contains zero physics logic.
    /// Target must be passed as the dispatch target GameObject.
    /// </summary>
    public class StartGrabAction : StateAction
    {
        public bool Succeeded { get; private set; }

        private GrabbableComponent _cachedGrabbable;

        public override bool CanExecute()
        {
            if (Context.GrabSystem == null || !Context.GrabSystem.isGrabbingEnabled)
                return false;

            if (Context.GrabSystem.IsActive) return false;

            if (Target == null) return false;

            _cachedGrabbable = Target.GetComponent<GrabbableComponent>();
            return _cachedGrabbable != null && !_cachedGrabbable.IsGrabbed;
        }

        public override void OnStart()
        {
            Context.GrabSystem.GrabObject(_cachedGrabbable);
            Succeeded = true;
            Complete();
        }
    }
}
