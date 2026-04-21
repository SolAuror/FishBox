using Sol.Grab;
using UnityEngine;

namespace Sol.Fishing
{
    public class FishingTackleItem : MonoBehaviour
    {
        [Header("Tackle")]
        [SerializeField, Min(0.5f)] private float _lureRange = 6f;
        #region Inspector Settings
        [Tooltip("Inspector: tunes cast prefab override.")]
        [SerializeField] private GameObject _castPrefabOverride;
        #endregion

        public float LureRange => _lureRange;
        public GameObject CastPrefabOverride => _castPrefabOverride;
        public ItemComponent ItemComponent => GetComponent<ItemComponent>();

        private void OnValidate()
        {
            _lureRange = Mathf.Max(0.5f, _lureRange);
        }
    }
}
