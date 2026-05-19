using UnityEngine;

namespace Sol.Rpg
{
    public sealed class StatIdDropdownAttribute : PropertyAttribute
    {
        public bool AllowEmpty { get; }

        public StatIdDropdownAttribute(bool allowEmpty = true)
        {
            AllowEmpty = allowEmpty;
        }
    }
}
