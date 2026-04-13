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

        [Header("Visuals")]
        public GameObject modelPrefab;
        [Min(0.01f)] public float modelScaleMultiplier = 1f;

        private void OnValidate()
        {
            maxSize = Mathf.Max(minSize, maxSize);
            maxWeight = Mathf.Max(minWeight, maxWeight);
            baitInterestMultiplier = Mathf.Max(0.1f, baitInterestMultiplier);
            modelScaleMultiplier = Mathf.Max(0.01f, modelScaleMultiplier);
        }
    }
}
