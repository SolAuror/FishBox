using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Actions;
using Sol.Grab;
using Sol.Locomotion;
using Sol.Outline;

namespace Sol.Fishing
{
    [DisallowMultipleComponent]
    public class FishingState : MonoBehaviour
    {
        private const string DefaultRodName = "Fishing Rod";
        private const string DefaultTackleName = "Tackle";
        private const string LineObjectName = "[FishingLine]";
#region Inspector Settings

        [Header("Animator")]
        [Tooltip("Inspector: tunes animator.")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _hasRodEquippedParameter = "hasRodEquipped";
        [Tooltip("Inspector: tunes cast rod trigger parameter.")]
        [SerializeField] private string _castRodTriggerParameter = "CastRod";
        [SerializeField] private string _isReelingParameter = "isReeling";
        [Tooltip("Inspector: tunes reel progress parameter.")]
        [SerializeField] private string _reelProgressParameter = "ReelProgress";

        [Header("References")]
        [Tooltip("Inspector: tunes equipment.")]
        [SerializeField] private Equipment _equipment;
        [SerializeField] private LocomotionInput _locomotionInput;
        [Tooltip("Inspector: tunes locomotion controller.")]
        [SerializeField] private LocomotionController _locomotionController;

        [Header("Fallback Rod Settings")]
        [Tooltip("Inspector: tunes fallback rod item name.")]
        [SerializeField] private string _fallbackRodItemName = DefaultRodName;
        [Tooltip("Inspector: tunes fallback tackle visual name.")]
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
        [Tooltip("Inspector: tunes fallback surface offset.")]
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

        [Header("Safety")]
        [SerializeField, Min(0.5f)] private float _castAnimationTimeout = 2f;
        [SerializeField, Min(0f)] private float _reelProgressResetDelay = 1f;
        [SerializeField, Min(0.5f)] private float _fallbackHookEscapeWindow = 2f;

        [Header("Fallback Line")]
        [SerializeField, Min(2)] private int _fallbackLineSegments = 12;
        [SerializeField, Min(0.001f)] private float _fallbackLineWidth = 0.015f;
        [SerializeField, Min(0f)] private float _fallbackLineSlack = 0.2f;
        [Tooltip("Inspector: tunes fallback line material.")]
        [SerializeField] private Material _fallbackLineMaterial;

        [Header("Cast Query")]
        [Tooltip("Inspector: tunes cast blocking layers.")]
        [SerializeField] private LayerMask _castBlockingLayers = ~0;
#endregion

        private static Material s_RuntimeLineMaterial;

        private readonly HashSet<int> _animatorParameters = new();

        private FishingRodItem _activeRod;
        private ItemComponent _activeRodItem;
        private Transform _activeLineOrigin;
        private Transform _restingTackleVisual;
        private LineRenderer _lineRenderer;
        private FishingTackleInstance _activeTackle;
        private ItemComponent _displayedCatchItem;
        private GrabbableComponent _displayedCatchGrabbable;
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
        private float _castStartTime;
        private bool _isReeling;
        private float _reelProgress;
        private float _reelTotalDistance;
        private float _reelReleaseTime = -1f;
        private Vector3 _reelStartPosition;

        public bool ShouldBlockDefaultAttack => _isRodEquipped;
        public bool HasLineOut => _activeTackle != null || _isCastPending;
        public bool HasEquippedRod => _isRodEquipped && _activeRod != null;
        public bool HasDisplayedCatch => _displayedCatchItem != null;
        public string DisplayedCatchPrompt => _displayedCatchItem == null
            ? string.Empty
            : $"E Take {_displayedCatchItem.ItemName}\nHold E Grab\nQ Drop";

        public bool CanCast => _isRodEquipped
            && _activeRodItem != null
            && _activeLineOrigin != null
            && !_isCastPending
            && _activeTackle == null
            && _displayedCatchItem == null;

        public void ExecuteCast() => BeginCast();

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
            if (_isCastPending && !_castReleasedByEvent && Time.time > _castStartTime + _castAnimationTimeout)
                ClearCastState(destroyTackle: false);

            UpdateDisplayedCatchState();
            UpdateTackleState();
            UpdateLineRenderer();

            if (!_isRodEquipped || _locomotionInput == null)
                return;

            if (_activeTackle != null)
            {
                UpdateReelInput();
                return;
            }

            if (!_locomotionInput.AttackPressed || _isCastPending || _displayedCatchItem != null)
                return;

            _locomotionInput.SetAttackPressedFalse();
            if (ActionSystem.Instance != null)
                ActionSystem.Instance.Dispatch(new CastFishingRodAction(), gameObject, null);
            else
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
            if (!HasEquippedRod || HasLineOut || _displayedCatchItem != null || item == null)
                return false;

            return _activeRod != null
                && _activeRod.LoadedBaitItem == null
                && item.GetComponent<FishingTackleItem>() != null;
        }

        public bool CanLoadBait(ItemComponent item)
        {
            if (!HasEquippedRod || HasLineOut || _displayedCatchItem != null || item == null)
                return false;

            return _activeRod != null
                && _activeRod.LoadedTackleItem != null
                && FishingBaitItem.IsSupportedBait(item);
        }

        public bool CanDetachTackle()
        {
            return HasEquippedRod
                && !HasLineOut
                && _displayedCatchItem == null
                && _activeRod != null
                && _activeRod.LoadedTackleItem != null
                && _activeRod.LoadedBaitItem == null;
        }

        public bool CanDetachBait()
        {
            return HasEquippedRod && !HasLineOut && _displayedCatchItem == null && _activeRod != null && _activeRod.LoadedBaitItem != null;
        }

        public bool TryTakeDisplayedCatch(Interactor interactor)
        {
            if (_displayedCatchItem == null || interactor?.Inventory == null)
                return false;

            ItemComponent item = _displayedCatchItem;
            DetachDisplayedCatchItemToWorld();

            bool added = interactor.Inventory.Add(item);
            if (added)
            {
                if (item != null)
                    item.gameObject.SetActive(false);

                return true;
            }

            AttachDisplayedCatchItem(item);
            return false;
        }

        public bool TryDropDisplayedCatch()
        {
            if (_displayedCatchItem == null)
                return false;

            DetachDisplayedCatchItemToWorld();
            return true;
        }

        public bool TryGrabDisplayedCatch(GameObject actor)
        {
            if (_displayedCatchItem == null || _displayedCatchGrabbable == null || actor == null || ActionSystem.Instance == null)
                return false;

            GrabbableComponent grabbable = _displayedCatchGrabbable;
            DetachDisplayedCatchItemToWorld();

            if (grabbable == null || grabbable.IsGrabbed)
                return false;

            return ActionSystem.Instance.Dispatch(new StartGrabAction(), actor, grabbable.gameObject);
        }

        public bool TryLoadTackle(InventorySlot slot, Inventory inventory)
        {
            if (slot?.Item == null || inventory == null || !CanLoadTackle(slot.Item))
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
                    // Inventory full ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â abort swap. Restore the old tackle to the rod.
                    previousTackle.transform.SetParent(_activeRod.transform, false);
                    _activeRod.SetLoadedTackleItem(previousTackle);
                    // The slot was already removed from inventory; return the new item safely.
                    ReturnItemToInventoryOrDrop(slotItem, inventory);
                    return false;
                }
            }

            slotItem.transform.SetParent(_activeRod.transform, false);
            slotItem.gameObject.SetActive(false);
            _activeRod.SetLoadedTackleItem(slotItem);
            return true;
        }

        public bool TryLoadBait(InventorySlot slot, Inventory inventory)
        {
            if (slot?.Item == null || inventory == null || !CanLoadBait(slot.Item))
                return false;

            ItemComponent slotItem = slot.PopItem();
            if (slotItem == null)
                return false;

            if (!inventory.Remove(slot, 1))
                return false;

            ItemComponent previousBait = _activeRod.UnloadBaitItem();
            if (previousBait != null)
            {
                previousBait.transform.SetParent(inventory.transform, false);
                previousBait.gameObject.SetActive(false);
                if (!inventory.Add(previousBait))
                {
                    // Inventory full ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â abort swap. Restore the old bait to the rod.
                    previousBait.transform.SetParent(_activeRod.transform, false);
                    _activeRod.SetLoadedBaitItem(previousBait);
                    ReturnItemToInventoryOrDrop(slotItem, inventory);
                    return false;
                }
            }

            _activeRod.SetLoadedBaitItem(slotItem);
            return true;
        }

        public bool TryDetachTackle(Inventory inventory)
        {
            if (!CanDetachTackle() || inventory == null)
                return false;

            ItemComponent tackle = _activeRod.UnloadTackleItem();
            if (tackle == null)
                return false;

            tackle.transform.SetParent(inventory.transform, false);
            tackle.gameObject.SetActive(false);
            if (inventory.Add(tackle))
                return true;

            _activeRod.SetLoadedTackleItem(tackle);
            return false;
        }

        public bool TryDetachBait(Inventory inventory)
        {
            if (!CanDetachBait() || inventory == null)
                return false;

            ItemComponent bait = _activeRod.UnloadBaitItem();
            if (bait == null)
                return false;

            bait.transform.SetParent(inventory.transform, false);
            bait.gameObject.SetActive(false);
            if (inventory.Add(bait))
                return true;

            _activeRod.SetLoadedBaitItem(bait);
            return false;
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
                DetachDisplayedCatchItemToWorld();
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
            if (_activeRodItem == null || _activeLineOrigin == null || _displayedCatchItem != null)
                return;

            if (!TryGetCastTarget(out Vector3 castTarget))
                return;

            _pendingCastTarget = castTarget;
            _pendingWaterVolume = WaterVolume.FindVolumeXZ(castTarget);
            _isCastPending = true;
            _castReleasedByEvent = false;
            _castStartTime = Time.time;

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

            if (_activeRod != null)
            {
                Transform baitPoint = _activeRod.FindBaitPoint(_activeTackle.transform);
                _activeRod.AttachLoadedBaitToExternalPoint(baitPoint);
            }

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
                GetTackleInterestRadius(),
                GetCurrentBaitItemId(),
                GetCurrentBaitName());
            _activeTackle.SetHookEscapeWindow(GetHookEscapeWindow());

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
            {
                _reelStartPosition = _activeTackle.transform.position;
                _reelTotalDistance = Vector3.Distance(_reelStartPosition, _activeLineOrigin.position);
                ForceReelAnimationProgress();
            }

            _isReeling = true;
            SetAnimatorReelState(true, _reelProgress);
        }

        private void UpdateTackleState()
        {
            if (_activeTackle != null)
                _activeTackle.SetReelInputActive(_isReeling);

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
                _reelReleaseTime = -1f;
                BeginReel();
                return;
            }

            if (_isReeling)
            {
                _isReeling = false;
                SetAnimatorReelState(false, _reelProgress);

                bool fishHooked = _activeTackle != null && _activeTackle.HasHookedFish;
                if (!fishHooked)
                    ResetReelProgress();
                else
                    _reelReleaseTime = Time.time;
            }
            else if (_reelReleaseTime >= 0f && Time.time >= _reelReleaseTime + _reelProgressResetDelay)
            {
                ResetReelProgress();
            }
        }

        private void ResetReelProgress()
        {
            _reelProgress = 0f;
            _reelTotalDistance = 0f;
            _reelReleaseTime = -1f;
            SetAnimatorReelState(false, 0f);
        }

        private void UpdateReelProgress()
        {
            if (_activeTackle == null || _activeLineOrigin == null)
            {
                ClearCastState(destroyTackle: true);
                return;
            }

            Vector3 rodTipPosition = _activeLineOrigin.position;

            // Surface target is the rod tip's XZ position at start-height. This is independent
            // of _reelProgress, which breaks the circular dependency that previously kept
            // surfaceTarget == tackle position (progress=0 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ lerp to start ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ tackle can't move).
            Vector3 surfaceTarget = new Vector3(rodTipPosition.x, _reelStartPosition.y, rodTipPosition.z);

            // Tell the tackle where to go first, then measure how far it has come.
            _activeTackle.SetManualReelTarget(
                surfaceTarget,
                rodTipPosition,
                GetReelLiftDistance(),
                GetReelSurfacePullStrength());

            float remaining = Vector3.Distance(_activeTackle.transform.position, rodTipPosition);
            float initial   = Mathf.Max(0.05f, _reelTotalDistance);
            _reelProgress   = Mathf.Clamp01(1f - remaining / initial);

            SetAnimatorReelState(true, _reelProgress);

            // 0.99f threshold: Distance() returning exactly 0 requires a perfect float snap.
            // ShouldCompleteReel is the physics signal (tackle within 0.1 units of rod tip).
            if (_reelProgress >= 0.99f || _activeTackle.ShouldCompleteReel)
                CompleteReel();
        }

        private void CompleteReel()
        {
            if (_activeTackle != null && _activeTackle.HasHookedFish)
            {
                AI_Fish hookedFish = _activeTackle.ConsumeHookedFish();
                if (hookedFish != null)
                {
                    ItemComponent caughtItem = hookedFish.Catch(null, _activeRod != null ? _activeRod.transform : transform);
                    if (caughtItem != null)
                        AttachDisplayedCatchItem(caughtItem);
                }
            }

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
            bool showRestingTackle = _isRodEquipped && _activeTackle == null && !_isCastPending && _displayedCatchItem == null;
            ItemComponent loadedTackleItem = _activeRod != null ? _activeRod.LoadedTackleItem : null;

            if (loadedTackleItem != null)
            {
                if (loadedTackleItem.gameObject.activeSelf != showRestingTackle)
                    loadedTackleItem.gameObject.SetActive(showRestingTackle);
            }
            else if (_restingTackleVisual != null && _restingTackleVisual.gameObject.activeSelf)
            {
                _restingTackleVisual.gameObject.SetActive(false);
            }
        }

        private void ClearCastState(bool destroyTackle)
        {
            _isCastPending = false;
            _castReleasedByEvent = false;
            _isReeling = false;
            _reelProgress = 0f;
            _reelTotalDistance = 0f;
            _reelReleaseTime = -1f;

            if (_activeTackle != null && _activeRod != null)
                _activeRod.ReattachLoadedBaitToRod();

            if (destroyTackle && _activeTackle != null)
                Destroy(_activeTackle.gameObject);

            _activeTackle = null;

            if (_lineRenderer != null)
                _lineRenderer.enabled = false;

            _locomotionController?.ClearMovementLock();
            SetAnimatorReelState(false, 0f);

            UpdateRestingTackleVisual();
        }

        private void UpdateDisplayedCatchState()
        {
            if (_displayedCatchItem == null)
                return;

            if (!_displayedCatchItem.gameObject.activeInHierarchy)
            {
                ClearDisplayedCatchReference(_displayedCatchItem);
                return;
            }

            if (_displayedCatchGrabbable != null && _displayedCatchGrabbable.IsGrabbed)
                DetachDisplayedCatchItem();
        }

        private void AttachDisplayedCatchItem(ItemComponent item)
        {
            if (item == null || _activeRod == null)
                return;

            if (_displayedCatchItem != null && _displayedCatchItem != item)
                DetachDisplayedCatchItemToWorld();

            _displayedCatchItem = item;
            _displayedCatchGrabbable = item.GetComponent<GrabbableComponent>()
                ?? item.GetComponentInChildren<GrabbableComponent>(true);
            _activeRod.SetDisplayedCatchItem(item);
            UpdateRestingTackleVisual();
        }

        private void DetachDisplayedCatchItem()
        {
            if (_displayedCatchItem == null)
                return;

            ItemComponent item = _displayedCatchItem;
            if (_activeRod != null)
                _activeRod.ReleaseDisplayedCatchItem(item, keepWorldTransform: true);
            else
                item.transform.SetParent(null, true);

            if (_displayedCatchGrabbable != null && _displayedCatchGrabbable.IsGrabbed)
                _displayedCatchGrabbable.RefreshStoredPhysicsState();

            ClearDisplayedCatchReference(item);
        }

        private void DetachDisplayedCatchItemToWorld()
        {
            if (_displayedCatchItem == null)
                return;

            ItemComponent item = _displayedCatchItem;
            if (_activeRod != null)
                _activeRod.ReleaseDisplayedCatchItem(item, keepWorldTransform: true);
            else
                item.transform.SetParent(null, true);

            ClearDisplayedCatchReference(item);
        }

        private void ClearDisplayedCatchReference(ItemComponent item)
        {
            if (item == null || _displayedCatchItem != item)
                return;

            _activeRod?.ForgetDisplayedCatchItem(item);
            _displayedCatchItem = null;
            _displayedCatchGrabbable = null;
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

        private void ReturnItemToInventoryOrDrop(ItemComponent item, Inventory inventory)
        {
            if (item == null)
                return;

            if (!inventory.Add(item))
            {
                item.transform.SetParent(null, worldPositionStays: true);
                item.gameObject.SetActive(true);
            }
        }

        private float GetHookEscapeWindow() => _fallbackHookEscapeWindow;

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
        private float GetReelSpeed()
        {
            float maxCastDistance = GetMaxCastDistance();
            float referenceDuration = GetReelDuration();
            return maxCastDistance / Mathf.Max(0.05f, referenceDuration);
        }
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
            {
                return FishingBaitItem.ResolveInterestMultiplier(
                    _activeRod.LoadedBaitItem,
                    _activeRod.DefaultBait,
                    _activeRod.BaitlessInterestMultiplier);
            }

            return _fallbackBaitlessInterestMultiplier;
        }

        private float GetTackleInterestRadius()
        {
            float baseRadius = _activeRod != null ? _activeRod.CurrentLureRange : _fallbackTackleInterestRadius;
            if (_activeRod != null)
            {
                return baseRadius * FishingBaitItem.ResolveRadiusMultiplier(_activeRod.LoadedBaitItem, _activeRod.DefaultBait);
            }

            return baseRadius;
        }

        private string GetCurrentBaitItemId()
        {
            return _activeRod != null
                ? FishingBaitItem.ResolveBaitItemId(_activeRod.LoadedBaitItem)
                : string.Empty;
        }

        private string GetCurrentBaitName()
        {
            if (_activeRod == null)
                return string.Empty;

            return FishingBaitItem.ResolveBaitName(_activeRod.LoadedBaitItem, _activeRod.DefaultBait);
        }

        private float CalculateReelDistance(Vector3 tacklePosition, Vector3 rodTipPosition)
        {
            Vector3 surfaceLegStart = new Vector3(tacklePosition.x, tacklePosition.y, tacklePosition.z);
            Vector3 surfaceLegEnd = new Vector3(rodTipPosition.x, tacklePosition.y, rodTipPosition.z);
            float surfaceDistance = Vector3.Distance(surfaceLegStart, surfaceLegEnd);
            float liftDistance = Vector3.Distance(surfaceLegEnd, rodTipPosition);
            return Mathf.Max(0.05f, surfaceDistance + liftDistance);
        }

        private int GetLineSegments() => _activeRod != null ? _activeRod.LineSegments : _fallbackLineSegments;
        private float GetLineWidth() => _activeRod != null ? _activeRod.LineWidth : _fallbackLineWidth;
        private float GetLineSlack() => _activeRod != null ? _activeRod.LineSlack : _fallbackLineSlack;
    }
}
