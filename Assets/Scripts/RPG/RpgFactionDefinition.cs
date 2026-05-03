using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.Rpg
{
    [Serializable]
    public sealed class RpgFactionRelationship
    {
        [Tooltip("Other faction id (FAC#####).")]
        public string FactionId = string.Empty;
        [Range(-100, 100)]
        public int Disposition = 0;
        public bool Hostile;
    }

    [CreateAssetMenu(fileName = "FAC00001_NewFaction", menuName = "Sol/RPG/Faction Definition")]
    public sealed class RpgFactionDefinition : RpgDefinition
    {
        [Header("Faction")]
        [SerializeField] private bool _joinable = true;
        [SerializeField] private bool _legalAuthority;
        [SerializeField] private bool _guardsAttackCriminals = true;
        [Min(0)]
        [SerializeField] private int _baseCrimeGold = 25;
        [SerializeField] private List<RpgFactionRelationship> _relationships = new();

        public bool Joinable => _joinable;
        public bool LegalAuthority => _legalAuthority;
        public bool GuardsAttackCriminals => _guardsAttackCriminals;
        public int BaseCrimeGold => _baseCrimeGold;
        public IReadOnlyList<RpgFactionRelationship> Relationships => _relationships;
    }
}
