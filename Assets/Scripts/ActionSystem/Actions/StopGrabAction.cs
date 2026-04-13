namespace Sol.Actions
{
    /// <summary>
    /// Discrete action: exit the grab state by releasing whatever the actor is holding.
    /// Delegates entirely to GrabManager.ForceRelease — contains zero physics logic.
    /// </summary>
    public class StopGrabAction : StateAction
    {
        public override bool CanExecute()
        {
            return Context.GrabSystem != null && Context.GrabSystem.IsActive;
        }

        public override void OnStart()
        {
            Context.GrabSystem.ForceRelease();
            Complete();
        }
    }
}
