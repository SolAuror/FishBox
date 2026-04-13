using Sol.Grab;
using UnityEngine;

namespace Sol.Fishing
{
    public class FishingTackleItem : MonoBehaviour
    {
        [Header("Tackle")]
        [SerializeField, Min(0.5f)] private float _lureRange = 6f;
        [SerializeField] private GameObject _castPrefabOverride;

        public float LureRange => _lureRange;
        public GameObject CastPrefabOverride => _castPrefabOverride;
        public ItemComponent ItemComponent => GetComponent<ItemComponent>();

        private void OnValidate()
        {
            _lureRange = Mathf.Max(0.5f, _lureRange);
        }
    }
}
