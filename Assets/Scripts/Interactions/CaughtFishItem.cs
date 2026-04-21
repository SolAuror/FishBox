using UnityEngine;
using Sol.AI;
using Sol.Outline;

namespace Sol.Grab
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ItemComponent))]
    public class CaughtFishItem : MonoBehaviour
    {
        #region Inspector Settings
        [Header("Species")]
        [Tooltip("Set this on the catch item prefab to match its FishDefinition. Required for visual restore after save/load.")]
        [SerializeField] private FishDefinition _fishDefinition;

        [Header("Caught Fish")]
        [SerializeField] private string _speciesName = "Fish";
        [SerializeField] private string _prefix = string.Empty;
        [SerializeField] private FishRarity _rarity = FishRarity.Common;
        [SerializeField] private float _rarityPercent;
        [SerializeField] private float _size;
        [SerializeField] private float _weight;
        [Tooltip("Inspector: tunes is predator.")]
        [SerializeField] private bool _isPredator;
        [SerializeField] private Transform _visualRoot;
        [Tooltip("Inspector: tunes fish code.")]
        [SerializeField] private string _fishCode;
        #endregion

        public string FishCode { get => _fishCode; set => _fishCode = value; }
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

            // Capture definition so visual can be restored after save/load even if prefab field was not set
            if (fish.Definition != null)
                _fishDefinition = fish.Definition;

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

            RefreshVisual(fish.CatchVisualPrefab, fish.CatchVisualLocalScale);
            gameObject.name = fish.FishName;
        }

        private void RefreshVisual(GameObject visualPrefab, Vector3 localScale)
        {
            Transform visualRoot = GetOrCreateVisualRoot();
            if (visualRoot == null)
                return;

            for (int i = visualRoot.childCount - 1; i >= 0; i--)
                Destroy(visualRoot.GetChild(i).gameObject);

            if (visualPrefab == null)
                return;

            GameObject visualInstance = Instantiate(visualPrefab, visualRoot, false);
            visualInstance.name = visualPrefab.name;
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = localScale;

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

        public void ConfigureFromData(CaughtFishData data)
        {
            if (data == null)
                return;

            // Match ConfigureFromFish: the catch item prefab is authored at a non-unit root scale
            // (e.g. 0.25) for editor convenience, but the runtime contract is that the item root is
            // scale 1 and all visible sizing lives on the inner visualInstance via catchVisualScale.
            transform.localScale = Vector3.one;

            _speciesName = data.speciesDisplayName;
            _prefix = data.prefix;
            _rarity = data.rarity;
            _rarityPercent = data.rarityPercent;
            _size = data.size;
            _weight = data.weight;
            _isPredator = data.isPredator;
            _fishCode = data.fishCode;

            string fishName = string.IsNullOrWhiteSpace(_prefix) ? _speciesName : $"{_prefix} {_speciesName}";
            ItemComponent item = GetComponent<ItemComponent>();
            if (item != null && data.cachedValue > 0)
            {
                string flavour = $"{_rarity} catch ({_rarityPercent:0.##}%). {(_isPredator ? "Predator" : "Non-predator")}. Size {_size:0.##}, Weight {_weight:0.##}.";
                item.ConfigureRuntimeItem(fishName, data.cachedValue, flavour);
            }

            gameObject.name = fishName;

            // Resolve the visual mesh by looking up the FishDefinition referenced in the save data
            // (via speciesAssetName, re-linked by FishRegistry.RepopulateRuntimeRefs). Falls back to
            // the prefab-assigned _fishDefinition if the registry lookup didn't resolve.
            GameObject visualPrefab = data.modelPrefab;
            if (visualPrefab == null && _fishDefinition != null)
                visualPrefab = _fishDefinition.modelPrefab;

            if (visualPrefab != null)
            {
                Vector3 scale = data.catchVisualScale != Vector3.zero ? data.catchVisualScale : Vector3.one;
                RefreshVisual(visualPrefab, scale);
            }
            else
            {
 Debug.LogWarning($"[CaughtFishItem] '{gameObject.name}' could not resolve a FishDefinition for speciesAssetName '{data.speciesAssetName}' - visual will be missing after load.");
            }
        }

        private static string BuildFlavourText(AI_Fish fish)
        {
            string predatorDescriptor = fish.IsPredator ? "Predator" : "Non-predator";
            return $"{fish.Rarity} catch ({fish.RarityPercent:0.##}%). {predatorDescriptor}. Size {fish.Size:0.##}, Weight {fish.Weight:0.##}.";
        }
    }
}
