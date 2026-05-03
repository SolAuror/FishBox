using System.Collections.Generic;
using Sol.AI;
using Sol.Quests;
using UnityEngine;

namespace Sol.Editor
{
    public enum NPCAuthoringWarningSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public sealed class NPCAuthoringWarning
    {
        public NPCAuthoringWarningSeverity Severity;
        public string Message;

        public NPCAuthoringWarning(NPCAuthoringWarningSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    public static class NPCAuthoringValidator
    {
        public static List<NPCAuthoringWarning> Validate(NPCSoul soul)
        {
            List<NPCAuthoringWarning> warnings = new();
            if (soul == null)
            {
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Error, "Missing NPCSoul component."));
                return warnings;
            }

            if (string.IsNullOrWhiteSpace(soul.OwnerId))
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Error, "Missing OwnerId (OWN#####)."));
            else if (!EntityCodeUtility.TryParse(soul.OwnerId, EntityCodeUtility.OwnerPrefix, out _))
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Error, $"OwnerId '{soul.OwnerId}' does not match OWN##### format."));

            if (string.IsNullOrWhiteSpace(soul.CharacterName))
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Warning, "Character name is empty — quests, dropdowns, and trade UI will fall back to the GameObject name."));

            GameObject go = soul.gameObject;
            AI_NPC aiNpc = go.GetComponent<AI_NPC>();
            if (aiNpc == null)
            {
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Warning, "Missing AI_NPC component — this NPC will not patrol, chase, or animate."));
            }
            else if (aiNpc.Config == null)
            {
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Warning, "AI_NPC has no AIConfig — patrol, chase, and idle behaviour will use built-in defaults."));
            }

            if (go.GetComponent<Sol.Inventory>() == null)
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Warning, "No Inventory component — trade and corpse loot will not work."));

            switch (soul.Archetype)
            {
                case NPCArchetype.Guard:
                    if (aiNpc != null && aiNpc.Config == null)
                        warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Info, "Guard archetype typically uses an AIConfig with patrol settings."));
                    break;
                case NPCArchetype.QuestGiver:
                    ValidateQuestGiver(aiNpc, warnings);
                    break;
            }

            ValidateDialogue(aiNpc, warnings);

            if (soul.MaxHealth <= 0f)
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Warning, "Max health is 0 — this NPC will be considered dead immediately."));
            if (soul.MaxStamina <= 0f)
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Info, "Max stamina is 0."));

            return warnings;
        }

        private static void ValidateQuestGiver(AI_NPC aiNpc, List<NPCAuthoringWarning> warnings)
        {
            if (aiNpc == null)
                return;

            DialogueGraph graph = aiNpc.DialogueGraph;
            if (graph == null || graph.Nodes == null || graph.Nodes.Count == 0)
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Info, "QuestGiver archetype has no authored dialogue graph yet."));
        }

        private static void ValidateDialogue(AI_NPC aiNpc, List<NPCAuthoringWarning> warnings)
        {
            DialogueGraph graph = aiNpc?.DialogueGraph;
            if (graph?.Nodes == null)
                return;

            QuestRegistry questRegistry = QuestRegistry.Get();
            HashSet<string> nodeIds = new(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                DialogueNode node = graph.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                    continue;
                if (!nodeIds.Add(node.NodeId.Trim()))
                    warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Warning, $"Dialogue node id '{node.NodeId}' is duplicated."));
            }

            for (int n = 0; n < graph.Nodes.Count; n++)
            {
                DialogueNode node = graph.Nodes[n];
                if (node?.Options == null)
                    continue;

                for (int o = 0; o < node.Options.Count; o++)
                {
                    DialogueOption option = node.Options[o];
                    if (option == null)
                        continue;

                    if (option.Action == DialogueOptionAction.OpenTrade && !aiNpc.IsTrader)
                        warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Warning, $"Dialogue option '{DisplayOption(option)}' opens trade, but Trader is unchecked."));

                    if (RequiresQuest(option.Action) && !QuestExists(questRegistry, option.QuestId))
                        warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Warning, $"Dialogue option '{DisplayOption(option)}' references missing quest '{option.QuestId}'."));

                    DialogueOptionVisibility visibility = option.Visibility;
                    if (visibility != null && IsQuestVisibility(visibility.Rule) && !QuestExists(questRegistry, visibility.QuestId))
                        warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Warning, $"Dialogue option '{DisplayOption(option)}' visibility references missing quest '{visibility.QuestId}'."));
                }
            }
        }

        private static bool RequiresQuest(DialogueOptionAction action)
        {
            return action == DialogueOptionAction.OfferQuest
                || action == DialogueOptionAction.TurnInQuest;
        }

        private static bool IsQuestVisibility(DialogueVisibilityRule rule)
        {
            return rule == DialogueVisibilityRule.QuestActive
                || rule == DialogueVisibilityRule.QuestNotStarted
                || rule == DialogueVisibilityRule.QuestReadyToTurnIn
                || rule == DialogueVisibilityRule.QuestCompleted
                || rule == DialogueVisibilityRule.QuestOnObjective;
        }

        private static bool QuestExists(QuestRegistry registry, string questId)
        {
            return !string.IsNullOrWhiteSpace(questId)
                && registry != null
                && registry.Find(questId) != null;
        }

        private static string DisplayOption(DialogueOption option)
        {
            return string.IsNullOrWhiteSpace(option.Label) ? option.Action.ToString() : option.Label.Trim();
        }

        public static List<NPCAuthoringWarning> ValidateRegistry(NPCRegistry registry)
        {
            List<NPCAuthoringWarning> warnings = new();
            if (registry == null)
            {
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Error, "NPCRegistry asset is missing."));
                return warnings;
            }

            HashSet<string> seen = new(System.StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<NPCRegistry.Entry> entries = registry.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                NPCRegistry.Entry entry = entries[i];
                if (entry == null)
                {
                    warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Error, $"Registry entry {i} is null."));
                    continue;
                }

                if (entry.Prefab == null)
                {
                    warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Error, $"Registry entry {i} points to a null prefab."));
                    continue;
                }

                string id = string.IsNullOrWhiteSpace(entry.OwnerId) ? entry.Prefab.OwnerId : entry.OwnerId.Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Error, $"{entry.Prefab.name} has no OwnerId."));
                    continue;
                }

                if (!seen.Add(id))
                    warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Error, $"Duplicate OwnerId {id}."));
            }

            return warnings;
        }

        public static bool HasWarnings(NPCSoul soul)
        {
            List<NPCAuthoringWarning> warnings = Validate(soul);
            return warnings.Count > 0;
        }
    }
}
