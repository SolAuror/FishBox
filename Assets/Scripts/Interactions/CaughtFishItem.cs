using UnityEngine;
using Sol.AI;
using Sol.Outline;

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
        [SerializeField] private Transform _visualRoot;

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

            transform.localScale = Vector3.one;
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

            RefreshVisual(fish);
            gameObject.name = fish.FishName;
        }

        private void RefreshVisual(AI_Fish fish)
        {
            Transform visualRoot = GetOrCreateVisualRoot();
            if (visualRoot == null)
                return;

            for (int i = visualRoot.childCount - 1; i >= 0; i--)
                Destroy(visualRoot.GetChild(i).gameObject);

            GameObject visualPrefab = fish.CatchVisualPrefab;
            if (visualPrefab == null)
                return;

            GameObject visualInstance = Instantiate(visualPrefab, visualRoot, false);
            visualInstance.name = visualPrefab.name;
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = fish.CatchVisualLocalScale;

            AI_Fish fishBehaviour = visualInstance.GetComponentInChildren<AI_Fish>(true);
            if (fishBehaviour != null)
                Destroy(fishBehaviour);

            foreach (ItemComponent item in visualInstance.GetComponentsInChildren<ItemComponent>(true))
                Destroy(item);

            foreach (GrabbableComponent grabbable in visualInstance.GetComponentsInChildren<GrabbableComponent>(true))
                Destroy(grabbable);

            foreach (CaughtFishItem caughtFish in visualInstance.GetComponentsInChildren<CaughtFishItem>(true))
            {
                if (caughtFish != this)
                    Destroy(caughtFish);
            }

            foreach (OutlineComponent outline in visualInstance.GetComponentsInChildren<OutlineComponent>(true))
                Destroy(outline);

            foreach (Collider collider in visualInstance.GetComponentsInChildren<Collider>(true))
                Destroy(collider);

            foreach (Rigidbody body in visualInstance.GetComponentsInChildren<Rigidbody>(true))
                Destroy(body);
        }

        private Transform GetOrCreateVisualRoot()
        {
            if (_visualRoot != null)
                return _visualRoot;

            Transform existing = transform.Find("VisualRoot");
            if (existing != null)
            {
                _visualRoot = existing;
                return _visualRoot;
            }

            GameObject rootObject = new("VisualRoot");
            _visualRoot = rootObject.transform;
            _visualRoot.SetParent(transform, false);
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one;
            return _visualRoot;
        }

        private static string BuildFlavourText(AI_Fish fish)
        {
            string predatorDescriptor = fish.IsPredator ? "Predator" : "Non-predator";
            return $"{fish.Rarity} catch ({fish.RarityPercent:0.##}%). {predatorDescriptor}. Size {fish.Size:0.##}, Weight {fish.Weight:0.##}.";
        }
    }
}
