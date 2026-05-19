using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.AI {
    /// <summary>
    /// Stores per-instance data for a uniquely caught fish, such as size, weight, rarity, etc.
    /// Used with FishRegistry and referenced by a unique code (e.g., FSH12345).
    /// </summary>
    [Serializable]
    public class CaughtFishData {
        // Unique registry-assigned code (FSH12345)
        public string fishCode;
        // Which base fish this is (ScriptableObject — not JSON-serializable; use speciesDisplayName for persistence)
        public FishDefinition species;
        // Plain-text species name, safe to serialize
        public string speciesDisplayName;
        // ScriptableObject asset name (.name), used to re-link modelPrefab after JSON load
        public string speciesAssetName;
        // Value computed at catch time (avoids needing FishDefinition at load time)
        public int cachedValue;
        // Visual scale computed at catch time (Vector3 survives JsonUtility; FishDefinition ref does not)
        public Vector3 catchVisualScale = Vector3.one;
        // Runtime-only model prefab reference — not serialized, repopulated after load via speciesAssetName
        [System.NonSerialized] public GameObject modelPrefab;
        // Caught properties
        public float size;
        public float weight;
        public FishRarity rarity;
        public float rarityPercent;
        public string prefix;
        public bool isPredator;
        public List<string> tagPaths = new();

        public CaughtFishData(string code, FishDefinition species, float size, float weight, FishRarity rarity, float rarityPercent, string prefix, bool isPredator) {
            this.fishCode = code;
            this.species = species;
            this.size = size;
            this.weight = weight;
            this.rarity = rarity;
            this.rarityPercent = rarityPercent;
            this.prefix = prefix;
            this.isPredator = isPredator;
        }
    }
}
