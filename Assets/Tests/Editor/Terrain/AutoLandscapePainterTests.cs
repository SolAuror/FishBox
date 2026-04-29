using NUnit.Framework;
using UnityEngine;

public sealed class AutoLandscapePainterTests
{
    [Test]
    public void EvaluateWeights_NormalizesEverySample()
    {
        AutoLandscapeSettings settings = DefaultSettings();

        for (int heightStep = 0; heightStep <= 10; heightStep++)
        {
            for (int slopeStep = 0; slopeStep <= 9; slopeStep++)
            {
                AutoLandscapePainter.Weights weights = AutoLandscapePainter.EvaluateWeights(settings, new AutoLandscapePainter.Sample
                {
                    NormalizedHeight = heightStep / 10f,
                    SlopeDegrees = slopeStep * 10f,
                    WorldY = 25f + heightStep,
                    WorldX = heightStep * 13f,
                    WorldZ = slopeStep * -17f
                });

                Assert.That(weights.Sum, Is.EqualTo(1f).Within(0.0001f));
            }
        }
    }

    [Test]
    public void EvaluateWeights_BelowWorldY30FlatSampleFavorsSand()
    {
        AutoLandscapePainter.Weights weights = AutoLandscapePainter.EvaluateWeights(DefaultSettings(), new AutoLandscapePainter.Sample
        {
            NormalizedHeight = 0.9f,
            SlopeDegrees = 1f,
            WorldY = 22f,
            WorldX = 0f,
            WorldZ = 0f
        });

        Assert.That(weights.Beach, Is.GreaterThan(weights.Base));
        Assert.That(weights.Beach, Is.GreaterThan(weights.Dirt));
        Assert.That(weights.Beach, Is.GreaterThan(weights.Rock));
    }

    [Test]
    public void EvaluateWeights_SteepSampleFavorsRock()
    {
        AutoLandscapePainter.Weights weights = AutoLandscapePainter.EvaluateWeights(DefaultSettings(), new AutoLandscapePainter.Sample
        {
            NormalizedHeight = 0.35f,
            SlopeDegrees = 46f,
            WorldY = 20f,
            WorldX = 10f,
            WorldZ = 20f
        });

        Assert.That(weights.Rock, Is.GreaterThan(weights.Beach));
        Assert.That(weights.Rock, Is.GreaterThan(weights.Base));
        Assert.That(weights.Rock, Is.GreaterThan(weights.Dirt));
    }

    [Test]
    public void EvaluateWeights_MidFlatSampleFavorsBase()
    {
        AutoLandscapePainter.Weights weights = AutoLandscapePainter.EvaluateWeights(DefaultSettings(), new AutoLandscapePainter.Sample
        {
            NormalizedHeight = 0.35f,
            SlopeDegrees = 2f,
            WorldY = 25f,
            WorldX = 40f,
            WorldZ = 80f
        });

        Assert.That(weights.Base, Is.GreaterThan(weights.Beach));
        Assert.That(weights.Base, Is.GreaterThan(weights.Rock));
    }

    [Test]
    public void Apply_WritesNormalizedAlphamap()
    {
        TerrainData terrainData = new TerrainData
        {
            heightmapResolution = 33,
            alphamapResolution = 16,
            size = new Vector3(32f, 12f, 32f),
            terrainLayers = CreateTerrainLayers()
        };

        GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        AutoLandscapeProfile profile = ScriptableObject.CreateInstance<AutoLandscapeProfile>();
        profile.beachLayer = terrainData.terrainLayers[0];
        profile.baseLayer = terrainData.terrainLayers[1];
        profile.dirtLayer = terrainData.terrainLayers[2];
        profile.rockLayer = terrainData.terrainLayers[3];

        try
        {
            AutoLandscapePainter.Apply(terrainObject.GetComponent<Terrain>(), profile);
            float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);

            Assert.That(alphamaps.GetLength(0), Is.EqualTo(16));
            Assert.That(alphamaps.GetLength(1), Is.EqualTo(16));
            Assert.That(alphamaps.GetLength(2), Is.EqualTo(4));

            for (int x = 0; x < terrainData.alphamapWidth; x++)
            {
                for (int y = 0; y < terrainData.alphamapHeight; y++)
                {
                    float sum = 0f;
                    for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
                        sum += alphamaps[x, y, layer];

                    Assert.That(sum, Is.EqualTo(1f).Within(0.0001f));
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(terrainObject);
            Object.DestroyImmediate(terrainData);
        }
    }

    private static AutoLandscapeSettings DefaultSettings()
    {
        var settings = new AutoLandscapeSettings
        {
            WaterLevelWorldY = 22.5f,
            RockSlope = 45f,
            RockFadeDegrees = 3f,
            NoiseScale = 0.018f,
            DirtCoverage = 0.3f,
            DirtOpacity = 0.55f,
            Seed = 1729
        };
        settings.Sanitize();
        return settings;
    }

    private static TerrainLayer[] CreateTerrainLayers()
    {
        return new[]
        {
            CreateTerrainLayer("Beach"),
            CreateTerrainLayer("Base"),
            CreateTerrainLayer("Dirt"),
            CreateTerrainLayer("Rock1")
        };
    }

    private static TerrainLayer CreateTerrainLayer(string layerName)
    {
        return new TerrainLayer
        {
            name = layerName,
            diffuseTexture = Texture2D.whiteTexture,
            tileSize = Vector2.one
        };
    }
}
