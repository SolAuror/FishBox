#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

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
#endif
