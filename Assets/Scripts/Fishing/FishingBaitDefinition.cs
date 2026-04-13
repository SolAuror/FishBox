using UnityEngine;

namespace Sol.Fishing
{
    [CreateAssetMenu(fileName = "FishingBait", menuName = "Sol/Fishing/Bait Definition")]
    public class FishingBaitDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string baitName = "Basic Bait";

        [Header("Attraction")]
        [Min(0.1f)] public float interestMultiplier = 2f;
        [Min(0.25f)] public float radiusMultiplier = 1.35f;

        private void OnValidate()
        {
            interestMultiplier = Mathf.Max(0.1f, interestMultiplier);
            radiusMultiplier = Mathf.Max(0.25f, radiusMultiplier);
        }
    }
}
