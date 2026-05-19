using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Actions;
using Sol.Grab;
using Sol.HUD;
using Sol.Rpg;
using Sol.ToD;
using System.Collections;

namespace Sol.Quests
{
    /// <summary>
    /// Central runtime singleton. Drives quest progress, timers, event wiring, and save/load of quest state.
    /// Attach to a persistent GameObject (e.g. alongside SaveManager).
    /// </summary>
    public partial class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

#region Inspector Settings
        [Tooltip("Optional: if unassigned, resolved by finding Inventory + Equipment on the first PlayerSoul.")]
        [SerializeField] private GameObject _playerRoot;
#endregion

        private Inventory _inventory;
        private Equipment _equipment;
        private Calendar _calendar;
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

        private struct FishSnapshot
        {
            public string Prefix;
            public FishRarity Rarity;
            public int Value;
            public GameplayTagSet Tags;
        }
    }
}
