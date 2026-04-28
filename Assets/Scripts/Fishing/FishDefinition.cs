using UnityEngine;

namespace Sol.AI
{
    [CreateAssetMenu(fileName = "FishDefinition", menuName = "Sol/Fishing/Fish Definition")]
    public class FishDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string fishName = "Fish";
        [Tooltip("Fallback tier used when the fish is spawned outside a weighted fish volume.")]
        public FishRarity rarity = FishRarity.Common;
        public bool isPredator;
        [Tooltip("Species baseline value before size and spawn rarity multipliers are applied.")]
        [Min(0)] public int baseValue = 10;
        public GameObject catchItemPrefab;

        [Header("Stats")]
        [Min(0.01f)] public float minSize = 0.5f;
        [Min(0.01f)] public float maxSize = 1.5f;
        [Min(0f)] public float minWeight = 0.2f;
        [Min(0f)] public float maxWeight = 5f;
        [Min(0f)] public float catchDifficulty = 1f;
        [Min(0.1f)] public float baitInterestMultiplier = 1f;

        [Header("Fight")]
        [Tooltip("Min seconds between struggle bursts at full stamina.")]
        [Min(0.1f)] public float struggleIntervalMin = 3.5f;
        [Tooltip("Max seconds between struggle bursts at full stamina.")]
        [Min(0.1f)] public float struggleIntervalMax = 6.5f;
        [Tooltip("Min struggle burst length.")]
        [Min(0.1f)] public float struggleDurationMin = 0.8f;
        [Tooltip("Max struggle burst length.")]
        [Min(0.1f)] public float struggleDurationMax = 2.2f;
        [Tooltip("Multiplier on tension rise during struggle. Larger / predator fish should set this higher.")]
        [Min(0f)] public float struggleStrength = 1f;
        [Tooltip("Seconds of full-strength fighting before stamina is exhausted.")]
        [Min(1f)] public float fightStaminaDuration = 18f;

        [Header("Bait Preference")]
        [Tooltip("Leave blank to treat any bait as neutral, with no favorite-bait bonus.")]
        public string favoriteBaitItemId = string.Empty;
        [Tooltip("Optional fallback bait name match when no specific item id is set.")]
        public string favoriteBaitName = string.Empty;
        [Min(1f)] public float favoriteBaitLureMultiplier = 1f;
        [Min(1f)] public float favoriteBaitCatchMultiplier = 1f;

        [Header("Visuals")]
        public GameObject modelPrefab;
        [Min(0.01f)] public float modelScaleMultiplier = 1f;

        private void OnValidate()
        {
            maxSize = Mathf.Max(minSize, maxSize);
            maxWeight = Mathf.Max(minWeight, maxWeight);
            baitInterestMultiplier = Mathf.Max(0.1f, baitInterestMultiplier);
            favoriteBaitLureMultiplier = Mathf.Max(1f, favoriteBaitLureMultiplier);
            favoriteBaitCatchMultiplier = Mathf.Max(1f, favoriteBaitCatchMultiplier);
            modelScaleMultiplier = Mathf.Max(0.01f, modelScaleMultiplier);
            struggleIntervalMax = Mathf.Max(struggleIntervalMin, struggleIntervalMax);
            struggleDurationMax = Mathf.Max(struggleDurationMin, struggleDurationMax);
        }
    }
}
