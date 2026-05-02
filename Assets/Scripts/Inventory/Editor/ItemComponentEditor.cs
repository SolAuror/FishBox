using Sol.Grab;
using UnityEditor;

namespace Sol.Editor
{
    [CustomEditor(typeof(ItemComponent))]
    public class ItemComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ItemComponentEditorUtility.DrawItemInspector(serializedObject, target as ItemComponent);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
