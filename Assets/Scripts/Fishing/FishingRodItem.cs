using UnityEngine;
using Sol.Grab;

namespace Sol.Fishing
{
    [DisallowMultipleComponent]
    public class FishingRodItem : MonoBehaviour
    {
        private const string BaitPointName = "BaitPoint";
#region Inspector Settings

        [Header("References")]
        [Tooltip("Inspector: tunes lure point.")]
        [SerializeField] private Transform _lurePoint;
        [SerializeField] private Transform _lineOrigin;
        [Tooltip("Inspector: tunes resting lure visual.")]
        [SerializeField] private Transform _restingLureVisual;
        [SerializeField] private GameObject _castLurePrefab;
        [Tooltip("Inspector: tunes line material.")]
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private FishingBaitDefinition _defaultBait;
        [Tooltip("Inspector: tunes loaded bait item id.")]
        [ItemIdDropdown]
        [SerializeField] private string _loadedBaitItemId = string.Empty;
        [ItemIdDropdown]
        [SerializeField] private string _loadedLureItemId = string.Empty;
        [Tooltip("Inspector: tunes caught fish local euler angles.")]
        [SerializeField] private Vector3 _caughtFishLocalEulerAngles = new(-90f, 0f, 0f);
        [SerializeField, Min(0f)] private float _caughtFishHangPadding = 0.015f;

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
        [Tooltip("Inspector: tunes surface offset.")]
        [SerializeField] private float _surfaceOffset = 0.04f;
#endregion
        [SerializeField, Min(0f)] private float _floatBobAmplitude = 0.035f;
        [SerializeField, Min(0f)] private float _floatBobFrequency = 2.1f;
        [SerializeField, Min(0f)] private float _floatDriftDistance = 0.18f;
        [SerializeField, Min(0f)] private float _floatDriftFrequency = 0.75f;
        [SerializeField, Min(0f)] private float _floatLineSlackDistance = 0.5f;
        [SerializeField, Min(0.01f)] private float _lineSlackRecoverSpeed = 0.35f;
        [SerializeField, Min(0.05f)] private float _floatSettleSpeed = 4.5f;
        [SerializeField, Min(0f)] private float _floatTensionStrength = 8f;
        [SerializeField, Min(0.25f)] private float _baitlessInterestMultiplier = 0.35f;
        [SerializeField, Min(0.5f)] private float _lureInterestRadius = 6f;

        [Header("Line")]
        [SerializeField, Min(2)] private int _lineSegments = 12;
        [SerializeField, Min(0.001f)] private float _lineWidth = 0.015f;
        [SerializeField, Min(0f)] private float _lineSlack = 0.2f;
        private ItemComponent _loadedBaitItem;
        private ItemComponent _loadedLureItem;
        private ItemComponent _displayedCatchItem;

        public Transform LurePoint => _lurePoint != null ? _lurePoint : transform;
        public Transform LineOrigin => _lineOrigin != null ? _lineOrigin : LurePoint;
        public Transform RestingLureVisual => _restingLureVisual;
        public GameObject CastLurePrefab => _castLurePrefab;
        public Material LineMaterial => _lineMaterial;
        public FishingBaitDefinition DefaultBait => _defaultBait;
        public string LoadedBaitItemId => _loadedBaitItemId;
        public string LoadedLureItemId => _loadedLureItemId;
        public ItemComponent LoadedBaitItem => _loadedBaitItem;
        public FishingBaitItem LoadedBait => _loadedBaitItem != null ? _loadedBaitItem.GetComponent<FishingBaitItem>() : null;
        public ItemComponent LoadedLureItem => _loadedLureItem;
        public FishingLureItem LoadedLure => _loadedLureItem != null ? _loadedLureItem.GetComponent<FishingLureItem>() : null;
        public ItemComponent DisplayedCatchItem => _displayedCatchItem;
        public GameObject ActiveCastLurePrefab => LoadedLure != null && LoadedLure.CastPrefabOverride != null
            ? LoadedLure.CastPrefabOverride
            : _castLurePrefab;
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
        public float LineSlackRecoverSpeed => _lineSlackRecoverSpeed;
        public float FloatSettleSpeed => _floatSettleSpeed;
        public float FloatTensionStrength => _floatTensionStrength;
        public float BaitlessInterestMultiplier => _baitlessInterestMultiplier;
        public float LureInterestRadius => _lureInterestRadius;
        public float CurrentLureRange => LoadedLure != null ? LoadedLure.LureRange : _lureInterestRadius;
        public int LineSegments => _lineSegments;
        public float LineWidth => _lineWidth;
        public float LineSlack => _lineSlack;

        public void SetLoadedBaitItem(ItemComponent item)
        {
            _loadedBaitItem = item;
            _loadedBaitItemId = NormalizeItemIdOrEmpty(item != null ? item.ItemId : string.Empty);
            if (_loadedBaitItem == null)
                return;

            AttachItemToPointPreservingWorldScale(_loadedBaitItem, GetBaitAttachmentPoint(), Quaternion.identity);
            SetAttachedLureState(_loadedBaitItem.transform, false);
            _loadedBaitItem.gameObject.SetActive(true);
        }

        public void AttachLoadedBaitToExternalPoint(Transform attachPoint)
        {
            if (_loadedBaitItem == null || attachPoint == null)
                return;

            AttachItemToPointPreservingWorldScale(_loadedBaitItem, attachPoint, Quaternion.identity);
            SetAttachedLureState(_loadedBaitItem.transform, false);
            _loadedBaitItem.gameObject.SetActive(true);
        }

        public void ReattachLoadedBaitToRod()
        {
            if (_loadedBaitItem == null)
                return;

            Transform point = GetBaitAttachmentPoint();
            if (point == null)
                return;

            AttachItemToPointPreservingWorldScale(_loadedBaitItem, point, Quaternion.identity);
            SetAttachedLureState(_loadedBaitItem.transform, false);
        }

        public Transform FindBaitPoint(Transform root)
        {
            if (root == null)
                return null;
            Transform point = FindChildByName(root, BaitPointName);
            return point != null ? point : root;
        }

        public ItemComponent UnloadBaitItem()
        {
            ItemComponent item = _loadedBaitItem;
            _loadedBaitItem = null;
            _loadedBaitItemId = string.Empty;
            if (item == null)
                return null;

            item.transform.SetParent(null, true);
            SetAttachedLureState(item.transform, false);
            item.gameObject.SetActive(false);
            return item;
        }

        public void SetLoadedLureItem(ItemComponent item)
        {
            _loadedLureItem = item;
            _loadedLureItemId = NormalizeItemIdOrEmpty(item != null ? item.ItemId : string.Empty);
            if (_loadedLureItem == null)
                return;

            AttachItemToPoint(_loadedLureItem, LurePoint);
            SetAttachedLureState(_loadedLureItem.transform, false);
            _loadedLureItem.gameObject.SetActive(true);

            if (_loadedBaitItem != null)
                SetLoadedBaitItem(_loadedBaitItem);
        }

        public ItemComponent UnloadLureItem()
        {
            ItemComponent item = _loadedLureItem;
            _loadedLureItem = null;
            _loadedLureItemId = string.Empty;
            if (item == null)
                return null;

            item.transform.SetParent(null, true);
            SetAttachedLureState(item.transform, false);
            item.gameObject.SetActive(false);
            return item;
        }

        public void SetDisplayedCatchItem(ItemComponent item)
        {
            _displayedCatchItem = item;
            if (_displayedCatchItem == null)
                return;

            AttachItemToPointPreservingWorldScale(_displayedCatchItem, LurePoint, Quaternion.Euler(_caughtFishLocalEulerAngles));
            PositionDisplayedCatchBelowPoint(_displayedCatchItem.transform, LurePoint);
            SetDisplayedCatchState(_displayedCatchItem.transform, attachedToRod: true);
            _displayedCatchItem.gameObject.SetActive(true);
        }

        public void ReleaseDisplayedCatchItem(ItemComponent item, bool keepWorldTransform)
        {
            if (item == null)
                return;

            if (_displayedCatchItem == item)
                _displayedCatchItem = null;

            item.transform.SetParent(null, keepWorldTransform);
            SetDisplayedCatchState(item.transform, attachedToRod: false);
            item.gameObject.SetActive(true);
        }

        public void ForgetDisplayedCatchItem(ItemComponent item)
        {
            if (_displayedCatchItem == item)
                _displayedCatchItem = null;
        }

        private void Awake()
        {
            ResolveReferences();
            ResolveLoadedItemsFromIds();
            SanitizeAttachedLure();
        }

        private void OnValidate()
        {
            ResolveReferences();
            _loadedBaitItemId = NormalizeItemIdOrEmpty(_loadedBaitItemId);
            _loadedLureItemId = NormalizeItemIdOrEmpty(_loadedLureItemId);
        }

        private void OnEnable()
        {
            SanitizeAttachedLure();
        }

        private void ResolveReferences()
        {
            if (_lurePoint == null)
                _lurePoint = FindChildByName("LurePoint");

            if (_lineOrigin == null)
                _lineOrigin = FindChildByName("LineOrigin");

            if (_lineOrigin == null && _lurePoint != null)
                _lineOrigin = _lurePoint;

            if (_lineOrigin == null && _restingLureVisual != null)
                _lineOrigin = _restingLureVisual;
        }

        private Transform FindChildByName(string childName)
        {
            return FindChildByName(transform, childName);
        }

        private void SanitizeAttachedLure()
        {
            if (!Application.isPlaying || !gameObject.scene.IsValid())
                return;

            if (_restingLureVisual != null)
                _restingLureVisual.gameObject.SetActive(false);

            if (_loadedBaitItem != null)
            {
                if (CanAttachRuntimeItem(_loadedBaitItem, GetBaitAttachmentPoint()))
                {
                    AttachItemToPointPreservingWorldScale(_loadedBaitItem, GetBaitAttachmentPoint(), Quaternion.identity);
                    SetAttachedLureState(_loadedBaitItem.transform, false);
                }
                else
                {
                    _loadedBaitItem = null;
                    _loadedBaitItemId = string.Empty;
                }
            }

            if (_loadedLureItem != null)
            {
                if (CanAttachRuntimeItem(_loadedLureItem, LurePoint))
                {
                    AttachItemToPoint(_loadedLureItem, LurePoint);
                    SetAttachedLureState(_loadedLureItem.transform, false);
                }
                else
                {
                    _loadedLureItem = null;
                    _loadedLureItemId = string.Empty;
                }
            }

            if (_displayedCatchItem != null)
            {
                if (CanAttachRuntimeItem(_displayedCatchItem, LurePoint))
                {
                    AttachItemToPointPreservingWorldScale(_displayedCatchItem, LurePoint, Quaternion.Euler(_caughtFishLocalEulerAngles));
                    PositionDisplayedCatchBelowPoint(_displayedCatchItem.transform, LurePoint);
                    SetDisplayedCatchState(_displayedCatchItem.transform, attachedToRod: true);
                }
                else
                {
                    _displayedCatchItem = null;
                }
            }
        }

        private Transform GetBaitAttachmentPoint()
        {
            if (_loadedLureItem == null)
                return LurePoint;

            Transform baitPoint = FindChildByName(_loadedLureItem.transform, BaitPointName);
            return baitPoint != null ? baitPoint : _loadedLureItem.transform;
        }

        private void ResolveLoadedItemsFromIds()
        {
            if (!Application.isPlaying || !gameObject.scene.IsValid())
                return;

            ItemRegistry registry = ItemRegistry.Get();
            if (registry == null)
                return;

            if (_loadedLureItem == null && !string.IsNullOrWhiteSpace(_loadedLureItemId))
            {
                ItemComponent lurePrefab = registry.GetPrefab(_loadedLureItemId);
                if (lurePrefab != null)
                {
                    ItemComponent lureInstance = Instantiate(lurePrefab, transform);
                    lureInstance.gameObject.SetActive(false);
                    SetLoadedLureItem(lureInstance);
                }
            }

            if (_loadedBaitItem == null && !string.IsNullOrWhiteSpace(_loadedBaitItemId))
            {
                ItemComponent baitPrefab = registry.GetPrefab(_loadedBaitItemId);
                if (baitPrefab != null)
                {
                    ItemComponent baitInstance = Instantiate(baitPrefab, transform);
                    baitInstance.gameObject.SetActive(false);
                    SetLoadedBaitItem(baitInstance);
                }
            }
        }

        private static string NormalizeItemIdOrEmpty(string rawItemId)
        {
            return Sol.EntityCodeUtility.NormalizeOrEmpty(rawItemId, Sol.EntityCodeUtility.ItemPrefix);
        }

        private static void AttachItemToPoint(ItemComponent item, Transform attachPoint)
        {
            AttachItemToPoint(item, attachPoint, Quaternion.identity);
        }

        private static void AttachItemToPoint(ItemComponent item, Transform attachPoint, Quaternion localRotation)
        {
            if (!CanAttachRuntimeItem(item, attachPoint))
                return;

            Transform itemTransform = item.transform;
            Vector3 authoredLocalScale = itemTransform.localScale;
            itemTransform.SetParent(attachPoint, false);
            itemTransform.localPosition = Vector3.zero;
            itemTransform.localRotation = localRotation;
            itemTransform.localScale = authoredLocalScale;
        }

        private static void AttachItemToPointPreservingWorldScale(ItemComponent item, Transform attachPoint, Quaternion localRotation)
        {
            if (!CanAttachRuntimeItem(item, attachPoint))
                return;

            Transform itemTransform = item.transform;
            Vector3 worldScale = itemTransform.lossyScale;
            itemTransform.SetParent(attachPoint, false);
            itemTransform.localPosition = Vector3.zero;
            itemTransform.localRotation = localRotation;
            itemTransform.localScale = DivideScale(worldScale, attachPoint.lossyScale);
        }

        private static bool CanAttachRuntimeItem(ItemComponent item, Transform attachPoint)
        {
            if (item == null || attachPoint == null)
                return false;

            GameObject itemObject = item.gameObject;
            GameObject attachObject = attachPoint.gameObject;
            if (itemObject == null || attachObject == null)
                return false;

            // Prefab-asset references can survive into serialized fields; never reparent them at runtime.
            return itemObject.scene.IsValid() && attachObject.scene.IsValid();
        }

        private void PositionDisplayedCatchBelowPoint(Transform itemTransform, Transform attachPoint)
        {
            if (itemTransform == null || attachPoint == null)
                return;

            Bounds? visualBounds = TryGetVisualBounds(itemTransform);
            if (!visualBounds.HasValue)
                return;

            Bounds bounds = visualBounds.Value;
            float topOffset = bounds.max.y - attachPoint.position.y;
            if (topOffset <= 0f)
                return;

            itemTransform.position += Vector3.down * (topOffset + _caughtFishHangPadding);
        }

        private static void SetAttachedLureState(Transform root, bool enabled)
        {
            if (root == null)
                return;

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody body = rigidbodies[i];
                if (body == null)
                    continue;

                body.isKinematic = !enabled;
                body.detectCollisions = enabled;
                body.useGravity = enabled;
                // Kinematic rigidbodies with Interpolate/Extrapolate lag behind a moving parent
                // transform, causing attached bait/lure to visibly drift while the rod moves.
                // Disable interpolation while attached so the body follows its parent exactly.
                body.interpolation = enabled ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                collider.enabled = enabled;
            }

            GrabbableComponent[] grabbables = root.GetComponentsInChildren<GrabbableComponent>(true);
            for (int i = 0; i < grabbables.Length; i++)
            {
                GrabbableComponent grabbable = grabbables[i];
                if (grabbable == null)
                    continue;

                grabbable.enabled = enabled;
            }
        }

        private static void SetDisplayedCatchState(Transform root, bool attachedToRod)
        {
            if (root == null)
                return;

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody body = rigidbodies[i];
                if (body == null)
                    continue;

                bool wasKinematic = body.isKinematic;
                if (attachedToRod && !wasKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                if (wasKinematic != attachedToRod)
                    body.isKinematic = attachedToRod;

                body.detectCollisions = true;
                body.useGravity = !attachedToRod;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                collider.enabled = true;
            }

            GrabbableComponent[] grabbables = root.GetComponentsInChildren<GrabbableComponent>(true);
            for (int i = 0; i < grabbables.Length; i++)
            {
                GrabbableComponent grabbable = grabbables[i];
                if (grabbable == null)
                    continue;

                grabbable.enabled = true;
            }
        }

        private static Vector3 DivideScale(Vector3 scale, Vector3 divisor)
        {
            return new Vector3(
                divisor.x == 0f ? scale.x : scale.x / divisor.x,
                divisor.y == 0f ? scale.y : scale.y / divisor.y,
                divisor.z == 0f ? scale.z : scale.z / divisor.z);
        }

        private static Bounds? TryGetVisualBounds(Transform root)
        {
            if (root == null)
                return null;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds? combinedBounds = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!combinedBounds.HasValue)
                    combinedBounds = renderer.bounds;
                else
                {
                    Bounds bounds = combinedBounds.Value;
                    bounds.Encapsulate(renderer.bounds);
                    combinedBounds = bounds;
                }
            }

            return combinedBounds;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
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
