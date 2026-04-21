using UnityEditor;
using Sol.Grab;

namespace Sol.Editor
{
    [CustomEditor(typeof(ItemComponent))]
    public class ItemComponentEditor : UnityEditor.Editor
    {
        private SerializedProperty _itemId;
        private SerializedProperty _itemName;
        private SerializedProperty _itemType;
        private SerializedProperty _value;
        private SerializedProperty _itemOwnerId;
        private SerializedProperty _isStolen;
        private SerializedProperty _flavourText;
        private SerializedProperty _icon;

        private SerializedProperty _isStackable;
        private SerializedProperty _isConsumable;
        private SerializedProperty _isTradeable;
        private SerializedProperty _maxStackSize;

        private SerializedProperty _damage;
        private SerializedProperty _defense;

        private SerializedProperty _equipBone;
        private SerializedProperty _equipOffset;
        private SerializedProperty _equipRotation;
        private SerializedProperty _equipDomain;
        private SerializedProperty _weaponHanding;
        private SerializedProperty _allowedEquipSlots;

        private SerializedProperty _pickupGrip;

        private void OnEnable()
        {
            _itemId = serializedObject.FindProperty("_itemId");
            _itemName = serializedObject.FindProperty("_itemName");
            _itemType = serializedObject.FindProperty("_itemType");
            _value = serializedObject.FindProperty("_value");
            _itemOwnerId = serializedObject.FindProperty("_itemOwnerId");
            _isStolen = serializedObject.FindProperty("_isStolen");
            _flavourText = serializedObject.FindProperty("_flavourText");
            _icon = serializedObject.FindProperty("_icon");

            _isStackable = serializedObject.FindProperty("_isStackable");
            _isConsumable = serializedObject.FindProperty("_isConsumable");
            _isTradeable = serializedObject.FindProperty("_isTradeable");
            _maxStackSize = serializedObject.FindProperty("_maxStackSize");

            _damage = serializedObject.FindProperty("_damage");
            _defense = serializedObject.FindProperty("_defense");

            _equipBone = serializedObject.FindProperty("_equipBone");
            _equipOffset = serializedObject.FindProperty("_equipOffset");
            _equipRotation = serializedObject.FindProperty("_equipRotation");
            _equipDomain = serializedObject.FindProperty("_equipDomain");
            _weaponHanding = serializedObject.FindProperty("_weaponHanding");
            _allowedEquipSlots = serializedObject.FindProperty("_allowedEquipSlots");

            _pickupGrip = serializedObject.FindProperty("_pickupGrip");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Item Info", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_itemId);
            EditorGUILayout.PropertyField(_itemName);
            EditorGUILayout.PropertyField(_itemType);
            EditorGUILayout.PropertyField(_value);
            EditorGUILayout.PropertyField(_itemOwnerId);
            EditorGUILayout.PropertyField(_isStolen);
            EditorGUILayout.PropertyField(_flavourText);
            EditorGUILayout.PropertyField(_icon);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_isStackable);
            EditorGUILayout.PropertyField(_isConsumable);
            EditorGUILayout.PropertyField(_isTradeable);
            EditorGUILayout.PropertyField(_maxStackSize);

            ItemType itemType = (ItemType)_itemType.enumValueIndex;
            if (ItemTypeRules.UsesWeaponStats(itemType))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Weapon Stats", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_damage);
            }

            if (ItemTypeRules.UsesArmorStats(itemType))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Armor Stats", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_defense);
            }

            if (ItemTypeRules.UsesEquipmentSettings(itemType))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Equipment", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_equipBone);
                EditorGUILayout.PropertyField(_equipOffset);
                EditorGUILayout.PropertyField(_equipRotation);
                EditorGUILayout.PropertyField(_equipDomain);
                EditorGUILayout.PropertyField(_weaponHanding);
                EditorGUILayout.PropertyField(_allowedEquipSlots, includeChildren: true);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Pickup", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_pickupGrip);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
