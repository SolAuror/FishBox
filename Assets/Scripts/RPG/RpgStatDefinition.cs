using UnityEngine;

namespace Sol.Rpg
{
    public enum RpgStatCategory
    {
        Primary = 0,
        Derived = 1,
        Combat = 2,
        Survival = 3,
        Social = 4,
        Utility = 5
    }

    [CreateAssetMenu(fileName = "STA00001_NewStat", menuName = "Sol/RPG/Stat Definition")]
    public sealed class RpgStatDefinition : RpgDefinition
    {
        [Header("Stat")]
        [SerializeField] private RpgStatCategory _category = RpgStatCategory.Primary;
        [SerializeField] private bool _canImproveWithUse = true;
        [SerializeField] private float _baseValue = 10f;
        [SerializeField] private float _minimumValue = 0f;
        [SerializeField] private float _maximumValue = 100f;

        public RpgStatCategory Category => _category;
        public bool CanImproveWithUse => _canImproveWithUse;
        public float BaseValue => _baseValue;
        public float MinimumValue => _minimumValue;
        public float MaximumValue => _maximumValue;

        private void OnValidate()
        {
            _maximumValue = Mathf.Max(_minimumValue, _maximumValue);
            _baseValue = Mathf.Clamp(_baseValue, _minimumValue, _maximumValue);
        }
    }
}
