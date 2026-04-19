using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.AI {
    /// <summary>
    /// Runtime registry holding all uniquely-caught fish this session, keyed by random 5-digit ID (FSH12345).
    /// Supports registration, lookup, and removal (when fish are consumed or removed from the world).
    /// </summary>
    public class FishRegistry {
        private static FishRegistry _instance;
        public static FishRegistry Instance => _instance ??= new FishRegistry();

        private readonly Dictionary<string, CaughtFishData> _fishDict = new();
        private readonly System.Random _rng = new();

        /// <summary>Get all registered fish codes/data.</summary>
        public IReadOnlyDictionary<string, CaughtFishData> All => _fishDict;

        /// <summary>
        /// Register a new caught fish. Generates a unique random code. Returns the code.
        /// </summary>
        public string RegisterFish(CaughtFishData fishData) {
            const int maxAttempts = 500;
            string code = string.Empty;

            for (int i = 0; i < maxAttempts; i++) {
                string candidate = "FSH" + _rng.Next(10000, 100000).ToString();
                if (!_fishDict.ContainsKey(candidate)) {
                    code = candidate;
                    break;
                }
            }

            if (string.IsNullOrEmpty(code)) {
                // Extremely unlikely (>89k fish registered). Fall back to a timestamp-seeded code.
                code = "FSH" + (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 90000 + 10000);
                Debug.LogWarning($"[FishRegistry] Code space near capacity. Used fallback code: {code}");
            }

            fishData.fishCode = code;
            _fishDict[code] = fishData;
            return code;
        }

        /// <summary>Look up fish data by unique code. Returns null if not found.</summary>
        public CaughtFishData GetFish(string code) {
            _fishDict.TryGetValue(code, out var data);
            return data;
        }

        /// <summary>Remove a fish entry permanently after consumption or destruction.</summary>
        public bool RemoveFish(string code) => _fishDict.Remove(code);

        /// <summary>For Save/Load integration: return all data for serialization.</summary>
        public IEnumerable<CaughtFishData> GetAllFishData() => _fishDict.Values;

        /// <summary>For Save/Load integration: bulk load previously saved caught fish (clears current registry first!).</summary>
        public void LoadRegistry(IEnumerable<CaughtFishData> allFish) {
            _fishDict.Clear();
            foreach (var fish in allFish) {
                if (!string.IsNullOrEmpty(fish.fishCode))
                    _fishDict[fish.fishCode] = fish;
            }
        }

        /// <summary>
        /// After loading the registry from JSON, FishDefinition references are null.
        /// Call this after LoadRegistry to re-link modelPrefab on each entry by matching
        /// speciesAssetName against all FishDefinition assets currently loaded in memory.
        /// </summary>
        public void RepopulateRuntimeRefs()
        {
            FishDefinition[] defs = Resources.FindObjectsOfTypeAll<FishDefinition>();
            foreach (CaughtFishData fishData in _fishDict.Values)
            {
                if (fishData.modelPrefab != null || string.IsNullOrEmpty(fishData.speciesAssetName))
                    continue;

                for (int i = 0; i < defs.Length; i++)
                {
                    if (defs[i] != null && defs[i].name == fishData.speciesAssetName)
                    {
                        fishData.modelPrefab = defs[i].modelPrefab;
                        break;
                    }
                }
            }
        }

        /// <summary>For testing/debugging: forcibly clear the registry (should only be used for dev mode).</summary>
        public void Clear() => _fishDict.Clear();
    }
}
