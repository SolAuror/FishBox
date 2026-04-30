using System.Collections.Generic;
using Sol.Fishing;
using Sol.Grab;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    public static class ItemComponentEditorUtility
    {
        public static void DrawItemInspector(SerializedObject serializedObject, ItemComponent item, bool showWarnings = true)
        {
            if (serializedObject == null)
                return;

            if (showWarnings && item != null)
                DrawWarnings(item);

            DrawBasicSection(serializedObject);
            DrawInventorySection(serializedObject);
            DrawUseSection(serializedObject);
            DrawEquipmentSection(serializedObject);
            DrawFishingSection(item);
            DrawOwnershipSection(serializedObject);
            DrawPrefabWorldSection(serializedObject);
            DrawAuthoringSection(serializedObject);
        }

        public static void DrawWarnings(ItemComponent item)
        {
            List<ItemAuthoringWarning> warnings = ItemAuthoringValidator.Validate(item);
            if (warnings.Count == 0)
                return;

            for (int i = 0; i < warnings.Count; i++)
            {
                ItemAuthoringWarning warning = warnings[i];
                MessageType type = warning.Severity switch
                {
                    ItemAuthoringWarningSeverity.Error => MessageType.Error,
                    ItemAuthoringWarningSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(warning.Message, type);
            }

            EditorGUILayout.Space(4f);
        }

        private static void DrawBasicSection(SerializedObject serializedObject)
        {
            DrawHeader("Basic");
            DrawProperty(serializedObject, "_itemId");
            DrawProperty(serializedObject, "_itemName");
            DrawProperty(serializedObject, "_itemType");
            DrawProperty(serializedObject, "_value");
            DrawProperty(serializedObject, "_icon");
            DrawProperty(serializedObject, "_flavourText");
        }

        private static void DrawInventorySection(SerializedObject serializedObject)
        {
            DrawHeader("Inventory");
            DrawProperty(serializedObject, "_isStackable");
            DrawProperty(serializedObject, "_maxStackSize");
            DrawProperty(serializedObject, "_isConsumable");
            DrawProperty(serializedObject, "_isTradeable");
        }

        private static void DrawUseSection(SerializedObject serializedObject)
        {
            SerializedProperty itemTypeProp = serializedObject.FindProperty("_itemType");
            SerializedProperty consumableProp = serializedObject.FindProperty("_isConsumable");
            bool show = consumableProp != null && consumableProp.boolValue;
            if (itemTypeProp != null)
            {
                ItemType itemType = (ItemType)itemTypeProp.enumValueIndex;
                show |= ItemTypeRules.IsConsumableType(itemType);
            }

            if (!show)
            {
                SerializedProperty effects = serializedObject.FindProperty("_useEffects");
                show = effects != null && effects.arraySize > 0;
            }

            if (!show)
                return;

            DrawHeader("Use");
            DrawProperty(serializedObject, "_useOccasion");
            DrawProperty(serializedObject, "_useEffects", includeChildren: true);
        }

        private static void DrawEquipmentSection(SerializedObject serializedObject)
        {
            SerializedProperty itemTypeProp = serializedObject.FindProperty("_itemType");
            if (itemTypeProp == null)
                return;

            ItemType itemType = (ItemType)itemTypeProp.enumValueIndex;
            bool showWeapon = ItemTypeRules.UsesWeaponStats(itemType);
            bool showArmor = ItemTypeRules.UsesArmorStats(itemType);
            bool showEquipment = ItemTypeRules.UsesEquipmentSettings(itemType);
            if (!showWeapon && !showArmor && !showEquipment)
                return;

            DrawHeader("Equipment");
            if (showWeapon)
                DrawProperty(serializedObject, "_damage");
            if (showArmor)
                DrawProperty(serializedObject, "_defense");
            if (showEquipment)
            {
                DrawProperty(serializedObject, "_equipBone");
                DrawProperty(serializedObject, "_equipOffset");
                DrawProperty(serializedObject, "_equipRotation");
                DrawProperty(serializedObject, "_equipDomain");
                DrawProperty(serializedObject, "_weaponHanding");
                DrawProperty(serializedObject, "_allowedEquipSlots", includeChildren: true);
            }
        }

        private static void DrawFishingSection(ItemComponent item)
        {
            if (item == null)
                return;

            FishingBaitItem bait = item.GetComponent<FishingBaitItem>();
            FishingLureItem lure = item.GetComponent<FishingLureItem>();
            bool show = bait != null || lure != null
                || item.AuthoringTemplate == ItemAuthoringTemplate.FishingBait
                || item.AuthoringTemplate == ItemAuthoringTemplate.FishingLure;

            if (!show)
                return;

            DrawHeader("Fishing");
            if (bait == null && GUILayout.Button("Add Fishing Bait Component"))
                bait = Undo.AddComponent<FishingBaitItem>(item.gameObject);
            if (lure == null && GUILayout.Button("Add Fishing Lure Component"))
                lure = Undo.AddComponent<FishingLureItem>(item.gameObject);

            if (bait != null)
            {
                SerializedObject baitObject = new(bait);
                baitObject.Update();
                DrawProperty(baitObject, "_baitDefinition");
                DrawProperty(baitObject, "_interestMultiplier");
                DrawProperty(baitObject, "_radiusMultiplier");
                baitObject.ApplyModifiedProperties();
            }

            if (lure != null)
            {
                SerializedObject lureObject = new(lure);
                lureObject.Update();
                DrawProperty(lureObject, "_lureRange");
                DrawProperty(lureObject, "_castPrefabOverride");
                lureObject.ApplyModifiedProperties();
            }
        }

        private static void DrawOwnershipSection(SerializedObject serializedObject)
        {
            DrawHeader("Ownership");
            DrawProperty(serializedObject, "_itemOwnerId");
            DrawProperty(serializedObject, "_isStolen");
        }

        private static void DrawPrefabWorldSection(SerializedObject serializedObject)
        {
            DrawHeader("Prefab / World");
            DrawProperty(serializedObject, "_pickupGrip");

            if (serializedObject.targetObject is not ItemComponent item)
                return;

            GameObject go = item.gameObject;
            MeshFilter filter = go.GetComponent<MeshFilter>();
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();

            if (filter == null || renderer == null)
            {
                EditorGUILayout.HelpBox(
                    "This prefab is missing MeshFilter / MeshRenderer. Click 'Fill Missing Defaults' above to add canonical components.",
                    MessageType.Info);
                return;
            }

            SerializedObject filterSO = new(filter);
            filterSO.Update();
            SerializedProperty meshProperty = filterSO.FindProperty("m_Mesh");
            if (meshProperty != null)
                EditorGUILayout.PropertyField(meshProperty, new GUIContent("Mesh"));
            filterSO.ApplyModifiedProperties();

            SerializedObject rendererSO = new(renderer);
            rendererSO.Update();
            SerializedProperty materialsProperty = rendererSO.FindProperty("m_Materials");
            if (materialsProperty != null)
                EditorGUILayout.PropertyField(materialsProperty, new GUIContent("Materials"), true);
            rendererSO.ApplyModifiedProperties();
        }

        private static void DrawAuthoringSection(SerializedObject serializedObject)
        {
            DrawHeader("Authoring");
            DrawProperty(serializedObject, "_authoringTemplate");
            DrawProperty(serializedObject, "_itemAuthoringNotes");
        }

        private static void DrawHeader(string label)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        private static void DrawProperty(SerializedObject serializedObject, string propertyName, bool includeChildren = false)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, includeChildren);
        }
    }
}
