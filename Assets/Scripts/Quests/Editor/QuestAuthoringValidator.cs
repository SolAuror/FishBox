using System.Collections.Generic;
using Sol;
using Sol.AI;
using Sol.Grab;
using Sol.Quests;
using UnityEngine;

namespace Sol.Editor
{
    public enum QuestAuthoringWarningSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public sealed class QuestAuthoringWarning
    {
        public QuestAuthoringWarningSeverity Severity;
        public string Message;

        public QuestAuthoringWarning(QuestAuthoringWarningSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    public static class QuestAuthoringValidator
    {
        public static List<QuestAuthoringWarning> Validate(QuestDefinition quest)
        {
            List<QuestAuthoringWarning> warnings = new();
            if (quest == null)
            {
                warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Error, "Missing QuestDefinition asset."));
                return warnings;
            }

            if (string.IsNullOrWhiteSpace(quest.QuestId))
                warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Error, "Missing QuestId (QST#####)."));
            else if (!EntityCodeUtility.TryParse(quest.QuestId, EntityCodeUtility.QuestPrefix, out _))
                warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Error, $"QuestId '{quest.QuestId}' does not match QST##### format."));

            if (string.IsNullOrWhiteSpace(quest.Title))
                warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Warning, "Quest title is empty."));

            ValidateGiver(quest, warnings);
            ValidatePrerequisites(quest, warnings);
            ValidateObjectives(quest, warnings);
            ValidateReward(quest, warnings);
            ValidateRepeatTiming(quest, warnings);

            return warnings;
        }

        public static List<QuestAuthoringWarning> ValidateRegistry(QuestRegistry registry)
        {
            List<QuestAuthoringWarning> warnings = new();
            if (registry == null)
            {
                warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Error, "QuestRegistry asset is missing."));
                return warnings;
            }

            HashSet<string> seenIds = new(System.StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<QuestDefinition> entries = registry.Quests;
            for (int i = 0; i < entries.Count; i++)
            {
                QuestDefinition entry = entries[i];
                if (entry == null)
                {
                    warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Error, $"Registry entry {i} is null."));
                    continue;
                }

                string id = entry.QuestId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Error, $"{entry.name} has no QuestId."));
                    continue;
                }

                if (!seenIds.Add(id.Trim()))
                    warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Error, $"Duplicate QuestId {id}."));
            }

            return warnings;
        }

        public static bool HasWarnings(QuestDefinition quest)
        {
            return Validate(quest).Count > 0;
        }

        public static bool HasPrerequisiteCycle(QuestDefinition quest)
        {
            if (quest == null)
                return false;

            QuestRegistry registry = QuestRegistry.Get();
            if (registry == null)
                return false;

            HashSet<string> visiting = new(System.StringComparer.OrdinalIgnoreCase);
            HashSet<string> visited = new(System.StringComparer.OrdinalIgnoreCase);
            return DetectCycleDfs(quest, registry, visiting, visited);
        }

        private static bool DetectCycleDfs(QuestDefinition quest, QuestRegistry registry, HashSet<string> visiting, HashSet<string> visited)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.QuestId))
                return false;

            string id = quest.QuestId.Trim();
            if (visited.Contains(id))
                return false;
            if (!visiting.Add(id))
                return true;

            IReadOnlyList<string> prereqs = quest.PrerequisiteQuestIds;
            if (prereqs != null)
            {
                for (int i = 0; i < prereqs.Count; i++)
                {
                    QuestDefinition next = registry.Find(prereqs[i]);
                    if (next == null)
                        continue;
                    if (DetectCycleDfs(next, registry, visiting, visited))
                        return true;
                }
            }

            visiting.Remove(id);
            visited.Add(id);
            return false;
        }

        private static void ValidateGiver(QuestDefinition quest, List<QuestAuthoringWarning> warnings)
        {
            string giver = quest.GiverNpcName;
            if (string.IsNullOrWhiteSpace(giver))
            {
                warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Info, "No quest giver — quest is board-only."));
                return;
            }

            NPCRegistry npcRegistry = NPCRegistry.Get();
            if (npcRegistry == null)
                return;

            if (npcRegistry.GetPrefab(giver) == null)
                warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Error, $"Giver OwnerId '{giver}' is not in the NPC registry."));
        }

        private static void ValidatePrerequisites(QuestDefinition quest, List<QuestAuthoringWarning> warnings)
        {
            IReadOnlyList<string> prereqs = quest.PrerequisiteQuestIds;
            if (prereqs == null || prereqs.Count == 0)
                return;

            QuestRegistry registry = QuestRegistry.Get();
            for (int i = 0; i < prereqs.Count; i++)
            {
                string prereqId = prereqs[i];
                if (string.IsNullOrWhiteSpace(prereqId))
                {
                    warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Warning, $"Prerequisite slot {i + 1} is empty."));
                    continue;
                }

                if (registry != null && registry.Find(prereqId) == null)
                    warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Error, $"Prerequisite '{prereqId}' is not in the quest registry."));
            }

            if (quest.AutoOffer)
                warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Warning, "AutoOffer is enabled but the quest has prerequisites — it will only auto-offer once those clear."));

            if (HasPrerequisiteCycle(quest))
                warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Error, "Prerequisite cycle detected involving this quest."));
        }

        private static void ValidateObjectives(QuestDefinition quest, List<QuestAuthoringWarning> warnings)
        {
            IReadOnlyList<QuestObjective> objectives = quest.Objectives;
            if (objectives == null || objectives.Count == 0)
            {
                warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Error, "Quest has no objectives."));
                return;
            }

            ItemRegistry itemRegistry = ItemRegistry.Get();
            NPCRegistry npcRegistry = NPCRegistry.Get();

            for (int i = 0; i < objectives.Count; i++)
            {
                QuestObjective objective = objectives[i];
                if (objective == null)
                {
                    warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Warning, $"Objective {i + 1} is empty."));
                    continue;
                }

                ValidateObjectiveItems(i, objective, itemRegistry, warnings);
                ValidateObjectiveNpc(i, objective, npcRegistry, warnings);
            }
        }

        private static void ValidateObjectiveItems(int index, QuestObjective objective, ItemRegistry itemRegistry, List<QuestAuthoringWarning> warnings)
        {
            switch (objective.Type)
            {
                case QuestObjectiveType.CollectItem:
                case QuestObjectiveType.DeliverItem:
                case QuestObjectiveType.EquipItem:
                    if (!objective.HasAnyAcceptableItemId() && string.IsNullOrWhiteSpace(objective.ItemTag))
                    {
                        warnings.Add(new QuestAuthoringWarning(
                            QuestAuthoringWarningSeverity.Error,
                            $"Objective {index + 1} ({objective.Type}) has no acceptable item ids."));
                        return;
                    }
                    break;
                default:
                    return;
            }

            if (itemRegistry == null)
                return;

            foreach (string acceptable in objective.EnumerateAcceptableItemIds())
            {
                if (itemRegistry.GetPrefab(acceptable) == null)
                    warnings.Add(new QuestAuthoringWarning(
                        QuestAuthoringWarningSeverity.Warning,
                        $"Objective {index + 1}: ItemId '{acceptable}' is not in the item registry."));
            }
        }

        private static void ValidateObjectiveNpc(int index, QuestObjective objective, NPCRegistry npcRegistry, List<QuestAuthoringWarning> warnings)
        {
            if (objective.Type != QuestObjectiveType.DeliverItem && objective.Type != QuestObjectiveType.TalkToNpc)
                return;

            if (string.IsNullOrWhiteSpace(objective.NpcName))
            {
                warnings.Add(new QuestAuthoringWarning(
                    QuestAuthoringWarningSeverity.Error,
                    $"Objective {index + 1} ({objective.Type}) needs an NPC OwnerId."));
                return;
            }

            if (npcRegistry == null)
                return;

            if (npcRegistry.GetPrefab(objective.NpcName) == null)
                warnings.Add(new QuestAuthoringWarning(
                    QuestAuthoringWarningSeverity.Warning,
                    $"Objective {index + 1}: NPC '{objective.NpcName}' is not in the NPC registry."));
        }

        private static void ValidateReward(QuestDefinition quest, List<QuestAuthoringWarning> warnings)
        {
            QuestReward reward = quest.Reward;
            if (reward?.Items == null)
                return;

            ItemRegistry itemRegistry = ItemRegistry.Get();
            for (int i = 0; i < reward.Items.Count; i++)
            {
                ItemReward itemReward = reward.Items[i];
                if (itemReward == null || string.IsNullOrWhiteSpace(itemReward.ItemId))
                {
                    warnings.Add(new QuestAuthoringWarning(QuestAuthoringWarningSeverity.Warning, $"Reward item {i + 1} has no ItemId."));
                    continue;
                }

                if (itemRegistry != null && itemRegistry.GetPrefab(itemReward.ItemId) == null)
                    warnings.Add(new QuestAuthoringWarning(
                        QuestAuthoringWarningSeverity.Warning,
                        $"Reward item '{itemReward.ItemId}' is not in the item registry."));
            }
        }

        private static void ValidateRepeatTiming(QuestDefinition quest, List<QuestAuthoringWarning> warnings)
        {
            if (quest.Repeatable)
                return;

            bool hasRepeatTiming = quest.RepeatAfterRealtimeSeconds > 0f
                || quest.RepeatAfterInGameDays > 0
                || quest.RepeatMode != QuestRepeatMode.Immediately;

            if (hasRepeatTiming)
                warnings.Add(new QuestAuthoringWarning(
                    QuestAuthoringWarningSeverity.Info,
                    "Repeat mode/timing values are ignored because Repeatable is disabled."));
        }
    }
}
