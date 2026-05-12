#if UNITY_EDITOR
using System.Collections.Generic;
using Sol.HUD;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal enum InteractionAuthoringWarningSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed class InteractionAuthoringWarning
    {
        public InteractionAuthoringWarningSeverity Severity;
        public string Message;

        public InteractionAuthoringWarning(InteractionAuthoringWarningSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    internal static class InteractionAuthoringValidator
    {
        public static List<InteractionAuthoringWarning> ValidateDefinition(InteractionDefinition definition)
        {
            List<InteractionAuthoringWarning> warnings = new();
            if (definition == null)
            {
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Error, "Missing InteractionDefinition asset."));
                return warnings;
            }

            if (string.IsNullOrWhiteSpace(definition.DefinitionId))
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Error, "Definition id is empty."));

            if (definition.AnimationType == InteractionPointAnimationType.CustomClip && definition.CustomClip == null)
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Error, "Custom Clip animation type needs a custom clip."));

            if (definition.AnimationType == InteractionPointAnimationType.RestSleep
                && definition.CompletionMode != InteractionCompletionMode.WaitForReadyThenExternalCompletion)
            {
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Warning, "Sleep interactions should wait for ready and external completion so the sleep menu opens after the actor is lying down."));
            }

            if (definition.CompletionMode == InteractionCompletionMode.WaitForReadyThenExternalCompletion
                && definition.AnimationType == InteractionPointAnimationType.None)
            {
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Warning, "External completion waits for a ready gate, but this definition has no typed animation."));
            }

            return warnings;
        }

        public static List<InteractionAuthoringWarning> ValidatePoint(InteractionPoint point)
        {
            List<InteractionAuthoringWarning> warnings = new();
            if (point == null)
            {
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Error, "Missing InteractionPoint."));
                return warnings;
            }

            if (point.AlignPoint == null || point.AlignPoint == point.transform)
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Warning, "No dedicated align point is assigned; actors will use the interaction object's transform."));

            if (point.Definition == null)
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Info, "No InteractionDefinition assigned; this point uses local legacy settings."));

            if (point.AnimationType == InteractionPointAnimationType.CustomClip && point.Definition == null)
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Warning, "Custom clip point has no definition. Confirm its local custom clip is assigned."));

            string ownerId = point.OwnerId;
            if (!string.IsNullOrWhiteSpace(ownerId)
                && !string.Equals(ownerId, EntityCodeUtility.DefaultPlayerOwnerId, System.StringComparison.OrdinalIgnoreCase)
                && !EntityCodeUtility.TryParse(ownerId, EntityCodeUtility.OwnerPrefix, out _))
            {
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Error, $"Owner id '{ownerId}' is not {EntityCodeUtility.DefaultPlayerOwnerId} or OWN#####."));
            }

            return warnings;
        }

        public static List<InteractionAuthoringWarning> ValidateBed(SleepInteractable bed)
        {
            List<InteractionAuthoringWarning> warnings = new();
            if (bed == null)
            {
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Error, "Missing SleepInteractable."));
                return warnings;
            }

            InteractionPoint point = bed.InteractionPoint;
            if (point == null)
            {
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Error, "Bed has no InteractionPoint; sleep cannot align or animate before opening the menu."));
                return warnings;
            }

            warnings.AddRange(ValidatePoint(point));
            if (point.AnimationType != InteractionPointAnimationType.RestSleep)
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Warning, "Bed InteractionPoint should use Rest.Sleep animation."));

            if (SleepMenuSystem.Instance == null)
                warnings.Add(new InteractionAuthoringWarning(InteractionAuthoringWarningSeverity.Info, "SleepMenuSystem is not registered in edit mode; verify the scene has one at runtime."));

            return warnings;
        }

        public static MessageType ToMessageType(InteractionAuthoringWarningSeverity severity)
        {
            return severity switch
            {
                InteractionAuthoringWarningSeverity.Error => MessageType.Error,
                InteractionAuthoringWarningSeverity.Warning => MessageType.Warning,
                _ => MessageType.Info
            };
        }

        public static SolDatabaseIssueSeverity ToDatabaseSeverity(InteractionAuthoringWarningSeverity severity)
        {
            return severity switch
            {
                InteractionAuthoringWarningSeverity.Error => SolDatabaseIssueSeverity.Error,
                InteractionAuthoringWarningSeverity.Warning => SolDatabaseIssueSeverity.Warning,
                _ => SolDatabaseIssueSeverity.Info
            };
        }
    }
}
#endif
