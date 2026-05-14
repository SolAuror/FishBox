using System;
using System.Collections.Generic;
using Sol.Fishing;
using Sol.Grab;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    public static class ItemComponentEditorUtility
    {
        private static readonly HashSet<int> _equipmentRevealOverrides = new();
        private const string FoldoutPrefPrefix = "Sol.ItemEditor.Section.";

        public static void DrawItemInspector(SerializedObject serializedObject, ItemComponent item, bool showWarnings = true)
        {
            if (serializedObject == null)
                return;

            if (showWarnings && item != null)
                DrawWarnings(item);

            if (DrawRegistryBackedInspector(serializedObject, item))
                return;

            DrawBasicSection(serializedObject, item);
            DrawAuthoringSection(serializedObject, item);
            DrawInventorySection(serializedObject);
            DrawUseSection(serializedObject);
            DrawEquipmentSection(serializedObject);
            DrawFishingSection(item);
            DrawOwnershipSection(serializedObject);
            DrawPrefabWorldSection(serializedObject);
        }

        private static bool DrawRegistryBackedInspector(SerializedObject itemObject, ItemComponent item)
        {
            if (item == null)
                return false;

            ItemRegistry registry = ItemRegistry.Get();
            int entryIndex = FindRegistryEntryIndex(registry, item);
            if (registry == null || entryIndex < 0)
                return false;

            SerializedObject registryObject = new(registry);
            registryObject.Update();
            SerializedProperty entries = registryObject.FindProperty("_entries");
            if (entries == null || entryIndex >= entries.arraySize)
                return false;

            SerializedProperty entry = entries.GetArrayElementAtIndex(entryIndex);
            DrawRegistryIdentitySection(entry, itemObject, item);
            DrawRegistryAuthoringSection(entry, item);
            SolDatabaseItemPage.DrawInventoryRulesSection(entry);
            SolDatabaseItemPage.DrawUseDefinitionSection(entry);
            SolDatabaseItemPage.DrawEquipmentDefinitionSection(entry);
            DrawFishingSection(item);
            DrawOwnershipSection(itemObject);
            DrawPrefabWorldSection(itemObject);

            if (registryObject.ApplyModifiedProperties())
            {
                registry.Entries[entryIndex]?.Normalize();
                EditorUtility.SetDirty(registry);
                ItemRegistry.NotifyEditorDefinitionsChanged();
            }

            return true;
        }

        private static void DrawRegistryIdentitySection(SerializedProperty entry, SerializedObject itemObject, ItemComponent item)
        {
            if (!BeginSection("Basic", defaultOpen: true))
            {
                EndSection();
                return;
            }

            SerializedProperty displayName = entry.FindPropertyRelative("DisplayName");
            if (displayName != null)
            {
                EditorGUILayout.LabelField("Item Name", EditorStyles.miniLabel);
                GUIStyle bigField = new(EditorStyles.textField) { fontSize = 14, fixedHeight = 22f };
                displayName.stringValue = EditorGUILayout.TextField(displayName.stringValue ?? string.Empty, bigField);
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("ItemId"));

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(item == null))
            {
                if (GUILayout.Button("Regenerate ItemId...", GUILayout.Width(150f)))
                    RegenerateItemId(itemObject, item);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(entry.FindPropertyRelative("ItemType"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Value"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Icon"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("FlavourText"));
            EndSection();
        }

        private static void DrawRegistryAuthoringSection(SerializedProperty entry, ItemComponent item)
        {
            if (!BeginSection("Authoring", defaultOpen: true))
            {
                EndSection();
                return;
            }

            SerializedProperty templateProp = entry.FindPropertyRelative("AuthoringTemplate");
            EditorGUILayout.PropertyField(templateProp);
            DrawRegistryTemplateActions(entry, item);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("AuthoringNotes"));
            EndSection();
        }

        private static void DrawRegistryTemplateActions(SerializedProperty entry, ItemComponent item)
        {
            SerializedProperty templateProp = entry.FindPropertyRelative("AuthoringTemplate");
            ItemAuthoringTemplate template = templateProp != null
                ? (ItemAuthoringTemplate)templateProp.enumValueIndex
                : ItemAuthoringTemplate.None;

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Fill Missing Defaults adds prefab setup only where empty/default. Reapply Template overwrites template-owned prefab setup; item design values remain in ItemRegistry.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(item == null || template == ItemAuthoringTemplate.None))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fill Missing Defaults", GUILayout.Width(150f)))
                {
                    entry.serializedObject.ApplyModifiedProperties();
                    ItemAuthoringEditorUtility.FillMissingTemplateDefaults(item, template, GetRegistryDisplayName(entry, item));
                    entry.serializedObject.Update();
                    ItemRegistry.ScheduleEditorSync();
                }
                if (GUILayout.Button("Reapply Template...", GUILayout.Width(150f)))
                {
                    ReapplyRegistryTemplate(entry, item, template);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void ReapplyRegistryTemplate(SerializedProperty entry, ItemComponent item, ItemAuthoringTemplate template)
        {
            List<string> changes = ItemAuthoringEditorUtility.BuildTemplateOverwritePreview(template);
            string message = "This will overwrite visual prefab setup fields:\n\n- "
                + string.Join("\n- ", changes)
                + "\n\nItem design values remain authoritative in ItemRegistry.";

            if (!EditorUtility.DisplayDialog("Reapply Item Template", message, "Overwrite Fields", "Cancel"))
                return;

            entry.serializedObject.ApplyModifiedProperties();
            ItemAuthoringEditorUtility.ApplyTemplate(item, template, GetRegistryDisplayName(entry, item));
            entry.serializedObject.Update();
            ItemRegistry.ScheduleEditorSync();
        }

        private static string GetRegistryDisplayName(SerializedProperty entry, ItemComponent item)
        {
            SerializedProperty displayName = entry.FindPropertyRelative("DisplayName");
            if (displayName != null && !string.IsNullOrWhiteSpace(displayName.stringValue))
                return displayName.stringValue;

            return item != null ? item.ItemName : string.Empty;
        }

        private static int FindRegistryEntryIndex(ItemRegistry registry, ItemComponent item)
        {
            if (registry?.Entries == null || item == null)
                return -1;

            for (int i = 0; i < registry.Entries.Count; i++)
            {
                ItemRegistry.Entry entry = registry.Entries[i];
                if (entry == null)
                    continue;

                if (entry.Prefab == item)
                    return i;

                if (!string.IsNullOrWhiteSpace(item.ItemId)
                    && string.Equals(entry.ItemId, item.ItemId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
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

        // ----- Section: Basic -----

        private static void DrawBasicSection(SerializedObject serializedObject, ItemComponent item)
        {
            if (!BeginSection("Basic", defaultOpen: true))
            {
                EndSection();
                return;
            }

            SerializedProperty nameProp = serializedObject.FindProperty("_itemName");
            if (nameProp != null)
            {
                EditorGUILayout.LabelField("Item Name", EditorStyles.miniLabel);
                GUIStyle bigField = new GUIStyle(EditorStyles.textField) { fontSize = 14, fixedHeight = 22f };
                nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue ?? string.Empty, bigField);
            }

            using (new EditorGUI.DisabledScope(true))
                DrawProperty(serializedObject, "_itemId");

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(item == null))
            {
                if (GUILayout.Button("Regenerate ItemId...", GUILayout.Width(150f)))
                    RegenerateItemId(serializedObject, item);
            }
            EditorGUILayout.EndHorizontal();

            DrawProperty(serializedObject, "_itemType");
            DrawProperty(serializedObject, "_value");
            DrawProperty(serializedObject, "_icon");
            DrawProperty(serializedObject, "_flavourText");

            EndSection();
        }

        private static void RegenerateItemId(SerializedObject serializedObject, ItemComponent item)
        {
            if (item == null)
                return;

            string oldId = item.ItemId;
            string message = string.IsNullOrWhiteSpace(oldId)
                ? "Assign a new item id to this prefab?"
                : $"Assign a new item id to this prefab?\n\nCurrent id: {oldId}\n\nExisting inventory, shop, loot, quest, or save references that use the current id may need to be updated.";

            if (!EditorUtility.DisplayDialog("Regenerate ItemId", message, "Regenerate", "Cancel"))
                return;

            SerializedProperty itemId = serializedObject.FindProperty("_itemId");
            if (itemId == null)
                return;

            string next = ItemAuthoringEditorUtility.NextItemId();
            if (string.IsNullOrEmpty(next))
                return;

            itemId.stringValue = next;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            if (serializedObject.targetObject != null)
                EditorUtility.SetDirty(serializedObject.targetObject);
            ItemRegistry.ScheduleEditorSync();
        }

        // ----- Section: Inventory -----

        private static void DrawInventorySection(SerializedObject serializedObject)
        {
            if (!BeginSection("Inventory", defaultOpen: true))
            {
                EndSection();
                return;
            }

            ItemType itemType = GetItemType(serializedObject);
            bool showStacking = ItemTypeRules.ShowStackingFields(itemType);
            bool showConsumable = ItemTypeRules.ShowConsumableFlag(itemType);

            if (showStacking)
            {
                DrawProperty(serializedObject, "_isStackable");
                SerializedProperty stackable = serializedObject.FindProperty("_isStackable");
                if (stackable != null && stackable.boolValue)
                    DrawProperty(serializedObject, "_maxStackSize");
            }

            if (showConsumable)
                DrawProperty(serializedObject, "_isConsumable");

            DrawProperty(serializedObject, "_isTradeable");

            EndSection();
        }

        // ----- Section: Use -----

        private static void DrawUseSection(SerializedObject serializedObject)
        {
            ItemType itemType = GetItemType(serializedObject);
            SerializedProperty consumableProp = serializedObject.FindProperty("_isConsumable");
            SerializedProperty effects = serializedObject.FindProperty("_useEffects");
            bool isConsumable = consumableProp != null && consumableProp.boolValue;
            int effectCount = effects != null ? effects.arraySize : 0;

            if (!ItemTypeRules.ShowUseSection(itemType, isConsumable, effectCount))
                return;

            if (!BeginSection("Use", defaultOpen: true))
            {
                EndSection();
                return;
            }

            DrawProperty(serializedObject, "_useOccasion");
            DrawProperty(serializedObject, "_useEffects", includeChildren: true);

            EndSection();
        }

        // ----- Section: Equipment -----

        private static void DrawEquipmentSection(SerializedObject serializedObject)
        {
            SerializedProperty itemTypeProp = serializedObject.FindProperty("_itemType");
            if (itemTypeProp == null)
                return;

            ItemType itemType = (ItemType)itemTypeProp.enumValueIndex;
            bool sectionApplies = ItemTypeRules.ShowEquipmentSection(itemType);

            if (!sectionApplies)
            {
                if (HasOrphanEquipmentData(serializedObject) && !IsEquipmentRevealed(serializedObject))
                {
                    if (BeginSection("Equipment", defaultOpen: true))
                    {
                        DrawHiddenSectionNotice(
                            $"This item is type '{itemType}' but has equipment data set. Reveal to clear or change type.",
                            () => SetEquipmentRevealed(serializedObject, true));
                    }
                    EndSection();
                    return;
                }

                if (!IsEquipmentRevealed(serializedObject))
                    return;
            }
            else
            {
                SetEquipmentRevealed(serializedObject, false);
            }

            if (!BeginSection("Equipment", defaultOpen: true))
            {
                EndSection();
                return;
            }

            bool showWeapon = sectionApplies && ItemTypeRules.UsesWeaponStats(itemType);
            bool showArmor = sectionApplies && ItemTypeRules.UsesArmorStats(itemType);

            if (!sectionApplies)
            {
                EditorGUILayout.HelpBox(
                    "Section revealed for cleanup. All fields are shown so you can clear stale data, then change item type back.",
                    MessageType.Info);
                DrawProperty(serializedObject, "_damage");
                DrawProperty(serializedObject, "_defense");
            }
            else
            {
                if (showWeapon)
                    DrawProperty(serializedObject, "_damage");
                if (showArmor)
                    DrawProperty(serializedObject, "_defense");
            }

            DrawProperty(serializedObject, "_equipBone");
            DrawProperty(serializedObject, "_equipOffset");
            DrawProperty(serializedObject, "_equipRotation");
            DrawProperty(serializedObject, "_equipDomain");
            DrawProperty(serializedObject, "_weaponHanding");
            DrawProperty(serializedObject, "_allowedEquipSlots", includeChildren: true);

            EndSection();
        }

        // ----- Section: Fishing -----

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

            if (!BeginSection("Fishing", defaultOpen: true))
            {
                EndSection();
                return;
            }

            if (item.AuthoringTemplate == ItemAuthoringTemplate.FishingBait && bait == null)
                EditorGUILayout.HelpBox("Fishing bait templates need a FishingBaitItem component.", MessageType.Warning);
            if (item.AuthoringTemplate == ItemAuthoringTemplate.FishingLure && lure == null)
                EditorGUILayout.HelpBox("Fishing lure templates need a FishingLureItem component.", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(bait != null))
            {
                if (GUILayout.Button("Add Fishing Bait Component"))
                    bait = Undo.AddComponent<FishingBaitItem>(item.gameObject);
            }
            using (new EditorGUI.DisabledScope(lure != null))
            {
                if (GUILayout.Button("Add Fishing Lure Component"))
                    lure = Undo.AddComponent<FishingLureItem>(item.gameObject);
            }
            EditorGUILayout.EndHorizontal();

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

            EndSection();
        }

        // ----- Section: Ownership -----

        private static void DrawOwnershipSection(SerializedObject serializedObject)
        {
            if (!BeginSection("Ownership", defaultOpen: false))
            {
                EndSection();
                return;
            }

            SerializedProperty ownerIdProp = serializedObject.FindProperty("_itemOwnerId");
            if (ownerIdProp != null)
                ReferenceDropdown.DrawNpcLayout(new GUIContent("Item Owner Id"), ownerIdProp);

            DrawProperty(serializedObject, "_isStolen");

            EndSection();
        }

        // ----- Section: Prefab / World -----

        private static void DrawPrefabWorldSection(SerializedObject serializedObject)
        {
            if (!BeginSection("Prefab / World", defaultOpen: false))
            {
                EndSection();
                return;
            }

            DrawProperty(serializedObject, "_pickupGrip");

            if (serializedObject.targetObject is not ItemComponent item)
            {
                EndSection();
                return;
            }

            GameObject go = item.gameObject;
            MeshFilter filter = go.GetComponent<MeshFilter>();
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();

            if (filter == null || renderer == null)
            {
                EditorGUILayout.HelpBox(
                    "This prefab is missing MeshFilter / MeshRenderer. Add canonical components to make the item usable as a world prefab.",
                    MessageType.Info);
                if (GUILayout.Button("Add Canonical Item Components"))
                {
                    ItemAuthoringEditorUtility.EnsureCanonicalComponents(item);
                    serializedObject.Update();
                    ItemRegistry.ScheduleEditorSync();
                }
                EndSection();
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

            EndSection();
        }

        // ----- Section: Authoring -----

        private static void DrawAuthoringSection(SerializedObject serializedObject, ItemComponent item)
        {
            if (!BeginSection("Authoring", defaultOpen: true))
            {
                EndSection();
                return;
            }

            DrawProperty(serializedObject, "_authoringTemplate");
            DrawTemplateActions(serializedObject, item);
            DrawProperty(serializedObject, "_itemAuthoringNotes");

            EndSection();
        }

        private static void DrawTemplateActions(SerializedObject serializedObject, ItemComponent item)
        {
            ItemAuthoringTemplate template = GetAuthoringTemplate(serializedObject);
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Fill Missing Defaults adds only empty/default template setup. Reapply Template overwrites template-owned fields.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(item == null || template == ItemAuthoringTemplate.None))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fill Missing Defaults", GUILayout.Width(150f)))
                    FillMissingTemplateDefaults(serializedObject, item, template);
                if (GUILayout.Button("Reapply Template...", GUILayout.Width(150f)))
                    ReapplyTemplate(serializedObject, item, template);
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void FillMissingTemplateDefaults(SerializedObject serializedObject, ItemComponent item, ItemAuthoringTemplate template)
        {
            if (item == null || template == ItemAuthoringTemplate.None)
                return;

            serializedObject.ApplyModifiedProperties();
            ItemAuthoringEditorUtility.FillMissingTemplateDefaults(item, template, GetItemName(serializedObject, item));
            serializedObject.Update();
            ItemRegistry.ScheduleEditorSync();
        }

        private static void ReapplyTemplate(SerializedObject serializedObject, ItemComponent item, ItemAuthoringTemplate template)
        {
            if (item == null || template == ItemAuthoringTemplate.None)
                return;

            List<string> changes = ItemAuthoringEditorUtility.BuildTemplateOverwritePreview(template);
            string message = "This will overwrite authored item fields:\n\n- "
                + string.Join("\n- ", changes)
                + "\n\nUse Fill Missing Defaults for the non-destructive path.";

            if (!EditorUtility.DisplayDialog("Reapply Item Template", message, "Overwrite Fields", "Cancel"))
                return;

            serializedObject.ApplyModifiedProperties();
            ItemAuthoringEditorUtility.ApplyTemplate(item, template, GetItemName(serializedObject, item));
            serializedObject.Update();
            ItemRegistry.ScheduleEditorSync();
        }

        // ----- Helpers -----

        private static ItemAuthoringTemplate GetAuthoringTemplate(SerializedObject serializedObject)
        {
            SerializedProperty templateProp = serializedObject.FindProperty("_authoringTemplate");
            return templateProp != null ? (ItemAuthoringTemplate)templateProp.enumValueIndex : ItemAuthoringTemplate.None;
        }

        private static string GetItemName(SerializedObject serializedObject, ItemComponent item)
        {
            SerializedProperty nameProp = serializedObject?.FindProperty("_itemName");
            if (nameProp != null && !string.IsNullOrWhiteSpace(nameProp.stringValue))
                return nameProp.stringValue;

            return item != null ? item.ItemName : string.Empty;
        }

        private static ItemType GetItemType(SerializedObject serializedObject)
        {
            SerializedProperty itemTypeProp = serializedObject.FindProperty("_itemType");
            return itemTypeProp != null ? (ItemType)itemTypeProp.enumValueIndex : ItemType.Material;
        }

        private static bool HasOrphanEquipmentData(SerializedObject serializedObject)
        {
            SerializedProperty damage = serializedObject.FindProperty("_damage");
            if (damage != null && damage.floatValue != 0f) return true;

            SerializedProperty defense = serializedObject.FindProperty("_defense");
            if (defense != null && defense.floatValue != 0f) return true;

            SerializedProperty bone = serializedObject.FindProperty("_equipBone");
            if (bone != null && !string.IsNullOrEmpty(bone.stringValue)) return true;

            SerializedProperty offset = serializedObject.FindProperty("_equipOffset");
            if (offset != null && offset.vector3Value != Vector3.zero) return true;

            SerializedProperty rotation = serializedObject.FindProperty("_equipRotation");
            if (rotation != null && rotation.vector3Value != Vector3.zero) return true;

            SerializedProperty slots = serializedObject.FindProperty("_allowedEquipSlots");
            if (slots != null && slots.arraySize > 0) return true;

            SerializedProperty domain = serializedObject.FindProperty("_equipDomain");
            if (domain != null && domain.enumValueIndex != (int)EquipDomain.Auto) return true;

            SerializedProperty handing = serializedObject.FindProperty("_weaponHanding");
            if (handing != null && handing.enumValueIndex != (int)WeaponHanding.OneHanded) return true;

            return false;
        }

        private static bool IsEquipmentRevealed(SerializedObject serializedObject)
        {
            int id = GetTargetId(serializedObject);
            return id != 0 && _equipmentRevealOverrides.Contains(id);
        }

        private static void SetEquipmentRevealed(SerializedObject serializedObject, bool revealed)
        {
            int id = GetTargetId(serializedObject);
            if (id == 0)
                return;

            if (revealed)
                _equipmentRevealOverrides.Add(id);
            else
                _equipmentRevealOverrides.Remove(id);
        }

        private static int GetTargetId(SerializedObject serializedObject)
        {
            return serializedObject != null && serializedObject.targetObject != null
                ? serializedObject.targetObject.GetInstanceID()
                : 0;
        }

        private static void DrawHiddenSectionNotice(string reason, Action onReveal)
        {
            EditorGUILayout.HelpBox(reason, MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reveal Section", GUILayout.Width(140f)))
                onReveal?.Invoke();
            EditorGUILayout.EndHorizontal();
        }

        private static bool BeginSection(string title, bool defaultOpen)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string key = FoldoutPrefPrefix + title;
            bool stored = EditorPrefs.GetBool(key, defaultOpen);

            GUIStyle style = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
            bool open = EditorGUILayout.Foldout(stored, title, true, style);
            if (open != stored)
                EditorPrefs.SetBool(key, open);

            return open;
        }

        private static void EndSection()
        {
            EditorGUILayout.EndVertical();
        }

        private static void DrawProperty(SerializedObject serializedObject, string propertyName, bool includeChildren = true)
        {
            SerializedProperty property = serializedObject?.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, includeChildren);
        }
    }
}
