using System;
using UnityEngine;
using Sol.Grab;

namespace Sol
{
    /// <summary>
    /// DEPRECATED data-only stub. All logic has migrated to AI_NPC partials
    /// (AI_NPC.Conversations.cs, AI_NPC.Shop.cs). This stub exists only so that
    /// existing NPC prefabs deserialize their authored fields, which the
    /// editor-time migration utility (AI_NPC_TraderMigration) copies onto AI_NPC
    /// before destroying this component. Once every prefab in the project has
    /// been migrated, this file (and the component) can be deleted.
    /// </summary>
    [Obsolete("NpcTrader has moved into AI_NPC.Conversations.cs / AI_NPC.Shop.cs. Run Tools/Sol/NPCs/Migrate NpcTrader → AI_NPC.")]
    [AddComponentMenu("")]
    public class NpcTrader : MonoBehaviour
    {
        // ----- Serialized fields preserved for migration. Field names must match
        // the corresponding fields on AI_NPC.Conversations.cs / AI_NPC.Shop.cs. -----
        [SerializeField] private string _prompt = "Trade";
        [SerializeField] private string _lootPrompt = "Loot";
        [SerializeField] private bool _useConversationWindow = true;
        [SerializeField] private string _talkPrompt = "Talk";
        [SerializeField] private string _greetingLine = "What can I do for you?";
        [SerializeField] private string _tradeOptionLabel = "Trade";
        [SerializeField] private string _goodbyeOptionLabel = "Goodbye";
        [SerializeField] private Sprite _speakerIcon;
        [SerializeField] private string _goldLootItemId = string.Empty;
        [SerializeField] private ItemComponent _goldLootItemTemplate;
        [SerializeField] [Min(1)] private int _maxGoldItemizeAttemptsPerOpen = 2000;
    }
}
