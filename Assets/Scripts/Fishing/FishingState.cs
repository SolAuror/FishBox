using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Actions;
using Sol.Grab;
using Sol.Locomotion;
using Sol.Outline;
using Sol.Audio;
using Sol.Rpg;

namespace Sol.Fishing
{
    [DisallowMultipleComponent]
    public class FishingState : MonoBehaviour
    {
        private const string DefaultRodName = "Fishing Rod";
        private const string DefaultLureName = "Lure";
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
        [Tooltip("Inspector: tunes fallback lure visual name.")]
        [SerializeField] private string _fallbackLureVisualName = DefaultLureName;
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
        [SerializeField, Min(0f)] private float _fallbackFloatLineSlackDistance = 0.5f;
        [SerializeField, Min(0.01f)] private float _fallbackLineSlackRecoverSpeed = 0.35f;
        [SerializeField, Min(0.05f)] private float _fallbackFloatSettleSpeed = 4.5f;
        [SerializeField, Min(0f)] private float _fallbackFloatTensionStrength = 8f;
        [SerializeField, Min(0.25f)] private float _fallbackBaitlessInterestMultiplier = 0.35f;
        [SerializeField, Min(0.5f)] private float _fallbackLureInterestRadius = 6f;

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
        private Transform _restingLureVisual;
        private LineRenderer _lineRenderer;
        private FishingLureInstance _activeLure;
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
        private float _currentLineTautDistance;
        private float _currentLineSlack;
        private bool _hasRuntimeLineLength;
        private string _fishingStatusText = string.Empty;
        private float _fishingStatusUntil;

        public bool ShouldBlockDefaultAttack => _isRodEquipped;
        public bool ShouldBlockCombatToggle => _isRodEquipped && (_activeLure != null || _isCastPending || _displayedCatchItem != null);
        public bool HasLineOut => _activeLure != null || _isCastPending;
        public bool HasEquippedRod => _isRodEquipped && _activeRod != null;
        public bool HasDisplayedCatch => _displayedCatchItem != null;
        public string DisplayedCatchPrompt => _displayedCatchItem == null
            ? string.Empty
            : $"E Take {_displayedCatchItem.ItemName}\nHold E Grab\nQ Drop";
        public string FishingStatusText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_fishingStatusText) && Time.time < _fishingStatusUntil)
                    return _fishingStatusText;

                if (_activeLure != null && _activeLure.HasHookedFish)
                    return _activeLure.LineTension >= 0.85f ? "Ease off" : "Hooked";

                return string.Empty;
            }
        }
        public bool HasFishingStatus => !string.IsNullOrWhiteSpace(FishingStatusText);
        public float FishingStatusUntil => _fishingStatusUntil;

        public bool CanCast => _isRodEquipped
            && _activeRodItem != null
            && _activeLineOrigin != null
            && !_isCastPending
            && _activeLure == null
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
            ClearCastState(destroyLure: true);
        }

        private void Update()
        {
            if (_isCastPending && !_castReleasedByEvent && Time.time > _castStartTime + _castAnimationTimeout)
                ClearCastState(destroyLure: false);

            UpdateDisplayedCatchState();
            UpdateLureState();
            UpdateLineRenderer();

            if (!_isRodEquipped || _locomotionInput == null)
                return;

            if (_activeLure != null)
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

        public bool CanLoadLure(ItemComponent item)
        {
            if (!HasEquippedRod || HasLineOut || _displayedCatchItem != null || item == null)
                return false;

            return _activeRod != null
                && _activeRod.LoadedBaitItem == null
                && item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFishingLure);
        }

        public bool CanLoadBait(ItemComponent item)
        {
            if (!HasEquippedRod || HasLineOut || _displayedCatchItem != null || item == null)
                return false;

            return _activeRod != null
                && _activeRod.LoadedLureItem != null
                && FishingBaitItem.IsSupportedBait(item);
        }

        public bool CanDetachLure()
        {
            return HasEquippedRod
                && !HasLineOut
                && _displayedCatchItem == null
                && _activeRod != null
                && _activeRod.LoadedLureItem != null
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

        public bool TryLoadLure(InventorySlot slot, Inventory inventory)
        {
            if (slot?.Item == null || inventory == null || !CanLoadLure(slot.Item))
                return false;

            inventory.BeginBulkUpdate();
            try
            {
                ItemComponent slotItem = slot.PopItem();
                if (slotItem == null)
                    return false;

                if (!slotItem.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFishingLure))
                    return false;

                if (!inventory.Remove(slot, 1))
                    return false;

                ItemComponent previousLure = _activeRod.UnloadLureItem();
                if (previousLure != null)
                {
                    previousLure.transform.SetParent(inventory.transform, false);
                    previousLure.gameObject.SetActive(false);
                    if (!inventory.Add(previousLure))
                    {
 // Inventory full - abort swap. Restore the old lure to the rod.
                        previousLure.transform.SetParent(_activeRod.transform, false);
                        _activeRod.SetLoadedLureItem(previousLure);
                        // The slot was already removed from inventory; return the new item safely.
                        ReturnItemToInventoryOrDrop(slotItem, inventory);
                        return false;
                    }
                }

                slotItem.transform.SetParent(_activeRod.transform, false);
                slotItem.gameObject.SetActive(false);
                _activeRod.SetLoadedLureItem(slotItem);
                return true;
            }
            finally
            {
                inventory.EndBulkUpdate();
            }
        }

        public bool TryLoadBait(InventorySlot slot, Inventory inventory)
        {
            if (slot?.Item == null || inventory == null || !CanLoadBait(slot.Item))
                return false;

            inventory.BeginBulkUpdate();
            try
            {
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
 // Inventory full - abort swap. Restore the old bait to the rod.
                        previousBait.transform.SetParent(_activeRod.transform, false);
                        _activeRod.SetLoadedBaitItem(previousBait);
                        ReturnItemToInventoryOrDrop(slotItem, inventory);
                        return false;
                    }
                }

                _activeRod.SetLoadedBaitItem(slotItem);
                return true;
            }
            finally
            {
                inventory.EndBulkUpdate();
            }
        }

        public bool TryDetachLure(Inventory inventory)
        {
            if (!CanDetachLure() || inventory == null)
                return false;

            ItemComponent lure = _activeRod.UnloadLureItem();
            if (lure == null)
                return false;

            lure.transform.SetParent(inventory.transform, false);
            lure.gameObject.SetActive(false);
            if (inventory.Add(lure))
                return true;

            _activeRod.SetLoadedLureItem(lure);
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
                ClearCastState(destroyLure: true);
                _activeRodItem = resolvedItem;
                _activeRod = resolvedRod;
                _lineRenderer = null;
            }

            _isRodEquipped = _activeRodItem != null;
            CacheActiveRodReferences();
            SetAnimatorRodEquipped(_isRodEquipped);
            UpdateRestingLureVisual();
        }

        private bool IsFishingRod(ItemComponent item, out FishingRodItem rod)
        {
            rod = null;
            if (item == null)
                return false;

            rod = item.GetComponent<FishingRodItem>();
            if (rod != null || item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFishingRod))
                return true;

            return !string.IsNullOrWhiteSpace(_fallbackRodItemName)
                && string.Equals(item.ItemName, _fallbackRodItemName, System.StringComparison.OrdinalIgnoreCase);
        }

        private void CacheActiveRodReferences()
        {
            if (_activeRod != null)
            {
                _activeLineOrigin = _activeRod.LineOrigin;
                _restingLureVisual = _activeRod.RestingLureVisual;
                return;
            }

            _activeLineOrigin = _activeRodItem != null ? _activeRodItem.transform : null;
            _restingLureVisual = null;

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

                if (_restingLureVisual == null && child.name == _fallbackLureVisualName)
                    _restingLureVisual = child;
            }

            if (_activeLineOrigin == null && _restingLureVisual != null)
                _activeLineOrigin = _restingLureVisual;
        }

        private void BeginCast()
        {
            if (_activeRodItem == null || _activeLineOrigin == null || _displayedCatchItem != null)
                return;

            if (!TryGetCastTarget(out Vector3 castTarget))
                return;

            ClearFishingStatus();
            _pendingCastTarget = castTarget;
            _pendingWaterVolume = WaterVolume.FindVolumeXZ(castTarget);
            _isCastPending = true;
            _castReleasedByEvent = false;
            _castStartTime = Time.time;
            AudioService.Instance?.PlaySfx(AudioEvent.FishingCast, _activeLineOrigin.position);

            TriggerCastAnimation();
            _locomotionController?.SetMovementLock(GetMovementLockDuration());
            UpdateRestingLureVisual();
        }

        private void ReleaseCast()
        {
            if (!_isCastPending || _activeLineOrigin == null)
                return;

            _isCastPending = false;

            FishingLureInstance lureInstance = SpawnLureInstance();
            if (lureInstance == null)
                return;

            _activeLure = lureInstance;
            _activeLure.OnEnteredFloating += HandleLureEnteredFloating;

            if (_activeRod != null)
            {
                Transform baitPoint = _activeRod.FindBaitPoint(_activeLure.transform);
                _activeRod.AttachLoadedBaitToExternalPoint(baitPoint);
            }

            _activeLure.Launch(
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
                GetLureInterestMultiplier(),
                GetLureInterestRadius(),
                GetCurrentBaitItemId(),
                GetCurrentBaitName(),
                GetCurrentBaitTagPaths());
            _activeLure.SetHookEscapeWindow(GetHookEscapeWindow());

            EnsureLineRenderer();
            ResetRuntimeLineLength();
            UpdateRestingLureVisual();
        }

        private void BeginReel()
        {
            if (_activeLure == null || _activeLineOrigin == null)
            {
                ClearCastState(destroyLure: true);
                return;
            }

            if (!_isReeling)
            {
                _reelStartPosition = _activeLure.transform.position;
                _reelTotalDistance = Vector3.Distance(_reelStartPosition, _activeLineOrigin.position);
                ForceReelAnimationProgress();
                AudioService.Instance?.PlaySfx(AudioEvent.FishingReelLoopStart, _activeLineOrigin.position);
            }

            _isReeling = true;
            SetAnimatorReelState(true, _reelProgress);
        }

        private void UpdateLureState()
        {
            bool fishHooked = _activeLure != null && _activeLure.HasHookedFish;
            if (fishHooked && _activeLineOrigin != null)
                _activeLure.SetFightContext(
                    _activeLineOrigin.position,
                    GetReelSurfacePullStrength(),
                    GetHookedFishEscapeDirection());

            if (_activeLure != null)
                _activeLure.SetReelInputActive(_isReeling);

            if (_activeLure != null && _activeLure.HasTerminalOutcome)
            {
                HandleLureTerminalOutcome(_activeLure.TerminalOutcome);
                return;
            }

            if (_activeLure != null && _activeLure.ShouldCancelCast)
            {
                HandleLureTerminalOutcome(FishingLureOutcome.InvalidCast);
                return;
            }

            if (_activeLure != null && _isReeling)
                UpdateReelProgress();

            if (_activeLure != null && _activeLineOrigin != null && (_activeLure.HasLanded || fishHooked))
                UpdateRuntimeLineLength(fishHooked);
            // Float anchor uses the cast line length plus recoverable slack, not the slack
            // alone. This keeps long casts out while player movement can still pull the
            // lure once the line becomes taut.
            if (_activeLure != null && _activeLure.IsFloating && !_isReeling && !_activeLure.HasHookedFish && _activeLineOrigin != null)
            {
                _activeLure.SetFloatAnchor(
                    _activeLineOrigin.position,
                    GetFloatAnchorDistance(),
                    GetFloatSettleSpeed(),
                    GetFloatTensionStrength(),
                    GetFloatDriftDistance(),
                    GetFloatDriftFrequency());
            }

            if (_activeLure != null)
                return;

            if (!_isCastPending)
                UpdateRestingLureVisual();
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
                AudioService.Instance?.PlaySfx(AudioEvent.FishingReelLoopStop, _activeLineOrigin != null ? _activeLineOrigin.position : transform.position);

                bool fishHooked = _activeLure != null && _activeLure.HasHookedFish;
                if (!fishHooked)
                {
                    _activeLure?.StopManualReel();
                    FreezeRuntimeLineAtCurrentLureDistance();
                    ResetReelProgress();
                }
                else
                {
                    _reelReleaseTime = Time.time;
                }
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
            if (_activeLure == null || _activeLineOrigin == null)
            {
                ClearCastState(destroyLure: true);
                return;
            }

            Vector3 rodTipPosition = _activeLineOrigin.position;

            // Surface target is the rod tip's XZ position at start-height. This is independent
            // of _reelProgress, which breaks the circular dependency that previously kept
 // surfaceTarget == lure position (progress=0 - lerp to start - lure can't move).
            Vector3 surfaceTarget = new Vector3(rodTipPosition.x, _reelStartPosition.y, rodTipPosition.z);

            // Tell the lure where to go first, then measure how far it has come.
            _activeLure.SetManualReelTarget(
                surfaceTarget,
                rodTipPosition,
                GetReelLiftDistance(),
                GetReelSurfacePullStrength());

            float remaining = Vector3.Distance(_activeLure.transform.position, rodTipPosition);
            float initial   = Mathf.Max(0.05f, _reelTotalDistance);
            _reelProgress   = Mathf.Clamp01(1f - remaining / initial);

            SetAnimatorReelState(true, _reelProgress);

            // 0.99f threshold: Distance() returning exactly 0 requires a perfect float snap.
            if (_reelProgress >= 0.99f && !_activeLure.HasHookedFish)
                CompleteReel();
        }

        private void UpdateRuntimeLineLength(bool fishHooked)
        {
            if (_activeLure == null || _activeLineOrigin == null)
            {
                ResetRuntimeLineLength();
                return;
            }

            float currentDistance = Vector3.Distance(_activeLure.transform.position, _activeLineOrigin.position);
            if (!_hasRuntimeLineLength)
                InitializeRuntimeLineLength(currentDistance);

            float defaultSlack = GetFloatLineSlackDistance();
            if (_isReeling)
            {
                float reelStep = GetReelSurfacePullStrength() * Time.deltaTime;
                _currentLineSlack = Mathf.MoveTowards(_currentLineSlack, 0f, reelStep);
                _currentLineTautDistance = Mathf.Min(_currentLineTautDistance, currentDistance + _currentLineSlack);
                _currentLineTautDistance = Mathf.Max(0.05f, _currentLineTautDistance - reelStep);
            }
            else
            {
                _currentLineSlack = Mathf.MoveTowards(
                    _currentLineSlack,
                    defaultSlack,
                    GetLineSlackRecoverSpeed() * Time.deltaTime);

                if (fishHooked)
                    _currentLineTautDistance = Mathf.Max(_currentLineTautDistance, currentDistance);
            }
        }

        private void InitializeRuntimeLineLength(float currentDistance)
        {
            _currentLineTautDistance = Mathf.Max(0.05f, currentDistance);
            _currentLineSlack = GetFloatLineSlackDistance();
            _hasRuntimeLineLength = true;
        }

        private void ResetRuntimeLineLength()
        {
            _currentLineTautDistance = 0f;
            _currentLineSlack = 0f;
            _hasRuntimeLineLength = false;
        }

        private void FreezeRuntimeLineAtCurrentLureDistance()
        {
            if (_activeLure == null || _activeLineOrigin == null)
                return;

            float currentDistance = Vector3.Distance(_activeLure.transform.position, _activeLineOrigin.position);
            _currentLineTautDistance = Mathf.Max(0.05f, currentDistance);
            _currentLineSlack = Mathf.Max(0f, _currentLineSlack);
            _hasRuntimeLineLength = true;
        }

        private float GetFloatAnchorDistance()
        {
            if (!_hasRuntimeLineLength && _activeLure != null && _activeLineOrigin != null)
                InitializeRuntimeLineLength(Vector3.Distance(_activeLure.transform.position, _activeLineOrigin.position));

            return _hasRuntimeLineLength
                ? Mathf.Max(0.05f, _currentLineTautDistance + _currentLineSlack)
                : GetMaxCastDistance() + GetFloatLineSlackDistance();
        }

        private void HandleLureTerminalOutcome(FishingLureOutcome outcome)
        {
            if (outcome == FishingLureOutcome.None)
                return;

            switch (outcome)
            {
                case FishingLureOutcome.Caught:
                    CompleteCatchFromLure();
                    break;
                case FishingLureOutcome.FishEscaped:
                    ShowFishingStatus("Fish escaped");
                    break;
                case FishingLureOutcome.LineSnapped:
                    ShowFishingStatus("Line snapped");
                    break;
            }

            ClearCastState(destroyLure: true);
        }

        private void CompleteCatchFromLure()
        {
            if (_activeLure == null || !_activeLure.HasHookedFish)
                return;

            AI_Fish hookedFish = _activeLure.ConsumeHookedFish();
            if (hookedFish == null)
                return;

            ItemComponent caughtItem = hookedFish.Catch(null, _activeRod != null ? _activeRod.transform : transform);
            if (caughtItem != null)
                AttachDisplayedCatchItem(caughtItem);
        }

        private void CompleteReel()
        {
            HandleLureTerminalOutcome(FishingLureOutcome.Retrieved);
        }

        private void ShowFishingStatus(string text, float duration = 1.5f)
        {
            _fishingStatusText = text ?? string.Empty;
            _fishingStatusUntil = Time.time + Mathf.Max(0.05f, duration);
        }

        private void ClearFishingStatus()
        {
            _fishingStatusText = string.Empty;
            _fishingStatusUntil = 0f;
        }

        private static readonly Color LineRestColor = Color.black;
        private static readonly Color LineSafeColor = new(0.35f, 1f, 0.45f, 1f);
        private static readonly Color LineWarnColor = new(1f, 0.95f, 0.3f, 1f);
        private static readonly Color LineDangerColor = new(1f, 0.35f, 0.35f, 1f);

        private void UpdateLineRenderer()
        {
            if (_lineRenderer == null)
                return;

            if (_activeLineOrigin == null || _activeLure == null)
            {
                _lineRenderer.enabled = false;
                return;
            }

            _lineRenderer.enabled = true;

            int segmentCount = Mathf.Max(2, GetLineSegments());
            if (_lineRenderer.positionCount != segmentCount)
                _lineRenderer.positionCount = segmentCount;

            ApplyLineTensionColor(_activeLure.LineTension);

            Vector3 start = _activeLineOrigin.position;
            Vector3 end = _activeLure.transform.position;
            float slack = GetLineRenderSlack();

            for (int i = 0; i < segmentCount; i++)
            {
                float t = segmentCount == 1 ? 0f : i / (float)(segmentCount - 1);
                Vector3 point = Vector3.Lerp(start, end, t);
                point.y -= Mathf.Sin(t * Mathf.PI) * slack;
                _lineRenderer.SetPosition(i, point);
            }
        }

        private void ApplyLineTensionColor(float tension)
        {
            Color color;
            if (tension < 0.3f)
                color = Color.Lerp(LineRestColor, LineSafeColor, tension / 0.3f);
            else if (tension < 0.6f)
                color = Color.Lerp(LineSafeColor, LineWarnColor, Mathf.InverseLerp(0.3f, 0.6f, tension));
            else
                color = Color.Lerp(LineWarnColor, LineDangerColor, Mathf.InverseLerp(0.6f, 1f, tension));

            if (tension >= 0.85f)
            {
                float pulse = (Mathf.Sin(Time.time * 28f) + 1f) * 0.5f;
                color = Color.Lerp(color, Color.white, pulse * 0.35f);
            }

            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;

            Material lineMaterial = _lineRenderer.material;
            if (lineMaterial == null)
                return;

            if (lineMaterial.HasProperty("_BaseColor"))
                lineMaterial.SetColor("_BaseColor", color);
            if (lineMaterial.HasProperty("_Color"))
                lineMaterial.SetColor("_Color", color);
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

        private FishingLureInstance SpawnLureInstance()
        {
            GameObject lurePrefab = _activeRod != null ? _activeRod.ActiveCastLurePrefab : null;
            GameObject lureObject = null;

            if (lurePrefab != null)
            {
                lureObject = Instantiate(lurePrefab, _activeLineOrigin.position, Quaternion.identity);
            }
            else if (_restingLureVisual != null)
            {
                lureObject = Instantiate(_restingLureVisual.gameObject, _activeLineOrigin.position, _restingLureVisual.rotation);
                lureObject.name = $"{_restingLureVisual.name}_Cast";
                lureObject.SetActive(true);
            }

            if (lureObject == null)
                return null;

            ItemComponent itemComponent = lureObject.GetComponent<ItemComponent>();
            if (itemComponent != null)
                Destroy(itemComponent);

            FishingLureInstance lureInstance = lureObject.GetComponent<FishingLureInstance>();
            if (lureInstance == null)
                lureInstance = lureObject.AddComponent<FishingLureInstance>();

            return lureInstance;
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
            WaterVolume targetWater = WaterVolume.FindVolumeXZ(castTarget);
            if (targetWater != null)
                castTarget.y = targetWater.GetSurfaceHeight(castTarget);

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

        private void UpdateRestingLureVisual()
        {
            bool showRestingLure = _isRodEquipped && _activeLure == null && !_isCastPending && _displayedCatchItem == null;
            ItemComponent loadedLureItem = _activeRod != null ? _activeRod.LoadedLureItem : null;

            if (loadedLureItem != null)
            {
                if (loadedLureItem.gameObject.activeSelf != showRestingLure)
                    loadedLureItem.gameObject.SetActive(showRestingLure);
            }
            else if (_restingLureVisual != null && _restingLureVisual.gameObject.activeSelf)
            {
                _restingLureVisual.gameObject.SetActive(false);
            }
        }

        private void ClearCastState(bool destroyLure)
        {
            if (_isReeling)
                AudioService.Instance?.PlaySfx(AudioEvent.FishingReelLoopStop, _activeLineOrigin != null ? _activeLineOrigin.position : transform.position);

            _isCastPending = false;
            _castReleasedByEvent = false;
            _isReeling = false;
            _reelProgress = 0f;
            _reelTotalDistance = 0f;
            _reelReleaseTime = -1f;
            ResetRuntimeLineLength();

            if (_activeLure != null && _activeRod != null)
                _activeRod.ReattachLoadedBaitToRod();

            if (destroyLure && _activeLure != null)
            {
                _activeLure.OnEnteredFloating -= HandleLureEnteredFloating;
                Destroy(_activeLure.gameObject);
            }

            if (_activeLure != null)
                _activeLure.OnEnteredFloating -= HandleLureEnteredFloating;

            _activeLure = null;

            if (_lineRenderer != null)
                _lineRenderer.enabled = false;

            _locomotionController?.ClearMovementLock();
            SetAnimatorReelState(false, 0f);

            UpdateRestingLureVisual();
        }

        private void HandleLureEnteredFloating(bool touchedWater)
        {
            if (!touchedWater)
                return;

            if (_activeLure != null && _activeLineOrigin != null)
                InitializeRuntimeLineLength(Vector3.Distance(_activeLure.transform.position, _activeLineOrigin.position));

            Vector3 splashPos = _activeLure != null ? _activeLure.transform.position : transform.position;
            AudioService.Instance?.PlaySfx(AudioEvent.FishingSplash, splashPos);
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
            UpdateRestingLureVisual();
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
            UpdateRestingLureVisual();
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

        private Vector3 GetHookedFishEscapeDirection()
        {
            Camera cameraToUse = GetActiveGameplayCamera();
            if (cameraToUse != null)
            {
                Vector3 cameraForward = cameraToUse.transform.forward;
                cameraForward.y = 0f;
                if (cameraForward.sqrMagnitude >= 0.001f)
                    return cameraForward.normalized;
            }

            if (_activeLineOrigin != null)
            {
                Vector3 lineForward = _activeLineOrigin.forward;
                lineForward.y = 0f;
                if (lineForward.sqrMagnitude >= 0.001f)
                    return lineForward.normalized;
            }

            return transform.forward;
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
        private float GetLineSlackRecoverSpeed() => _activeRod != null ? _activeRod.LineSlackRecoverSpeed : _fallbackLineSlackRecoverSpeed;
        private float GetFloatSettleSpeed() => _activeRod != null ? _activeRod.FloatSettleSpeed : _fallbackFloatSettleSpeed;
        private float GetFloatTensionStrength() => _activeRod != null ? _activeRod.FloatTensionStrength : _fallbackFloatTensionStrength;
        private float GetLureInterestMultiplier()
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

        private float GetLureInterestRadius()
        {
            float baseRadius = _activeRod != null ? _activeRod.CurrentLureRange : _fallbackLureInterestRadius;
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

        private IEnumerable<string> GetCurrentBaitTagPaths()
        {
            if (_activeRod?.LoadedBaitItem == null)
                return System.Array.Empty<string>();

            return _activeRod.LoadedBaitItem.Tags.EnumerateTagPaths();
        }

        private float CalculateReelDistance(Vector3 lurePosition, Vector3 rodTipPosition)
        {
            Vector3 surfaceLegStart = new Vector3(lurePosition.x, lurePosition.y, lurePosition.z);
            Vector3 surfaceLegEnd = new Vector3(rodTipPosition.x, lurePosition.y, rodTipPosition.z);
            float surfaceDistance = Vector3.Distance(surfaceLegStart, surfaceLegEnd);
            float liftDistance = Vector3.Distance(surfaceLegEnd, rodTipPosition);
            return Mathf.Max(0.05f, surfaceDistance + liftDistance);
        }

        private int GetLineSegments() => _activeRod != null ? _activeRod.LineSegments : _fallbackLineSegments;
        private float GetLineWidth() => _activeRod != null ? _activeRod.LineWidth : _fallbackLineWidth;
        private float GetLineSlack() => _activeRod != null ? _activeRod.LineSlack : _fallbackLineSlack;
        private float GetLineRenderSlack() => _hasRuntimeLineLength ? _currentLineSlack : GetLineSlack();
    }
}
