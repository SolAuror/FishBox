using UnityEngine;
using Sol.Grab;

namespace Sol.Fishing
{
    [DisallowMultipleComponent]
    public class FishingRodItem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _lineOrigin;
        [SerializeField] private Transform _restingTackleVisual;
        [SerializeField] private GameObject _castTacklePrefab;
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private FishingBaitDefinition _defaultBait;
        [SerializeField] private ItemComponent _loadedTackleItem;

        [Header("Casting")]
        [SerializeField, Min(1f)] private float _maxCastDistance = 18f;
        [SerializeField, Min(0.05f)] private float _castFlightDuration = 0.45f;
        [SerializeField, Min(0.1f)] private float _castArcHeight = 1.15f;
        [SerializeField, Min(0.05f)] private float _castLaunchForwardDistance = 1.4f;
        [SerializeField, Min(0f)] private float _castLaunchUpwardLift = 0.45f;
        [SerializeField, Min(0f)] private float _castCollisionEnableDelay = 0.06f;
        [SerializeField, Min(0.1f)] private float _castWaterContactTimeout = 2.5f;
        [SerializeField, Min(0.05f)] private float _reelDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float _reelLiftDistance = 1.6f;
        [SerializeField, Min(0.05f)] private float _reelSurfacePullStrength = 8f;
        [SerializeField, Min(0f)] private float _movementLockDuration = 0.32f;
        [SerializeField] private float _surfaceOffset = 0.04f;
        [SerializeField, Min(0f)] private float _floatBobAmplitude = 0.035f;
        [SerializeField, Min(0f)] private float _floatBobFrequency = 2.1f;
        [SerializeField, Min(0f)] private float _floatDriftDistance = 0.18f;
        [SerializeField, Min(0f)] private float _floatDriftFrequency = 0.75f;
        [SerializeField, Min(0.05f)] private float _floatLineSlackDistance = 1.1f;
        [SerializeField, Min(0.05f)] private float _floatSettleSpeed = 4.5f;
        [SerializeField, Min(0f)] private float _floatTensionStrength = 8f;
        [SerializeField, Min(0.25f)] private float _baitlessInterestMultiplier = 0.35f;
        [SerializeField, Min(0.5f)] private float _tackleInterestRadius = 6f;

        [Header("Line")]
        [SerializeField, Min(2)] private int _lineSegments = 12;
        [SerializeField, Min(0.001f)] private float _lineWidth = 0.015f;
        [SerializeField, Min(0f)] private float _lineSlack = 0.2f;

        public Transform LineOrigin => _lineOrigin != null ? _lineOrigin : transform;
        public Transform RestingTackleVisual => _restingTackleVisual;
        public GameObject CastTacklePrefab => _castTacklePrefab;
        public Material LineMaterial => _lineMaterial;
        public FishingBaitDefinition DefaultBait => _defaultBait;
        public ItemComponent LoadedTackleItem => _loadedTackleItem;
        public FishingTackleItem LoadedTackle => _loadedTackleItem != null ? _loadedTackleItem.GetComponent<FishingTackleItem>() : null;
        public GameObject ActiveCastTacklePrefab => LoadedTackle != null && LoadedTackle.CastPrefabOverride != null
            ? LoadedTackle.CastPrefabOverride
            : _castTacklePrefab;
        public float MaxCastDistance => _maxCastDistance;
        public float CastFlightDuration => _castFlightDuration;
        public float CastArcHeight => _castArcHeight;
        public float CastLaunchForwardDistance => _castLaunchForwardDistance;
        public float CastLaunchUpwardLift => _castLaunchUpwardLift;
        public float CastCollisionEnableDelay => _castCollisionEnableDelay;
        public float CastWaterContactTimeout => _castWaterContactTimeout;
        public float ReelDuration => _reelDuration;
        public float ReelLiftDistance => _reelLiftDistance;
        public float ReelSurfacePullStrength => _reelSurfacePullStrength;
        public float MovementLockDuration => _movementLockDuration;
        public float SurfaceOffset => _surfaceOffset;
        public float FloatBobAmplitude => _floatBobAmplitude;
        public float FloatBobFrequency => _floatBobFrequency;
        public float FloatDriftDistance => _floatDriftDistance;
        public float FloatDriftFrequency => _floatDriftFrequency;
        public float FloatLineSlackDistance => _floatLineSlackDistance;
        public float FloatSettleSpeed => _floatSettleSpeed;
        public float FloatTensionStrength => _floatTensionStrength;
        public float BaitlessInterestMultiplier => _baitlessInterestMultiplier;
        public float TackleInterestRadius => _tackleInterestRadius;
        public float CurrentLureRange => LoadedTackle != null ? LoadedTackle.LureRange : _tackleInterestRadius;
        public int LineSegments => _lineSegments;
        public float LineWidth => _lineWidth;
        public float LineSlack => _lineSlack;

        public void SetLoadedTackleItem(ItemComponent item)
        {
            _loadedTackleItem = item;
            if (_loadedTackleItem == null)
                return;

            _loadedTackleItem.transform.SetParent(transform, false);
            _loadedTackleItem.gameObject.SetActive(false);
        }

        public ItemComponent UnloadTackleItem()
        {
            ItemComponent item = _loadedTackleItem;
            _loadedTackleItem = null;
            if (item == null)
                return null;

            item.transform.SetParent(null, true);
            item.gameObject.SetActive(false);
            return item;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (_lineOrigin == null)
                _lineOrigin = FindChildByName("LineOrigin");

            if (_restingTackleVisual == null)
                _restingTackleVisual = FindChildByName("Tackle");

            if (_lineOrigin == null && _restingTackleVisual != null)
                _lineOrigin = _restingTackleVisual;
        }

        private Transform FindChildByName(string childName)
        {
            if (string.IsNullOrWhiteSpace(childName))
                return null;

            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && child.name == childName)
                    return child;
            }

            return null;
        }
    }
}
