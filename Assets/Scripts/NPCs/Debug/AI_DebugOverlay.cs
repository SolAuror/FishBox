#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Sol.AI
{
    /// <summary>
    /// Compatibility shim: in-game AI debug UI now lives on NPCDebugUI/NPC_BarUI panels.
    /// This component remains on the manager prefab to avoid scene/prefab breakage.
    /// </summary>
    public class AI_DebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool logMigrationNotice = false;
        private bool _logged;

        private void OnEnable()
        {
            if (_logged || !logMigrationNotice) return;
            _logged = true;
            Debug.Log("[AI_DebugOverlay] In-game overlay moved to NPCDebugUI world panels.", this);
        }
    }
}
#endif