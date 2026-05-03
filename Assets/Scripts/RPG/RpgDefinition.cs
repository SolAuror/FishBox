using UnityEngine;

namespace Sol.Rpg
{
    public abstract class RpgDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [TextArea(2, 6)]
        [SerializeField] private string _description = string.Empty;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
    }
}
