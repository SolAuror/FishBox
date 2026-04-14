using UnityEngine;
using Sol.Grab;

namespace Sol.Fishing
{
    [DisallowMultipleComponent]
    public class FishingRodItem : MonoBehaviour
    {
        private const string BaitPointName = "BaitPoint";

        [Header("References")]
        [SerializeField] private Transform _tacklePoint;
        [SerializeField] private Transform _lineOrigin;
        [SerializeField] private Transform _restingTackleVisual;
        [SerializeField] private GameObject _castTacklePrefab;
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private FishingBaitDefinition _defaultBait;
        [SerializeField] private ItemComponent _loadedBaitItem;
        [SerializeField] private ItemComponent _loadedTackleItem;
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
        private ItemComponent _displayedCatchItem;

        public Transform TacklePoint => _tacklePoint != null ? _tacklePoint : transform;
        public Transform LineOrigin => _lineOrigin != null ? _lineOrigin : TacklePoint;
        public Transform RestingTackleVisual => _restingTackleVisual;
        public GameObject CastTacklePrefab => _castTacklePrefab;
        public Material LineMaterial => _lineMaterial;
        public FishingBaitDefinition DefaultBait => _defaultBait;
        public ItemComponent LoadedBaitItem => _loadedBaitItem;
        public FishingBaitItem LoadedBait => _loadedBaitItem != null ? _loadedBaitItem.GetComponent<FishingBaitItem>() : null;
        public ItemComponent LoadedTackleItem => _loadedTackleItem;
        public FishingTackleItem LoadedTackle => _loadedTackleItem != null ? _loadedTackleItem.GetComponent<FishingTackleItem>() : null;
        public ItemComponent DisplayedCatchItem => _displayedCatchItem;
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

        public void SetLoadedBaitItem(ItemComponent item)
        {
            _loadedBaitItem = item;
            if (_loadedBaitItem == null)
                return;

            AttachItemToPoint(_loadedBaitItem, GetBaitAttachmentPoint());
            SetAttachedTackleState(_loadedBaitItem.transform, false);
            _loadedBaitItem.gameObject.SetActive(true);
        }

        public ItemComponent UnloadBaitItem()
        {
            ItemComponent item = _loadedBaitItem;
            _loadedBaitItem = null;
            if (item == null)
                return null;

            item.transform.SetParent(null, true);
            SetAttachedTackleState(item.transform, false);
            item.gameObject.SetActive(false);
            return item;
        }

        public void SetLoadedTackleItem(ItemComponent item)
        {
            _loadedTackleItem = item;
            if (_loadedTackleItem == null)
                return;

            AttachItemToPoint(_loadedTackleItem, TacklePoint);
            SetAttachedTackleState(_loadedTackleItem.transform, false);
            _loadedTackleItem.gameObject.SetActive(true);

            if (_loadedBaitItem != null)
                SetLoadedBaitItem(_loadedBaitItem);
        }

        public ItemComponent UnloadTackleItem()
        {
            ItemComponent item = _loadedTackleItem;
            _loadedTackleItem = null;
            if (item == null)
                return null;

            item.transform.SetParent(null, true);
            SetAttachedTackleState(item.transform, false);
            item.gameObject.SetActive(false);
            return item;
        }

        public void SetDisplayedCatchItem(ItemComponent item)
        {
            _displayedCatchItem = item;
            if (_displayedCatchItem == null)
                return;

            AttachItemToPointPreservingWorldScale(_displayedCatchItem, TacklePoint, Quaternion.Euler(_caughtFishLocalEulerAngles));
            PositionDisplayedCatchBelowPoint(_displayedCatchItem.transform, TacklePoint);
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
            SanitizeAttachedTackle();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            SanitizeAttachedTackle();
        }

        private void ResolveReferences()
        {
            if (_tacklePoint == null)
                _tacklePoint = FindChildByName("TacklePoint");

            if (_lineOrigin == null)
                _lineOrigin = FindChildByName("LineOrigin");

            if (_lineOrigin == null && _tacklePoint != null)
                _lineOrigin = _tacklePoint;

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

        private void SanitizeAttachedTackle()
        {
            if (!Application.isPlaying || !gameObject.scene.IsValid())
                return;

            if (_restingTackleVisual != null)
                _restingTackleVisual.gameObject.SetActive(false);

            if (_loadedBaitItem != null)
            {
                AttachItemToPoint(_loadedBaitItem, GetBaitAttachmentPoint());
                SetAttachedTackleState(_loadedBaitItem.transform, false);
            }

            if (_loadedTackleItem != null)
            {
                AttachItemToPoint(_loadedTackleItem, TacklePoint);
                SetAttachedTackleState(_loadedTackleItem.transform, false);
            }

            if (_displayedCatchItem != null)
            {
                AttachItemToPointPreservingWorldScale(_displayedCatchItem, TacklePoint, Quaternion.Euler(_caughtFishLocalEulerAngles));
                PositionDisplayedCatchBelowPoint(_displayedCatchItem.transform, TacklePoint);
                SetDisplayedCatchState(_displayedCatchItem.transform, attachedToRod: true);
            }
        }

        private Transform GetBaitAttachmentPoint()
        {
            if (_loadedTackleItem == null)
                return TacklePoint;

            Transform baitPoint = FindChildByName(_loadedTackleItem.transform, BaitPointName);
            return baitPoint != null ? baitPoint : _loadedTackleItem.transform;
        }

        private static void AttachItemToPoint(ItemComponent item, Transform attachPoint)
        {
            AttachItemToPoint(item, attachPoint, Quaternion.identity);
        }

        private static void AttachItemToPoint(ItemComponent item, Transform attachPoint, Quaternion localRotation)
        {
            if (item == null || attachPoint == null)
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
            if (item == null || attachPoint == null)
                return;

            Transform itemTransform = item.transform;
            Vector3 worldScale = itemTransform.lossyScale;
            itemTransform.SetParent(attachPoint, false);
            itemTransform.localPosition = Vector3.zero;
            itemTransform.localRotation = localRotation;
            itemTransform.localScale = DivideScale(worldScale, attachPoint.lossyScale);
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

        private static void SetAttachedTackleState(Transform root, bool enabled)
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
