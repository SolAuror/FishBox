using UnityEngine;
using Sol.Grab;
using Sol.Fishing;

namespace Sol.Actions
{
    /// <summary>
    /// Cached snapshot of an actor's system references, built once per actor registration.
    /// Actions access all systems exclusively through this - no GetComponent inside actions.
    /// </summary>
    public sealed class ActionContext
    {
        public GameObject Actor { get; }
        public Transform Transform { get; }
        public Inventory Inventory { get; }
        public Equipment Equipment { get; }
        public FishingRodState FishingRodState { get; }
        // GrabManager is scene-owned and may be recreated; always resolve live instance.
        public GrabManager GrabSystem => GrabManager.Instance;

        /// <summary>True if the Actor GameObject has not been destroyed.</summary>
        public bool IsValid => Actor != null;

        public ActionContext(GameObject actor)
        {
            Actor = actor;
            Transform = actor.transform;
            Inventory = actor.GetComponent<Inventory>();
            Equipment = actor.GetComponent<Equipment>();
            FishingRodState = actor.GetComponent<FishingRodState>();
        }
    }
}
