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

        // ----- Section: Basic -----

        private static void DrawBasicSection(SerializedObject serializedObject)
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

            DrawProperty(serializedObject, "_itemType");
            DrawProperty(serializedObject, "_value");
            DrawProperty(serializedObject, "_icon");
            DrawProperty(serializedObject, "_flavourText");

            EndSection();
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
                    "This prefab is missing MeshFilter / MeshRenderer. Click 'Fill Missing Defaults' above to add canonical components.",
                    MessageType.Info);
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

        private static void DrawAuthoringSection(SerializedObject serializedObject)
        {
            if (!BeginSection("Authoring", defaultOpen: false))
            {
                EndSection();
                return;
            }

            DrawProperty(serializedObject, "_authoringTemplate");
            DrawProperty(serializedObject, "_itemAuthoringNotes");

            EndSection();
        }

        // ----- Helpers -----

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
