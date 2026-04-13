using System.Collections.Generic;
using UnityEngine;
using Sol.Grab;
using Sol.Locomotion;
using Sol.Outline;

namespace Sol.Fishing
{
    [DisallowMultipleComponent]
    public class FishingRodState : MonoBehaviour
    {
        private const string DefaultRodName = "Fishing Rod";
        private const string DefaultTackleName = "Tackle";
        private const string LineObjectName = "[FishingLine]";

        [Header("Animator")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _hasRodEquippedParameter = "hasRodEquipped";
        [SerializeField] private string _castRodTriggerParameter = "CastRod";
        [SerializeField] private string _isReelingParameter = "isReeling";
        [SerializeField] private string _reelProgressParameter = "ReelProgress";

        [Header("References")]
        [SerializeField] private Equipment _equipment;
        [SerializeField] private LocomotionInput _locomotionInput;
        [SerializeField] private LocomotionController _locomotionController;

        [Header("Fallback Rod Settings")]
        [SerializeField] private string _fallbackRodItemName = DefaultRodName;
        [SerializeField] private string _fallbackTackleVisualName = DefaultTackleName;
        [SerializeField, Min(1f)] private float _fallbackMaxCastDistance = 18f;
        [SerializeField, Min(0.05f)] private float _fallbackCastFlightDuration = 0.45f;
        [SerializeField, Min(0.1f)] private float _fallbackCastArcHeight = 1.15f;
        [SerializeField, Min(0.05f)] private float _fallbackCastLaunchForwardDistance = 1.4f;
        [SerializeField, Min(0f)] private float _fallbackCastLaunchUpwardLift = 0.45f;
        [SerializeField, Min(0f)] private float _fallbackCastCollisionEnableDelay = 0.06f;
        [SerializeField, Min(0.1f)] private float _fallbackCastWaterContactTimeout = 2.5f;
        [SerializeField, Min(0.05f)] private float _fallbackReelDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float _fallbackReelLiftDistance = 1.6f;
        [SerializeField, Min(0.05f)] private float _fallbackReelSurfacePullStrength = 8f;
        [SerializeField, Min(0f)] private float _fallbackMovementLockDuration = 0.32f;
        [SerializeField] private float _fallbackSurfaceOffset = 0.04f;
        [SerializeField, Min(0f)] private float _fallbackFloatBobAmplitude = 0.035f;
        [SerializeField, Min(0f)] private float _fallbackFloatBobFrequency = 2.1f;
        [SerializeField, Min(0f)] private float _fallbackFloatDriftDistance = 0.18f;
        [SerializeField, Min(0f)] private float _fallbackFloatDriftFrequency = 0.75f;
        [SerializeField, Min(0.05f)] private float _fallbackFloatLineSlackDistance = 1.1f;
        [SerializeField, Min(0.05f)] private float _fallbackFloatSettleSpeed = 4.5f;
        [SerializeField, Min(0f)] private float _fallbackFloatTensionStrength = 8f;
        [SerializeField, Min(0.25f)] private float _fallbackBaitlessInterestMultiplier = 0.35f;
        [SerializeField, Min(0.5f)] private float _fallbackTackleInterestRadius = 6f;

        [Header("Fallback Line")]
        [SerializeField, Min(2)] private int _fallbackLineSegments = 12;
        [SerializeField, Min(0.001f)] private float _fallbackLineWidth = 0.015f;
        [SerializeField, Min(0f)] private float _fallbackLineSlack = 0.2f;
        [SerializeField] private Material _fallbackLineMaterial;

        [Header("Cast Query")]
        [SerializeField] private LayerMask _castBlockingLayers = ~0;

        private static Material s_RuntimeLineMaterial;

        private readonly HashSet<int> _animatorParameters = new();

        private FishingRodItem _activeRod;
        private ItemComponent _activeRodItem;
        private Transform _activeLineOrigin;
        private Transform _restingTackleVisual;
        private LineRenderer _lineRenderer;
        private FishingTackleInstance _activeTackle;
        private WaterVolume _pendingWaterVolume;
        private Vector3 _pendingCastTarget;
        private int _hasRodEquippedHash;
        private int _castRodTriggerHash;
        private int _isReelingHash;
        private int _reelProgressHash;
        private int _upperBodyLayerIndex = -1;
        private int _fishingReelStateHash;
        private bool _isRodEquipped;
        private bool _isCastPending;
        private bool _castReleasedByEvent;
        private bool _isReeling;
        private float _reelProgress;
        private Vector3 _reelStartPosition;

        public bool ShouldBlockDefaultAttack => _isRodEquipped;
        public bool HasLineOut => _activeTackle != null || _isCastPending;
        public bool HasEquippedRod => _isRodEquipped && _activeRod != null;

        private void Awake()
        {
            if (_equipment == null)
                _equipment = GetComponent<Equipment>();

            if (_locomotionInput == null)
                _locomotionInput = GetComponent<LocomotionInput>();

            if (_locomotionController == null)
                _locomotionController = GetComponent<LocomotionController>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            _hasRodEquippedHash = Animator.StringToHash(_hasRodEquippedParameter);
            _castRodTriggerHash = Animator.StringToHash(_castRodTriggerParameter);
            _isReelingHash = Animator.StringToHash(_isReelingParameter);
            _reelProgressHash = Animator.StringToHash(_reelProgressParameter);
            _upperBodyLayerIndex = _animator != null ? _animator.GetLayerIndex("Upper Body") : -1;
            _fishingReelStateHash = Animator.StringToHash("Upper Body.FishingState.Fishing_Reel");
            CacheAnimatorParameters();
            ResolveEquippedRod(forceRefresh: true);
        }

        private void OnEnable()
        {
            if (_equipment != null)
                _equipment.OnChanged += HandleEquipmentChanged;
        }

        private void OnDisable()
        {
            if (_equipment != null)
                _equipment.OnChanged -= HandleEquipmentChanged;

            SetAnimatorRodEquipped(false);
            SetAnimatorReelState(false, 0f);
            ClearCastState(destroyTackle: true);
        }

        private void Update()
        {
            ResolveEquippedRod(forceRefresh: false);
            UpdateTackleState();
            UpdateLineRenderer();

            if (!_isRodEquipped || _locomotionInput == null)
                return;

            if (_activeTackle != null)
            {
                UpdateReelInput();
                return;
            }

            if (!_locomotionInput.AttackPressed || _isCastPending)
                return;

            _locomotionInput.SetAttackPressedFalse();
            if (!_isCastPending)
                BeginCast();
        }

        public void OnCastRelease()
        {
            if (!_isCastPending || _castReleasedByEvent)
                return;

            _castReleasedByEvent = true;
            ReleaseCast();
        }

        public bool CanLoadTackle(ItemComponent item)
        {
            if (!HasEquippedRod || HasLineOut || item == null)
                return false;

            return item.GetComponent<FishingTackleItem>() != null;
        }

        public bool TryLoadTackle(InventorySlot slot, Inventory inventory)
        {
            if (!HasEquippedRod || HasLineOut || slot?.Item == null || inventory == null)
                return false;

            ItemComponent slotItem = slot.PopItem();
            if (slotItem == null)
                return false;

            FishingTackleItem incomingTackle = slotItem.GetComponent<FishingTackleItem>();
            if (incomingTackle == null)
                return false;

            if (!inventory.Remove(slot, 1))
                return false;

            ItemComponent previousTackle = _activeRod.UnloadTackleItem();
            if (previousTackle != null)
            {
                previousTackle.transform.SetParent(inventory.transform, false);
                previousTackle.gameObject.SetActive(false);
                if (!inventory.Add(previousTackle))
                {
                    _activeRod.SetLoadedTackleItem(previousTackle);
                    slot.PushExtra(slotItem);
                    slot.Count++;
                    return false;
                }
            }

            slotItem.transform.SetParent(_activeRod.transform, false);
            slotItem.gameObject.SetActive(false);
            _activeRod.SetLoadedTackleItem(slotItem);
            return true;
        }

        private void HandleEquipmentChanged()
        {
            ResolveEquippedRod(forceRefresh: true);
        }

        private void ResolveEquippedRod(bool forceRefresh)
        {
            ItemComponent resolvedItem = null;
            FishingRodItem resolvedRod = null;

            if (_equipment != null)
            {
                foreach (KeyValuePair<EquipmentSlotType, ItemComponent> kv in _equipment.Equipped)
                {
                    ItemComponent candidate = kv.Value;
                    if (!IsFishingRod(candidate, out FishingRodItem rod))
                        continue;

                    resolvedItem = candidate;
                    resolvedRod = rod;
                    break;
                }
            }

            if (!forceRefresh && resolvedItem == _activeRodItem && resolvedRod == _activeRod)
                return;

            bool rodChanged = resolvedItem != _activeRodItem || resolvedRod != _activeRod;
            if (rodChanged)
            {
                ClearCastState(destroyTackle: true);
                _activeRodItem = resolvedItem;
                _activeRod = resolvedRod;
                _lineRenderer = null;
            }

            _isRodEquipped = _activeRodItem != null;
            CacheActiveRodReferences();
            SetAnimatorRodEquipped(_isRodEquipped);
            UpdateRestingTackleVisual();
        }

        private bool IsFishingRod(ItemComponent item, out FishingRodItem rod)
        {
            rod = null;
            if (item == null)
                return false;

            rod = item.GetComponent<FishingRodItem>();
            if (rod != null)
                return true;

            return !string.IsNullOrWhiteSpace(_fallbackRodItemName)
                && string.Equals(item.ItemName, _fallbackRodItemName, System.StringComparison.OrdinalIgnoreCase);
        }

        private void CacheActiveRodReferences()
        {
            if (_activeRod != null)
            {
                _activeLineOrigin = _activeRod.LineOrigin;
                _restingTackleVisual = _activeRod.RestingTackleVisual;
                return;
            }

            _activeLineOrigin = _activeRodItem != null ? _activeRodItem.transform : null;
            _restingTackleVisual = null;

            if (_activeRodItem == null)
                return;

            Transform[] children = _activeRodItem.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null)
                    continue;

                if (_activeLineOrigin == _activeRodItem.transform && child.name == "LineOrigin")
                    _activeLineOrigin = child;

                if (_restingTackleVisual == null && child.name == _fallbackTackleVisualName)
                    _restingTackleVisual = child;
            }

            if (_activeLineOrigin == null && _restingTackleVisual != null)
                _activeLineOrigin = _restingTackleVisual;
        }

        private void BeginCast()
        {
            if (_activeRodItem == null || _activeLineOrigin == null)
                return;

            if (!TryGetCastTarget(out Vector3 castTarget))
                return;

            _pendingCastTarget = castTarget;
            _pendingWaterVolume = WaterVolume.FindVolumeXZ(castTarget);
            _isCastPending = true;
            _castReleasedByEvent = false;

            TriggerCastAnimation();
            _locomotionController?.SetMovementLock(GetMovementLockDuration());
            UpdateRestingTackleVisual();
        }

        private void ReleaseCast()
        {
            if (!_isCastPending || _activeLineOrigin == null)
                return;

            _isCastPending = false;

            FishingTackleInstance tackleInstance = SpawnTackleInstance();
            if (tackleInstance == null)
                return;

            _activeTackle = tackleInstance;
            _activeTackle.Launch(
                _activeLineOrigin.position,
                _pendingCastTarget,
                _activeLineOrigin.forward,
                _pendingWaterVolume,
                GetCastFlightDuration(),
                GetCastArcHeight(),
                GetCastLaunchForwardDistance(),
                GetCastLaunchUpwardLift(),
                GetSurfaceOffset(),
                GetFloatBobAmplitude(),
                GetFloatBobFrequency(),
                GetCastCollisionEnableDelay(),
                GetCastWaterContactTimeout(),
                GetTackleInterestMultiplier(),
                GetTackleInterestRadius());

            EnsureLineRenderer();
            UpdateRestingTackleVisual();
        }

        private void BeginReel()
        {
            if (_activeTackle == null || _activeLineOrigin == null)
            {
                ClearCastState(destroyTackle: true);
                return;
            }

            if (!_isReeling)
                _reelStartPosition = _activeTackle.transform.position;

            _isReeling = true;
            SetAnimatorReelState(true, _reelProgress);
        }

        private void UpdateTackleState()
        {
            if (_activeTackle != null && _activeTackle.ShouldCancelCast)
            {
                ClearCastState(destroyTackle: true);
                return;
            }

            if (_activeTackle != null && _isReeling)
                UpdateReelProgress();

            if (_activeTackle != null && _isReeling && _activeTackle.ShouldCompleteReel)
            {
                CompleteReel();
                return;
            }

            if (_activeTackle != null && !_isReeling && _activeLineOrigin != null)
            {
                _activeTackle.SetFloatAnchor(
                    _activeLineOrigin.position,
                    GetFloatLineSlackDistance(),
                    GetFloatSettleSpeed(),
                    GetFloatTensionStrength(),
                    GetFloatDriftDistance(),
                    GetFloatDriftFrequency());
            }

            if (_activeTackle != null)
                return;

            if (!_isCastPending)
                UpdateRestingTackleVisual();
        }

        private void UpdateReelInput()
        {
            if (_locomotionInput.AttackPressed)
                _locomotionInput.SetAttackPressedFalse();

            if (_locomotionInput.ReelHeld)
            {
                BeginReel();
                return;
            }

            if (_isReeling)
            {
                _isReeling = false;
                SetAnimatorReelState(false, _reelProgress);
            }
        }

        private void UpdateReelProgress()
        {
            if (_activeTackle == null || _activeLineOrigin == null)
            {
                ClearCastState(destroyTackle: true);
                return;
            }

            float reelDuration = Mathf.Max(0.05f, GetReelDuration());
            _reelProgress = Mathf.Clamp01(_reelProgress + (Time.deltaTime / reelDuration));
            Vector3 rodTipPosition = _activeLineOrigin.position;
            Vector3 surfaceTarget = Vector3.Lerp(
                _reelStartPosition,
                new Vector3(rodTipPosition.x, _reelStartPosition.y, rodTipPosition.z),
                _reelProgress);
            _activeTackle.SetManualReelTarget(
                surfaceTarget,
                rodTipPosition,
                GetReelLiftDistance(),
                GetReelSurfacePullStrength());
            SetAnimatorReelState(true, _reelProgress);
            ForceReelAnimationProgress();

            if (_reelProgress >= 1f)
                CompleteReel();
        }

        private void CompleteReel()
        {
            if (_activeTackle != null)
                Destroy(_activeTackle.gameObject);

            _activeTackle = null;
            _isReeling = false;
            _reelProgress = 0f;
            SetAnimatorReelState(false, 0f);
            UpdateRestingTackleVisual();
        }

        private void UpdateLineRenderer()
        {
            if (_lineRenderer == null)
                return;

            if (_activeLineOrigin == null || _activeTackle == null)
            {
                _lineRenderer.enabled = false;
                return;
            }

            _lineRenderer.enabled = true;

            int segmentCount = Mathf.Max(2, GetLineSegments());
            if (_lineRenderer.positionCount != segmentCount)
                _lineRenderer.positionCount = segmentCount;

            Vector3 start = _activeLineOrigin.position;
            Vector3 end = _activeTackle.transform.position;
            float slack = GetLineSlack();

            for (int i = 0; i < segmentCount; i++)
            {
                float t = segmentCount == 1 ? 0f : i / (float)(segmentCount - 1);
                Vector3 point = Vector3.Lerp(start, end, t);
                point.y -= Mathf.Sin(t * Mathf.PI) * slack;
                _lineRenderer.SetPosition(i, point);
            }
        }

        private void EnsureLineRenderer()
        {
            if (_activeRodItem == null)
                return;

            if (_lineRenderer != null)
                return;

            Transform lineTransform = _activeRodItem.transform.Find(LineObjectName);
            if (lineTransform == null)
            {
                GameObject lineObject = new(LineObjectName);
                lineTransform = lineObject.transform;
                lineTransform.SetParent(_activeRodItem.transform, false);
            }

            _lineRenderer = lineTransform.GetComponent<LineRenderer>();
            if (_lineRenderer == null)
                _lineRenderer = lineTransform.gameObject.AddComponent<LineRenderer>();

            _lineRenderer.useWorldSpace = true;
            _lineRenderer.alignment = LineAlignment.View;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.textureMode = LineTextureMode.Stretch;
            _lineRenderer.material = GetLineMaterial();
            _lineRenderer.startWidth = GetLineWidth();
            _lineRenderer.endWidth = GetLineWidth();
            _lineRenderer.widthMultiplier = GetLineWidth();
            _lineRenderer.positionCount = Mathf.Max(2, GetLineSegments());
            _lineRenderer.enabled = false;
        }

        private FishingTackleInstance SpawnTackleInstance()
        {
            GameObject tacklePrefab = _activeRod != null ? _activeRod.ActiveCastTacklePrefab : null;
            GameObject tackleObject = null;

            if (tacklePrefab != null)
            {
                tackleObject = Instantiate(tacklePrefab, _activeLineOrigin.position, Quaternion.identity);
            }
            else if (_restingTackleVisual != null)
            {
                tackleObject = Instantiate(_restingTackleVisual.gameObject, _activeLineOrigin.position, _restingTackleVisual.rotation);
                tackleObject.name = $"{_restingTackleVisual.name}_Cast";
                tackleObject.SetActive(true);
            }

            if (tackleObject == null)
                return null;

            ItemComponent itemComponent = tackleObject.GetComponent<ItemComponent>();
            if (itemComponent != null)
                Destroy(itemComponent);

            OutlineComponent outlineComponent = tackleObject.GetComponent<OutlineComponent>();
            if (outlineComponent != null)
                Destroy(outlineComponent);

            FishingTackleInstance tackleInstance = tackleObject.GetComponent<FishingTackleInstance>();
            if (tackleInstance == null)
                tackleInstance = tackleObject.AddComponent<FishingTackleInstance>();

            return tackleInstance;
        }

        private bool TryGetCastTarget(out Vector3 castTarget)
        {
            castTarget = transform.position;

            Camera cameraToUse = GetActiveGameplayCamera();
            if (cameraToUse == null)
                return false;

            float maxDistance = GetMaxCastDistance();
            Ray aimRay = cameraToUse.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float rayDistance = maxDistance;
            if (Physics.Raycast(aimRay, out RaycastHit hit, maxDistance, _castBlockingLayers, QueryTriggerInteraction.Ignore))
                rayDistance = Mathf.Max(1f, hit.distance);

            castTarget = aimRay.GetPoint(rayDistance);
            return true;
        }

        private void TriggerCastAnimation()
        {
            if (_animator == null || !_animatorParameters.Contains(_castRodTriggerHash))
                return;

            _animator.ResetTrigger(_castRodTriggerHash);
            _animator.SetTrigger(_castRodTriggerHash);
        }

        private void SetAnimatorRodEquipped(bool value)
        {
            if (_animator == null || !_animatorParameters.Contains(_hasRodEquippedHash))
                return;

            _animator.SetBool(_hasRodEquippedHash, value);
        }

        private void SetAnimatorReelState(bool isReeling, float progress)
        {
            if (_animator == null)
                return;

            if (_animatorParameters.Contains(_isReelingHash))
                _animator.SetBool(_isReelingHash, isReeling);

            if (_animatorParameters.Contains(_reelProgressHash))
                _animator.SetFloat(_reelProgressHash, Mathf.Clamp01(progress));
        }

        private void ForceReelAnimationProgress()
        {
            if (_animator == null || _upperBodyLayerIndex < 0)
                return;

            _animator.Play(_fishingReelStateHash, _upperBodyLayerIndex, Mathf.Clamp01(_reelProgress));
        }

        private void UpdateRestingTackleVisual()
        {
            if (_restingTackleVisual == null)
                return;

            bool showRestingTackle = _isRodEquipped && _activeTackle == null && !_isCastPending;
            if (_restingTackleVisual.gameObject.activeSelf != showRestingTackle)
                _restingTackleVisual.gameObject.SetActive(showRestingTackle);
        }

        private void ClearCastState(bool destroyTackle)
        {
            _isCastPending = false;
            _castReleasedByEvent = false;
            _isReeling = false;
            _reelProgress = 0f;

            if (destroyTackle && _activeTackle != null)
                Destroy(_activeTackle.gameObject);

            _activeTackle = null;

            if (_lineRenderer != null)
                _lineRenderer.enabled = false;

            _locomotionController?.ClearMovementLock();
            SetAnimatorReelState(false, 0f);

            UpdateRestingTackleVisual();
        }

        private Camera GetActiveGameplayCamera()
        {
            if (LocomotionInputManager.Instance != null
                && LocomotionInputManager.Instance.TryGetCameraContext(out CameraContext context)
                && context.UnityCamera != null)
            {
                return context.UnityCamera;
            }

            if (_locomotionController != null && _locomotionController.PlayerCamera != null)
                return _locomotionController.PlayerCamera;

            return Camera.main;
        }

        private void CacheAnimatorParameters()
        {
            _animatorParameters.Clear();
            if (_animator == null)
                return;

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
                _animatorParameters.Add(parameters[i].nameHash);
        }

        private Material GetLineMaterial()
        {
            if (_activeRod != null && _activeRod.LineMaterial != null)
                return _activeRod.LineMaterial;

            if (_fallbackLineMaterial != null)
                return _fallbackLineMaterial;

            if (s_RuntimeLineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    s_RuntimeLineMaterial = new Material(shader)
                    {
                        name = "FishingLine_Runtime"
                    };
                }
            }

            return s_RuntimeLineMaterial;
        }

        private float GetMaxCastDistance() => _activeRod != null ? _activeRod.MaxCastDistance : _fallbackMaxCastDistance;
        private float GetCastFlightDuration() => _activeRod != null ? _activeRod.CastFlightDuration : _fallbackCastFlightDuration;
        private float GetCastArcHeight() => _activeRod != null ? _activeRod.CastArcHeight : _fallbackCastArcHeight;
        private float GetCastLaunchForwardDistance() => _activeRod != null ? _activeRod.CastLaunchForwardDistance : _fallbackCastLaunchForwardDistance;
        private float GetCastLaunchUpwardLift() => _activeRod != null ? _activeRod.CastLaunchUpwardLift : _fallbackCastLaunchUpwardLift;
        private float GetCastCollisionEnableDelay() => _activeRod != null ? _activeRod.CastCollisionEnableDelay : _fallbackCastCollisionEnableDelay;
        private float GetCastWaterContactTimeout() => _activeRod != null ? _activeRod.CastWaterContactTimeout : _fallbackCastWaterContactTimeout;
        private float GetReelDuration() => _activeRod != null ? _activeRod.ReelDuration : _fallbackReelDuration;
        private float GetReelLiftDistance() => _activeRod != null ? _activeRod.ReelLiftDistance : _fallbackReelLiftDistance;
        private float GetReelSurfacePullStrength() => _activeRod != null ? _activeRod.ReelSurfacePullStrength : _fallbackReelSurfacePullStrength;
        private float GetMovementLockDuration() => _activeRod != null ? _activeRod.MovementLockDuration : _fallbackMovementLockDuration;
        private float GetSurfaceOffset() => _activeRod != null ? _activeRod.SurfaceOffset : _fallbackSurfaceOffset;
        private float GetFloatBobAmplitude() => _activeRod != null ? _activeRod.FloatBobAmplitude : _fallbackFloatBobAmplitude;
        private float GetFloatBobFrequency() => _activeRod != null ? _activeRod.FloatBobFrequency : _fallbackFloatBobFrequency;
        private float GetFloatDriftDistance() => _activeRod != null ? _activeRod.FloatDriftDistance : _fallbackFloatDriftDistance;
        private float GetFloatDriftFrequency() => _activeRod != null ? _activeRod.FloatDriftFrequency : _fallbackFloatDriftFrequency;
        private float GetFloatLineSlackDistance() => _activeRod != null ? _activeRod.FloatLineSlackDistance : _fallbackFloatLineSlackDistance;
        private float GetFloatSettleSpeed() => _activeRod != null ? _activeRod.FloatSettleSpeed : _fallbackFloatSettleSpeed;
        private float GetFloatTensionStrength() => _activeRod != null ? _activeRod.FloatTensionStrength : _fallbackFloatTensionStrength;
        private float GetTackleInterestMultiplier()
        {
            if (_activeRod != null)
                return _activeRod.DefaultBait != null
                    ? _activeRod.DefaultBait.interestMultiplier
                    : _activeRod.BaitlessInterestMultiplier;

            return _fallbackBaitlessInterestMultiplier;
        }

        private float GetTackleInterestRadius()
        {
            float baseRadius = _activeRod != null ? _activeRod.CurrentLureRange : _fallbackTackleInterestRadius;
            if (_activeRod != null && _activeRod.DefaultBait != null)
                return baseRadius * _activeRod.DefaultBait.radiusMultiplier;

            return baseRadius;
        }
        private int GetLineSegments() => _activeRod != null ? _activeRod.LineSegments : _fallbackLineSegments;
        private float GetLineWidth() => _activeRod != null ? _activeRod.LineWidth : _fallbackLineWidth;
        private float GetLineSlack() => _activeRod != null ? _activeRod.LineSlack : _fallbackLineSlack;
    }
}
