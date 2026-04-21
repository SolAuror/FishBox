using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Grab;

namespace Sol.Quests
{
    /// <summary>
    /// Central runtime singleton. Drives quest progress, timers, and save/load of quest state.
    /// Attach to a persistent GameObject (e.g. alongside SaveManager).
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }
#region Inspector Settings

        [Tooltip("Optional â€” if unassigned, resolved by finding Inventory + Equipment on the first PlayerSoul.")]
        [SerializeField] private GameObject _playerRoot;
#endregion

        private Inventory _inventory;
        private Equipment _equipment;

        private readonly List<QuestSaveData> _active = new();
        private readonly HashSet<string> _completedIds = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Maps questId â†’ set of FishCodes already counted toward that quest's current objective.</summary>
        private readonly Dictionary<string, HashSet<string>> _countedFishByQuest = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Optional tracked quest (the one the HUD displays). Null = show first active.</summary>
        private string _trackedQuestId;

        public event Action<QuestSaveData> OnQuestAccepted;
        public event Action<QuestSaveData> OnQuestUpdated;
        public event Action<QuestSaveData> OnObjectiveAdvanced;
        public event Action<QuestSaveData> OnQuestCompleted;
        public event Action<QuestSaveData> OnQuestFailed;

        public IReadOnlyList<QuestSaveData> Active => _active;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            UnsubscribeFromPlayer();
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ResolvePlayerRefs();
            SubscribeToPlayer();
            TryAutoOffer();
        }

        private void Update()
        {
            if (_active.Count == 0 || Time.timeScale <= 0f) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                QuestSaveData q = _active[i];
                if (q.State != QuestState.Active) continue;
                QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
                if (def == null || !def.IsTimed) continue;
                q.RemainingSeconds -= dt;
                if (q.RemainingSeconds <= 0f)
                {
                    q.RemainingSeconds = 0f;
                    FailQuest(q);
                }
                else
                {
                    OnQuestUpdated?.Invoke(q);
                }
            }
        }

        // ---------- Offer / accept / turn-in ----------

        /// <summary>Quests that can currently be offered from the given NPC (or the quest board if npcName is null/empty).</summary>
        public List<QuestDefinition> GetOfferableQuests(string npcName)
        {
            List<QuestDefinition> result = new();
            QuestRegistry reg = QuestRegistry.Get();
            if (reg == null) return result;
            bool fromBoard = string.IsNullOrWhiteSpace(npcName);
            float now = Time.realtimeSinceStartup;

            for (int i = 0; i < reg.Quests.Count; i++)
            {
                QuestDefinition def = reg.Quests[i];
                if (def == null || string.IsNullOrWhiteSpace(def.QuestId)) continue;
                if (_completedIds.Contains(def.QuestId)) continue;
                if (FindActive(def.QuestId) != null) continue;

                // Source filter: board entries must have no giver; NPC entries must match giver.
                bool giverMatches = fromBoard
                    ? string.IsNullOrWhiteSpace(def.GiverNpcName)
                    : string.Equals(def.GiverNpcName, npcName, StringComparison.OrdinalIgnoreCase);
                if (!giverMatches) continue;

                if (!ArePrerequisitesMet(def)) continue;

                // Retry cooldown from previous failure.
                QuestSaveData prevFail = FindFailedCached(def.QuestId);
                if (prevFail != null && (now - prevFail.FailedAtRealtime) < def.RetryAfterSeconds) continue;

                result.Add(def);
            }
            return result;
        }

        public bool ArePrerequisitesMet(QuestDefinition def)
        {
            for (int i = 0; i < def.PrerequisiteQuestIds.Count; i++)
            {
                string req = def.PrerequisiteQuestIds[i];
                if (!string.IsNullOrWhiteSpace(req) && !_completedIds.Contains(req)) return false;
            }
            return true;
        }

        public bool TryAccept(string questId, string giverNpcName)
        {
            QuestDefinition def = QuestRegistry.Get()?.Find(questId);
            if (def == null) return false;
            if (_completedIds.Contains(questId)) return false;
            if (FindActive(questId) != null) return false;
            if (!ArePrerequisitesMet(def)) return false;

            QuestSaveData q = new()
            {
                QuestId = questId,
                State = QuestState.Active,
                CurrentObjectiveIndex = 0,
                CurrentObjectiveProgress = 0,
                RemainingSeconds = def.IsTimed ? def.TimeLimitSeconds : 0f,
                GiverNpcName = giverNpcName ?? def.GiverNpcName,
            };
            InitPrefixProgress(q, def);
            _active.Add(q);
            _countedFishByQuest[questId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_trackedQuestId == null) _trackedQuestId = questId;

            OnQuestAccepted?.Invoke(q);
            OnQuestUpdated?.Invoke(q);

            // Immediate evaluation against current inventory/equipment in case objective 0 is already satisfied.
            EvaluateQuest(q);
            return true;
        }

        /// <summary>Try to turn in a quest whose state is ReadyToTurnIn. NpcName must match the quest's giver.</summary>
        public bool TryTurnIn(string questId, string npcName)
        {
            QuestSaveData q = FindActive(questId);
            if (q == null || q.State != QuestState.ReadyToTurnIn) return false;
            QuestDefinition def = QuestRegistry.Get()?.Find(questId);
            if (def == null) return false;
            if (!string.IsNullOrWhiteSpace(def.GiverNpcName)
                && !string.Equals(def.GiverNpcName, npcName, StringComparison.OrdinalIgnoreCase))
                return false;

            GrantRewards(def.Reward);
            q.State = QuestState.Completed;
            _completedIds.Add(questId);
            _active.Remove(q);
            _countedFishByQuest.Remove(questId);
            if (_trackedQuestId == questId) _trackedQuestId = _active.Count > 0 ? _active[0].QuestId : null;

            OnQuestCompleted?.Invoke(q);
            return true;
        }

        public QuestSaveData GetTrackedQuest()
        {
            if (_trackedQuestId != null)
            {
                QuestSaveData t = FindActive(_trackedQuestId);
                if (t != null) return t;
                _trackedQuestId = null;
            }
            return _active.Count > 0 ? _active[0] : null;
        }

        public bool IsCompleted(string questId) => _completedIds.Contains(questId);

        // ---------- External notify hooks (called by NpcTradeInteractable / delivery flow) ----------

        public void NotifyTalkedToNpc(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName)) return;
            for (int i = 0; i < _active.Count; i++)
            {
                QuestSaveData q = _active[i];
                if (q.State != QuestState.Active) continue;
                QuestObjective obj = CurrentObjective(q);
                if (obj == null || obj.Type != QuestObjectiveType.TalkToNpc) continue;
                if (string.Equals(obj.NpcName, npcName, StringComparison.OrdinalIgnoreCase))
                {
                    q.CurrentObjectiveProgress = 1;
                    AdvanceIfComplete(q);
                }
            }
        }

        public void NotifyDeliveredItem(string npcName, string itemId)
        {
            if (string.IsNullOrWhiteSpace(npcName) || string.IsNullOrWhiteSpace(itemId)) return;
            for (int i = 0; i < _active.Count; i++)
            {
                QuestSaveData q = _active[i];
                if (q.State != QuestState.Active) continue;
                QuestObjective obj = CurrentObjective(q);
                if (obj == null || obj.Type != QuestObjectiveType.DeliverItem) continue;
                if (!string.Equals(obj.NpcName, npcName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(obj.ItemId, itemId, StringComparison.OrdinalIgnoreCase)) continue;
                q.CurrentObjectiveProgress = Mathf.Min(q.CurrentObjectiveProgress + 1, obj.Count);
                AdvanceIfComplete(q);
            }
        }

        // ---------- Event wiring ----------

        private void HandleInventoryChanged() { EvaluateAll(); }
        private void HandleEquipmentChanged() { EvaluateAll(); }

        private void EvaluateAll()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                QuestSaveData q = _active[i];
                if (q.State == QuestState.Active) EvaluateQuest(q);
            }
        }

        private void EvaluateQuest(QuestSaveData q)
        {
            QuestObjective obj = CurrentObjective(q);
            if (obj == null) return;

            switch (obj.Type)
            {
                case QuestObjectiveType.CollectItem:
                    q.CurrentObjectiveProgress = Mathf.Min(CountItemsInInventory(obj.ItemId), obj.Count);
                    break;

                case QuestObjectiveType.CatchCount:
                    CountNewFish(q, obj, static (_, _) => true);
                    break;

                case QuestObjectiveType.CatchRarity:
                    CountNewFish(q, obj, (fish, _) => fish.Rarity == obj.Rarity);
                    break;

                case QuestObjectiveType.CatchTotalValue:
                    // Accumulate total value of uncounted fish until threshold reached.
                    AccumulateFishValue(q, obj);
                    break;

                case QuestObjectiveType.CatchByPrefix:
                    EvaluatePrefixObjective(q, obj);
                    break;

                case QuestObjectiveType.EquipItem:
                    q.CurrentObjectiveProgress = IsRequiredItemEquipped(obj) ? 1 : 0;
                    break;

                // TalkToNpc / DeliverItem driven by explicit Notify* calls, not inventory events.
                case QuestObjectiveType.TalkToNpc:
                case QuestObjectiveType.DeliverItem:
                    break;
            }

            OnQuestUpdated?.Invoke(q);
            AdvanceIfComplete(q);
        }

        private void AdvanceIfComplete(QuestSaveData q)
        {
            QuestObjective obj = CurrentObjective(q);
            if (obj == null) return;
            if (!IsObjectiveSatisfied(q, obj)) { OnQuestUpdated?.Invoke(q); return; }

            QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
            if (def == null) return;

            q.CurrentObjectiveIndex++;
            q.CurrentObjectiveProgress = 0;
            if (_countedFishByQuest.TryGetValue(q.QuestId, out var set)) set.Clear();

            if (q.CurrentObjectiveIndex >= def.Objectives.Count)
            {
                q.State = QuestState.ReadyToTurnIn;
                OnObjectiveAdvanced?.Invoke(q);
                OnQuestUpdated?.Invoke(q);
                return;
            }

            InitPrefixProgress(q, def);
            OnObjectiveAdvanced?.Invoke(q);
            OnQuestUpdated?.Invoke(q);
            // New objective may already be satisfied by current state â€” re-evaluate once.
            EvaluateQuest(q);
        }

        private bool IsObjectiveSatisfied(QuestSaveData q, QuestObjective obj)
        {
            switch (obj.Type)
            {
                case QuestObjectiveType.CatchByPrefix:
                    if (obj.PrefixRequirements == null || obj.PrefixRequirements.Count == 0) return true;
                    for (int i = 0; i < obj.PrefixRequirements.Count; i++)
                    {
                        int have = (i < q.PrefixProgress.Count) ? q.PrefixProgress[i] : 0;
                        if (have < obj.PrefixRequirements[i].Count) return false;
                    }
                    return true;

                case QuestObjectiveType.CatchTotalValue:
                    return q.CurrentObjectiveProgress >= obj.GoldAmount;

                case QuestObjectiveType.TalkToNpc:
                case QuestObjectiveType.EquipItem:
                    return q.CurrentObjectiveProgress >= 1;

                default:
                    return q.CurrentObjectiveProgress >= obj.Count;
            }
        }

        // ---------- Evaluation helpers ----------

        private int CountItemsInInventory(string itemId)
        {
            if (_inventory == null || string.IsNullOrWhiteSpace(itemId)) return 0;
            int total = 0;
            IReadOnlyList<InventorySlot> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot s = slots[i];
                if (s?.Item == null) continue;
                if (string.Equals(s.Item.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    total += Mathf.Max(1, s.Count);
            }
            return total;
        }

        private void CountNewFish(QuestSaveData q, QuestObjective obj, Func<CaughtFishItem, InventorySlot, bool> match)
        {
            if (_inventory == null) return;
            if (!_countedFishByQuest.TryGetValue(q.QuestId, out var seen))
                _countedFishByQuest[q.QuestId] = seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IReadOnlyList<InventorySlot> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot s = slots[i];
                if (s?.Item == null || string.IsNullOrWhiteSpace(s.FishCode)) continue;
                if (!seen.Add(s.FishCode)) continue;
                CaughtFishItem fish = s.Item.GetComponent<CaughtFishItem>();
                if (fish == null) continue;
                if (!match(fish, s)) continue;
                q.CurrentObjectiveProgress = Mathf.Min(q.CurrentObjectiveProgress + 1, obj.Count);
            }
        }

        private void AccumulateFishValue(QuestSaveData q, QuestObjective obj)
        {
            if (_inventory == null) return;
            if (!_countedFishByQuest.TryGetValue(q.QuestId, out var seen))
                _countedFishByQuest[q.QuestId] = seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IReadOnlyList<InventorySlot> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot s = slots[i];
                if (s?.Item == null || string.IsNullOrWhiteSpace(s.FishCode)) continue;
                if (!seen.Add(s.FishCode)) continue;
                q.CurrentObjectiveProgress = Mathf.Min(q.CurrentObjectiveProgress + s.Item.Value, obj.GoldAmount);
            }
        }

        private void EvaluatePrefixObjective(QuestSaveData q, QuestObjective obj)
        {
            if (_inventory == null || obj.PrefixRequirements == null) return;
            if (!_countedFishByQuest.TryGetValue(q.QuestId, out var seen))
                _countedFishByQuest[q.QuestId] = seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (q.PrefixProgress.Count < obj.PrefixRequirements.Count) q.PrefixProgress.Add(0);

            IReadOnlyList<InventorySlot> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot s = slots[i];
                if (s?.Item == null || string.IsNullOrWhiteSpace(s.FishCode)) continue;
                if (!seen.Add(s.FishCode)) continue;
                CaughtFishItem fish = s.Item.GetComponent<CaughtFishItem>();
                if (fish == null) continue;
                for (int p = 0; p < obj.PrefixRequirements.Count; p++)
                {
                    PrefixRequirement req = obj.PrefixRequirements[p];
                    if (string.IsNullOrWhiteSpace(req.Prefix)) continue;
                    if (!string.Equals(fish.Prefix, req.Prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    if (q.PrefixProgress[p] < req.Count) q.PrefixProgress[p]++;
                    break;
                }
            }
        }

        private bool IsRequiredItemEquipped(QuestObjective obj)
        {
            if (_equipment == null) return false;
            foreach (var kvp in _equipment.Equipped)
            {
                ItemComponent eq = kvp.Value;
                if (eq == null) continue;
                if (!string.IsNullOrWhiteSpace(obj.ItemId)
                    && string.Equals(eq.ItemId, obj.ItemId, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (!string.IsNullOrWhiteSpace(obj.ItemTag)
                    && eq.ItemName != null
                    && eq.ItemName.IndexOf(obj.ItemTag, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        // ---------- Rewards ----------

        private void GrantRewards(QuestReward reward)
        {
            if (reward == null) return;
            if (_inventory != null && reward.Gold > 0) _inventory.Gold += reward.Gold;
            if (reward.Items == null || _inventory == null) return;
            ItemRegistry itemReg = ItemRegistry.Get();
            if (itemReg == null) return;
            for (int i = 0; i < reward.Items.Count; i++)
            {
                ItemReward r = reward.Items[i];
                if (r == null || string.IsNullOrWhiteSpace(r.ItemId)) continue;
                ItemComponent prefab = itemReg.GetPrefab(r.ItemId);
                if (prefab == null) { Debug.LogWarning($"[QuestManager] Reward item '{r.ItemId}' not in ItemRegistry."); continue; }
                for (int n = 0; n < Mathf.Max(1, r.Count); n++)
                {
                    ItemComponent item = UnityEngine.Object.Instantiate(prefab);
                    if (item == null) continue;
                    _inventory.Add(item);
                }
            }
        }

        // ---------- Timer failure ----------

        private void FailQuest(QuestSaveData q)
        {
            q.State = QuestState.Failed;
            q.FailedAtRealtime = Time.realtimeSinceStartup;
            _active.Remove(q);
            _countedFishByQuest.Remove(q.QuestId);
            if (_trackedQuestId == q.QuestId) _trackedQuestId = _active.Count > 0 ? _active[0].QuestId : null;
            OnQuestFailed?.Invoke(q);
            _failedCache[q.QuestId] = q;
        }

        private readonly Dictionary<string, QuestSaveData> _failedCache =
            new(StringComparer.OrdinalIgnoreCase);

        private QuestSaveData FindFailedCached(string questId)
        {
            _failedCache.TryGetValue(questId, out QuestSaveData q);
            return q;
        }

        // ---------- Auto-offer (tutorial) ----------

        private void TryAutoOffer()
        {
            QuestRegistry reg = QuestRegistry.Get();
            if (reg == null) return;
            for (int i = 0; i < reg.Quests.Count; i++)
            {
                QuestDefinition def = reg.Quests[i];
                if (def == null || !def.AutoOffer) continue;
                if (_completedIds.Contains(def.QuestId)) continue;
                if (FindActive(def.QuestId) != null) continue;
                if (!ArePrerequisitesMet(def)) continue;
                TryAccept(def.QuestId, def.GiverNpcName);
            }
        }

        // ---------- Save / load ----------

        [Serializable]
        public class QuestPersistBlob
        {
            public List<QuestSaveData> Active = new();
            public List<string> Completed = new();
        }

        public QuestPersistBlob CollectSaveData()
        {
            QuestPersistBlob blob = new();
            for (int i = 0; i < _active.Count; i++) blob.Active.Add(_active[i]);
            foreach (string id in _completedIds) blob.Completed.Add(id);
            return blob;
        }

        public void ApplySaveData(QuestPersistBlob blob)
        {
            _active.Clear();
            _completedIds.Clear();
            _countedFishByQuest.Clear();
            _failedCache.Clear();
            _trackedQuestId = null;

            if (blob == null) return;
            for (int i = 0; i < blob.Completed.Count; i++)
            {
                string id = blob.Completed[i];
                if (!string.IsNullOrWhiteSpace(id)) _completedIds.Add(id);
            }
            for (int i = 0; i < blob.Active.Count; i++)
            {
                QuestSaveData q = blob.Active[i];
                if (q == null || string.IsNullOrWhiteSpace(q.QuestId)) continue;
                _active.Add(q);
                _countedFishByQuest[q.QuestId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            if (_active.Count > 0) _trackedQuestId = _active[0].QuestId;

            // After load, re-evaluate current objectives against restored inventory state
            // so count-based objectives show correct progress in the HUD.
            EvaluateAll();
        }

        // ---------- Internals ----------

        private QuestSaveData FindActive(string questId)
        {
            for (int i = 0; i < _active.Count; i++)
                if (string.Equals(_active[i].QuestId, questId, StringComparison.OrdinalIgnoreCase))
                    return _active[i];
            return null;
        }

        private static QuestObjective CurrentObjective(QuestSaveData q)
        {
            QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
            if (def == null) return null;
            if (q.CurrentObjectiveIndex < 0 || q.CurrentObjectiveIndex >= def.Objectives.Count) return null;
            return def.Objectives[q.CurrentObjectiveIndex];
        }

        private static void InitPrefixProgress(QuestSaveData q, QuestDefinition def)
        {
            q.PrefixProgress.Clear();
            if (q.CurrentObjectiveIndex < 0 || q.CurrentObjectiveIndex >= def.Objectives.Count) return;
            QuestObjective obj = def.Objectives[q.CurrentObjectiveIndex];
            if (obj.Type != QuestObjectiveType.CatchByPrefix || obj.PrefixRequirements == null) return;
            for (int i = 0; i < obj.PrefixRequirements.Count; i++) q.PrefixProgress.Add(0);
        }

        private void ResolvePlayerRefs()
        {
            if (_inventory != null && _equipment != null) return;
            GameObject root = _playerRoot;
            if (root == null)
            {
                Sol.Player.PlayerSoul soul = FindFirstObjectByType<Sol.Player.PlayerSoul>();
                if (soul != null) root = soul.gameObject;
            }
            if (root == null) return;
            _inventory = root.GetComponentInChildren<Inventory>(true);
            _equipment = root.GetComponentInChildren<Equipment>(true);
        }

        private void SubscribeToPlayer()
        {
            if (_inventory != null) _inventory.OnChanged += HandleInventoryChanged;
            if (_equipment != null) _equipment.OnChanged += HandleEquipmentChanged;
        }

        private void UnsubscribeFromPlayer()
        {
            if (_inventory != null) _inventory.OnChanged -= HandleInventoryChanged;
            if (_equipment != null) _equipment.OnChanged -= HandleEquipmentChanged;
        }
    }
}
