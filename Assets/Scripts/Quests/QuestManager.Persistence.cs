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
                CompletedAtRealtime = src.CompletedAtRealtime,
                CompletedAtTotalDays = src.CompletedAtTotalDays,
                GiverNpcName = src.GiverNpcName,
                PrefixProgress = src.PrefixProgress != null ? new List<int>(src.PrefixProgress) : new List<int>()
            };
            return clone;
        }
    }
}
