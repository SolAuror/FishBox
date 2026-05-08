using UnityEngine;
using Sol.Grab;
using Sol.Fishing;
using Sol.Combat;

namespace Sol.Actions
{
    /// <summary>
    /// Cached snapshot of an actor's system references, built once per actor registration.
    /// Actions access all systems exclusively through this - no GetComponent inside actions.
    /// </summary>
    public sealed class ActionContext
    {
        private readonly GameObject _actor;
        private BasicMeleeAttack _basicMeleeAttack;

        public GameObject Actor { get; }
        public Transform Transform { get; }
        public Inventory Inventory { get; }
        public Equipment Equipment { get; }
        public FishingState FishingState { get; }
        public BasicMeleeAttack BasicMeleeAttack
        {
            get
            {
                if (_basicMeleeAttack == null && _actor != null)
                    _basicMeleeAttack = _actor.GetComponent<BasicMeleeAttack>();

                return _basicMeleeAttack;
            }
        }
        // GrabManager is scene-owned and may be recreated; always resolve live instance.
        public GrabManager GrabSystem => GrabManager.Instance;

        /// <summary>True if the Actor GameObject has not been destroyed.</summary>
        public bool IsValid => Actor != null;

        public ActionContext(GameObject actor)
        {
            _actor = actor;
            Actor = actor;
            Transform = actor.transform;
            Inventory = actor.GetComponent<Inventory>();
            Equipment = actor.GetComponent<Equipment>();
            FishingState = actor.GetComponent<FishingState>();
            _basicMeleeAttack = actor.GetComponent<BasicMeleeAttack>();
        }
    }
}
