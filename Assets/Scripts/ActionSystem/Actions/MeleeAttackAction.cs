using Sol.Combat;

namespace Sol.Actions
{
    /// <summary>
    /// Starts a melee attack through the shared combat component.
    /// Hit timing remains animation-event driven by BasicMeleeAttack.
    /// </summary>
    public class MeleeAttackAction : GameAction
    {
        public bool Succeeded { get; private set; }

        public override ActionPriority Priority => ActionPriority.High;

        public override bool CanExecute()
        {
            return Context != null
                && Context.BasicMeleeAttack != null
                && Context.BasicMeleeAttack.CanStartAttack();
        }

        public override void OnStart()
        {
            Succeeded = Context.BasicMeleeAttack != null
                && Context.BasicMeleeAttack.TryBeginAttack();

            Complete();
        }
    }
}
