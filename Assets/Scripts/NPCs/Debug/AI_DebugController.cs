#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sol.AI
{
    /// <summary>
    /// Editor/dev debug controller for AI NPCs.
    /// Keybindings operate on all AI_NPC instances in the scene.
    /// </summary>
    public class AI_DebugController : MonoBehaviour
    {
        private static AI_NPC[] FindAllNPCs() =>
            Object.FindObjectsByType<AI_NPC>(FindObjectsSortMode.None);

        private void Update()
        {
            if (Keyboard.current[Key.F9].wasPressedThisFrame) AI_DebugUISettings.ToggleVisible();
            if (Keyboard.current[Key.F1].wasPressedThisFrame) ForceState(AI_NPC.State.Patrol);
            if (Keyboard.current[Key.F2].wasPressedThisFrame) ForceState(AI_NPC.State.Idle);
            if (Keyboard.current[Key.F3].wasPressedThisFrame) KillAll();
            if (Keyboard.current[Key.F4].wasPressedThisFrame) ForceState(AI_NPC.State.Dead);
            if (Keyboard.current[Key.F5].wasPressedThisFrame) KillAll();
        }

        private static void ForceState(AI_NPC.State state)
        {
            foreach (var npc in FindAllNPCs())
            {
                npc.ForceState(state);
                Debug.Log($"[AI_Debug] {npc.name} ? {state}");
            }
        }

        private static void KillAll()
        {
            foreach (var npc in FindAllNPCs())
            {
                if (npc.Soul != null)
                    npc.Soul.TakeDamage(npc.Soul.MaxHealth);
            }
        }
    }
}
#endif
