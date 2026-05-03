using UnityEngine;

namespace Sol.Rpg
{
    public enum RpgSkillCategory
    {
        Combat = 0,
        Magic = 1,
        Crafting = 2,
        Social = 3,
        Survival = 4,
        Movement = 5,
        Utility = 6
    }

    [CreateAssetMenu(fileName = "SKL00001_NewSkill", menuName = "Sol/RPG/Skill Definition")]
    public sealed class RpgSkillDefinition : RpgDefinition
    {
        [Header("Skill")]
        [SerializeField] private RpgSkillCategory _category = RpgSkillCategory.Utility;
        [Tooltip("Optional governing stat id (STA#####).")]
        [SerializeField] private string _governingStatId = string.Empty;
        [SerializeField] private bool _startsKnown = true;
        [Min(1)]
        [SerializeField] private int _maxLevel = 100;
        [Min(0.01f)]
        [SerializeField] private float _useXpMultiplier = 1f;

        public RpgSkillCategory Category => _category;
        public string GoverningStatId => _governingStatId;
        public bool StartsKnown => _startsKnown;
        public int MaxLevel => _maxLevel;
        public float UseXpMultiplier => _useXpMultiplier;
    }
}
