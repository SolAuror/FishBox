using System.Collections.Generic;
using Sol.Grab;
using Sol.Rpg;
using UnityEngine;

namespace Sol.Combat
{
    public sealed class DamagePacket
    {
        private readonly List<string> _damageTagPaths = new();

        public Combatant Attacker { get; }
        public Combatant Target { get; }
        public ItemComponent SourceWeapon { get; }
        public float OriginalDamage { get; }
        public float Amount { get; set; }
        public bool IsPeriodic { get; }
        public IReadOnlyList<string> DamageTagPaths => _damageTagPaths;

        public DamagePacket(
            Combatant attacker,
            Combatant target,
            ItemComponent sourceWeapon,
            float damage,
            IEnumerable<string> damageTagPaths = null,
            bool isPeriodic = false)
        {
            Attacker = attacker;
            Target = target;
            SourceWeapon = sourceWeapon;
            OriginalDamage = Mathf.Max(0f, damage);
            Amount = OriginalDamage;
            IsPeriodic = isPeriodic;
            AddDamageTags(damageTagPaths);
        }

        public static DamagePacket FromHit(CombatHit hit)
        {
            return new DamagePacket(
                hit.Attacker,
                hit.Target,
                hit.SourceWeapon,
                hit.BaseDamage,
                hit.DamageTagPaths,
                isPeriodic: false);
        }

        public static DamagePacket StatusTick(Combatant source, Combatant target, float damage, IEnumerable<string> damageTagPaths)
        {
            return new DamagePacket(source, target, null, damage, damageTagPaths, isPeriodic: true);
        }

        public bool HasDamageTagOrChild(string tagPath)
        {
            string normalized = GameplayTagUtility.NormalizePathOrEmpty(tagPath);
            if (string.IsNullOrEmpty(normalized))
                return false;

            for (int i = 0; i < _damageTagPaths.Count; i++)
            {
                if (GameplayTagUtility.IsChildOfOrEqual(_damageTagPaths[i], normalized))
                    return true;
            }

            return false;
        }

        private void AddDamageTags(IEnumerable<string> damageTagPaths)
        {
            HashSet<string> seen = new(System.StringComparer.OrdinalIgnoreCase);
            if (damageTagPaths != null)
            {
                foreach (string path in damageTagPaths)
                {
                    string normalized = GameplayTagUtility.NormalizePathOrEmpty(path);
                    if (!string.IsNullOrEmpty(normalized) && seen.Add(normalized))
                        _damageTagPaths.Add(normalized);
                }
            }

            if (_damageTagPaths.Count == 0)
                _damageTagPaths.Add("Damage.Physical");
        }
    }
}
