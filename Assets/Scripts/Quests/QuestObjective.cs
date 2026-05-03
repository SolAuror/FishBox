using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Sol.AI;
using Sol;

namespace Sol.Quests
{
    public enum QuestObjectiveType
    {
        CatchCount = 0,
        CatchTotalValue = 1,
        CatchRarity = 2,
        CatchByPrefix = 3,
        CollectItem = 4,
        DeliverItem = 5,
        TalkToNpc = 6,
        EquipItem = 7,
    }

    [Serializable]
    public class PrefixRequirement
    {
        public string Prefix = string.Empty;
        [Min(1)] public int Count = 1;
    }

    [Serializable]
    public class ItemIdRequirement
    {
        [ItemIdDropdown]
        public string ItemId = string.Empty;
    }

    /// <summary>
    /// Polymorphic sequential quest objective. Only the fields relevant to <see cref="Type"/> are used.
    /// </summary>
    [Serializable]
    public class QuestObjective
    {
        public const string GoldItemId = "ITM00003";

        public QuestObjectiveType Type = QuestObjectiveType.CatchCount;

        [Tooltip("Human-readable description shown in the HUD tracker (e.g. 'Catch 5 fish').")]
        public string DisplayText = string.Empty;

        [Tooltip("For CatchCount / CollectItem / DeliverItem: target count. For CatchRarity: count of that rarity.")]
        [Min(1)] public int Count = 1;

        [FormerlySerializedAs("ItemId")]
        [SerializeField, HideInInspector]
        private string _legacySingleItemId = string.Empty;

        [Tooltip("For CollectItem / DeliverItem / EquipItem: acceptable ItemIds (any one can match).")]
        public List<ItemIdRequirement> AcceptableItemIds = new();

        [Tooltip("For EquipItem: optional tag on ItemComponent (e.g. 'Lure', 'Bait') when acceptable ids are empty.")]
        public string ItemTag = string.Empty;

        [Tooltip("For EquipItem: when true, restrict matching to the selected equipment slot.")]
        public bool MatchSlot = false;

        [Tooltip("For EquipItem: target equipment slot when MatchSlot is enabled.")]
        public EquipmentSlotType Slot = EquipmentSlotType.RightHand;

        [Tooltip("For CatchTotalValue: total gold value threshold.")]
        public int GoldAmount = 0;

        [Tooltip("For CatchRarity: required fish rarity.")]
        public FishRarity Rarity = FishRarity.Common;

        [Tooltip("For CatchByPrefix: list of prefix+count pairs (e.g. 2 Tiny + 1 Lofty).")]
        public List<PrefixRequirement> PrefixRequirements = new();

        [Tooltip("For DeliverItem / TalkToNpc: NPC owner id (OWN#####).")]
        [NpcIdDropdown]
        public string NpcName = string.Empty;

        [Tooltip("For TalkToNpc: optional topic id. Empty = any conversation with the NPC counts. " +
                 "When set, only NotifyTalkedAboutTopic events whose topic matches case-insensitively advance this objective.")]
        public string Topic = string.Empty;

        public bool IsGoldPaymentObjective()
        {
            return Type == QuestObjectiveType.DeliverItem && GetRequiredGoldPaymentAmount() > 0;
        }

        public int GetRequiredGoldPaymentAmount()
        {
            if (Type != QuestObjectiveType.DeliverItem)
                return 0;

            if (GoldAmount > 0)
                return Mathf.Max(1, GoldAmount);

            return MatchesAnyAcceptableItemId(GoldItemId) ? Mathf.Max(1, Count) : 0;
        }

        public bool HasAnyAcceptableItemId()
        {
            foreach (string _ in EnumerateAcceptableItemIds())
                return true;

            return false;
        }

        public bool MatchesAnyAcceptableItemId(string candidateItemId)
        {
            if (string.IsNullOrWhiteSpace(candidateItemId))
                return false;

            foreach (string acceptable in EnumerateAcceptableItemIds())
            {
                if (string.Equals(acceptable, candidateItemId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public IEnumerable<string> EnumerateAcceptableItemIds()
        {
            if (!string.IsNullOrWhiteSpace(_legacySingleItemId))
                yield return _legacySingleItemId.Trim();

            if (AcceptableItemIds == null)
                yield break;

            for (int i = 0; i < AcceptableItemIds.Count; i++)
            {
                string acceptable = AcceptableItemIds[i]?.ItemId;
                if (string.IsNullOrWhiteSpace(acceptable))
                    continue;

                yield return acceptable.Trim();
            }
        }
    }
}
