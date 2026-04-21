using UnityEngine;
using Sol.Grab;

namespace Sol.Fishing
{
    [DisallowMultipleComponent]
    public class FishingBaitItem : MonoBehaviour
    {
        public const float DefaultFoodInterestMultiplier = 1.35f;
        public const float DefaultFoodRadiusMultiplier = 1.1f;
#region Inspector Settings

        [Tooltip("Inspector: tunes bait definition.")]
        [SerializeField] private FishingBaitDefinition _baitDefinition;
#endregion
        [SerializeField, Min(0.1f)] private float _interestMultiplier = 2f;
        [SerializeField, Min(0.25f)] private float _radiusMultiplier = 1.35f;

        public FishingBaitDefinition BaitDefinition => _baitDefinition;
        public ItemComponent ItemComponent => GetComponent<ItemComponent>();
        public float InterestMultiplier => _baitDefinition != null ? _baitDefinition.interestMultiplier : _interestMultiplier;
        public float RadiusMultiplier => _baitDefinition != null ? _baitDefinition.radiusMultiplier : _radiusMultiplier;
        public string BaitName
        {
            get
            {
                if (_baitDefinition != null && !string.IsNullOrWhiteSpace(_baitDefinition.baitName))
                    return _baitDefinition.baitName;

                ItemComponent item = GetComponent<ItemComponent>();
                return item != null && !string.IsNullOrWhiteSpace(item.ItemName)
                    ? item.ItemName
                    : "Bait";
            }
        }

        private void OnValidate()
        {
            _interestMultiplier = Mathf.Max(0.1f, _interestMultiplier);
            _radiusMultiplier = Mathf.Max(0.25f, _radiusMultiplier);
        }

        public static bool IsSupportedBait(ItemComponent item)
        {
            if (item == null)
                return false;

            return item.GetComponent<FishingBaitItem>() != null
                || item.Type == ItemType.Food;
        }

        public static float ResolveInterestMultiplier(ItemComponent item, FishingBaitDefinition fallbackBait = null, float baitlessFallback = 1f)
        {
            if (item != null)
            {
                FishingBaitItem baitItem = item.GetComponent<FishingBaitItem>();
                if (baitItem != null)
                    return baitItem.InterestMultiplier;

                if (item.Type == ItemType.Food)
                    return DefaultFoodInterestMultiplier;
            }

            if (fallbackBait != null)
                return Mathf.Max(0.1f, fallbackBait.interestMultiplier);

            return Mathf.Max(0.1f, baitlessFallback);
        }

        public static float ResolveRadiusMultiplier(ItemComponent item, FishingBaitDefinition fallbackBait = null)
        {
            if (item != null)
            {
                FishingBaitItem baitItem = item.GetComponent<FishingBaitItem>();
                if (baitItem != null)
                    return baitItem.RadiusMultiplier;

                if (item.Type == ItemType.Food)
                    return DefaultFoodRadiusMultiplier;
            }

            if (fallbackBait != null)
                return Mathf.Max(0.25f, fallbackBait.radiusMultiplier);

            return 1f;
        }

        public static string ResolveBaitName(ItemComponent item, FishingBaitDefinition fallbackBait = null)
        {
            if (item != null)
            {
                FishingBaitItem baitItem = item.GetComponent<FishingBaitItem>();
                if (baitItem != null && !string.IsNullOrWhiteSpace(baitItem.BaitName))
                    return baitItem.BaitName.Trim();

                if (!string.IsNullOrWhiteSpace(item.ItemName))
                    return item.ItemName.Trim();
            }

            if (fallbackBait != null && !string.IsNullOrWhiteSpace(fallbackBait.baitName))
                return fallbackBait.baitName.Trim();

            return string.Empty;
        }

        public static string ResolveBaitItemId(ItemComponent item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                return string.Empty;

            return item.ItemId.Trim();
        }
    }
}
