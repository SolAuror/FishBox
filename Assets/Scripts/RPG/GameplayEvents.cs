using System;
using Sol;
using Sol.Combat;
using Sol.Grab;
using UnityEngine;

namespace Sol.Rpg
{
    public readonly struct ItemConsumedEvent
    {
        public ItemComponent Item { get; }
        public Interactor Interactor { get; }

        public ItemConsumedEvent(ItemComponent item, Interactor interactor)
        {
            Item = item;
            Interactor = interactor;
        }
    }

    public readonly struct StatusEffectEvent
    {
        public GameObject Target { get; }
        public StatusEffectDefinition Definition { get; }

        public StatusEffectEvent(GameObject target, StatusEffectDefinition definition)
        {
            Target = target;
            Definition = definition;
        }
    }

    public static class GameplayEvents
    {
        public static event Action<DamagePacket> OnDamagePreparing;
        public static event Action<CombatDamageResult> OnDamageResolved;
        public static event Action<CombatDamageResult> OnActorKilled;
        public static event Action<ItemConsumedEvent> OnItemConsumed;
        public static event Action<StatusEffectEvent> OnStatusApplied;
        public static event Action<StatusEffectEvent> OnStatusRemoved;
        public static event Action<float> OnGameplayTick;

        public static void RaiseDamagePreparing(DamagePacket packet) => OnDamagePreparing?.Invoke(packet);
        public static void RaiseDamageResolved(CombatDamageResult result) => OnDamageResolved?.Invoke(result);
        public static void RaiseActorKilled(CombatDamageResult result) => OnActorKilled?.Invoke(result);
        public static void RaiseItemConsumed(ItemComponent item, Interactor interactor) => OnItemConsumed?.Invoke(new ItemConsumedEvent(item, interactor));
        public static void RaiseStatusApplied(GameObject target, StatusEffectDefinition definition) => OnStatusApplied?.Invoke(new StatusEffectEvent(target, definition));
        public static void RaiseStatusRemoved(GameObject target, StatusEffectDefinition definition) => OnStatusRemoved?.Invoke(new StatusEffectEvent(target, definition));
        public static void RaiseGameplayTick(float deltaTime) => OnGameplayTick?.Invoke(deltaTime);
    }
}
