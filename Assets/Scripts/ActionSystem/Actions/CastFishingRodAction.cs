namespace Sol.Actions
{
    /// <summary>
    /// Routes the player's intent to cast a fishing rod through the ActionSystem.
    /// Execution delegates entirely to FishingRodState — this action owns no logic.
    /// </summary>
    public sealed class CastFishingRodAction : GameAction
    {
        public override bool CanExecute()
            => Context?.FishingRodState != null && Context.FishingRodState.CanCast;

        public override void OnStart()
        {
            Context.FishingRodState.ExecuteCast();
            Complete();
        }
    }
}
