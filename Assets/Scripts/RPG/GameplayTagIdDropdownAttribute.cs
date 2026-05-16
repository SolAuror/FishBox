using UnityEngine;

namespace Sol.Rpg
{
    public sealed class GameplayTagIdDropdownAttribute : PropertyAttribute
    {
        public bool AllowEmpty { get; }

        public GameplayTagIdDropdownAttribute(bool allowEmpty = true)
        {
            AllowEmpty = allowEmpty;
        }
    }
}
