using System.Collections.Generic;
using Sol.AI;
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

            NpcTrader trader = go.GetComponent<NpcTrader>();
            switch (soul.AuthoringTemplate)
            {
                case NPCAuthoringTemplate.Trader:
                case NPCAuthoringTemplate.QuestGiver:
                    if (trader == null)
                        warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Warning, $"Template '{soul.AuthoringTemplate}' expects an NpcTrader component."));
                    break;
                case NPCAuthoringTemplate.Patroller:
                    if (aiNpc != null && aiNpc.Config == null)
                        warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Info, "Patroller template typically uses an AIConfig with patrol settings."));
                    break;
            }

            if (soul.MaxHealth <= 0f)
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Warning, "Max health is 0 — this NPC will be considered dead immediately."));
            if (soul.MaxStamina <= 0f)
                warnings.Add(new NPCAuthoringWarning(NPCAuthoringWarningSeverity.Info, "Max stamina is 0."));

            return warnings;
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
