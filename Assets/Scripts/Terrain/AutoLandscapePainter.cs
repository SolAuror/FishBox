using System;
using UnityEngine;

public static class AutoLandscapePainter
{
    public struct ApplySummary
    {
        public int SampleCount;
        public int SandDominant;
        public int GrassDominant;
        public int DirtDominant;
        public int RockDominant;
        public float MinWorldY;
        public float MaxWorldY;
        public float MinSlope;
        public float MaxSlope;
        public float WaterLevelWorldY;

        public float SandPercent => Percent(SandDominant);
        public float GrassPercent => Percent(GrassDominant);
        public float DirtPercent => Percent(DirtDominant);
        public float RockPercent => Percent(RockDominant);

        private float Percent(int value)
        {
            return SampleCount > 0 ? value * 100f / SampleCount : 0f;
        }
    }

    public struct Sample
    {
        public float NormalizedHeight;
        public float SlopeDegrees;
        public float WorldY;
        public float WorldX;
        public float WorldZ;
    }

    public struct Weights
    {
        public float Beach;
        public float Base;
        public float Dirt;
        public float Rock;

        public float Sum => Beach + Base + Dirt + Rock;

        public void Normalize()
        {
            float sum = Sum;
            if (sum <= 0.0001f)
            {
                Beach = 0f;
                Base = 1f;
                Dirt = 0f;
                Rock = 0f;
                return;
            }

            Beach /= sum;
            Base /= sum;
            Dirt /= sum;
            Rock /= sum;
        }
    }

    private const float SeedStride = 37.719f;

    public static Weights EvaluateWeights(AutoLandscapeSettings settings, Sample sample)
    {
        settings.Sanitize();

        float slope = Mathf.Clamp(sample.SlopeDegrees, 0f, 90f);
        float noise = Mathf.PerlinNoise(
            (sample.WorldX + settings.Seed * SeedStride) * settings.NoiseScale,
            (sample.WorldZ - settings.Seed * SeedStride) * settings.NoiseScale);

        if (slope > settings.RockSlope)
        {
            return new Weights
            {
                Beach = 0f,
                Base = 0f,
                Dirt = 0f,
                Rock = 1f
            };
        }

        if (sample.WorldY < settings.WaterLevelWorldY)
        {
            return new Weights
            {
                Beach = 1f,
                Base = 0f,
                Dirt = 0f,
                Rock = 0f
            };
        }

        float rockBlend = SmoothBand(settings.RockSlope - settings.RockFadeDegrees, settings.RockSlope, slope);
        float walkable = Mathf.Clamp01(1f - rockBlend);

        float highNoise = Mathf.PerlinNoise(
            (sample.WorldX - settings.Seed * SeedStride) * settings.NoiseScale * 2.7f,
            (sample.WorldZ + settings.Seed * SeedStride) * settings.NoiseScale * 2.7f);
        float splotchNoise = noise * 0.7f + highNoise * 0.3f;
        float dirtThreshold = 1f - settings.DirtCoverage;
        float dirtSplotch = SmoothBand(dirtThreshold - 0.08f, dirtThreshold + 0.08f, splotchNoise);
        float dirt = walkable * dirtSplotch * settings.DirtOpacity;
        float baseWeight = Mathf.Max(0f, walkable - dirt);

        var weights = new Weights
        {
            Beach = 0f,
            Base = baseWeight,
            Dirt = dirt,
            Rock = rockBlend
        };
        weights.Normalize();
        return weights;
    }

    public static ApplySummary Apply(Terrain terrain, AutoLandscapeProfile profile)
    {
        return Apply(terrain, profile, float.NaN);
    }

    public static ApplySummary Apply(Terrain terrain, AutoLandscapeProfile profile, float waterLevelWorldYOverride)
    {
        if (terrain == null)
            throw new ArgumentNullException(nameof(terrain));
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
        if (terrain.terrainData == null)
            throw new ArgumentException("Terrain has no TerrainData.", nameof(terrain));

        TerrainData terrainData = terrain.terrainData;
        TerrainLayer[] terrainLayers = terrainData.terrainLayers;
        if (terrainLayers == null || terrainLayers.Length == 0)
            throw new InvalidOperationException("TerrainData must have at least one TerrainLayer.");

        LayerIndices indices = ResolveLayerIndices(terrainLayers, profile);
        AutoLandscapeSettings settings = profile.ToSettings();
        if (!float.IsNaN(waterLevelWorldYOverride))
            settings.WaterLevelWorldY = waterLevelWorldYOverride;
        settings.Sanitize();

        int width = terrainData.alphamapWidth;
        int height = terrainData.alphamapHeight;
        int layerCount = terrainLayers.Length;
        float[,,] alphamaps = new float[width, height, layerCount];

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;
        ApplySummary summary = new ApplySummary
        {
            MinWorldY = float.MaxValue,
            MaxWorldY = float.MinValue,
            MinSlope = float.MaxValue,
            MaxSlope = float.MinValue,
            WaterLevelWorldY = settings.WaterLevelWorldY
        };

        for (int y = 0; y < height; y++)
        {
            float normalizedZ = height > 1 ? y / (float)(height - 1) : 0f;
            for (int x = 0; x < width; x++)
            {
                float normalizedX = width > 1 ? x / (float)(width - 1) : 0f;
                float terrainHeight = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
                var sample = new Sample
                {
                    NormalizedHeight = Mathf.Clamp01(terrainHeight / terrainSize.y),
                    SlopeDegrees = terrainData.GetSteepness(normalizedX, normalizedZ),
                    WorldY = terrainPosition.y + terrainHeight,
                    WorldX = terrainPosition.x + normalizedX * terrainSize.x,
                    WorldZ = terrainPosition.z + normalizedZ * terrainSize.z
                };

                Weights weights = EvaluateWeights(settings, sample);
                alphamaps[x, y, indices.Beach] += weights.Beach;
                alphamaps[x, y, indices.Base] += weights.Base;
                alphamaps[x, y, indices.Dirt] += weights.Dirt;
                alphamaps[x, y, indices.Rock] += weights.Rock;
                NormalizeCell(alphamaps, x, y, layerCount);
                AddSummarySample(ref summary, sample, weights);
            }
        }

        terrainData.SetAlphamaps(0, 0, alphamaps);
        terrain.Flush();
        return summary;
    }

    private static LayerIndices ResolveLayerIndices(TerrainLayer[] layers, AutoLandscapeProfile profile)
    {
        int baseIndex = FindLayerIndex(layers, profile.baseLayer, "Base", 0);
        return new LayerIndices
        {
            Beach = FindLayerIndex(layers, profile.beachLayer, "Beach", baseIndex),
            Base = baseIndex,
            Dirt = FindLayerIndex(layers, profile.dirtLayer, "Dirt", baseIndex),
            Rock = FindLayerIndex(layers, profile.rockLayer, "Rock1", baseIndex)
        };
    }

    private static int FindLayerIndex(TerrainLayer[] layers, TerrainLayer preferred, string fallbackName, int fallbackIndex)
    {
        if (preferred != null)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == preferred)
                    return i;
            }
        }

        for (int i = 0; i < layers.Length; i++)
        {
            TerrainLayer layer = layers[i];
            if (layer != null && string.Equals(layer.name, fallbackName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return Mathf.Clamp(fallbackIndex, 0, layers.Length - 1);
    }

    private static float SmoothBand(float min, float max, float value)
    {
        if (Mathf.Approximately(min, max))
            return value >= max ? 1f : 0f;

        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));
    }

    private static void NormalizeCell(float[,,] alphamaps, int x, int y, int layerCount)
    {
        float sum = 0f;
        for (int layer = 0; layer < layerCount; layer++)
            sum += alphamaps[x, y, layer];

        if (sum <= 0.0001f)
        {
            alphamaps[x, y, 0] = 1f;
            return;
        }

        for (int layer = 0; layer < layerCount; layer++)
            alphamaps[x, y, layer] /= sum;
    }

    private static void AddSummarySample(ref ApplySummary summary, Sample sample, Weights weights)
    {
        summary.SampleCount++;
        summary.MinWorldY = Mathf.Min(summary.MinWorldY, sample.WorldY);
        summary.MaxWorldY = Mathf.Max(summary.MaxWorldY, sample.WorldY);
        summary.MinSlope = Mathf.Min(summary.MinSlope, sample.SlopeDegrees);
        summary.MaxSlope = Mathf.Max(summary.MaxSlope, sample.SlopeDegrees);

        if (weights.Rock >= weights.Beach && weights.Rock >= weights.Base && weights.Rock >= weights.Dirt)
            summary.RockDominant++;
        else if (weights.Dirt >= weights.Beach && weights.Dirt >= weights.Base)
            summary.DirtDominant++;
        else if (weights.Beach >= weights.Base)
            summary.SandDominant++;
        else
            summary.GrassDominant++;
    }

    private struct LayerIndices
    {
        public int Beach;
        public int Base;
        public int Dirt;
        public int Rock;
    }
}

[Serializable]
public struct AutoLandscapeSettings
{
    public float WaterLevelWorldY;
    public float RockSlope;
    public float RockFadeDegrees;
    public float NoiseScale;
    public float DirtCoverage;
    public float DirtOpacity;
    public int Seed;

    public void Sanitize()
    {
        RockSlope = Mathf.Clamp(RockSlope, 0f, 90f);
        RockFadeDegrees = Mathf.Max(0.001f, RockFadeDegrees);
        NoiseScale = Mathf.Max(0.0001f, NoiseScale);
        DirtCoverage = Mathf.Clamp01(DirtCoverage);
        DirtOpacity = Mathf.Clamp01(DirtOpacity);
    }
}
