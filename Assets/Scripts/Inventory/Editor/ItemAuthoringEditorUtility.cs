using System.Collections.Generic;
using System.IO;
using Sol.Fishing;
using Sol.Grab;
using Sol.Outline;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    public static class ItemAuthoringEditorUtility
    {
        public const string DefaultItemFolder = "Assets/ItemPrefabs";

        public static ItemComponent CreateItemPrefab(ItemAuthoringTemplate template, string itemName = null)
        {
            EnsureDefaultFolder();

            string resolvedName = string.IsNullOrWhiteSpace(itemName)
                ? DefaultNameFor(template)
                : itemName.Trim();

            GameObject itemObject = new(resolvedName);
            itemObject.tag = "Item";
            itemObject.AddComponent<BoxCollider>();
            itemObject.AddComponent<GrabbableComponent>();
            ItemComponent item = itemObject.AddComponent<ItemComponent>();
            EnsureCanonicalComponents(item);
            ApplyTemplate(item, template, resolvedName, assignNewId: true);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultItemFolder}/{resolvedName}.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(itemObject, path);
            Object.DestroyImmediate(itemObject);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            ItemRegistry.ForceEditorSyncNow();
            return prefab != null ? prefab.GetComponent<ItemComponent>() : null;
        }

        public static ItemComponent DuplicateItemPrefab(ItemComponent source)
        {
            if (source == null)
                return null;

            string sourcePath = AssetDatabase.GetAssetPath(source.gameObject);
            if (string.IsNullOrWhiteSpace(sourcePath))
                return null;

            string folder = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/") ?? DefaultItemFolder;
            string fileName = $"{source.ItemName} Copy.prefab";
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}");
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                return null;

            AssetDatabase.ImportAsset(targetPath);
            GameObject duplicatedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            ItemComponent duplicatedItem = duplicatedRoot != null ? duplicatedRoot.GetComponentInChildren<ItemComponent>(true) : null;
            if (duplicatedItem != null)
            {
                SerializedObject serializedObject = new(duplicatedItem);
                serializedObject.Update();
                SetString(serializedObject, "_itemId", NextItemId());
                SetString(serializedObject, "_itemName", $"{source.ItemName} Copy");
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(duplicatedItem);
            }

            AssetDatabase.SaveAssets();
            ItemRegistry.ForceEditorSyncNow();
            return duplicatedItem;
        }

        public static void ApplyTemplate(ItemComponent item, ItemAuthoringTemplate template, string itemName = null, bool assignNewId = false)
        {
            if (item == null)
                return;

            Undo.RecordObject(item, "Apply Item Template");
            SerializedObject serializedObject = new(item);
            serializedObject.Update();

            if (!string.IsNullOrWhiteSpace(itemName))
                SetString(serializedObject, "_itemName", itemName.Trim());

            SerializedProperty itemId = serializedObject.FindProperty("_itemId");
            if (itemId != null && (assignNewId || string.IsNullOrWhiteSpace(itemId.stringValue)))
                itemId.stringValue = NextItemId();
            SetEnum(serializedObject, "_authoringTemplate", (int)template);
            SetBool(serializedObject, "_isTradeable", template != ItemAuthoringTemplate.KeyItem);
            SetBool(serializedObject, "_isStackable", false);
            SetBool(serializedObject, "_isConsumable", false);
            SetInt(serializedObject, "_maxStackSize", 1);
            SetInt(serializedObject, "_value", 0);

            switch (template)
            {
                case ItemAuthoringTemplate.Consumable:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Consumable);
                    SetBool(serializedObject, "_isStackable", true);
                    SetBool(serializedObject, "_isConsumable", true);
                    SetInt(serializedObject, "_maxStackSize", 10);
                    SetEnum(serializedObject, "_useOccasion", (int)ItemUseOccasion.InventoryOnly);
                    EnsureDefaultUseEffect(serializedObject);
                    break;
                case ItemAuthoringTemplate.KeyItem:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Key);
                    SetBool(serializedObject, "_isTradeable", false);
                    SetEnum(serializedObject, "_useOccasion", (int)ItemUseOccasion.Never);
                    break;
                case ItemAuthoringTemplate.Equipment:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Equipable);
                    SetEnum(serializedObject, "_equipDomain", (int)EquipDomain.WeaponTool);
                    break;
                case ItemAuthoringTemplate.FishingBait:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Material);
                    SetBool(serializedObject, "_isStackable", true);
                    SetInt(serializedObject, "_maxStackSize", 20);
                    if (item.GetComponent<FishingBaitItem>() == null)
                        AddTemplateComponent<FishingBaitItem>(item);
                    break;
                case ItemAuthoringTemplate.FishingLure:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Miscellaneous);
                    if (item.GetComponent<FishingLureItem>() == null)
                        AddTemplateComponent<FishingLureItem>(item);
                    break;
                case ItemAuthoringTemplate.Currency:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Gold);
                    SetBool(serializedObject, "_isStackable", true);
                    SetInt(serializedObject, "_maxStackSize", 999);
                    SetInt(serializedObject, "_value", 1);
                    SetEnum(serializedObject, "_useOccasion", (int)ItemUseOccasion.Never);
                    break;
                default:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Material);
                    break;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        }

        public static void FillMissingTemplateDefaults(ItemComponent item, ItemAuthoringTemplate template, string itemName = null)
        {
            if (item == null || template == ItemAuthoringTemplate.None)
                return;

            Undo.RecordObject(item, "Fill Item Template Defaults");
            EnsureCanonicalComponents(item);
            SerializedObject serializedObject = new(item);
            serializedObject.Update();

            SetEnum(serializedObject, "_authoringTemplate", (int)template);

            SerializedProperty itemId = serializedObject.FindProperty("_itemId");
            if (itemId != null && string.IsNullOrWhiteSpace(itemId.stringValue))
                itemId.stringValue = NextItemId();

            SerializedProperty itemNameProperty = serializedObject.FindProperty("_itemName");
            if (itemNameProperty != null
                && (string.IsNullOrWhiteSpace(itemNameProperty.stringValue)
                    || string.Equals(itemNameProperty.stringValue.Trim(), "Item", System.StringComparison.Ordinal))
                && !string.IsNullOrWhiteSpace(itemName))
            {
                itemNameProperty.stringValue = itemName.Trim();
            }

            if (IsBlankItem(serializedObject))
                FillBlankTemplateDefaults(serializedObject, template);

            switch (template)
            {
                case ItemAuthoringTemplate.Consumable:
                    SetEnumIfDefault(serializedObject, "_useOccasion", (int)ItemUseOccasion.InventoryOnly, (int)ItemUseOccasion.InventoryOnly);
                    EnsureDefaultUseEffect(serializedObject);
                    break;
                case ItemAuthoringTemplate.KeyItem:
                    SetEnumIfDefault(serializedObject, "_useOccasion", (int)ItemUseOccasion.InventoryOnly, (int)ItemUseOccasion.Never);
                    break;
                case ItemAuthoringTemplate.Equipment:
                    SetEnumIfDefault(serializedObject, "_equipDomain", (int)EquipDomain.Auto, (int)EquipDomain.WeaponTool);
                    break;
                case ItemAuthoringTemplate.FishingBait:
                    if (item.GetComponent<FishingBaitItem>() == null)
                        AddTemplateComponent<FishingBaitItem>(item);
                    break;
                case ItemAuthoringTemplate.FishingLure:
                    if (item.GetComponent<FishingLureItem>() == null)
                        AddTemplateComponent<FishingLureItem>(item);
                    break;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        }

        public static List<string> BuildTemplateOverwritePreview(ItemAuthoringTemplate template)
        {
            List<string> changes = new();
            changes.Add("Authoring template marker");

            switch (template)
            {
                case ItemAuthoringTemplate.Consumable:
                    changes.AddRange(new[] { "Type", "Stackable", "Consumable", "Max stack size", "Use occasion", "Default use effect" });
                    break;
                case ItemAuthoringTemplate.KeyItem:
                    changes.AddRange(new[] { "Type", "Tradeable", "Use occasion" });
                    break;
                case ItemAuthoringTemplate.Equipment:
                    changes.AddRange(new[] { "Type", "Stackable", "Consumable", "Max stack size", "Value", "Equip domain" });
                    break;
                case ItemAuthoringTemplate.FishingBait:
                    changes.AddRange(new[] { "Type", "Stackable", "Max stack size", "FishingBaitItem component" });
                    break;
                case ItemAuthoringTemplate.FishingLure:
                    changes.AddRange(new[] { "Type", "FishingLureItem component" });
                    break;
                case ItemAuthoringTemplate.Currency:
                    changes.AddRange(new[] { "Type", "Stackable", "Max stack size", "Value", "Use occasion" });
                    break;
                default:
                    changes.Add("Type");
                    break;
            }

            return changes;
        }

        public static string NextItemId()
        {
            ItemRegistry registry = ItemRegistry.Get();
            return NextItemIdFromEntries(registry?.Entries);
        }

        internal static string NextItemIdFromEntries(IReadOnlyList<ItemRegistry.Entry> entries)
        {
            HashSet<int> used = new();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    string id = entries[i]?.ItemId;
                    if (EntityCodeUtility.TryParse(id, EntityCodeUtility.ItemPrefix, out int numeric))
                        used.Add(numeric);
                }
            }

            for (int next = 1; next <= 99999; next++)
            {
                if (!used.Contains(next))
                    return EntityCodeUtility.Format(EntityCodeUtility.ItemPrefix, next);
            }

            return string.Empty;
        }

        public static string GetPrefabPath(ItemComponent item)
        {
            if (item == null)
                return string.Empty;

            return AssetDatabase.GetAssetPath(item.gameObject);
        }

        private static void EnsureDefaultFolder()
        {
            if (AssetDatabase.IsValidFolder(DefaultItemFolder))
                return;

            if (!AssetDatabase.IsValidFolder("Assets"))
                return;

            AssetDatabase.CreateFolder("Assets", "ItemPrefabs");
        }

        private static string DefaultNameFor(ItemAuthoringTemplate template)
        {
            return template switch
            {
                ItemAuthoringTemplate.Consumable => "New Consumable",
                ItemAuthoringTemplate.KeyItem => "New Key",
                ItemAuthoringTemplate.Equipment => "New Equipment",
                ItemAuthoringTemplate.FishingBait => "New Bait",
                ItemAuthoringTemplate.FishingLure => "New Lure",
                ItemAuthoringTemplate.Currency => "New Gold",
                _ => "New Item"
            };
        }

        private static void EnsureDefaultUseEffect(SerializedObject serializedObject)
        {
            SerializedProperty effects = serializedObject.FindProperty("_useEffects");
            if (effects == null || effects.arraySize > 0)
                return;

            effects.arraySize = 1;
            SerializedProperty effect = effects.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("_effectType").enumValueIndex = (int)ItemUseEffectType.HealHealthFlat;
            effect.FindPropertyRelative("_amount").floatValue = 10f;
        }

        private static void FillBlankTemplateDefaults(SerializedObject serializedObject, ItemAuthoringTemplate template)
        {
            switch (template)
            {
                case ItemAuthoringTemplate.Consumable:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Consumable);
                    SetBool(serializedObject, "_isStackable", true);
                    SetBool(serializedObject, "_isConsumable", true);
                    SetInt(serializedObject, "_maxStackSize", 10);
                    break;
                case ItemAuthoringTemplate.KeyItem:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Key);
                    SetBool(serializedObject, "_isTradeable", false);
                    break;
                case ItemAuthoringTemplate.Equipment:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Equipable);
                    break;
                case ItemAuthoringTemplate.FishingBait:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Material);
                    SetBool(serializedObject, "_isStackable", true);
                    SetInt(serializedObject, "_maxStackSize", 20);
                    break;
                case ItemAuthoringTemplate.FishingLure:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Miscellaneous);
                    break;
                case ItemAuthoringTemplate.Currency:
                    SetEnum(serializedObject, "_itemType", (int)ItemType.Gold);
                    SetBool(serializedObject, "_isStackable", true);
                    SetInt(serializedObject, "_maxStackSize", 999);
                    SetInt(serializedObject, "_value", 1);
                    break;
            }
        }

        private static bool IsBlankItem(SerializedObject serializedObject)
        {
            return IsEnum(serializedObject, "_itemType", (int)ItemType.Material)
                && IsInt(serializedObject, "_value", 0)
                && IsBool(serializedObject, "_isStackable", false)
                && IsBool(serializedObject, "_isConsumable", false)
                && IsBool(serializedObject, "_isTradeable", true)
                && IsInt(serializedObject, "_maxStackSize", 1);
        }

        private static T AddTemplateComponent<T>(ItemComponent item) where T : Component
        {
            T component = Undo.AddComponent<T>(item.gameObject);
            EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(item.gameObject);
            return component;
        }

        private static void EnsureCanonicalComponents(ItemComponent item)
        {
            GameObject go = item.gameObject;

            if (go.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = AddTemplateComponent<Rigidbody>(item);
                rb.mass = 0.1f;
                rb.linearDamping = 1.2f;
                rb.angularDamping = 1.4f;
                rb.useGravity = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            if (go.GetComponent<MeshFilter>() == null)
                AddTemplateComponent<MeshFilter>(item);

            if (go.GetComponent<MeshRenderer>() == null)
                AddTemplateComponent<MeshRenderer>(item);

            if (go.GetComponent<OutlineComponent>() == null)
                AddTemplateComponent<OutlineComponent>(item);
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value ?? string.Empty;
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void SetEnum(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.enumValueIndex = value;
        }

        private static void SetEnumIfDefault(SerializedObject serializedObject, string propertyName, int defaultValue, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.enumValueIndex == defaultValue)
                property.enumValueIndex = value;
        }

        private static bool IsBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.boolValue == value;
        }

        private static bool IsInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.intValue == value;
        }

        private static bool IsEnum(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.enumValueIndex == value;
        }
    }
}
