using UnityEngine;
using Sol.AI;

namespace Sol.Grab
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ItemComponent))]
    public class CaughtFishItem : MonoBehaviour
    {
        [Header("Caught Fish")]
        [SerializeField] private string _speciesName = "Fish";
        [SerializeField] private string _prefix = string.Empty;
        [SerializeField] private FishRarity _rarity = FishRarity.Common;
        [SerializeField] private float _rarityPercent;
        [SerializeField] private float _size;
        [SerializeField] private float _weight;
        [SerializeField] private bool _isPredator;

        public string SpeciesName => _speciesName;
        public string Prefix => _prefix;
        public FishRarity Rarity => _rarity;
        public float RarityPercent => _rarityPercent;
        public float Size => _size;
        public float Weight => _weight;
        public bool IsPredator => _isPredator;

        public void ConfigureFromFish(AI_Fish fish)
        {
            if (fish == null)
                return;

            _speciesName = fish.SpeciesName;
            _prefix = fish.Prefix;
            _rarity = fish.Rarity;
            _rarityPercent = fish.RarityPercent;
            _size = fish.Size;
            _weight = fish.Weight;
            _isPredator = fish.IsPredator;

            ItemComponent item = GetComponent<ItemComponent>();
            if (item != null)
            {
                item.ConfigureRuntimeItem(
                    fish.FishName,
                    fish.Value,
                    BuildFlavourText(fish));
            }

            gameObject.name = fish.FishName;
        }

        private static string BuildFlavourText(AI_Fish fish)
        {
            string predatorDescriptor = fish.IsPredator ? "Predator" : "Non-predator";
            return $"{fish.Rarity} catch ({fish.RarityPercent:0.##}%). {predatorDescriptor}. Size {fish.Size:0.##}, Weight {fish.Weight:0.##}.";
        }
    }
}
