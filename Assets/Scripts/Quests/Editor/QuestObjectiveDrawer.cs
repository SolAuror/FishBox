using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sol.Quests;

[CustomPropertyDrawer(typeof(QuestObjective))]
public class QuestObjectiveDrawer : PropertyDrawer
{
    private static readonly float Line = EditorGUIUtility.singleLineHeight;
    private static readonly float Space = EditorGUIUtility.standardVerticalSpacing;

    private static readonly string[] AlwaysFields =
    {
        nameof(QuestObjective.Type),
        nameof(QuestObjective.DisplayText),
    };

    private static readonly string[] CatchCountFields = { nameof(QuestObjective.Count) };
    private static readonly string[] CatchTotalValueFields = { nameof(QuestObjective.GoldAmount) };
    private static readonly string[] CatchRarityFields = { nameof(QuestObjective.Rarity), nameof(QuestObjective.Count) };
    private static readonly string[] CatchByPrefixFields = { nameof(QuestObjective.PrefixRequirements) };
    private static readonly string[] CollectItemFields = { nameof(QuestObjective.AcceptableItemIds), nameof(QuestObjective.Count) };
    private static readonly string[] DeliverItemFields = { nameof(QuestObjective.AcceptableItemIds), nameof(QuestObjective.Count), nameof(QuestObjective.GoldAmount), nameof(QuestObjective.NpcName) };
    private static readonly string[] TalkToNpcFields = { nameof(QuestObjective.NpcName), nameof(QuestObjective.Topic) };
    private static readonly string[] EquipItemFields = { nameof(QuestObjective.AcceptableItemIds), nameof(QuestObjective.ItemTag), nameof(QuestObjective.MatchSlot) };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect row = new(position.x, position.y, position.width, Line);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        row.y += Line + Space;
        DrawObjectiveBody(ref row, property);
        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return Line;

        return Line + GetObjectiveBodyHeight(property);
    }

    /// <summary>
    /// Renders the per-type objective fields (Type + DisplayText + type-conditional fields)
    /// at the given row rect. Advances <paramref name="row"/> as it goes. Reusable from
    /// the property drawer and the Quest Database window.
    /// </summary>
    internal static void DrawObjectiveBody(ref Rect row, SerializedProperty property)
    {
        SerializedProperty typeProp = property.FindPropertyRelative(nameof(QuestObjective.Type));

        DrawProperty(ref row, property, AlwaysFields[0]);
        DrawProperty(ref row, property, AlwaysFields[1]);

        QuestObjectiveType objectiveType = (QuestObjectiveType)typeProp.enumValueIndex;
        IReadOnlyList<string> typeFields = FieldsFor(objectiveType);
        for (int i = 0; i < typeFields.Count; i++)
            DrawProperty(ref row, property, typeFields[i]);

        if (objectiveType == QuestObjectiveType.EquipItem)
        {
            SerializedProperty matchSlotProp = property.FindPropertyRelative(nameof(QuestObjective.MatchSlot));
            if (matchSlotProp != null && matchSlotProp.boolValue)
                DrawProperty(ref row, property, nameof(QuestObjective.Slot));
        }
    }

    internal static float GetObjectiveBodyHeight(SerializedProperty property)
    {
        SerializedProperty typeProp = property.FindPropertyRelative(nameof(QuestObjective.Type));
        float height = 0f;

        height += HeightFor(property, AlwaysFields[0]);
        height += HeightFor(property, AlwaysFields[1]);

        QuestObjectiveType objectiveType = (QuestObjectiveType)typeProp.enumValueIndex;
        IReadOnlyList<string> typeFields = FieldsFor(objectiveType);
        for (int i = 0; i < typeFields.Count; i++)
            height += HeightFor(property, typeFields[i]);

        if (objectiveType == QuestObjectiveType.EquipItem)
        {
            SerializedProperty matchSlotProp = property.FindPropertyRelative(nameof(QuestObjective.MatchSlot));
            if (matchSlotProp != null && matchSlotProp.boolValue)
                height += HeightFor(property, nameof(QuestObjective.Slot));
        }

        return height;
    }

    /// <summary>
    /// Returns the ordered list of field names rendered for a given objective type, excluding
    /// the always-rendered Type/DisplayText prefix and the conditional EquipItem.Slot.
    /// </summary>
    internal static IReadOnlyList<string> FieldsFor(QuestObjectiveType type)
    {
        return type switch
        {
            QuestObjectiveType.CatchCount => CatchCountFields,
            QuestObjectiveType.CatchTotalValue => CatchTotalValueFields,
            QuestObjectiveType.CatchRarity => CatchRarityFields,
            QuestObjectiveType.CatchByPrefix => CatchByPrefixFields,
            QuestObjectiveType.CollectItem => CollectItemFields,
            QuestObjectiveType.DeliverItem => DeliverItemFields,
            QuestObjectiveType.TalkToNpc => TalkToNpcFields,
            QuestObjectiveType.EquipItem => EquipItemFields,
            _ => System.Array.Empty<string>(),
        };
    }

    private static float HeightFor(SerializedProperty objectiveProp, string relativeName)
    {
        SerializedProperty property = objectiveProp.FindPropertyRelative(relativeName);
        if (property == null)
            return 0f;
        return EditorGUI.GetPropertyHeight(property, true) + Space;
    }

    private static void DrawProperty(ref Rect row, SerializedProperty objectiveProp, string relativeName)
    {
        SerializedProperty property = objectiveProp.FindPropertyRelative(relativeName);
        if (property == null)
            return;
        float propertyHeight = EditorGUI.GetPropertyHeight(property, true);
        row.height = propertyHeight;
        EditorGUI.PropertyField(row, property, true);
        row.y += propertyHeight + Space;
        row.height = Line;
    }
}
