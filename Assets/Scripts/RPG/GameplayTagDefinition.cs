using UnityEngine;

namespace Sol.Rpg
{
    public enum GameplayTagCategory
    {
        Actor = 0,
        Item = 1,
        Faction = 2,
        Job = 3,
        Race = 4,
        Status = 5,
        Trait = 6,
        Skill = 7,
        Damage = 8,
        Defense = 9,
        World = 10,
        Other = 99
    }

    [CreateAssetMenu(fileName = "TAG00001_NewTag", menuName = "Sol/RPG/Gameplay Tag Definition")]
    public sealed class GameplayTagDefinition : RpgDefinition
    {
        [Header("Gameplay Tag")]
        [SerializeField] private GameplayTagCategory _category = GameplayTagCategory.Other;
        [SerializeField] private string _tagPath = string.Empty;
        [TextArea(2, 6)]
        [SerializeField] private string _authoringNotes = string.Empty;

        public GameplayTagCategory Category => _category;
        public string TagPath => _tagPath;
        public string AuthoringNotes => _authoringNotes;

        private void OnValidate()
        {
            _tagPath = GameplayTagUtility.NormalizePathOrEmpty(_tagPath);
            _authoringNotes = _authoringNotes?.Trim() ?? string.Empty;
        }
    }
}
