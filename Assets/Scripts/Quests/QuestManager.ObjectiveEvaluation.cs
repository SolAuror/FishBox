using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Actions;
using Sol.Fishing;
using Sol.Grab;
using Sol.HUD;
using Sol.ToD;
using System.Collections;

namespace Sol.Quests
{
    public partial class QuestManager : MonoBehaviour
    {

        private void HandleInventoryChanged() => EvaluateAll();


        private void HandleEquipmentChanged() => EvaluateAll();


        private void HandleActionCompleted(GameObject actor, GameAction action)
        {
            if (action is not OpenConversationAction)
                return;

            GameObject target = action.Target;
            if (target == null)
                return;

            NPCSoul soul = target.GetComponent<NPCSoul>();
            string npcName = soul != null && !string.IsNullOrWhiteSpace(soul.CharacterName)
                ? soul.CharacterName
                : target.name;

            string npcReference = soul != null && !string.IsNullOrWhiteSpace(soul.OwnerId)
                ? soul.OwnerId
                : npcName;

            NotifyTalkedToNpc(npcReference);
        }


        private void EvaluateAll()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                QuestSaveData q = _active[i];
                if (q.State == QuestState.Active)
                    EvaluateQuest(q);
            }
        }


        private void EvaluateQuest(QuestSaveData q)
        {
            QuestObjective obj = CurrentObjective(q);
            if (obj == null)
                return;

            switch (obj.Type)
            {
                case QuestObjectiveType.CollectItem:
                    q.CurrentObjectiveProgress = Mathf.Min(CountItemsInInventory(obj), Mathf.Max(1, obj.Count));
                    OnQuestUpdated?.Invoke(q);
                    break;

                case QuestObjectiveType.CatchCount:
                    CountNewFishForObjective(q, obj, _ => true, (snapshot, slot) =>
                    {
                        q.CurrentObjectiveProgress = Mathf.Min(q.CurrentObjectiveProgress + 1, Mathf.Max(1, obj.Count));
                    });
                    break;

                case QuestObjectiveType.CatchRarity:
                    CountNewFishForObjective(q, obj, snapshot => snapshot.Rarity == obj.Rarity, (snapshot, slot) =>
                    {
                        q.CurrentObjectiveProgress = Mathf.Min(q.CurrentObjectiveProgress + 1, Mathf.Max(1, obj.Count));
                    });
                    break;

                case QuestObjectiveType.CatchTotalValue:
                    CountNewFishForObjective(q, obj, _ => true, (snapshot, slot) =>
                    {
                        q.CurrentObjectiveProgress = Mathf.Min(
                            q.CurrentObjectiveProgress + Mathf.Max(0, snapshot.Value),
                            Mathf.Max(0, obj.GoldAmount));
                    });
                    break;

                case QuestObjectiveType.CatchByPrefix:
                    EvaluatePrefixObjective(q, obj);
                    break;

                case QuestObjectiveType.EquipItem:
                    q.CurrentObjectiveProgress = IsRequiredItemEquipped(obj) ? 1 : 0;
                    OnQuestUpdated?.Invoke(q);
                    break;

                case QuestObjectiveType.TalkToNpc:
                case QuestObjectiveType.DeliverItem:
                    break;
            }

            AdvanceIfComplete(q);
        }


        private void AdvanceIfComplete(QuestSaveData q)
        {
            QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
            if (def == null)
                return;

            bool changed = false;
            int guard = 32;
            while (guard-- > 0)
            {
                QuestObjective obj = CurrentObjective(q);
                if (obj == null || !IsObjectiveSatisfied(q, obj))
                    break;

                q.CurrentObjectiveIndex++;
                q.CurrentObjectiveProgress = 0;
                OnObjectiveAdvanced?.Invoke(q);
                changed = true;

                if (q.CurrentObjectiveIndex >= def.Objectives.Count)
                {
                    q.State = QuestState.ReadyToTurnIn;
                    break;
                }

                PrepareObjectiveTracking(q, def);
                EvaluateQuest(q);
                if (q.State != QuestState.Active)
                    break;
            }

            if (changed)
                OnQuestUpdated?.Invoke(q);
        }


        private bool IsObjectiveSatisfied(QuestSaveData q, QuestObjective obj)
        {
            switch (obj.Type)
            {
                case QuestObjectiveType.CatchByPrefix:
                    if (obj.PrefixRequirements == null || obj.PrefixRequirements.Count == 0)
                        return true;

                    for (int i = 0; i < obj.PrefixRequirements.Count; i++)
                    {
                        int required = Mathf.Max(1, obj.PrefixRequirements[i].Count);
                        int have = i < q.PrefixProgress.Count ? q.PrefixProgress[i] : 0;
                        if (have < required)
                            return false;
                    }
                    return true;

                case QuestObjectiveType.CatchTotalValue:
                    return q.CurrentObjectiveProgress >= Mathf.Max(0, obj.GoldAmount);

                case QuestObjectiveType.TalkToNpc:
                    return q.CurrentObjectiveProgress >= 1;

                case QuestObjectiveType.EquipItem:
                    return q.CurrentObjectiveProgress >= 1;

                case QuestObjectiveType.DeliverItem:
                    int requiredGold = obj.GetRequiredGoldPaymentAmount();
                    return requiredGold > 0
                        ? q.CurrentObjectiveProgress >= requiredGold
                        : q.CurrentObjectiveProgress >= Mathf.Max(1, obj.Count);

                default:
                    return q.CurrentObjectiveProgress >= Mathf.Max(1, obj.Count);
            }
        }


        private int CountItemsInInventory(QuestObjective objective)
        {
            if (_inventory == null || objective == null)
                return 0;

            int total = 0;
            IReadOnlyList<InventorySlot> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot?.Item == null)
                    continue;

                if (objective.MatchesAnyAcceptableItemId(slot.Item.ItemId))
                    total += Mathf.Max(1, slot.Count);
            }

            return total;
        }


        private void EvaluatePrefixObjective(QuestSaveData q, QuestObjective obj)
        {
            if (obj.PrefixRequirements == null)
                return;

            while (q.PrefixProgress.Count < obj.PrefixRequirements.Count)
                q.PrefixProgress.Add(0);

            CountNewFishForObjective(q, obj, _ => true, (snapshot, slot) =>
            {
                for (int p = 0; p < obj.PrefixRequirements.Count; p++)
                {
                    PrefixRequirement req = obj.PrefixRequirements[p];
                    if (string.IsNullOrWhiteSpace(req.Prefix))
                        continue;

                    if (!string.Equals(req.Prefix, snapshot.Prefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    int needed = Mathf.Max(1, req.Count);
                    if (q.PrefixProgress[p] < needed)
                        q.PrefixProgress[p]++;
                    break;
                }
            });
        }


        private void CountNewFishForObjective(
            QuestSaveData q,
            QuestObjective obj,
            Func<FishSnapshot, bool> match,
            Action<FishSnapshot, InventorySlot> apply)
        {
            if (_inventory == null)
                return;

            HashSet<string> seen = GetOrCreateSeenFishSet(q.QuestId);
            IReadOnlyList<InventorySlot> slots = _inventory.Slots;
            bool changed = false;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (!TryGetFishSnapshot(slot, out FishSnapshot snapshot, out string fishKey))
                    continue;

                if (!seen.Add(fishKey))
                    continue;

                if (!match(snapshot))
                    continue;

                apply(snapshot, slot);
                changed = true;
            }

            if (changed)
                OnQuestUpdated?.Invoke(q);
        }


        private bool IsRequiredItemEquipped(QuestObjective obj)
        {
            if (_equipment == null)
                return false;

            HashSet<ItemComponent> processedRods = null;
            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> kvp in _equipment.Equipped)
            {
                ItemComponent equipped = kvp.Value;
                if (equipped == null)
                    continue;

                if (obj.MatchSlot && kvp.Key != obj.Slot)
                    continue;

                if (DoesObjectiveMatchItem(obj, equipped))
                    return true;

                FishingRodItem rod = equipped.GetComponent<FishingRodItem>();
                if (rod == null)
                    continue;

                processedRods ??= new HashSet<ItemComponent>();
                if (!processedRods.Add(equipped))
                    continue;

                if (!obj.MatchSlot && IsRodLoadoutMatch(obj, rod))
                    return true;

                if (!obj.HasAnyAcceptableItemId() && string.IsNullOrWhiteSpace(obj.ItemTag) && obj.MatchSlot)
                    return true;
            }

            return false;
        }

        private static bool IsRodLoadoutMatch(QuestObjective obj, FishingRodItem rod)
        {
            if (obj == null || rod == null)
                return false;

            return DoesObjectiveMatchItem(obj, rod.LoadedLureItem)
                || DoesObjectiveMatchItem(obj, rod.LoadedBaitItem);
        }

        private static bool DoesObjectiveMatchItem(QuestObjective obj, ItemComponent item)
        {
            if (obj == null || item == null)
                return false;

            if (obj.MatchesAnyAcceptableItemId(item.ItemId))
                return true;

            if (!string.IsNullOrWhiteSpace(obj.ItemTag)
                && !string.IsNullOrWhiteSpace(item.ItemName)
                && item.ItemName.IndexOf(obj.ItemTag, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }


        private HashSet<string> GetOrCreateSeenFishSet(string questId)
        {
            if (!_countedFishByQuest.TryGetValue(questId, out HashSet<string> seen))
            {
                seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _countedFishByQuest[questId] = seen;
            }

            return seen;
        }


        private bool TryGetFishSnapshot(InventorySlot slot, out FishSnapshot snapshot, out string fishKey)
        {
            snapshot = default;
            fishKey = string.Empty;

            if (slot?.Item == null)
                return false;

            if (!string.IsNullOrWhiteSpace(slot.FishCode))
            {
                fishKey = slot.FishCode;
                CaughtFishData fishData = FishRegistry.Instance.GetFish(slot.FishCode);
                if (fishData != null)
                {
                    snapshot = new FishSnapshot
                    {
                        Prefix = fishData.prefix,
                        Rarity = fishData.rarity,
                        Value = fishData.cachedValue,
                    };
                    return true;
                }
            }

            CaughtFishItem fish = slot.Item.GetComponent<CaughtFishItem>();
            if (fish == null)
                return false;

            if (string.IsNullOrWhiteSpace(fishKey))
                fishKey = fish.FishCode;

            if (string.IsNullOrWhiteSpace(fishKey))
                fishKey = slot.Item.GetInstanceID().ToString();

            snapshot = new FishSnapshot
            {
                Prefix = fish.Prefix,
                Rarity = fish.Rarity,
                Value = Mathf.Max(0, slot.Item.Value),
            };
            return true;
        }


        private static bool IsCatchDrivenObjective(QuestObjective obj)
        {
            return obj != null
                && (obj.Type == QuestObjectiveType.CatchCount
                    || obj.Type == QuestObjectiveType.CatchTotalValue
                    || obj.Type == QuestObjectiveType.CatchRarity
                    || obj.Type == QuestObjectiveType.CatchByPrefix);
        }
    }
}
