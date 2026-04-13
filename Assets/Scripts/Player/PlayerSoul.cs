using UnityEngine;
using Sol.AI;

namespace Sol.Player
{
    /// <summary>
    /// Player-specific soul component.
    ///
    /// Inherits NPCSoul so player and NPC characters share the same simplified
    /// health/death runtime model without duplicate plumbing.
    /// </summary>
    [AddComponentMenu("Sol/Player/Player Soul")]
    public class PlayerSoul : NPCSoul
    {
        private void Reset()
        {
            SoulKind = SoulType.Player;
        }

        private void OnValidate()
        {
            SoulKind = SoulType.Player;
        }
    }
}
