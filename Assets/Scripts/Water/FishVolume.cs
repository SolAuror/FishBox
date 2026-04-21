using System.Collections.Generic;
using UnityEngine;
using Sol.AI;

[System.Serializable]
public class FishSpawnEntry
{
    public FishDefinition definition;
    [Min(0f)] public float spawnChance = 1f;
}

[RequireComponent(typeof(BoxCollider))]
public class FishVolume : MonoBehaviour
{
    #region Inspector Settings
    [Header("References")]
    [Tooltip("Inspector: tunes water volume.")]
    [SerializeField] WaterVolume waterVolume;
    [Tooltip("Inspector: tunes fish parent.")]
    [SerializeField] Transform fishParent;

    [Header("Fish")]
    [Tooltip("Inspector: tunes fish prefabs.")]
    [SerializeField] GameObject[] fishPrefabs;
    [SerializeField] FishDefinition[] fishDefinitions;
    [Tooltip("Inspector: tunes fish spawn entries.")]
    [SerializeField] FishSpawnEntry[] fishSpawnEntries;
    [SerializeField] int fishSpawnCount = 8;
    [Tooltip("Inspector: tunes fish spawn attempts per fish.")]
    [SerializeField] int fishSpawnAttemptsPerFish = 12;

    [Header("Spawn Bounds")]
    [Tooltip("Inspector: tunes edge padding.")]
    [SerializeField] float edgePadding = 1f;
    [SerializeField] float surfacePadding = 0.75f;
    [Tooltip("Inspector: tunes bottom padding.")]
    [SerializeField] float bottomPadding = 0.5f;
    [Tooltip("Inspector: tunes terrain clearance.")]
    [SerializeField] float terrainClearance = 0.4f;

    [Header("Follow Player")]
    [Tooltip("Inspector: tunes follow player.")]
    [SerializeField] bool followPlayer = true;
    [Tooltip("Inspector: tunes player.")]
    [SerializeField] Transform player;
    #endregion

    BoxCollider _col;
    readonly List<GameObject> _spawnedFish = new();

    void Awake()
    {
        _col = GetComponent<BoxCollider>();
        ResolveWaterVolume();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    void Start()
    {
        ResolveWaterVolume();
        FillFish();
    }

    void Update()
    {
        if (followPlayer && player != null)
        {
            Vector3 pos = transform.position;
            pos.x = player.position.x;
            pos.z = player.position.z;
            transform.position = pos;
        }

        CullOutOfBounds();
        FillFish();
    }

    void OnDisable()
    {
        ClearSpawnedFish();
    }

    void OnValidate()
    {
        if (_col == null)
            _col = GetComponent<BoxCollider>();

        ResolveWaterVolume();
    }

    void ResolveWaterVolume()
    {
        if (waterVolume != null)
            return;

        waterVolume = GetComponent<WaterVolume>();
        if (waterVolume != null)
            return;

        waterVolume = WaterVolume.FindVolumeXZ(transform.position);
    }

    void CullOutOfBounds()
    {
        if (_col == null)
            return;

        Bounds bounds = _col.bounds;

        for (int i = _spawnedFish.Count - 1; i >= 0; i--)
        {
            GameObject fish = _spawnedFish[i];
            if (fish == null)
            {
                _spawnedFish.RemoveAt(i);
                continue;
            }

            if (!bounds.Contains(fish.transform.position))
            {
                Destroy(fish);
                _spawnedFish.RemoveAt(i);
            }
        }
    }

    void FillFish()
    {
        if (fishPrefabs == null || fishPrefabs.Length == 0)
            return;

        if (_col == null || waterVolume == null)
            return;

        Bounds bounds = _col.bounds;
        int attemptsPerFish = Mathf.Max(1, fishSpawnAttemptsPerFish);
        Transform parent = fishParent != null ? fishParent : transform;
        int target = Mathf.Max(0, fishSpawnCount);

        while (_spawnedFish.Count < target)
        {
            GameObject prefab = GetRandomFishPrefab();
            if (prefab == null)
                break;

            if (!TryGetRandomFishSelection(out FishDefinition definition, out float spawnChanceNormalized, out float rarestSpeciesChanceNormalized, out float mostCommonSpeciesChanceNormalized))
                break;

            bool foundSpawn = false;
            Vector3 spawnPosition = bounds.center;

            for (int attempt = 0; attempt < attemptsPerFish; attempt++)
            {
                if (!TryGetSpawnPosition(bounds, out spawnPosition))
                    continue;

                foundSpawn = true;
                break;
            }

            if (!foundSpawn)
                break;

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject fishInstance = Instantiate(prefab, spawnPosition, rotation, parent);

            if (definition != null && fishInstance.TryGetComponent(out AI_Fish fishAi))
            {
                fishAi.ApplyDefinition(
                    definition,
                    spawnChanceNormalized,
                    rarestSpeciesChanceNormalized,
                    mostCommonSpeciesChanceNormalized);
            }

            _spawnedFish.Add(fishInstance);
        }
    }

    GameObject GetRandomFishPrefab()
    {
        for (int attempt = 0; attempt < fishPrefabs.Length; attempt++)
        {
            GameObject prefab = fishPrefabs[Random.Range(0, fishPrefabs.Length)];
            if (prefab != null)
                return prefab;
        }

        return null;
    }

    bool TryGetRandomFishSelection(
        out FishDefinition definition,
        out float spawnChanceNormalized,
        out float rarestSpeciesChanceNormalized,
        out float mostCommonSpeciesChanceNormalized)
    {
        if (TryGetWeightedFishSelection(
            fishSpawnEntries,
            out definition,
            out spawnChanceNormalized,
            out rarestSpeciesChanceNormalized,
            out mostCommonSpeciesChanceNormalized))
        {
            return true;
        }

        if (fishDefinitions == null || fishDefinitions.Length == 0)
        {
            definition = null;
            spawnChanceNormalized = 0f;
            rarestSpeciesChanceNormalized = 0f;
            mostCommonSpeciesChanceNormalized = 0f;
            return false;
        }

        int validCount = 0;
        for (int i = 0; i < fishDefinitions.Length; i++)
        {
            if (fishDefinitions[i] != null)
                validCount++;
        }

        if (validCount == 0)
        {
            definition = null;
            spawnChanceNormalized = 0f;
            rarestSpeciesChanceNormalized = 0f;
            mostCommonSpeciesChanceNormalized = 0f;
            return false;
        }

        float equalChance = 1f / validCount;

        for (int attempt = 0; attempt < fishDefinitions.Length; attempt++)
        {
            definition = fishDefinitions[Random.Range(0, fishDefinitions.Length)];
            if (definition != null)
            {
                spawnChanceNormalized = equalChance;
                rarestSpeciesChanceNormalized = equalChance;
                mostCommonSpeciesChanceNormalized = equalChance;
                return true;
            }
        }

        definition = null;
        spawnChanceNormalized = 0f;
        rarestSpeciesChanceNormalized = 0f;
        mostCommonSpeciesChanceNormalized = 0f;
        return false;
    }

    bool TryGetWeightedFishSelection(
        FishSpawnEntry[] entries,
        out FishDefinition definition,
        out float spawnChanceNormalized,
        out float rarestSpeciesChanceNormalized,
        out float mostCommonSpeciesChanceNormalized)
    {
        definition = null;
        spawnChanceNormalized = 0f;
        rarestSpeciesChanceNormalized = 0f;
        mostCommonSpeciesChanceNormalized = 0f;

        if (entries == null || entries.Length == 0)
            return false;

        float totalChance = 0f;
        float minChance = float.MaxValue;
        float maxChance = 0f;

        for (int i = 0; i < entries.Length; i++)
        {
            FishSpawnEntry entry = entries[i];
            if (entry == null || entry.definition == null || entry.spawnChance <= 0f)
                continue;

            totalChance += entry.spawnChance;
            minChance = Mathf.Min(minChance, entry.spawnChance);
            maxChance = Mathf.Max(maxChance, entry.spawnChance);
        }

        if (totalChance <= 0f)
            return false;

        float roll = Random.Range(0f, totalChance);
        float cumulativeChance = 0f;

        for (int i = 0; i < entries.Length; i++)
        {
            FishSpawnEntry entry = entries[i];
            if (entry == null || entry.definition == null || entry.spawnChance <= 0f)
                continue;

            cumulativeChance += entry.spawnChance;
            if (roll > cumulativeChance)
                continue;

            definition = entry.definition;
            spawnChanceNormalized = entry.spawnChance / totalChance;
            rarestSpeciesChanceNormalized = minChance / totalChance;
            mostCommonSpeciesChanceNormalized = maxChance / totalChance;
            return true;
        }

        return false;
    }

    bool TryGetSpawnPosition(Bounds bounds, out Vector3 spawnPosition)
    {
        spawnPosition = bounds.center;

        float minX = bounds.min.x + edgePadding;
        float maxX = bounds.max.x - edgePadding;
        float minZ = bounds.min.z + edgePadding;
        float maxZ = bounds.max.z - edgePadding;

        if (minX > maxX)
            minX = maxX = bounds.center.x;

        if (minZ > maxZ)
            minZ = maxZ = bounds.center.z;

        spawnPosition.x = Random.Range(minX, maxX);
        spawnPosition.z = Random.Range(minZ, maxZ);

        float minY = bounds.min.y + bottomPadding;
        if (TryGetTerrainHeight(spawnPosition, out float terrainHeight))
            minY = Mathf.Max(minY, terrainHeight + terrainClearance);

        float maxY = Mathf.Min(bounds.max.y - surfacePadding, waterVolume.GetSurfaceHeight(spawnPosition) - surfacePadding);
        if (maxY <= minY)
            return false;

        spawnPosition.y = Random.Range(minY, maxY);
        return true;
    }

    bool TryGetTerrainHeight(Vector3 worldPos, out float terrainHeight)
    {
        terrainHeight = float.MinValue;
        bool foundTerrain = false;

        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            bool insideTerrainXZ =
                worldPos.x >= terrainPosition.x &&
                worldPos.x <= terrainPosition.x + terrainSize.x &&
                worldPos.z >= terrainPosition.z &&
                worldPos.z <= terrainPosition.z + terrainSize.z;

            if (!insideTerrainXZ)
                continue;

            float sampledHeight = terrain.SampleHeight(worldPos) + terrainPosition.y;
            if (!foundTerrain || sampledHeight > terrainHeight)
                terrainHeight = sampledHeight;

            foundTerrain = true;
        }

        return foundTerrain;
    }

    void ClearSpawnedFish()
    {
        for (int i = _spawnedFish.Count - 1; i >= 0; i--)
        {
            GameObject fish = _spawnedFish[i];
            if (fish != null)
                Destroy(fish);
        }

        _spawnedFish.Clear();
    }
}
