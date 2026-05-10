using Sol.AI;
using Sol.Player;
using UnityEngine;

namespace Sol.Combat
{
    public partial class Combatant
    {
        public void RefreshReferences()
        {
            if (_playerSoul == null)
                _playerSoul = GetComponent<PlayerSoul>();
            if (_npcSoul == null)
                _npcSoul = GetComponent<NPCSoul>();
            if (_equipment == null)
                _equipment = GetComponent<Equipment>();
        }

        public static Combatant ResolveOrAdd(GameObject actor)
        {
            if (actor == null)
                return null;

            Combatant combatant = actor.GetComponent<Combatant>();
            if (combatant == null)
                combatant = actor.AddComponent<Combatant>();

            combatant.RefreshReferences();
            return combatant;
        }

        public static Combatant ResolveOrAdd(Transform target)
        {
            if (target == null)
                return null;

            Combatant combatant = target.GetComponentInParent<Combatant>();
            if (combatant != null)
            {
                combatant.RefreshReferences();
                return combatant;
            }

            PlayerSoul playerSoul = target.GetComponentInParent<PlayerSoul>();
            if (playerSoul != null)
                return ResolveOrAdd(playerSoul.gameObject);

            NPCSoul npcSoul = target.GetComponentInParent<NPCSoul>();
            if (npcSoul != null)
                return ResolveOrAdd(npcSoul.gameObject);

            return null;
        }
    }
}
