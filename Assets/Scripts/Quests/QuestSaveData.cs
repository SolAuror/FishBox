using System;
using System.Collections.Generic;

namespace Sol.Quests
{
    public enum QuestState
    {
        Active = 0,
        ReadyToTurnIn = 1,
        Completed = 2,
        Failed = 3,
    }

    /// <summary>
    /// Persisted per-quest runtime state. Lives inside GameSaveData.Quests.
    /// Sequential: only <see cref="CurrentObjectiveIndex"/> is evaluated against events,
    /// with its progress tracked in <see cref="CurrentObjectiveProgress"/>
    /// (or <see cref="PrefixProgress"/> for CatchByPrefix objectives).
    /// </summary>
    [Serializable]
    public class QuestSaveData
    {
        public string QuestId = string.Empty;
        public QuestState State = QuestState.Active;
        public int CurrentObjectiveIndex = 0;
        public int CurrentObjectiveProgress = 0;

        /// <summary>Per-prefix running counts for CatchByPrefix objectives (index-aligned with objective.PrefixRequirements).</summary>
        public List<int> PrefixProgress = new();

        /// <summary>Remaining real-time seconds for timed quests. 0 when untimed or expired.</summary>
        public float RemainingSeconds = 0f;

        /// <summary>Realtime-since-startup when the quest last failed (for retry cooldown).</summary>
        public float FailedAtRealtime = 0f;

        public string GiverNpcName = string.Empty;
    }
}
