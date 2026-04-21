#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Sol.Grab;
using Sol.Quests;

namespace Sol.AI.Editor
{
    public static class FishDefinitionAssetGenerator
    {
        [MenuItem("Assets/Sol/Fishing/Create Fish Definitions From Selection", priority = 2100)]
        private static void CreateDefinitionsFromSelection()
        {
            Object[] selection = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets);
            if (selection.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Create Fish Definitions",
                    "Select one or more fish model prefabs or FBX assets in the Project window first.",
                    "OK");
                return;
            }

            string firstAssetPath = AssetDatabase.GetAssetPath(selection[0]);
            string parentFolder = Path.GetDirectoryName(firstAssetPath)?.Replace("\\", "/") ?? "Assets";
            string definitionsFolder = AssetDatabase.IsValidFolder($"{parentFolder}/FishDefinitions")
                ? $"{parentFolder}/FishDefinitions"
                : AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(parentFolder, "FishDefinitions"));

            int createdCount = 0;
            int skippedCount = 0;

            foreach (Object selectedObject in selection)
            {
                if (selectedObject is not GameObject modelPrefab)
                {
                    skippedCount++;
                    continue;
                }

                string assetName = $"{modelPrefab.name}Definition.asset";
                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{definitionsFolder}/{assetName}");

                var definition = ScriptableObject.CreateInstance<FishDefinition>();
                definition.fishName = ObjectNames.NicifyVariableName(modelPrefab.name);
                definition.modelPrefab = modelPrefab;

                AssetDatabase.CreateAsset(definition, assetPath);
                createdCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Create Fish Definitions",
                $"Created {createdCount} fish definition asset(s). Skipped {skippedCount}.",
                "OK");
        }

        [MenuItem("Assets/Sol/Fishing/Create Fish Definitions From Selection", true)]
        private static bool ValidateCreateDefinitionsFromSelection()
        {
            return Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets).Length > 0;
        }
    }
}

namespace Sol.Editor
{
    [CustomPropertyDrawer(typeof(ItemIdDropdownAttribute))]
    public sealed class ItemIdDropdownDrawer : PropertyDrawer
    {
        private const string NoneLabel = "<None>";
        private const string MissingLabelPrefix = "<Missing>";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            ItemIdDropdownAttribute config = (ItemIdDropdownAttribute)attribute;
            List<ItemOption> options = GatherItemOptions();
            if (options.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            List<ItemOption> choices = new List<ItemOption>(options.Count + 1);
            if (config.AllowEmpty)
                choices.Add(new ItemOption { ItemId = string.Empty, Display = NoneLabel });
            choices.AddRange(options);

            string current = property.stringValue ?? string.Empty;
            int currentIndex = choices.FindIndex(option =>
                string.Equals(option.ItemId, current, System.StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                string missingDisplay = string.IsNullOrEmpty(current)
                    ? NoneLabel
                    : $"{MissingLabelPrefix} {current}";
                choices.Add(new ItemOption { ItemId = current, Display = missingDisplay });
                currentIndex = choices.Count - 1;
            }

            string[] display = new string[choices.Count];
            for (int i = 0; i < choices.Count; i++)
                display[i] = choices[i].Display;

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            int selected = EditorGUI.Popup(position, label.text, currentIndex, display);
            EditorGUI.showMixedValue = false;
            if (selected >= 0 && selected < choices.Count)
                property.stringValue = choices[selected].ItemId;
            EditorGUI.EndProperty();
        }

        private static List<ItemOption> GatherItemOptions()
        {
            List<ItemOption> items = new List<ItemOption>();
            ItemRegistry registry = ItemRegistry.Get();
            if (registry == null || registry.Entries == null)
                return items;

            HashSet<string> seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < registry.Entries.Count; i++)
            {
                ItemRegistry.Entry entry = registry.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId))
                    continue;

                string id = entry.ItemId.Trim();
                if (!seen.Add(id))
                    continue;

                string itemName = entry.Prefab != null && !string.IsNullOrWhiteSpace(entry.Prefab.ItemName)
                    ? entry.Prefab.ItemName.Trim()
                    : id;

                items.Add(new ItemOption
                {
                    ItemId = id,
                    Display = itemName
                });
            }

            items.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.Display, b.Display));
            return items;
        }

        private struct ItemOption
        {
            public string ItemId;
            public string Display;
        }
    }

    [CustomPropertyDrawer(typeof(OwnerIdDropdownAttribute))]
    public sealed class OwnerIdDropdownDrawer : PropertyDrawer
    {
        private const string NoneLabel = "<None>";
        private const string MissingLabelPrefix = "<Missing>";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            OwnerIdDropdownAttribute config = (OwnerIdDropdownAttribute)attribute;
            List<OwnerChoice> choices = BuildOwnerChoices(config.AllowEmpty);
            if (choices.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            string current = property.stringValue ?? string.Empty;
            int currentIndex = -1;
            for (int i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i].Id, current, System.StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                choices.Add(new OwnerChoice
                {
                    Id = current,
                    Display = string.IsNullOrWhiteSpace(current) ? NoneLabel : $"{MissingLabelPrefix} {current}"
                });
                currentIndex = choices.Count - 1;
            }

            string[] display = new string[choices.Count];
            for (int i = 0; i < choices.Count; i++)
                display[i] = choices[i].Display;

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            int selected = EditorGUI.Popup(position, label.text, currentIndex, display);
            EditorGUI.showMixedValue = false;
            if (selected >= 0 && selected < choices.Count)
                property.stringValue = choices[selected].Id;
            EditorGUI.EndProperty();
        }

        private static List<OwnerChoice> BuildOwnerChoices(bool allowEmpty)
        {
            List<OwnerChoice> choices = new List<OwnerChoice>();
            HashSet<string> seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            if (allowEmpty)
            {
                choices.Add(new OwnerChoice { Id = string.Empty, Display = NoneLabel });
                seen.Add(string.Empty);
            }

            choices.Add(new OwnerChoice
            {
                Id = EntityCodeUtility.DefaultPlayerOwnerId,
                Display = "Player (PLY00001)"
            });
            seen.Add(EntityCodeUtility.DefaultPlayerOwnerId);

            Sol.AI.NPCSoul[] npcSouls = Object.FindObjectsByType<Sol.AI.NPCSoul>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < npcSouls.Length; i++)
            {
                Sol.AI.NPCSoul owner = npcSouls[i];
                if (owner == null || string.IsNullOrWhiteSpace(owner.OwnerId) || !seen.Add(owner.OwnerId))
                    continue;

                string ownerName = string.IsNullOrWhiteSpace(owner.CharacterName) ? owner.gameObject.name : owner.CharacterName;
                choices.Add(new OwnerChoice
                {
                    Id = owner.OwnerId,
                    Display = $"{ownerName} ({owner.OwnerId})"
                });
            }

            List<OwnerRegistry.OwnerEntry> runtimeEntries = OwnerRegistry.GetEntries();
            for (int i = 0; i < runtimeEntries.Count; i++)
            {
                OwnerRegistry.OwnerEntry entry = runtimeEntries[i];
                if (string.IsNullOrWhiteSpace(entry.OwnerId) || !seen.Add(entry.OwnerId))
                    continue;

                string ownerName = entry.Owner != null ? entry.Owner.name : "Owner";
                choices.Add(new OwnerChoice
                {
                    Id = entry.OwnerId,
                    Display = $"{ownerName} ({entry.OwnerId})"
                });
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                Sol.AI.NPCSoul owner = prefab.GetComponentInChildren<Sol.AI.NPCSoul>(true);
                if (owner == null || string.IsNullOrWhiteSpace(owner.OwnerId) || !seen.Add(owner.OwnerId))
                    continue;

                string ownerName = string.IsNullOrWhiteSpace(owner.CharacterName) ? prefab.name : owner.CharacterName;
                choices.Add(new OwnerChoice
                {
                    Id = owner.OwnerId,
                    Display = $"{ownerName} ({owner.OwnerId})"
                });
            }

            choices.Sort((a, b) =>
            {
                bool aPinned = string.IsNullOrEmpty(a.Id) || string.Equals(a.Id, EntityCodeUtility.DefaultPlayerOwnerId, System.StringComparison.OrdinalIgnoreCase);
                bool bPinned = string.IsNullOrEmpty(b.Id) || string.Equals(b.Id, EntityCodeUtility.DefaultPlayerOwnerId, System.StringComparison.OrdinalIgnoreCase);
                if (aPinned && bPinned) return 0;
                if (aPinned) return -1;
                if (bPinned) return 1;
                return System.StringComparer.OrdinalIgnoreCase.Compare(a.Display, b.Display);
            });

            return choices;
        }

        private struct OwnerChoice
        {
            public string Id;
            public string Display;
        }
    }

    [CustomPropertyDrawer(typeof(NpcIdDropdownAttribute))]
    public sealed class NpcIdDropdownDrawer : PropertyDrawer
    {
        private const string NoneLabel = "<None>";
        private const string MissingLabelPrefix = "<Missing>";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            NpcIdDropdownAttribute config = (NpcIdDropdownAttribute)attribute;
            List<NpcChoice> choices = BuildNpcChoices(config.AllowEmpty);
            if (choices.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            string current = property.stringValue ?? string.Empty;
            int currentIndex = choices.FindIndex(choice =>
                string.Equals(choice.Id, current, System.StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                choices.Add(new NpcChoice
                {
                    Id = current,
                    Display = string.IsNullOrWhiteSpace(current) ? NoneLabel : $"{MissingLabelPrefix} {current}"
                });
                currentIndex = choices.Count - 1;
            }

            string[] display = new string[choices.Count];
            for (int i = 0; i < choices.Count; i++)
                display[i] = choices[i].Display;

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            int selected = EditorGUI.Popup(position, label.text, currentIndex, display);
            EditorGUI.showMixedValue = false;
            if (selected >= 0 && selected < choices.Count)
                property.stringValue = choices[selected].Id;
            EditorGUI.EndProperty();
        }

        private static List<NpcChoice> BuildNpcChoices(bool allowEmpty)
        {
            List<NpcChoice> choices = new List<NpcChoice>();
            HashSet<string> seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            if (allowEmpty)
            {
                choices.Add(new NpcChoice { Id = string.Empty, Display = NoneLabel });
                seen.Add(string.Empty);
            }

            Sol.AI.NPCSoul[] npcSouls = Object.FindObjectsByType<Sol.AI.NPCSoul>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < npcSouls.Length; i++)
            {
                Sol.AI.NPCSoul soul = npcSouls[i];
                if (soul == null || string.IsNullOrWhiteSpace(soul.OwnerId) || !seen.Add(soul.OwnerId))
                    continue;

                string displayName = string.IsNullOrWhiteSpace(soul.CharacterName) ? soul.gameObject.name : soul.CharacterName;
                choices.Add(new NpcChoice
                {
                    Id = soul.OwnerId,
                    Display = $"{displayName} ({soul.OwnerId})"
                });
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                Sol.AI.NPCSoul soul = prefab.GetComponentInChildren<Sol.AI.NPCSoul>(true);
                if (soul == null || string.IsNullOrWhiteSpace(soul.OwnerId) || !seen.Add(soul.OwnerId))
                    continue;

                string displayName = string.IsNullOrWhiteSpace(soul.CharacterName) ? prefab.name : soul.CharacterName;
                choices.Add(new NpcChoice
                {
                    Id = soul.OwnerId,
                    Display = $"{displayName} ({soul.OwnerId})"
                });
            }

            choices.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.Display, b.Display));
            return choices;
        }

        private struct NpcChoice
        {
            public string Id;
            public string Display;
        }
    }

    [CustomPropertyDrawer(typeof(QuestIdDropdownAttribute))]
    public sealed class QuestIdDropdownDrawer : PropertyDrawer
    {
        private const string NoneLabel = "<None>";
        private const string MissingLabelPrefix = "<Missing>";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            QuestIdDropdownAttribute config = (QuestIdDropdownAttribute)attribute;
            List<QuestChoice> choices = GatherQuestChoices(config.AllowEmpty);
            if (choices.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            string current = property.stringValue ?? string.Empty;
            int currentIndex = choices.FindIndex(choice =>
                string.Equals(choice.Id, current, System.StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                choices.Add(new QuestChoice
                {
                    Id = current,
                    Display = string.IsNullOrWhiteSpace(current) ? NoneLabel : $"{MissingLabelPrefix} {current}"
                });
                currentIndex = choices.Count - 1;
            }

            string[] display = new string[choices.Count];
            for (int i = 0; i < choices.Count; i++)
                display[i] = choices[i].Display;

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            int selected = EditorGUI.Popup(position, label.text, currentIndex, display);
            EditorGUI.showMixedValue = false;
            if (selected >= 0 && selected < choices.Count)
                property.stringValue = choices[selected].Id;
            EditorGUI.EndProperty();
        }

        private static List<QuestChoice> GatherQuestChoices(bool allowEmpty)
        {
            List<QuestChoice> choices = new List<QuestChoice>();
            HashSet<string> seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            if (allowEmpty)
            {
                choices.Add(new QuestChoice { Id = string.Empty, Display = NoneLabel });
                seen.Add(string.Empty);
            }

            QuestRegistry registry = QuestRegistry.Get();
            if (registry != null && registry.Quests != null)
            {
                for (int i = 0; i < registry.Quests.Count; i++)
                {
                    QuestDefinition def = registry.Quests[i];
                    if (def == null || string.IsNullOrWhiteSpace(def.QuestId))
                        continue;

                    string id = def.QuestId.Trim();
                    if (!seen.Add(id))
                        continue;

                    string title = string.IsNullOrWhiteSpace(def.Title) ? id : def.Title.Trim();
                    choices.Add(new QuestChoice
                    {
                        Id = id,
                        Display = $"{title} ({id})"
                    });
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:QuestDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                QuestDefinition def = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if (def == null || string.IsNullOrWhiteSpace(def.QuestId))
                    continue;

                string id = def.QuestId.Trim();
                if (!seen.Add(id))
                    continue;

                string title = string.IsNullOrWhiteSpace(def.Title) ? id : def.Title.Trim();
                choices.Add(new QuestChoice
                {
                    Id = id,
                    Display = $"{title} ({id})"
                });
            }

            choices.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.Display, b.Display));
            return choices;
        }

        private struct QuestChoice
        {
            public string Id;
            public string Display;
        }
    }

    [CustomEditor(typeof(Inventory))]
    public sealed class InventoryEditor : UnityEditor.Editor
    {
        private ReorderableList _seedList;
        private SerializedProperty _capacityProp;
        private SerializedProperty _goldProp;
        private SerializedProperty _containerTypeProp;
        private SerializedProperty _seedContentsProp;
        private SerializedProperty _ownerProp;
        private SerializedProperty _ownerIdProp;
        private SerializedProperty _isLockedProp;
        private SerializedProperty _isLockpickableProp;
        private SerializedProperty _lockLevelProp;
        private SerializedProperty _requiredKeyItemNameProp;
        private SerializedProperty _requiredKeyItemIdProp;

        private void OnEnable()
        {
            _capacityProp = serializedObject.FindProperty("_capacity");
            _goldProp = serializedObject.FindProperty("_gold");
            _containerTypeProp = serializedObject.FindProperty("_containerType");
            _seedContentsProp = serializedObject.FindProperty("_inspectorContents");
            _ownerProp = serializedObject.FindProperty("_owner");
            _ownerIdProp = serializedObject.FindProperty("_ownerId");
            _isLockedProp = serializedObject.FindProperty("_isLocked");
            _isLockpickableProp = serializedObject.FindProperty("_isLockpickable");
            _lockLevelProp = serializedObject.FindProperty("_lockLevel");
            _requiredKeyItemNameProp = serializedObject.FindProperty("_requiredKeyItemName");
            _requiredKeyItemIdProp = serializedObject.FindProperty("_requiredKeyItemId");

            BuildSeedList();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_capacityProp);
            EditorGUILayout.PropertyField(_goldProp);
            EditorGUILayout.PropertyField(_containerTypeProp);

            EditorGUILayout.Space(6f);
            if (_seedList != null)
                _seedList.DoLayoutList();

            bool isWorldContainer = _containerTypeProp.enumValueIndex == (int)InventoryContainerType.Container;
            if (isWorldContainer)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("World Container Security", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_ownerProp);
                DrawOwnerIdDropdown(_ownerIdProp, "Owner Id");
                EditorGUILayout.PropertyField(_isLockedProp);
                EditorGUILayout.PropertyField(_isLockpickableProp);
                EditorGUILayout.PropertyField(_lockLevelProp);
                EditorGUILayout.PropertyField(_requiredKeyItemNameProp);
                EditorGUILayout.PropertyField(_requiredKeyItemIdProp);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void BuildSeedList()
        {
            if (_seedContentsProp == null)
                return;

            _seedList = new ReorderableList(serializedObject, _seedContentsProp, true, true, true, true);
            _seedList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Inspector Contents");
            _seedList.elementHeight = EditorGUIUtility.singleLineHeight * 2f + 8f;
            _seedList.drawElementCallback = DrawSeedElement;
        }

        private void DrawSeedElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = _seedContentsProp.GetArrayElementAtIndex(index);
            SerializedProperty itemProp = element.FindPropertyRelative("Item");
            SerializedProperty quantityProp = element.FindPropertyRelative("Quantity");

            Rect itemRect = new Rect(rect.x, rect.y + 2f, rect.width, EditorGUIUtility.singleLineHeight);
            Rect qtyRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight + 6f, rect.width, EditorGUIUtility.singleLineHeight);

            DrawItemPrefabDropdown(itemRect, itemProp, "Item");
            EditorGUI.PropertyField(qtyRect, quantityProp);
        }

        private static void DrawItemPrefabDropdown(Rect rect, SerializedProperty itemProp, string label)
        {
            ItemRegistry registry = ItemRegistry.Get();
            if (registry == null || registry.Entries == null || registry.Entries.Count == 0)
            {
                EditorGUI.PropertyField(rect, itemProp, new GUIContent(label));
                return;
            }

            List<ItemChoice> choices = BuildItemChoices(registry);
            ItemComponent current = itemProp.objectReferenceValue as ItemComponent;
            int currentIndex = FindItemChoiceIndex(choices, current);
            if (currentIndex < 0)
            {
                choices.Add(new ItemChoice
                {
                    Id = "<missing>",
                    Name = current != null ? current.name : "<None>",
                    Prefab = current,
                    Display = current != null ? $"<Missing> {current.name}" : "<None>"
                });
                currentIndex = choices.Count - 1;
            }

            string[] display = new string[choices.Count];
            for (int i = 0; i < choices.Count; i++)
                display[i] = choices[i].Display;

            int selected = EditorGUI.Popup(rect, label, currentIndex, display);
            if (selected >= 0 && selected < choices.Count)
                itemProp.objectReferenceValue = choices[selected].Prefab;
        }

        private static List<ItemChoice> BuildItemChoices(ItemRegistry registry)
        {
            List<ItemChoice> choices = new List<ItemChoice>
            {
                new ItemChoice
                {
                    Id = string.Empty,
                    Name = "<None>",
                    Prefab = null,
                    Display = "<None>"
                }
            };

            for (int i = 0; i < registry.Entries.Count; i++)
            {
                ItemRegistry.Entry entry = registry.Entries[i];
                if (entry == null || entry.Prefab == null || string.IsNullOrWhiteSpace(entry.ItemId))
                    continue;

                string id = entry.ItemId.Trim();
                string name = string.IsNullOrWhiteSpace(entry.Prefab.ItemName) ? entry.Prefab.name : entry.Prefab.ItemName.Trim();
                choices.Add(new ItemChoice
                {
                    Id = id,
                    Name = name,
                    Prefab = entry.Prefab,
                    Display = $"{name} ({id})"
                });
            }

            choices.Sort((a, b) =>
            {
                bool aNone = a.Prefab == null;
                bool bNone = b.Prefab == null;
                if (aNone && bNone) return 0;
                if (aNone) return -1;
                if (bNone) return 1;
                return System.StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            });

            return choices;
        }

        private static int FindItemChoiceIndex(List<ItemChoice> choices, ItemComponent current)
        {
            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i].Prefab == current)
                    return i;
            }

            return -1;
        }

        private static void DrawOwnerIdDropdown(SerializedProperty ownerIdProp, string label)
        {
            List<OwnerChoice> choices = BuildOwnerChoices();
            string current = ownerIdProp.stringValue ?? string.Empty;

            int currentIndex = -1;
            for (int i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i].Id, current, System.StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                choices.Add(new OwnerChoice
                {
                    Id = current,
                    Display = string.IsNullOrWhiteSpace(current) ? "<None>" : $"<Missing> {current}"
                });
                currentIndex = choices.Count - 1;
            }

            string[] display = new string[choices.Count];
            for (int i = 0; i < choices.Count; i++)
                display[i] = choices[i].Display;

            int selected = EditorGUILayout.Popup(label, currentIndex, display);
            if (selected >= 0 && selected < choices.Count)
                ownerIdProp.stringValue = choices[selected].Id;
        }

        private static List<OwnerChoice> BuildOwnerChoices()
        {
            List<OwnerChoice> choices = new List<OwnerChoice>
            {
                new OwnerChoice { Id = string.Empty, Display = "<None>" },
                new OwnerChoice { Id = EntityCodeUtility.DefaultPlayerOwnerId, Display = "Player (PLY00001)" }
            };

            HashSet<string> seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                string.Empty,
                EntityCodeUtility.DefaultPlayerOwnerId
            };

            Sol.AI.NPCSoul[] npcSouls = Object.FindObjectsByType<Sol.AI.NPCSoul>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < npcSouls.Length; i++)
            {
                Sol.AI.NPCSoul owner = npcSouls[i];
                if (owner == null || string.IsNullOrWhiteSpace(owner.OwnerId) || !seen.Add(owner.OwnerId))
                    continue;

                choices.Add(new OwnerChoice
                {
                    Id = owner.OwnerId,
                    Display = $"{owner.CharacterName} ({owner.OwnerId})"
                });
            }

            var runtimeEntries = OwnerRegistry.GetEntries();
            for (int i = 0; i < runtimeEntries.Count; i++)
            {
                var entry = runtimeEntries[i];
                if (string.IsNullOrWhiteSpace(entry.OwnerId) || !seen.Add(entry.OwnerId))
                    continue;

                string ownerName = entry.Owner != null ? entry.Owner.name : "Owner";
                choices.Add(new OwnerChoice
                {
                    Id = entry.OwnerId,
                    Display = $"{ownerName} ({entry.OwnerId})"
                });
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                Sol.AI.NPCSoul owner = prefab.GetComponentInChildren<Sol.AI.NPCSoul>(true);
                if (owner == null || string.IsNullOrWhiteSpace(owner.OwnerId) || !seen.Add(owner.OwnerId))
                    continue;

                choices.Add(new OwnerChoice
                {
                    Id = owner.OwnerId,
                    Display = $"{owner.CharacterName} ({owner.OwnerId})"
                });
            }

            choices.Sort((a, b) =>
            {
                bool aPinned = string.IsNullOrEmpty(a.Id) || string.Equals(a.Id, EntityCodeUtility.DefaultPlayerOwnerId, System.StringComparison.OrdinalIgnoreCase);
                bool bPinned = string.IsNullOrEmpty(b.Id) || string.Equals(b.Id, EntityCodeUtility.DefaultPlayerOwnerId, System.StringComparison.OrdinalIgnoreCase);
                if (aPinned && bPinned) return 0;
                if (aPinned) return -1;
                if (bPinned) return 1;
                return System.StringComparer.OrdinalIgnoreCase.Compare(a.Display, b.Display);
            });

            return choices;
        }

        private struct ItemChoice
        {
            public string Id;
            public string Name;
            public ItemComponent Prefab;
            public string Display;
        }

        private struct OwnerChoice
        {
            public string Id;
            public string Display;
        }
    }
}
#endif
