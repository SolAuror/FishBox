using UnityEngine;

namespace Sol.AI
{
    /// <summary>
    /// Read-only context passed to states to avoid repeated calculations.
    /// Populated once per frame in AI_NPC.Update() before Tick().
    /// </summary>
    public readonly struct IntentContext
    {
        public readonly NPCSoul Soul;

        public IntentContext(NPCSoul soul)
        {
            Soul = soul;
        }
    }
}
