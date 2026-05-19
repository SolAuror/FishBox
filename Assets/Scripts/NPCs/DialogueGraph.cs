using System;
using System.Collections.Generic;
using Sol.Rpg;
using Sol.Quests;
using UnityEngine;

namespace Sol.AI
{
    /// <summary>
    /// Inline branching-dialogue graph authored on AI_NPC. Empty graph ==> NPC uses the
    /// legacy hardcoded conversation flow (greeting + Trade/Quest/Goodbye).
    /// </summary>
    [Serializable]
    public class DialogueGraph
    {
        [Tooltip("NodeId of the entry node. Defaults to 'root'.")]
        public string RootNodeId = "root";

        public List<DialogueNode> Nodes = new();
    }

    [Serializable]
    public class DialogueNode
    {
        [Tooltip("Unique within the graph. Used to target this node from option NextNodeId.")]
        public string NodeId = "";

        [Tooltip("Line spoken by the NPC when this node is shown.")]
        [TextArea(2, 5)]
        public string SpeakerLine = "";

        public DialogueOptionVisibility Visibility = new();

        public List<DialogueOption> Options = new();
    }

    [Serializable]
    public class DialogueOption
    {
        [Tooltip("Player-facing button label.")]
        public string Label = "";

        public DialogueOptionAction Action = DialogueOptionAction.GoToNode;

        [Tooltip("Target node id (for GoToNode actions).")]
        public string NextNodeId = "";

        [Tooltip("Target quest id (for OfferQuest / TurnInQuest actions).")]
        [QuestIdDropdown]
        public string QuestId = "";

        [Tooltip("Topic id passed to QuestManager.NotifyTalkedAboutTopic. Free-text, matches against TalkToNpc objective Topic.")]
        public string Topic = "";

        [Tooltip("NPC-local flag used by local flag visibility rules and optional flag mutation.")]
        public string LocalFlag = "";

        [Tooltip("Optional NPC-local flag change applied when this option is selected.")]
        public DialogueFlagMutation FlagMutation = DialogueFlagMutation.None;

        public DialogueOptionVisibility Visibility = new();
    }

    public enum DialogueOptionAction
    {
        GoToNode = 0,
        EndConversation = 1,
        OpenTrade = 2,
        OfferQuest = 3,
        TurnInQuest = 4,
        DeliverItemForQuest = 5,
        NotifyTalkedAboutTopic = 6,
    }

    public enum DialogueFlagMutation
    {
        None = 0,
        Set = 1,
        Clear = 2,
        Toggle = 3,
    }

    [Serializable]
    public class DialogueOptionVisibility
    {
        public DialogueVisibilityRule Rule = DialogueVisibilityRule.Always;

        [QuestIdDropdown]
        public string QuestId = "";

        [Tooltip("For QuestOnObjective: the required CurrentObjectiveIndex (-1 = any objective).")]
        public int RequiredObjectiveIndex = -1;

        [Tooltip("For ScheduleActivity: the required current NPC schedule activity.")]
        public NpcScheduleActivity ScheduleActivity = NpcScheduleActivity.Work;

        [Tooltip("For ScheduleLocation: the required current NPC schedule location id.")]
        public string ScheduleLocationId = "";

        [Tooltip("For TimeWindow: inclusive start hour in sky-aligned civil time.")]
        [Range(0f, 24f)]
        public float StartHour = 0f;

        [Tooltip("For TimeWindow: exclusive end hour in sky-aligned civil time. Can wrap past midnight.")]
        [Range(0f, 24f)]
        public float EndHour = 24f;

        public GameplayTagSet RequiredSpeakerTags = new();
        public GameplayTagSet ForbiddenSpeakerTags = new();
        public GameplayTagSet RequiredPlayerTags = new();
        public GameplayTagSet ForbiddenPlayerTags = new();
    }

    public enum DialogueVisibilityRule
    {
        Always = 0,
        QuestActive = 1,
        QuestNotStarted = 2,
        QuestReadyToTurnIn = 3,
        QuestCompleted = 4,
        QuestOnObjective = 5,
        LocalFlagSet = 6,
        LocalFlagNotSet = 7,
        ScheduleActivity = 8,
        ScheduleLocation = 9,
        TimeWindow = 10,
    }
}
