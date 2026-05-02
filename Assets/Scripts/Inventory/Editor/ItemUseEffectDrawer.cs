using Sol.Grab;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    [CustomPropertyDrawer(typeof(ItemUseEffect))]
    public sealed class ItemUseEffectDrawer : PropertyDrawer
    {
        private const float Gap = 6f;
        private const float HintWidth = 68f;
        private const float AmountWidth = 72f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty effectType = property.FindPropertyRelative("_effectType");
            SerializedProperty amount = property.FindPropertyRelative("_amount");

            EditorGUI.BeginProperty(position, label, property);
            Rect content = EditorGUI.PrefixLabel(position, label);
            float typeWidth = Mathf.Max(80f, content.width - AmountWidth - HintWidth - Gap * 2f);
            Rect typeRect = new(content.x, content.y, typeWidth, EditorGUIUtility.singleLineHeight);
            Rect amountRect = new(typeRect.xMax + Gap, content.y, AmountWidth, EditorGUIUtility.singleLineHeight);
            Rect hintRect = new(amountRect.xMax + Gap, content.y, HintWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(typeRect, effectType, GUIContent.none);
            EditorGUI.PropertyField(amountRect, amount, GUIContent.none);
            EditorGUI.LabelField(hintRect, ResolveHint(effectType), EditorStyles.miniLabel);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        private static string ResolveHint(SerializedProperty effectType)
        {
            if (effectType == null)
                return string.Empty;

            ItemUseEffectType type = (ItemUseEffectType)effectType.enumValueIndex;
            return type == ItemUseEffectType.HealHealthPercent || type == ItemUseEffectType.RestoreStaminaPercent
                ? "% max"
                : "points";
        }
    }
}
