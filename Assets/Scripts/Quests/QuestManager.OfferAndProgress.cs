using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Actions;
using Sol.Grab;
using Sol.HUD;
using Sol.ToD;
using System.Collections;

namespace Sol.Quests
{
    public partial class QuestManager : MonoBehaviour
    {

        public List<QuestDefinition> GetOfferableQuests(string npcNameOrBoard)
        {
            List<QuestDefinition> result = new();
            QuestRegistry reg = QuestRegistry.Get();
            if (reg == null)
                return result;

            bool fromBoard = string.IsNullOrWhiteSpace(npcNameOrBoard);

            for (int i = 0; i < reg.Quests.Count; i++)
            {
                QuestDefinition def = reg.Quests[i];
                if (!CanOfferQuest(def, npcNameOrBoard, fromBoard))
                    continue;

                result.Add(def);
            }

            return result;
        }


        public bool ArePrerequisitesMet(QuestDefinition def)
        {
            if (def == null)
                return false;

            for (int i = 0; i < def.PrerequisiteQuestIds.Count; i++)
            {
                string req = def.PrerequisiteQuestIds[i];
                if (!string.IsNullOrWhiteSpace(req) && !_completedIds.Contains(req))
                    return false;
            }

            return true;
        }


        public bool TryAccept(string questId, string giverNpcName)
        {
            QuestDefinition def = QuestRegistry.Get()?.Find(questId);
            if (def == null)
                return false;

            bool fromBoard = string.IsNullOrWhiteSpace(giverNpcName);
            if (!CanOfferQuest(def, giverNpcName, fromBoard))
                return false;

            QuestSaveData q = new()
            {
                QuestId = questId,
                State = QuestState.Active,
                CurrentObjectiveIndex = 0,
                CurrentObjectiveProgress = 0,
                RemainingSeconds = def.IsTimed ? def.TimeLimitSeconds : 0f,
                FailedAtRealtime = 0f,
                GiverNpcName = string.IsNullOrWhiteSpace(giverNpcName) ? def.GiverNpcName : giverNpcName,
            };

            PrepareObjectiveTracking(q, def);
            _active.Add(q);
            if (_trackedQuestId == null)
                _trackedQuestId = questId;

            OnQuestAccepted?.Invoke(q);
            OnQuestUpdated?.Invoke(q);

            EvaluateQuest(q);
            return true;
        }


        public bool TryTurnIn(string questId, string npcName)
        {
            QuestSaveData q = FindActive(questId);
            if (q == null || q.State != QuestState.ReadyToTurnIn)
                return false;

            QuestDefinition def = QuestRegistry.Get()?.Find(questId);
            if (def == null)
                return false;

            if (!string.IsNullOrWhiteSpace(def.GiverNpcName)
                && !NpcReferenceMatches(def.GiverNpcName, npcName))
            {
                return false;
            }

            GrantRewards(def.Reward);

            q.State = QuestState.Completed;
            q.CompletedAtRealtime = Time.realtimeSinceStartup;
            q.CompletedAtTotalDays = GetCurrentTotalDaysElapsed();
            _completedIds.Add(questId);
            _completedCache[questId] = CloneSaveData(q);

            _active.Remove(q);
            _countedFishByQuest.Remove(questId);
            if (_trackedQuestId == questId)
                _trackedQuestId = _active.Count > 0 ? _active[0].QuestId : null;

            OnQuestCompleted?.Invoke(q);
            return true;
        }


        public QuestSaveData GetTrackedQuest()
        {
            if (!string.IsNullOrWhiteSpace(_trackedQuestId))
            {
                QuestSaveData tracked = FindActive(_trackedQuestId);
                if (tracked != null)
                    return tracked;

                _trackedQuestId = null;
            }

            return _active.Count > 0 ? _active[0] : null;
        }


        public bool IsCompleted(string questId) => _completedIds.Contains(questId);


        public QuestSaveData GetReadyToTurnInQuest(string giverNpcName)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                QuestSaveData q = _active[i];
                if (q.State != QuestState.ReadyToTurnIn)
                    continue;

                QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
                if (def == null)
                    continue;

                if (string.IsNullOrWhiteSpace(def.GiverNpcName)
                    || NpcReferenceMatches(def.GiverNpcName, giverNpcName))
                {
                    return q;
                }
            }

            return null;
        }


        public void NotifyTalkedToNpc(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return;

            for (int i = 0; i < _active.Count; i++)
            {
                QuestSaveData q = _active[i];
                if (q.State != QuestState.Active)
                    continue;

                QuestObjective obj = CurrentObjective(q);
                if (obj == null || obj.Type != QuestObjectiveType.TalkToNpc)
                    continue;

                if (!NpcReferenceMatches(obj.NpcName, npcName))
                    continue;

                q.CurrentObjectiveProgress = 1;
                OnQuestUpdated?.Invoke(q);
                AdvanceIfComplete(q);
            }
        }


        public void NotifyDeliveredItem(string npcName, string itemId)
        {
            if (string.IsNullOrWhiteSpace(npcName) || string.IsNullOrWhiteSpace(itemId))
                return;

            for (int i = 0; i < _active.Count; i++)
            {
                QuestSaveData q = _active[i];
                if (q.State != QuestState.Active)
                    continue;

                QuestObjective obj = CurrentObjective(q);
                if (obj == null || obj.Type != QuestObjectiveType.DeliverItem)
                    continue;

                if (!NpcReferenceMatches(obj.NpcName, npcName)
                    || !obj.MatchesAnyAcceptableItemId(itemId))
                {
                    continue;
                }

                q.CurrentObjectiveProgress = Mathf.Min(q.CurrentObjectiveProgress + 1, Mathf.Max(1, obj.Count));
                OnQuestUpdated?.Invoke(q);
                AdvanceIfComplete(q);
            }
        }


        public static bool NpcReferenceMatches(string requiredReference, string candidateReference)
        {
            if (string.IsNullOrWhiteSpace(requiredReference) || string.IsNullOrWhiteSpace(candidateReference))
                return false;

            string required = requiredReference.Trim();
            string candidate = candidateReference.Trim();
            if (string.Equals(required, candidate, StringComparison.OrdinalIgnoreCase))
                return true;

            string requiredName = ResolveNpcDisplayName(required);
            string candidateName = ResolveNpcDisplayName(candidate);
            return !string.IsNullOrWhiteSpace(requiredName)
                && !string.IsNullOrWhiteSpace(candidateName)
                && string.Equals(requiredName, candidateName, StringComparison.OrdinalIgnoreCase);
        }


        private static string ResolveNpcDisplayName(string npcReference)
        {
            if (string.IsNullOrWhiteSpace(npcReference))
                return string.Empty;

            string trimmed = npcReference.Trim();
            string normalizedOwnerId = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(trimmed);
            if (!string.IsNullOrWhiteSpace(normalizedOwnerId))
            {
                GameObject npc = OwnerRegistry.Resolve(normalizedOwnerId);
                if (npc != null)
                {
                    NPCSoul soul = npc.GetComponent<NPCSoul>();
                    if (soul != null && !string.IsNullOrWhiteSpace(soul.CharacterName))
                        return soul.CharacterName.Trim();

                    return npc.name;
                }
            }

            return trimmed;
        }


        private void GrantRewards(QuestReward reward)
        {
            if (reward == null || _inventory == null)
                return;

            if (reward.Gold > 0)
                _inventory.Gold += reward.Gold;

            if (reward.Items == null || reward.Items.Count == 0)
                return;

            ItemRegistry itemRegistry = ItemRegistry.Get();
            if (itemRegistry == null)
                return;

            for (int i = 0; i < reward.Items.Count; i++)
            {
                ItemReward itemReward = reward.Items[i];
                if (itemReward == null || string.IsNullOrWhiteSpace(itemReward.ItemId))
                    continue;

                ItemComponent prefab = itemRegistry.GetPrefab(itemReward.ItemId);
                if (prefab == null)
                {
                    Debug.LogWarning($"[QuestManager] Reward item '{itemReward.ItemId}' not found in ItemRegistry.");
                    continue;
                }

                int count = Mathf.Max(1, itemReward.Count);
                for (int n = 0; n < count; n++)
                {
                    ItemComponent instance = UnityEngine.Object.Instantiate(prefab);
                    _inventory.Add(instance);
                }
            }
        }


        private void FailQuest(QuestSaveData q)
        {
            q.State = QuestState.Failed;
            q.FailedAtRealtime = Time.realtimeSinceStartup;

            _active.Remove(q);
            _failedCache[q.QuestId] = CloneSaveData(q);
            _countedFishByQuest.Remove(q.QuestId);
            if (_trackedQuestId == q.QuestId)
                _trackedQuestId = _active.Count > 0 ? _active[0].QuestId : null;

            OnQuestFailed?.Invoke(q);
            OnQuestUpdated?.Invoke(q);
        }


        private bool TryAutoOfferNow()
        {
            QuestRegistry registry = QuestRegistry.Get();
            if (registry == null)
                return false;

            for (int i = 0; i < registry.Quests.Count; i++)
            {
                QuestDefinition def = registry.Quests[i];
                if (def == null || !def.AutoOffer)
                    continue;

                if (!CanOfferQuest(def, def.GiverNpcName, fromBoard: false))
                    continue;

                DialoguePromptSystem prompt = DialoguePromptSystem.ResolveInstance(true);
                if (prompt == null)
                {
                    return false;
                }

                prompt.Show(
                    def.Title,
                    def.Summary,
                    confirmAction: () => TryAccept(def.QuestId, def.GiverNpcName),
                    cancelAction: null,
                    confirmLabel: "Accept",
                    cancelLabel: "Decline");
                return true;
            }

            return true;
        }


        private bool CanOfferQuest(QuestDefinition def, string npcNameOrBoard, bool fromBoard)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.QuestId))
                return false;

            if (FindActive(def.QuestId) != null)
                return false;

            if (!fromBoard && !NpcReferenceMatches(def.GiverNpcName, npcNameOrBoard))
                return false;

            if (!ArePrerequisitesMet(def))
                return false;

            float now = Time.realtimeSinceStartup;
            if (_failedCache.TryGetValue(def.QuestId, out QuestSaveData failed)
                && now - failed.FailedAtRealtime < def.RetryAfterSeconds)
            {
                return false;
            }

            if (!_completedCache.TryGetValue(def.QuestId, out QuestSaveData completed))
                return true;

            if (!def.Repeatable)
                return false;

            return IsRepeatCooldownComplete(def, completed, now);
        }


        private bool IsRepeatCooldownComplete(QuestDefinition def, QuestSaveData completed, float nowRealtime)
        {
            if (def == null || completed == null)
                return false;

            switch (def.RepeatMode)
            {
                case QuestRepeatMode.Immediately:
                    return true;

                case QuestRepeatMode.AfterRealtimeSeconds:
                    return nowRealtime - completed.CompletedAtRealtime >= Mathf.Max(0f, def.RepeatAfterRealtimeSeconds);

                case QuestRepeatMode.AfterInGameDays:
                    int requiredDays = Mathf.Max(0, def.RepeatAfterInGameDays);
                    int dayDelta = GetCurrentTotalDaysElapsed() - Mathf.Max(0, completed.CompletedAtTotalDays);
                    return dayDelta >= requiredDays;

                default:
                    return true;
            }
        }


        private int GetCurrentTotalDaysElapsed()
        {
            if (_calendar == null)
                _calendar = FindFirstObjectByType<Calendar>();

            return _calendar != null ? Mathf.Max(0, _calendar.TotalDaysElapsed) : 0;
        }


        private QuestSaveData FindActive(string questId)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (string.Equals(_active[i].QuestId, questId, StringComparison.OrdinalIgnoreCase))
                    return _active[i];
            }
            return null;
        }


        private static QuestObjective CurrentObjective(QuestSaveData q)
        {
            QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
            if (def == null)
                return null;

            if (q.CurrentObjectiveIndex < 0 || q.CurrentObjectiveIndex >= def.Objectives.Count)
                return null;

            return def.Objectives[q.CurrentObjectiveIndex];
        }


        private void PrepareObjectiveTracking(QuestSaveData q, QuestDefinition def)
        {
            q.PrefixProgress ??= new List<int>();
            q.PrefixProgress.Clear();

            if (q.CurrentObjectiveIndex < 0 || q.CurrentObjectiveIndex >= def.Objectives.Count)
                return;

            QuestObjective obj = def.Objectives[q.CurrentObjectiveIndex];
            if (obj.Type == QuestObjectiveType.CatchByPrefix && obj.PrefixRequirements != null)
            {
                for (int i = 0; i < obj.PrefixRequirements.Count; i++)
                    q.PrefixProgress.Add(0);
            }

            HashSet<string> seen = GetOrCreateSeenFishSet(q.QuestId);
            seen.Clear();

            if (!IsCatchDrivenObjective(obj) || _inventory == null)
                return;

            IReadOnlyList<InventorySlot> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (!TryGetFishSnapshot(slot, out _, out string fishKey))
                    continue;
                seen.Add(fishKey);
            }
        }
    }
}
