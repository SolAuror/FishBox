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
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ItemIdDropdownAttribute config = (ItemIdDropdownAttribute)attribute;
            ReferenceDropdown.DrawItem(position, label, property, config?.AllowEmpty ?? true);
        }
    }

    [CustomPropertyDrawer(typeof(OwnerIdDropdownAttribute))]
    public sealed class OwnerIdDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            OwnerIdDropdownAttribute config = (OwnerIdDropdownAttribute)attribute;
            ReferenceDropdown.DrawNpc(position, label, property, config?.AllowEmpty ?? true);
        }
    }

    [CustomPropertyDrawer(typeof(NpcIdDropdownAttribute))]
    public sealed class NpcIdDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            NpcIdDropdownAttribute config = (NpcIdDropdownAttribute)attribute;
            ReferenceDropdown.DrawNpc(position, label, property, config?.AllowEmpty ?? true);
        }
    }

    [CustomPropertyDrawer(typeof(QuestIdDropdownAttribute))]
    public sealed class QuestIdDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            QuestIdDropdownAttribute config = (QuestIdDropdownAttribute)attribute;
            ReferenceDropdown.DrawQuest(position, label, property, config?.AllowEmpty ?? true);
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
                if (_ownerIdProp != null)
                    ReferenceDropdown.DrawNpcLayout(new GUIContent("Owner Id"), _ownerIdProp);
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
            _seedList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, $"Seed Contents ({_seedContentsProp.arraySize})", EditorStyles.boldLabel);
            _seedList.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
            _seedList.drawElementCallback = DrawSeedElement;
            _seedList.onAddCallback = list =>
            {
                int last = _seedContentsProp.arraySize;
                _seedContentsProp.arraySize = last + 1;
                SerializedProperty element = _seedContentsProp.GetArrayElementAtIndex(last);
                SerializedProperty idProp = element.FindPropertyRelative("_itemId");
                SerializedProperty qtyProp = element.FindPropertyRelative("Quantity");
                if (idProp != null) idProp.stringValue = string.Empty;
                if (qtyProp != null) qtyProp.intValue = 1;
            };
        }

        private void DrawSeedElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= _seedContentsProp.arraySize)
                return;

            SerializedProperty element = _seedContentsProp.GetArrayElementAtIndex(index);
            SerializedProperty idProp = element.FindPropertyRelative("_itemId");
            SerializedProperty qtyProp = element.FindPropertyRelative("Quantity");

            Rect row = new Rect(rect.x, rect.y + 3f, rect.width, EditorGUIUtility.singleLineHeight);
            float gap = 6f;
            float qtyWidth = 70f;
            float itemWidth = row.width - qtyWidth - gap;
            Rect itemRect = new Rect(row.x, row.y, itemWidth, row.height);
            Rect qtyRect = new Rect(row.x + itemWidth + gap, row.y, qtyWidth, row.height);

            if (idProp != null)
                ReferenceDropdown.DrawItem(itemRect, GUIContent.none, idProp);
            if (qtyProp != null)
                EditorGUI.PropertyField(qtyRect, qtyProp, GUIContent.none);
        }
    }
}
#endif
