using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Actions;
using Sol.Grab;
using Sol.HUD;
using System.Collections;

namespace Sol.Quests
{
    /// <summary>
    /// Central runtime singleton. Drives quest progress, timers, event wiring, and save/load of quest state.
    /// Attach to a persistent GameObject (e.g. alongside SaveManager).
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

#region Inspector Settings
        [Tooltip("Optional: if unassigned, resolved by finding Inventory + Equipment on the first PlayerSoul.")]
        [SerializeField] private GameObject _playerRoot;
#endregion

        private Inventory _inventory;
        private Equipment _equipment;
        private bool _actionEventsSubscribed;

        private readonly List<QuestSaveData> _active = new();
        private readonly HashSet<string> _completedIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, QuestSaveData> _completedCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, QuestSaveData> _failedCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _countedFishByQuest = new(StringComparer.OrdinalIgnoreCase);

        private string _trackedQuestId;
        private bool _autoOfferRoutineRunning;

        public event Action<QuestSaveData> OnQuestAccepted;
        public event Action<QuestSaveData> OnQuestUpdated;
        public event Action<QuestSaveData> OnObjectiveAdvanced;
        public event Action<QuestSaveData> OnQuestCompleted;
        public event Action<QuestSaveData> OnQuestFailed;

        public IReadOnlyList<QuestSaveData> Active => _active;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            UnsubscribeFromPlayer();
            UnsubscribeFromActionSystem();
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ResolvePlayerRefs();
            SubscribeToPlayer();
            TrySubscribeToActionSystem();
            TryBeginAutoOfferRoutine();
        }

        private void Update()
        {
            TrySubscribeToActionSystem();

            if (_active.Count == 0 || Time.timeScale <= 0f || UIStateOwnership.IsBlockingUiOpen())
                return;

            float dt = Time.unscaledDeltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                QuestSaveData q = _active[i];
                if (q.State != QuestState.Active)
                    continue;

                QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
                if (def == null || !def.IsTimed)
                    continue;

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

        public List<QuestDefinition> GetOfferableQuests(string npcNameOrBoard)
        {
            List<QuestDefinition> result = new();
            QuestRegistry reg = QuestRegistry.Get();
            if (reg == null)
                return result;

            bool fromBoard = string.IsNullOrWhiteSpace(npcNameOrBoard);
            float now = Time.realtimeSinceStartup;

            for (int i = 0; i < reg.Quests.Count; i++)
            {
                QuestDefinition def = reg.Quests[i];
                if (def == null || string.IsNullOrWhiteSpace(def.QuestId))
                    continue;

                if (_completedIds.Contains(def.QuestId))
                    continue;

                if (FindActive(def.QuestId) != null)
                    continue;

                if (!fromBoard
                    && !string.Equals(def.GiverNpcName, npcNameOrBoard, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!ArePrerequisitesMet(def))
                    continue;

                if (_failedCache.TryGetValue(def.QuestId, out QuestSaveData failed)
                    && now - failed.FailedAtRealtime < def.RetryAfterSeconds)
                {
                    continue;
                }

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

            if (_completedIds.Contains(questId) || FindActive(questId) != null)
                return false;

            if (!ArePrerequisitesMet(def))
                return false;

            float now = Time.realtimeSinceStartup;
            if (_failedCache.TryGetValue(questId, out QuestSaveData failed)
                && now - failed.FailedAtRealtime < def.RetryAfterSeconds)
            {
                return false;
            }

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
                && !string.Equals(def.GiverNpcName, npcName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            GrantRewards(def.Reward);

            q.State = QuestState.Completed;
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
                    || string.Equals(def.GiverNpcName, giverNpcName, StringComparison.OrdinalIgnoreCase))
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

                if (!string.Equals(obj.NpcName, npcName, StringComparison.OrdinalIgnoreCase))
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

                if (!string.Equals(obj.NpcName, npcName, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(obj.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                q.CurrentObjectiveProgress = Mathf.Min(q.CurrentObjectiveProgress + 1, Mathf.Max(1, obj.Count));
                OnQuestUpdated?.Invoke(q);
                AdvanceIfComplete(q);
            }
        }

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

            NotifyTalkedToNpc(npcName);
        }

        private void TrySubscribeToActionSystem()
        {
            if (_actionEventsSubscribed || ActionSystem.Instance == null)
                return;

            ActionSystem.Instance.OnActionCompleted += HandleActionCompleted;
            _actionEventsSubscribed = true;
        }

        private void UnsubscribeFromActionSystem()
        {
            if (!_actionEventsSubscribed || ActionSystem.Instance == null)
                return;

            ActionSystem.Instance.OnActionCompleted -= HandleActionCompleted;
            _actionEventsSubscribed = false;
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
                    q.CurrentObjectiveProgress = Mathf.Min(CountItemsInInventory(obj.ItemId), Mathf.Max(1, obj.Count));
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
                case QuestObjectiveType.EquipItem:
                    return q.CurrentObjectiveProgress >= 1;

                default:
                    return q.CurrentObjectiveProgress >= Mathf.Max(1, obj.Count);
            }
        }

        private int CountItemsInInventory(string itemId)
        {
            if (_inventory == null || string.IsNullOrWhiteSpace(itemId))
                return 0;

            int total = 0;
            IReadOnlyList<InventorySlot> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot?.Item == null)
                    continue;

                if (string.Equals(slot.Item.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
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

            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> kvp in _equipment.Equipped)
            {
                ItemComponent equipped = kvp.Value;
                if (equipped == null)
                    continue;

                if (obj.MatchSlot && kvp.Key != obj.Slot)
                    continue;

                if (!string.IsNullOrWhiteSpace(obj.ItemId)
                    && string.Equals(equipped.ItemId, obj.ItemId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(obj.ItemTag)
                    && !string.IsNullOrWhiteSpace(equipped.ItemName)
                    && equipped.ItemName.IndexOf(obj.ItemTag, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (string.IsNullOrWhiteSpace(obj.ItemId) && string.IsNullOrWhiteSpace(obj.ItemTag) && obj.MatchSlot)
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

        private struct FishSnapshot
        {
            public string Prefix;
            public FishRarity Rarity;
            public int Value;
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

        private void TryBeginAutoOfferRoutine()
        {
            if (_autoOfferRoutineRunning)
                return;

            _autoOfferRoutineRunning = true;
            StartCoroutine(AutoOfferWhenUiReady());
        }

        private IEnumerator AutoOfferWhenUiReady()
        {
            // Let scene UI systems finish Awake/Start before we attempt to prompt.
            yield return null;
            yield return null;

            const float maxWaitSeconds = 10f;
            float elapsed = 0f;

            while (elapsed < maxWaitSeconds)
            {
                if (TryAutoOfferNow())
                    break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _autoOfferRoutineRunning = false;
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

                if (_completedIds.Contains(def.QuestId) || FindActive(def.QuestId) != null)
                    continue;

                if (!ArePrerequisitesMet(def))
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

        public List<QuestSaveData> CollectSaveData()
        {
            List<QuestSaveData> result = new();

            for (int i = 0; i < _active.Count; i++)
                result.Add(CloneSaveData(_active[i]));

            foreach (QuestSaveData completed in _completedCache.Values)
                result.Add(CloneSaveData(completed));

            foreach (QuestSaveData failed in _failedCache.Values)
                result.Add(CloneSaveData(failed));

            return result;
        }

        public void ApplySaveData(List<QuestSaveData> saved)
        {
            _active.Clear();
            _completedIds.Clear();
            _completedCache.Clear();
            _failedCache.Clear();
            _countedFishByQuest.Clear();
            _trackedQuestId = null;

            if (saved == null)
                return;

            for (int i = 0; i < saved.Count; i++)
            {
                QuestSaveData q = saved[i];
                if (q == null || string.IsNullOrWhiteSpace(q.QuestId))
                    continue;

                switch (q.State)
                {
                    case QuestState.Completed:
                        _completedIds.Add(q.QuestId);
                        _completedCache[q.QuestId] = CloneSaveData(q);
                        break;
                    case QuestState.Failed:
                        _failedCache[q.QuestId] = CloneSaveData(q);
                        break;
                    case QuestState.Active:
                    case QuestState.ReadyToTurnIn:
                        _active.Add(CloneSaveData(q));
                        break;
                }
            }

            if (_active.Count > 0)
            {
                _trackedQuestId = _active[0].QuestId;
                for (int i = 0; i < _active.Count; i++)
                {
                    QuestSaveData q = _active[i];
                    QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
                    if (def == null)
                        continue;

                    PrepareObjectiveTracking(q, def);
                    if (q.State == QuestState.Active)
                        EvaluateQuest(q);
                }
            }
        }

        private static QuestSaveData CloneSaveData(QuestSaveData src)
        {
            if (src == null)
                return null;

            QuestSaveData clone = new()
            {
                QuestId = src.QuestId,
                State = src.State,
                CurrentObjectiveIndex = src.CurrentObjectiveIndex,
                CurrentObjectiveProgress = src.CurrentObjectiveProgress,
                RemainingSeconds = src.RemainingSeconds,
                FailedAtRealtime = src.FailedAtRealtime,
                GiverNpcName = src.GiverNpcName,
                PrefixProgress = src.PrefixProgress != null ? new List<int>(src.PrefixProgress) : new List<int>()
            };
            return clone;
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

        private static bool IsCatchDrivenObjective(QuestObjective obj)
        {
            return obj != null
                && (obj.Type == QuestObjectiveType.CatchCount
                    || obj.Type == QuestObjectiveType.CatchTotalValue
                    || obj.Type == QuestObjectiveType.CatchRarity
                    || obj.Type == QuestObjectiveType.CatchByPrefix);
        }

        private void ResolvePlayerRefs()
        {
            if (_inventory != null && _equipment != null)
                return;

            GameObject root = _playerRoot;
            if (root == null)
            {
                Sol.Player.PlayerSoul soul = FindFirstObjectByType<Sol.Player.PlayerSoul>();
                if (soul != null)
                    root = soul.gameObject;
            }

            if (root == null)
                return;

            _inventory = root.GetComponentInChildren<Inventory>(true);
            _equipment = root.GetComponentInChildren<Equipment>(true);
        }

        private void SubscribeToPlayer()
        {
            if (_inventory != null)
                _inventory.OnChanged += HandleInventoryChanged;
            if (_equipment != null)
                _equipment.OnChanged += HandleEquipmentChanged;
        }

        private void UnsubscribeFromPlayer()
        {
            if (_inventory != null)
                _inventory.OnChanged -= HandleInventoryChanged;
            if (_equipment != null)
                _equipment.OnChanged -= HandleEquipmentChanged;
        }
    }
}
