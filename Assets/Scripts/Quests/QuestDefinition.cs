using System.Collections.Generic;
using UnityEngine;
using Sol;

namespace Sol.Quests
{
    public enum QuestRepeatMode
    {
        Immediately = 0,
        AfterRealtimeSeconds = 1,
        AfterInGameDays = 2,
    }

    /// <summary>
    /// Authored quest definition. Runtime state lives in <see cref="QuestSaveData"/> owned by the QuestManager.
    /// </summary>
    [CreateAssetMenu(fileName = "QST00000_Quest", menuName = "Sol/Quests/Quest Definition")]
    public class QuestDefinition : ScriptableObject
    {
        #region Inspector Settings
        [Tooltip("Stable identifier, format QST##### (e.g. QST00001).")]
        [SerializeField] private string _questId = string.Empty;
        [SerializeField] private string _title = string.Empty;
        [TextArea(2, 6)]
        [SerializeField] private string _summary = string.Empty;

        [Tooltip("NPC owner id (OWN#####) that offers and accepts turn-in. Empty = quest board only.")]
        [NpcIdDropdown]
        [SerializeField] private string _giverNpcName = string.Empty;

        [Tooltip("Objectives are completed in order.")]
        [SerializeField] private List<QuestObjective> _objectives = new();

        [SerializeField] private QuestReward _reward = new();

        [Tooltip("Quest IDs that must be Completed before this quest is offered.")]
        [QuestIdDropdown]
        [SerializeField] private List<string> _prerequisiteQuestIds = new();

        [Tooltip("0 = untimed. Otherwise real-time seconds before quest fails.")]
        [Min(0f)]
        [SerializeField] private float _timeLimitSeconds = 0f;

        [Tooltip("After a failure, cooldown (real-time seconds) before the quest can be re-offered.")]
        [Min(0f)]
        [SerializeField] private float _retryAfterSeconds = 0f;

        [Tooltip("If true, offered automatically on first game start when prerequisites pass (e.g. tutorial).")]
        [SerializeField] private bool _autoOffer = false;

        [Tooltip("If enabled, this quest can be offered again after it has been completed.")]
        [SerializeField] private bool _repeatable = false;

        [Tooltip("How this repeatable quest becomes available again after completion.")]
        [SerializeField] private QuestRepeatMode _repeatMode = QuestRepeatMode.Immediately;

        [Tooltip("Used when Repeat Mode is AfterRealtimeSeconds.")]
        [Min(0f)]
        [SerializeField] private float _repeatAfterRealtimeSeconds = 0f;

        [Tooltip("Used when Repeat Mode is AfterInGameDays.")]
        [Min(0)]
        [SerializeField] private int _repeatAfterInGameDays = 0;
        #endregion

        public string QuestId => _questId;
        public string Title => _title;
        public string Summary => _summary;
        public string GiverNpcName => _giverNpcName;
        public IReadOnlyList<QuestObjective> Objectives => _objectives;
        public QuestReward Reward => _reward;
        public IReadOnlyList<string> PrerequisiteQuestIds => _prerequisiteQuestIds;
        public float TimeLimitSeconds => _timeLimitSeconds;
        public float RetryAfterSeconds => _retryAfterSeconds;
        public bool AutoOffer => _autoOffer;
        public bool Repeatable => _repeatable;
        public QuestRepeatMode RepeatMode => _repeatMode;
        public float RepeatAfterRealtimeSeconds => _repeatAfterRealtimeSeconds;
        public int RepeatAfterInGameDays => _repeatAfterInGameDays;
        public bool IsTimed => _timeLimitSeconds > 0f;
    }
}
