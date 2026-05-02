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

    [Header("Runtime")]
    [Tooltip("Cull fish when they swim outside this volume. Disable when render-distance culling should own despawn.")]
    [SerializeField] bool constrainSpawnedFishToBounds = true;
    [Tooltip("Destroy owned fish when this volume is disabled.")]
    [SerializeField] bool clearSpawnedFishOnDisable = true;
    [Tooltip("Seconds between owned-fish bounds checks. Raising this smooths CPU cost for large schools.")]
    [SerializeField, Min(0.02f)] float boundsCullInterval = 0.25f;
    [Tooltip("Seconds between automatic refill checks after fish are culled.")]
    [SerializeField, Min(0.02f)] float fillInterval = 0.25f;
    [Tooltip("Maximum fish spawned by one automatic refill pass. Prevents single-frame instantiate spikes.")]
    [SerializeField, Min(1)] int maxFishSpawnsPerFill = 2;
    #endregion

    sealed class TrackedFish
    {
        public GameObject fish;
        public Transform transform;
        public AI_Fish fishAi;
        public Renderer[] renderers;
        public FishVolume owner;
        public float offscreenSince = -1f;
    }

    static readonly List<TrackedFish> s_TrackedFish = new();
    static readonly Dictionary<GameObject, TrackedFish> s_TrackedByFish = new();
    static readonly Plane[] s_FrustumPlanes = new Plane[6];
    static int s_HybridCullCursor;

    BoxCollider _col;
    readonly List<GameObject> _spawnedFish = new();
    float _nextBoundsCullTime;
    float _nextFillTime;

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

        if (constrainSpawnedFishToBounds && Time.time >= _nextBoundsCullTime)
        {
            _nextBoundsCullTime = Time.time + Mathf.Max(0.02f, boundsCullInterval);
            CullOutOfBounds();
        }

        if (Time.time >= _nextFillTime)
        {
            _nextFillTime = Time.time + Mathf.Max(0.02f, fillInterval);
            FillFish(maxFishSpawnsPerFill);
        }
    }

    void OnDisable()
    {
        if (clearSpawnedFishOnDisable)
            ClearSpawnedFish();
    }

    public void SetFishSpawnCount(int count)
    {
        fishSpawnCount = Mathf.Max(0, count);

        for (int i = _spawnedFish.Count - 1; i >= fishSpawnCount; i--)
        {
            GameObject fish = _spawnedFish[i];
            if (!CanDespawnFish(fish))
                continue;

            if (fish != null)
            {
                UntrackFish(fish);
                DestroyUnityObject(fish);
            }

            _spawnedFish.RemoveAt(i);
        }

        FillFish();
    }

    public static void RunHybridCulling(
        Camera cullCamera,
        Transform cullTarget,
        float nearKeepDistance,
        float farCullDistance,
        float offscreenCullDelay,
        int maxFishCheckedPerPass = int.MaxValue)
    {
        if (!Application.isPlaying || s_TrackedFish.Count == 0)
            return;

        Vector3 targetPosition;
        if (cullTarget != null)
            targetPosition = cullTarget.position;
        else if (cullCamera != null)
            targetPosition = cullCamera.transform.position;
        else
            return;

        Plane[] frustumPlanes = null;
        if (cullCamera != null)
        {
            GeometryUtility.CalculateFrustumPlanes(cullCamera, s_FrustumPlanes);
            frustumPlanes = s_FrustumPlanes;
        }

        float now = Time.time;
        float nearSqr = Mathf.Max(0f, nearKeepDistance) * Mathf.Max(0f, nearKeepDistance);
        float far = Mathf.Max(nearKeepDistance, farCullDistance);
        float farSqr = far * far;
        float delay = Mathf.Max(0f, offscreenCullDelay);
        int budget = Mathf.Clamp(maxFishCheckedPerPass, 1, s_TrackedFish.Count);

        for (int processed = 0; processed < budget && s_TrackedFish.Count > 0; processed++)
        {
            if (s_HybridCullCursor >= s_TrackedFish.Count)
                s_HybridCullCursor = 0;

            int i = s_HybridCullCursor;
            TrackedFish tracked = s_TrackedFish[i];
            if (tracked == null || tracked.fish == null || tracked.transform == null)
            {
                RemoveTrackedAt(i);
                continue;
            }

            AI_Fish fishAi = tracked.fishAi;
            if (fishAi != null && fishAi.IsCullProtected)
            {
                tracked.offscreenSince = -1f;
                s_HybridCullCursor++;
                continue;
            }

            float sqrDistance = (tracked.transform.position - targetPosition).sqrMagnitude;
            if (sqrDistance <= nearSqr)
            {
                tracked.offscreenSince = -1f;
                s_HybridCullCursor++;
                continue;
            }

            if (sqrDistance >= farSqr)
            {
                CullTrackedFish(i);
                continue;
            }

            if (frustumPlanes == null || IsVisibleToCamera(tracked, frustumPlanes))
            {
                tracked.offscreenSince = -1f;
                s_HybridCullCursor++;
                continue;
            }

            if (delay <= 0f)
            {
                CullTrackedFish(i);
                continue;
            }

            if (tracked.offscreenSince < 0f)
            {
                tracked.offscreenSince = now;
                s_HybridCullCursor++;
                continue;
            }

            if (now - tracked.offscreenSince >= delay)
            {
                CullTrackedFish(i);
                continue;
            }

            s_HybridCullCursor++;
        }
    }

    void OnValidate()
    {
        boundsCullInterval = Mathf.Max(0.02f, boundsCullInterval);
        fillInterval = Mathf.Max(0.02f, fillInterval);
        maxFishSpawnsPerFill = Mathf.Max(1, maxFishSpawnsPerFill);

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

        CleanupSpawnedFishList();
        Bounds bounds = _col.bounds;

        for (int i = _spawnedFish.Count - 1; i >= 0; i--)
        {
            GameObject fish = _spawnedFish[i];
            if (fish == null)
            {
                _spawnedFish.RemoveAt(i);
                continue;
            }

            AI_Fish fishBehaviour = GetTrackedFishAi(fish);
            if (fishBehaviour != null && fishBehaviour.IsCullProtected)
                continue;

            if (!bounds.Contains(fish.transform.position))
            {
                UntrackFish(fish);
                DestroyUnityObject(fish);
                _spawnedFish.RemoveAt(i);
            }
        }
    }

    void FillFish(int maxSpawnsThisPass = int.MaxValue)
    {
        CleanupSpawnedFishList();

        if (fishPrefabs == null || fishPrefabs.Length == 0)
            return;

        if (_col == null || waterVolume == null)
            return;

        Bounds bounds = _col.bounds;
        int attemptsPerFish = Mathf.Max(1, fishSpawnAttemptsPerFish);
        Transform parent = waterVolume != null
            ? waterVolume.transform
            : (fishParent != null ? fishParent : transform);
        int target = Mathf.Max(0, fishSpawnCount);
        int spawnBudget = Mathf.Max(1, maxSpawnsThisPass);
        int spawnedThisPass = 0;

        while (_spawnedFish.Count < target && spawnedThisPass < spawnBudget)
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

            TrackFish(fishInstance);
            spawnedThisPass++;
        }
    }

    void CleanupSpawnedFishList()
    {
        for (int i = _spawnedFish.Count - 1; i >= 0; i--)
        {
            if (_spawnedFish[i] == null)
                _spawnedFish.RemoveAt(i);
        }
    }

    static bool CanDespawnFish(GameObject fish)
    {
        if (fish == null)
            return true;

        AI_Fish fishAi = GetTrackedFishAi(fish);
        return fishAi == null || !fishAi.IsCullProtected;
    }

    static AI_Fish GetTrackedFishAi(GameObject fish)
    {
        if (fish == null)
            return null;

        if (s_TrackedByFish.TryGetValue(fish, out TrackedFish tracked))
            return tracked.fishAi;

        return fish.GetComponent<AI_Fish>();
    }

    void TrackFish(GameObject fish)
    {
        if (fish == null)
            return;

        if (s_TrackedByFish.TryGetValue(fish, out TrackedFish existing))
        {
            existing.owner?._spawnedFish.Remove(fish);
            existing.owner = this;
            existing.transform = fish.transform;
            existing.fishAi = fish.GetComponent<AI_Fish>();
            existing.renderers = fish.GetComponentsInChildren<Renderer>(false);
            existing.offscreenSince = -1f;
        }
        else
        {
            existing = new TrackedFish
            {
                fish = fish,
                transform = fish.transform,
                fishAi = fish.GetComponent<AI_Fish>(),
                renderers = fish.GetComponentsInChildren<Renderer>(false),
                owner = this
            };
            s_TrackedByFish.Add(fish, existing);
            s_TrackedFish.Add(existing);
        }

        if (!_spawnedFish.Contains(fish))
            _spawnedFish.Add(fish);
    }

    void UntrackFish(GameObject fish)
    {
        if (fish == null)
            return;

        if (!s_TrackedByFish.TryGetValue(fish, out TrackedFish tracked))
            return;

        s_TrackedByFish.Remove(fish);
        s_TrackedFish.Remove(tracked);
    }

    static void RemoveTrackedAt(int index)
    {
        TrackedFish tracked = s_TrackedFish[index];
        if (tracked != null && !ReferenceEquals(tracked.fish, null))
            s_TrackedByFish.Remove(tracked.fish);

        if (tracked?.owner != null)
            tracked.owner._spawnedFish.Remove(tracked.fish);

        s_TrackedFish.RemoveAt(index);
    }

    static void CullTrackedFish(int index)
    {
        TrackedFish tracked = s_TrackedFish[index];
        GameObject fish = tracked.fish;

        RemoveTrackedAt(index);

        if (fish != null)
            DestroyUnityObject(fish);
    }

    static bool IsVisibleToCamera(TrackedFish tracked, Plane[] frustumPlanes)
    {
        Bounds bounds = new Bounds(tracked.transform.position, Vector3.one);
        Renderer[] renderers = tracked.renderers;
        bool hasRenderer = false;

        for (int i = 0; renderers != null && i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasRenderer)
            {
                bounds = renderer.bounds;
                hasRenderer = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
    }

    static void DestroyUnityObject(Object obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
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
            {
                UntrackFish(fish);
                DestroyUnityObject(fish);
            }
        }

        _spawnedFish.Clear();
    }
}
