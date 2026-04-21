using UnityEngine;
using Sol.AI;
using Sol.Player;

namespace Sol
{
    /// <summary>
    /// Structured context passed to IInteractable methods. Wraps both player and NPC interactions.
    /// </summary>
    public class Interactor
    {
        public GameObject Owner { get; }
        public Transform Transform { get; }
        public Inventory Inventory { get; }
        public NPCSoul NpcSoul { get; }
        public PlayerSoul PlayerSoul { get; }
        public bool IsPlayer { get; }

        public Interactor(GameObject owner, bool isPlayer)
        {
            Owner = owner;
            Transform = owner.transform;
            Inventory = owner.GetComponent<Inventory>();
            NpcSoul = owner.GetComponent<NPCSoul>();
            PlayerSoul = owner.GetComponent<PlayerSoul>();
            IsPlayer = isPlayer;
        }
    }
}
