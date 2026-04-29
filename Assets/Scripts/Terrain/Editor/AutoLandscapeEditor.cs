#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AutoLandscapeEditor
{
    public const string DefaultProfilePath = "Assets/Scenes/SOL_Fishbox/AutoLandscapeProfile_SOL_Fishbox.asset";
    public const string TerrainPrefabPath = "Assets/Scenes/SOL_Fishbox/Terrain.prefab";

    [MenuItem("Tools/Fishbox/Terrain/Apply Auto Landscape")]
    public static void ApplyAutoLandscape()
    {
        AutoLandscapeProfile profile = LoadOrCreateDefaultProfile();
        Terrain terrain = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<Terrain>()
            : null;

        if (terrain == null)
            terrain = Object.FindFirstObjectByType<Terrain>();

        if (terrain == null)
        {
            GameObject terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerrainPrefabPath);
            if (terrainPrefab != null)
                terrain = terrainPrefab.GetComponent<Terrain>();
        }

        if (terrain == null)
        {
            EditorUtility.DisplayDialog(
                "Apply Auto Landscape",
                "No Terrain was selected or found in the open scene.",
                "OK");
            return;
        }

        ApplyAutoLandscape(terrain, profile);
    }

    [MenuItem("Tools/Fishbox/Terrain/Create Default Auto Landscape Profile")]
    public static void CreateDefaultProfileMenu()
    {
        AutoLandscapeProfile profile = LoadOrCreateDefaultProfile();
        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }

    public static void ApplyAutoLandscape(Terrain terrain, AutoLandscapeProfile profile)
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Apply Auto Landscape");
        float waterLevel = DetectWaterLevel(profile.waterLevelWorldY);
        AutoLandscapePainter.ApplySummary summary = AutoLandscapePainter.Apply(terrain, profile, waterLevel);
        EditorUtility.SetDirty(terrain.terrainData);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();

        string message =
            $"Painted {summary.SampleCount:N0} terrain samples.\n" +
            $"Sand: {summary.SandPercent:0.0}%\n" +
            $"Grass: {summary.GrassPercent:0.0}%\n" +
            $"Dirt: {summary.DirtPercent:0.0}%\n" +
            $"Rock: {summary.RockPercent:0.0}%\n\n" +
            $"Water level Y: {summary.WaterLevelWorldY:0.0}\n" +
            $"World Y range: {summary.MinWorldY:0.0} to {summary.MaxWorldY:0.0}\n" +
            $"Slope range: {summary.MinSlope:0.0} to {summary.MaxSlope:0.0}";
        Debug.Log($"Apply Auto Landscape\n{message}", terrain);
        EditorUtility.DisplayDialog("Apply Auto Landscape", message, "OK");
    }

    public static AutoLandscapeProfile LoadOrCreateDefaultProfile()
    {
        AutoLandscapeProfile profile = AssetDatabase.LoadAssetAtPath<AutoLandscapeProfile>(DefaultProfilePath);
        if (profile != null)
            return profile;

        profile = ScriptableObject.CreateInstance<AutoLandscapeProfile>();
        AssignDefaultLayers(profile);

        AssetDatabase.CreateAsset(profile, DefaultProfilePath);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static void AssignDefaultLayers(AutoLandscapeProfile profile)
    {
        profile.beachLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Scenes/SOL_Fishbox/Beach.terrainlayer");
        profile.baseLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Scenes/SOL_Fishbox/Base.terrainlayer");
        profile.dirtLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Scenes/SOL_Fishbox/Dirt.terrainlayer");
        profile.rockLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Scenes/SOL_Fishbox/Rock1.terrainlayer");
    }

    private static float DetectWaterLevel(float fallback)
    {
        WaterVolume waterVolume = Object.FindFirstObjectByType<WaterVolume>();
        if (waterVolume != null)
            return waterVolume.SurfaceY;

        SolWaterManager waterManager = Object.FindFirstObjectByType<SolWaterManager>();
        if (waterManager != null && !Mathf.Approximately(waterManager.waterLevel, 0f))
            return waterManager.waterLevel;

        return fallback;
    }
}
#endif
