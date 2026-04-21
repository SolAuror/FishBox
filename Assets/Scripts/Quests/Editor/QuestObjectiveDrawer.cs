using UnityEditor;
using UnityEngine;
using Sol.Quests;

[CustomPropertyDrawer(typeof(QuestObjective))]
public class QuestObjectiveDrawer : PropertyDrawer
{
    private static readonly float Line = EditorGUIUtility.singleLineHeight;
    private static readonly float Space = EditorGUIUtility.standardVerticalSpacing;

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

        SerializedProperty typeProp = property.FindPropertyRelative(nameof(QuestObjective.Type));
        SerializedProperty displayTextProp = property.FindPropertyRelative(nameof(QuestObjective.DisplayText));
        SerializedProperty countProp = property.FindPropertyRelative(nameof(QuestObjective.Count));
        SerializedProperty acceptableItemIdsProp = property.FindPropertyRelative(nameof(QuestObjective.AcceptableItemIds));
        SerializedProperty itemTagProp = property.FindPropertyRelative(nameof(QuestObjective.ItemTag));
        SerializedProperty matchSlotProp = property.FindPropertyRelative(nameof(QuestObjective.MatchSlot));
        SerializedProperty slotProp = property.FindPropertyRelative(nameof(QuestObjective.Slot));
        SerializedProperty goldAmountProp = property.FindPropertyRelative(nameof(QuestObjective.GoldAmount));
        SerializedProperty rarityProp = property.FindPropertyRelative(nameof(QuestObjective.Rarity));
        SerializedProperty prefixRequirementsProp = property.FindPropertyRelative(nameof(QuestObjective.PrefixRequirements));
        SerializedProperty npcNameProp = property.FindPropertyRelative(nameof(QuestObjective.NpcName));

        row.y += Line + Space;
        DrawProperty(ref row, typeProp);
        DrawProperty(ref row, displayTextProp);

        QuestObjectiveType objectiveType = (QuestObjectiveType)typeProp.enumValueIndex;
        switch (objectiveType)
        {
            case QuestObjectiveType.CatchCount:
                DrawProperty(ref row, countProp);
                break;

            case QuestObjectiveType.CatchTotalValue:
                DrawProperty(ref row, goldAmountProp);
                break;

            case QuestObjectiveType.CatchRarity:
                DrawProperty(ref row, rarityProp);
                DrawProperty(ref row, countProp);
                break;

            case QuestObjectiveType.CatchByPrefix:
                DrawProperty(ref row, prefixRequirementsProp);
                break;

            case QuestObjectiveType.CollectItem:
                DrawProperty(ref row, acceptableItemIdsProp);
                DrawProperty(ref row, countProp);
                break;

            case QuestObjectiveType.DeliverItem:
                DrawProperty(ref row, acceptableItemIdsProp);
                DrawProperty(ref row, countProp);
                DrawProperty(ref row, npcNameProp);
                break;

            case QuestObjectiveType.TalkToNpc:
                DrawProperty(ref row, npcNameProp);
                break;

            case QuestObjectiveType.EquipItem:
                DrawProperty(ref row, acceptableItemIdsProp);
                DrawProperty(ref row, itemTagProp);
                DrawProperty(ref row, matchSlotProp);
                if (matchSlotProp.boolValue)
                {
                    DrawProperty(ref row, slotProp);
                }
                break;
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = Line;
        if (!property.isExpanded)
        {
            return height;
        }

        SerializedProperty typeProp = property.FindPropertyRelative(nameof(QuestObjective.Type));
        SerializedProperty displayTextProp = property.FindPropertyRelative(nameof(QuestObjective.DisplayText));
        SerializedProperty countProp = property.FindPropertyRelative(nameof(QuestObjective.Count));
        SerializedProperty acceptableItemIdsProp = property.FindPropertyRelative(nameof(QuestObjective.AcceptableItemIds));
        SerializedProperty itemTagProp = property.FindPropertyRelative(nameof(QuestObjective.ItemTag));
        SerializedProperty matchSlotProp = property.FindPropertyRelative(nameof(QuestObjective.MatchSlot));
        SerializedProperty slotProp = property.FindPropertyRelative(nameof(QuestObjective.Slot));
        SerializedProperty goldAmountProp = property.FindPropertyRelative(nameof(QuestObjective.GoldAmount));
        SerializedProperty rarityProp = property.FindPropertyRelative(nameof(QuestObjective.Rarity));
        SerializedProperty prefixRequirementsProp = property.FindPropertyRelative(nameof(QuestObjective.PrefixRequirements));
        SerializedProperty npcNameProp = property.FindPropertyRelative(nameof(QuestObjective.NpcName));

        height += HeightFor(typeProp);
        height += HeightFor(displayTextProp);

        QuestObjectiveType objectiveType = (QuestObjectiveType)typeProp.enumValueIndex;
        switch (objectiveType)
        {
            case QuestObjectiveType.CatchCount:
                height += HeightFor(countProp);
                break;

            case QuestObjectiveType.CatchTotalValue:
                height += HeightFor(goldAmountProp);
                break;

            case QuestObjectiveType.CatchRarity:
                height += HeightFor(rarityProp);
                height += HeightFor(countProp);
                break;

            case QuestObjectiveType.CatchByPrefix:
                height += HeightFor(prefixRequirementsProp);
                break;

            case QuestObjectiveType.CollectItem:
                height += HeightFor(acceptableItemIdsProp);
                height += HeightFor(countProp);
                break;

            case QuestObjectiveType.DeliverItem:
                height += HeightFor(acceptableItemIdsProp);
                height += HeightFor(countProp);
                height += HeightFor(npcNameProp);
                break;

            case QuestObjectiveType.TalkToNpc:
                height += HeightFor(npcNameProp);
                break;

            case QuestObjectiveType.EquipItem:
                height += HeightFor(acceptableItemIdsProp);
                height += HeightFor(itemTagProp);
                height += HeightFor(matchSlotProp);
                if (matchSlotProp.boolValue)
                {
                    height += HeightFor(slotProp);
                }
                break;
        }

        return height;
    }

    private static float HeightFor(SerializedProperty property)
    {
        return EditorGUI.GetPropertyHeight(property, true) + Space;
    }

    private static void DrawProperty(ref Rect row, SerializedProperty property)
    {
        float propertyHeight = EditorGUI.GetPropertyHeight(property, true);
        row.height = propertyHeight;
        EditorGUI.PropertyField(row, property, true);
        row.y += propertyHeight + Space;
        row.height = Line;
    }
}
