using System.Collections.Generic;
using NUnit.Framework;
using Sol.AI;
using Sol.Editor;
using Sol.Grab;
using Sol.Quests;
using Sol.Rpg;
using UnityEditor;
using UnityEngine;

public sealed class RpgDatabaseFoundationTests
{
    private sealed class ReferenceDropdownTarget : ScriptableObject
    {
        [SerializeField] private string _referenceId;

        public string ReferenceId => _referenceId;
    }

    [Test]
    public void ReferenceDropdownSelection_WritesToSerializedTarget()
    {
        ReferenceDropdownTarget target = ScriptableObject.CreateInstance<ReferenceDropdownTarget>();
        try
        {
            SerializedObject serializedObject = new(target);
            serializedObject.Update();

            bool changed = ReferenceDropdown.ApplyReferenceSelection(
                ReferenceDropdown.ReferenceKind.Item,
                serializedObject,
                target,
                "_referenceId",
                "ITM12345");

            Assert.That(changed, Is.True);
            Assert.That(target.ReferenceId, Is.EqualTo("ITM12345"));
            Assert.That(serializedObject.FindProperty("_referenceId").stringValue, Is.EqualTo("ITM12345"));
        }
        finally
        {
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void LegacyDatabaseSelectors_RouteToUnifiedHub()
    {
        ItemDatabaseWindow.SelectByItemId("ITM12345");
        Assert.That(SolDatabaseWindow.LastOpenedTab, Is.EqualTo(SolDatabaseTab.Items));
        Assert.That(SolDatabaseWindow.LastRequestedSelectionId, Is.EqualTo("ITM12345"));

        NPCDatabaseWindow.SelectByOwnerId("OWN12345");
        Assert.That(SolDatabaseWindow.LastOpenedTab, Is.EqualTo(SolDatabaseTab.NPCs));
        Assert.That(SolDatabaseWindow.LastRequestedSelectionId, Is.EqualTo("OWN12345"));

        QuestDatabaseWindow.SelectByQuestId("QST12345");
        Assert.That(SolDatabaseWindow.LastOpenedTab, Is.EqualTo(SolDatabaseTab.Quests));
        Assert.That(SolDatabaseWindow.LastRequestedSelectionId, Is.EqualTo("QST12345"));

        EditorWindow.GetWindow<SolDatabaseWindow>()?.Close();
    }

    [Test]
    public void UnifiedItemRows_FilterBySearchTypeAndWarningFlags()
    {
        GameObject itemObject = new("Iron Sword");
        try
        {
            itemObject.AddComponent<BoxCollider>();
            itemObject.AddComponent<GrabbableComponent>();
            ItemComponent item = itemObject.AddComponent<ItemComponent>();
            var row = new SolDatabaseItemPage.ItemRow
            {
                Item = item,
                ItemId = "ITM00010",
                ItemName = "Iron Sword",
                ItemType = ItemType.Weapon,
                SearchText = "iron sword itm00010 weapon",
                IsEquipable = true,
                IsConsumable = false,
                IsStackable = false,
                MissingIcon = true,
                WarningCount = 1
            };

            Assert.That(SolDatabaseItemPage.RowMatchesFilters(row, "iron", SolDatabaseItemPage.ItemFilter.All, false, ItemType.Material), Is.True);
            Assert.That(SolDatabaseItemPage.RowMatchesFilters(row, "itm00010", SolDatabaseItemPage.ItemFilter.Equipable, true, ItemType.Weapon), Is.True);
            Assert.That(SolDatabaseItemPage.RowMatchesFilters(row, "iron", SolDatabaseItemPage.ItemFilter.Consumable, false, ItemType.Material), Is.False);
            Assert.That(SolDatabaseItemPage.RowMatchesFilters(row, "missing", SolDatabaseItemPage.ItemFilter.All, false, ItemType.Material), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(itemObject);
        }
    }

    [Test]
    public void UnifiedNPCRows_FilterBySearchArchetypeAndFlags()
    {
        GameObject npcObject = new("Gate Guard");
        try
        {
            NPCSoul soul = npcObject.AddComponent<NPCSoul>();
            var row = new SolDatabaseNPCPage.NPCRow
            {
                Soul = soul,
                OwnerId = "OWN00010",
                CharacterName = "Gate Guard",
                Archetype = NPCArchetype.Guard,
                SearchText = "gate guard own00010 guard",
                HasTrader = false,
                IsHostile = false,
                MissingAIConfig = true,
                WarningCount = 2
            };

            Assert.That(SolDatabaseNPCPage.RowMatchesFilters(row, "gate", SolDatabaseNPCPage.NPCFilter.All), Is.True);
            Assert.That(SolDatabaseNPCPage.RowMatchesFilters(row, "own00010", SolDatabaseNPCPage.NPCFilter.Guard), Is.True);
            Assert.That(SolDatabaseNPCPage.RowMatchesFilters(row, "gate", SolDatabaseNPCPage.NPCFilter.MissingAIConfig), Is.True);
            Assert.That(SolDatabaseNPCPage.RowMatchesFilters(row, "gate", SolDatabaseNPCPage.NPCFilter.Trader), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(npcObject);
        }
    }

    [Test]
    public void UnifiedQuestRows_FilterBySearchFlowAndWarningFlags()
    {
        QuestDefinition quest = ScriptableObject.CreateInstance<QuestDefinition>();
        try
        {
            var row = new SolDatabaseQuestPage.QuestRow
            {
                Quest = quest,
                QuestId = "QST00010",
                Title = "The Locked Gate",
                GiverName = "Gate Guard",
                SearchText = "the locked gate qst00010 gate guard",
                AutoOffer = false,
                Repeatable = true,
                IsTimed = false,
                MissingGiver = true,
                HasOrphanedPrereqs = true,
                WarningCount = 3
            };

            Assert.That(SolDatabaseQuestPage.RowMatchesFilters(row, "locked", SolDatabaseQuestPage.QuestFilter.All), Is.True);
            Assert.That(SolDatabaseQuestPage.RowMatchesFilters(row, "qst00010", SolDatabaseQuestPage.QuestFilter.Repeatable), Is.True);
            Assert.That(SolDatabaseQuestPage.RowMatchesFilters(row, "gate", SolDatabaseQuestPage.QuestFilter.MissingGiver), Is.True);
            Assert.That(SolDatabaseQuestPage.RowMatchesFilters(row, "locked", SolDatabaseQuestPage.QuestFilter.Timed), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(quest);
        }
    }

    [Test]
    public void OverviewAggregation_CountsIssueSeverities()
    {
        var issues = new List<SolDatabaseIssue>
        {
            new(SolDatabaseIssueSeverity.Error, SolDatabaseTab.Items, "ITM1", "Bad Item", "Error"),
            new(SolDatabaseIssueSeverity.Warning, SolDatabaseTab.NPCs, "OWN1", "Warn NPC", "Warning"),
            new(SolDatabaseIssueSeverity.Warning, SolDatabaseTab.Quests, "QST1", "Warn Quest", "Warning"),
            new(SolDatabaseIssueSeverity.Info, SolDatabaseTab.Quests, "QST2", "Info Quest", "Info")
        };

        SolDatabaseOverviewPage.CountIssues(issues, out int errors, out int warnings, out int infos);

        Assert.That(errors, Is.EqualTo(1));
        Assert.That(warnings, Is.EqualTo(2));
        Assert.That(infos, Is.EqualTo(1));
    }

    [Test]
    public void Phase2DefinitionIds_UseCategoryPrefixes()
    {
        RpgSkillDefinition skillOne = ScriptableObject.CreateInstance<RpgSkillDefinition>();
        RpgSkillDefinition skillTwo = ScriptableObject.CreateInstance<RpgSkillDefinition>();
        try
        {
            SetDefinitionIdentity(skillOne, "SKL00001", "One");
            SetDefinitionIdentity(skillTwo, "SKL00003", "Three");

            var definitions = new List<RpgDefinition> { skillOne, skillTwo };

            Assert.That(RpgDefinitionEditorUtility.NextIdFromDefinitions(RpgDefinitionIds.SkillPrefix, definitions), Is.EqualTo("SKL00002"));
            Assert.That(RpgDefinitionEditorUtility.NextIdFromDefinitions(RpgDefinitionIds.FactionPrefix, definitions), Is.EqualTo("FAC00001"));
        }
        finally
        {
            Object.DestroyImmediate(skillOne);
            Object.DestroyImmediate(skillTwo);
        }
    }

    [Test]
    public void Phase2SkillValidation_CatchesMissingGoverningStat()
    {
        RpgSkillDefinition skill = ScriptableObject.CreateInstance<RpgSkillDefinition>();
        try
        {
            SetDefinitionIdentity(skill, "SKL00001", "Lockpicking");
            SerializedObject so = new(skill);
            so.Update();
            so.FindProperty("_governingStatId").stringValue = "STA99999";
            so.ApplyModifiedPropertiesWithoutUndo();

            List<RpgAuthoringWarning> warnings = RpgDefinitionValidator.Validate(skill, registry: null);

            Assert.That(warnings.Exists(w => w.Message.Contains("Governing stat")), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    [Test]
    public void Phase2DefinitionRows_FilterBySearchAndWarnings()
    {
        RpgStatDefinition stat = ScriptableObject.CreateInstance<RpgStatDefinition>();
        try
        {
            SetDefinitionIdentity(stat, "STA00007", "Endurance");
            var row = new SolDatabaseRpgDefinitionPage<RpgStatDefinition>.DefinitionRow
            {
                Definition = stat,
                Id = "STA00007",
                DisplayName = "Endurance",
                Subtitle = "Primary",
                SearchText = "endurance sta00007 primary",
                WarningCount = 1
            };

            Assert.That(SolDatabaseRpgDefinitionPage<RpgStatDefinition>.RowMatchesFilters(row, "endur", SolDatabaseRpgDefinitionPage<RpgStatDefinition>.DefinitionFilter.All), Is.True);
            Assert.That(SolDatabaseRpgDefinitionPage<RpgStatDefinition>.RowMatchesFilters(row, "sta00007", SolDatabaseRpgDefinitionPage<RpgStatDefinition>.DefinitionFilter.HasWarnings), Is.True);
            Assert.That(SolDatabaseRpgDefinitionPage<RpgStatDefinition>.RowMatchesFilters(row, "missing", SolDatabaseRpgDefinitionPage<RpgStatDefinition>.DefinitionFilter.All), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(stat);
        }
    }

    private static void SetDefinitionIdentity(RpgDefinition definition, string id, string displayName)
    {
        SerializedObject so = new(definition);
        so.Update();
        so.FindProperty("_id").stringValue = id;
        so.FindProperty("_displayName").stringValue = displayName;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
