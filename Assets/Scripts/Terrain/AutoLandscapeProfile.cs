using UnityEngine;

[CreateAssetMenu(menuName = "Fishbox/Terrain/Auto Landscape Profile", fileName = "AutoLandscapeProfile")]
public sealed class AutoLandscapeProfile : ScriptableObject
{
    [Header("Layer Roles")]
    public TerrainLayer beachLayer;
    public TerrainLayer baseLayer;
    public TerrainLayer dirtLayer;
    public TerrainLayer rockLayer;

    [Header("Water Level")]
    [Tooltip("Fallback water surface Y used when no WaterVolume is found in the open scene.")]
    public float waterLevelWorldY = 22.5f;

    [Header("Slope Thresholds")]
    [Range(0f, 90f)] public float rockSlope = 45f;
    [Range(0.001f, 20f)] public float rockFadeDegrees = 3f;

    [Header("Dirt Splotches")]
    [Min(0.0001f)] public float noiseScale = 0.04f;
    [Range(0f, 1f)] public float dirtCoverage = 0.45f;
    [Range(0f, 1f)] public float dirtOpacity = 0.85f;
    public int seed = 1729;

    public AutoLandscapeSettings ToSettings()
    {
        return new AutoLandscapeSettings
        {
            WaterLevelWorldY = waterLevelWorldY,
            RockSlope = rockSlope,
            RockFadeDegrees = rockFadeDegrees,
            NoiseScale = noiseScale,
            DirtCoverage = dirtCoverage,
            DirtOpacity = dirtOpacity,
            Seed = seed
        };
    }
}
