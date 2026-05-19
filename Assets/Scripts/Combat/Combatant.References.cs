using Sol.AI;
using Sol.Player;
using Sol.Rpg;
using UnityEngine;

namespace Sol.Combat
{
    public partial class Combatant
    {
        public void RefreshReferences()
        {
            if (_vitals is UnityEngine.Object vitalsObject && vitalsObject == null)
                _vitals = null;

            if (_playerSoul == null)
                _playerSoul = GetComponent<PlayerSoul>();
            if (_npcSoul == null)
                _npcSoul = GetComponent<NPCSoul>();
            _vitals ??= _playerSoul as IActorVitals;
            _vitals ??= _npcSoul as IActorVitals;
            _vitals ??= GetComponent<IActorVitals>();
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

            IActorVitals vitals = FindVitalsInParent(target);
            if (vitals is Component vitalsComponent)
                return ResolveOrAdd(vitalsComponent.gameObject);

            return null;
        }

        private static IActorVitals FindVitalsInParent(Transform target)
        {
            if (target == null)
                return null;

            MonoBehaviour[] behaviours = target.GetComponentsInParent<MonoBehaviour>(includeInactive: true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IActorVitals vitals)
                    return vitals;
            }

            return null;
        }
    }
}
