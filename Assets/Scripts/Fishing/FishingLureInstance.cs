using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Audio;
using Sol.Outline;
using Sol.Rpg;

namespace Sol.Fishing
{
    public enum FishBiteState
    {
        None = 0,
        Nibbling = 1,
        HookPending = 2,
        Hooked = 3
    }

    public enum FishingLureOutcome
    {
        None = 0,
        InvalidCast = 1,
        Retrieved = 2,
        Caught = 3,
        FishEscaped = 4,
        LineSnapped = 5
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public class FishingLureInstance : MonoBehaviour
    {
        private static readonly List<FishingLureInstance> s_ActiveInstances = new();
        private static readonly Color CastOutlineColor = new(1f, 0.5f, 0f, 1f);
        private static readonly Color InterestedOutlineColor = new(1f, 0.92f, 0.2f, 1f);
        private static readonly Color HookedOutlineColor = new(0.2f, 1f, 0.35f, 1f);
        private static readonly Color HookPendingOutlineColor = new(1f, 1f, 1f, 1f);
        private static readonly Color BiteFailOutlineColor = new(1f, 0.2f, 0.2f, 1f);

        private enum LureState
        {
            Flying = 0,
            Floating = 1,
            Reeling = 2,
            Fighting = 3
        }

        private LureState _state;
        private WaterVolume _waterVolume;
        private Rigidbody _rigidbody;
        private Collider[] _colliders;
        private OutlineComponent[] _outlineComponents;
        private Color[] _outlineBaseColors;
        private AI_Fish _committedFish;
        private AI_Fish _hookedFish;
        private Vector3 _floatAnchorPosition;
        private Vector3 _floatRestPosition;
        private Vector3 _floatHorizontalVelocity;
        private Vector3 _manualReelSurfaceTarget;
        private Vector3 _manualReelTipTarget;
        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private Vector3 _reelStartPosition;
        private Vector3 _lastPosition;
        private float _surfaceOffset;


        private float _floatSlackDistance;
        private float _floatSettleSpeed;
        private float _floatTensionStrength;
        private float _floatStartTime;
        private float _targetHorizontalDistance;
        private float _lastSurfaceHeight;
        private FishingLureOutcome _terminalOutcome;
        private float _collisionEnableTime;
        private float _waterContactTimeout;
        private float _manualReelLiftDistance;
        private float _manualReelSurfacePullStrength;
        private float _interestMultiplier = 1f;
        private float _interestRadius = 6f;
        private float _hookEscapeDeadline;
        private float _biteFailFlashUntilTime;
        private string _baitItemId = string.Empty;
        private string _baitName = string.Empty;
        private GameplayTagSet _baitTags = new();
        private float _timer;
        private float _duration;
        private bool _shouldCancelCast;
        private bool _shouldCompleteReel;
        private bool _reelInputActive;
        private float _hookEscapeWindow = 2f;
        private bool _collidersEnabled;
        private bool _hasFloatAnchor;
        private bool _hasLastSurfaceHeight;



        private FishBiteState _biteState = FishBiteState.None;
        private float _hookSetWindow = 1f;
        private float _hookSetDeadline;
        private float _fishAtBaitUntil;
        private float _nibbleNextTime;
        private float _dipStartTime;
        private float _dipDuration;
        private float _dipDepth;
        private const float NibbleDipDepthDefault = 0.07f;
        private const float NibbleDipDuration = 0.32f;
        private const float HookYankDipDepth = 0.5f;
        private const float HookYankDuration = 0.4f;
        private static readonly Vector2 NibbleIntervalRange = new(0.55f, 1.35f);

        private float _lineTension;
        private float _lineSnapTimer;
        private const float LineTensionRiseRate = 0.13f;
        private const float LineTensionFallRate = 0.65f;
        private const float LineTensionStruggleBonus = 0.30f;
        private const float LineTensionDangerStart = 0.78f;
        private const float LineSnapHoldTime = 1.1f;
        private const float FightFishDragSpeed = 2.0f;
        private const float FightBaseReelResistance = 0.22f;
        private const float FightMaxReelResistance = 0.72f;
        private const float FightStruggleDragMultiplier = 2.4f;
        private const float FightStruggleReelResistance = 0.55f;
        private const float FightStruggleLateralKick = 0.9f;
        private const float FightCompleteHorizontalDistance = 0.18f;
        private const float FishPullNotifyTimeout = 0.25f;
        private float _struggleIntensity;
        private Vector3 _struggleDirection;
        private float _struggleNotifiedUntil;
        private Vector3 _fishPullDirection;
        private float _fishPullIntensity;
        private float _fishPullNotifiedUntil;
        private Vector3 _fightEscapeDirection;
        private const float StruggleNotifyTimeout = 0.2f;

        public static IReadOnlyList<FishingLureInstance> ActiveInstances => s_ActiveInstances;
        public event System.Action<bool> OnEnteredFloating;
        public bool ShouldCancelCast => _shouldCancelCast || _terminalOutcome is FishingLureOutcome.InvalidCast or FishingLureOutcome.FishEscaped or FishingLureOutcome.LineSnapped;
        public bool ShouldCompleteReel => _shouldCompleteReel || _terminalOutcome is FishingLureOutcome.Retrieved or FishingLureOutcome.Caught;
        public FishingLureOutcome TerminalOutcome => _terminalOutcome;
        public bool HasTerminalOutcome => _terminalOutcome != FishingLureOutcome.None;
        public bool IsFloating => _state == LureState.Floating;
        public bool HasLanded => _state != LureState.Flying;
        public bool CanAttractFish => _state == LureState.Floating && !HasTerminalOutcome && _hookedFish == null;
        public float InterestMultiplier => _interestMultiplier;
        public float InterestRadius => _interestRadius;
        public string BaitItemId => _baitItemId;
        public string BaitName => _baitName;
        public GameplayTagSet BaitTags => _baitTags ?? GameplayTagSet.Empty;
        public bool HasBait => !string.IsNullOrWhiteSpace(_baitItemId)
            || !string.IsNullOrWhiteSpace(_baitName)
            || (_baitTags != null && !_baitTags.IsEmpty());
        public WaterVolume WaterVolume => _waterVolume;
        public AI_Fish CommittedFish => _committedFish;
        public bool HasHookedFish => _hookedFish != null;
        public FishBiteState BiteState => _biteState;
        public bool IsAwaitingHookSet => _biteState == FishBiteState.HookPending;
        public float LineTension => _lineTension;
        public bool IsStruggleActive => _biteState == FishBiteState.Hooked && Time.time < _struggleNotifiedUntil;
        public bool IsSurfaceFightActive
        {
            get
            {
                if (_state != LureState.Fighting || _biteState != FishBiteState.Hooked)
                    return false;

                Vector3 toTip = _manualReelTipTarget - transform.position;
                toTip.y = 0f;
                return toTip.magnitude > _manualReelLiftDistance;
            }
        }

        private void OnEnable()
        {
            if (!s_ActiveInstances.Contains(this))
                s_ActiveInstances.Add(this);
        }

        private void OnDisable()
        {
            s_ActiveInstances.Remove(this);
        }

        private void Awake()
        {
            EnsurePhysicsComponents();
            CacheOutlineComponents();
        }

        public void Launch(
            Vector3 startPosition,
            Vector3 targetPosition,
            Vector3 launchForward,
            WaterVolume waterVolume,
            float flightDuration,
            float arcHeight,
            float launchForwardDistance,
            float launchUpwardLift,
            float surfaceOffset,
            float floatBobAmplitude,
            float floatBobFrequency,
            float collisionEnableDelay,
            float waterContactTimeout,
            float interestMultiplier,
            float interestRadius,
            string baitItemId,
            string baitName,
            IEnumerable<string> baitTagPaths = null)
        {
            EnsurePhysicsComponents();

            _state = LureState.Flying;
            _waterVolume = waterVolume;
            _startPosition = startPosition;
            _targetPosition = targetPosition;
            _surfaceOffset = surfaceOffset;
            _targetHorizontalDistance = Vector2.Distance(new Vector2(_startPosition.x, _startPosition.z), new Vector2(_targetPosition.x, _targetPosition.z));
            _floatStartTime = Time.time;
            _timer = 0f;
            _duration = Mathf.Max(0.05f, flightDuration);
            _waterContactTimeout = Mathf.Max(0.1f, waterContactTimeout);
            _interestMultiplier = Mathf.Max(0.1f, interestMultiplier);
            _interestRadius = Mathf.Max(0.5f, interestRadius);
            _baitItemId = string.IsNullOrWhiteSpace(baitItemId) ? string.Empty : baitItemId.Trim();
            _baitName = string.IsNullOrWhiteSpace(baitName) ? string.Empty : baitName.Trim();
            _baitTags = new GameplayTagSet();
            _baitTags.AddRuntimeTagPaths(baitTagPaths);
            _shouldCancelCast = false;
            _shouldCompleteReel = false;
            _terminalOutcome = FishingLureOutcome.None;
            _hasLastSurfaceHeight = false;
            _lastSurfaceHeight = _startPosition.y;
            _reelInputActive = false;
            _hookEscapeDeadline = 0f;
            _biteFailFlashUntilTime = 0f;
            _biteState = FishBiteState.None;
            _hookSetDeadline = 0f;
            _fishAtBaitUntil = 0f;
            _nibbleNextTime = 0f;
            _dipStartTime = 0f;
            _dipDuration = 0f;
            _dipDepth = 0f;
            _lineTension = 0f;
            _lineSnapTimer = 0f;
            _struggleIntensity = 0f;
            _struggleNotifiedUntil = 0f;
            _fishPullIntensity = 0f;
            _fishPullNotifiedUntil = 0f;
            _fishPullDirection = Vector3.zero;
            _fightEscapeDirection = Vector3.zero;

            transform.position = _startPosition;
            transform.rotation = Quaternion.LookRotation(GetLaunchDirection(_startPosition, _targetPosition, launchForward), Vector3.up);
            _lastPosition = transform.position;
            SetCollidersEnabled(false);
            _collisionEnableTime = Time.time + Mathf.Max(0f, collisionEnableDelay);
            CacheOutlineComponents();
            ApplyOutlineState();

            if (_rigidbody == null)
                return;

            _rigidbody.position = _startPosition;
            _rigidbody.rotation = transform.rotation;
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
            _rigidbody.detectCollisions = true;
            _rigidbody.linearVelocity = CalculateLaunchVelocity(
                _startPosition,
                _targetPosition,
                _duration,
                arcHeight,
                launchForward,
                launchForwardDistance,
                launchUpwardLift);
        }

        public void SetManualReelTarget(
            Vector3 surfaceTarget,
            Vector3 tipTarget,
            float liftDistance,
            float surfacePullStrength)
        {
            _manualReelSurfaceTarget = surfaceTarget;
            _manualReelTipTarget = tipTarget;
            _manualReelLiftDistance = Mathf.Max(0.05f, liftDistance);
            _manualReelSurfacePullStrength = Mathf.Max(0.05f, surfacePullStrength);
 // Do NOT reset _shouldCompleteReel here - the per-state Update* sets it when the
            // lure physically arrives and it must survive until UpdateLureState reads it.
            // Launch() resets it correctly at cast start.

            // Only commandeer state for the empty-lure retrieve path. Fighting owns its
            // state transition via ConfirmHookSet.
            if (_state == LureState.Floating)
            {
                _state = LureState.Reeling;
                SetKinematicState(true);
                SetCollidersEnabled(false);
            }
        }
        public void StopManualReel()
        {
            if (_state != LureState.Reeling)
                return;

            _state = LureState.Floating;
            _floatRestPosition = transform.position;
            _floatHorizontalVelocity = Vector3.zero;
            SetKinematicState(true);
            SetCollidersEnabled(true);
        }

        public void SetFightContext(Vector3 rodTipPosition, float reelPullStrength, Vector3 escapeDirection)
        {
            _manualReelTipTarget = rodTipPosition;
            _manualReelSurfacePullStrength = Mathf.Max(0.05f, reelPullStrength);

            escapeDirection.y = 0f;
            if (escapeDirection.sqrMagnitude < 0.001f)
                return;

            if (_biteState != FishBiteState.Hooked)
                _fightEscapeDirection = escapeDirection.normalized;
        }

        public void SetHookEscapeWindow(float window)
        {
            _hookEscapeWindow = Mathf.Max(0.5f, window);
        }

        public void SetHookSetWindow(float window)
        {
            _hookSetWindow = Mathf.Max(0.2f, window);
        }

        public void NotifyFishAtBait()
        {
            _fishAtBaitUntil = Time.time + 0.25f;
        }

        public void NotifyStruggle(Vector3 horizontalDirection, float intensity)
        {
            _struggleDirection = horizontalDirection;
            _struggleIntensity = Mathf.Clamp01(intensity);
            _struggleNotifiedUntil = Time.time + StruggleNotifyTimeout;
        }

        public void NotifyHookedFishPull(Vector3 fishPosition, Vector3 horizontalDirection, float intensity)
        {
            Vector3 direction = new(horizontalDirection.x, 0f, horizontalDirection.z);
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = fishPosition - transform.position;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < 0.001f)
                return;

            _fishPullDirection = GetFightEscapeDirection(direction);
            _fishPullIntensity = Mathf.Clamp01(intensity);
            _fishPullNotifiedUntil = Time.time + FishPullNotifyTimeout;
        }

        public void SetReelInputActive(bool isReeling)
        {
            bool wasReeling = _reelInputActive;
            _reelInputActive = isReeling;

            if (isReeling && !wasReeling && _biteState == FishBiteState.HookPending)
                ConfirmHookSet();
        }

        public void FlashBiteFail()
        {
            _biteFailFlashUntilTime = Time.time + 0.2f;
            ApplyOutlineState();
        }

        public bool TryCommitFish(AI_Fish fish)
        {
            if (fish == null || !CanAttractFish)
                return false;

            if (_committedFish != null && _committedFish != fish)
                return false;

            _committedFish = fish;
            ApplyOutlineState();
            return true;
        }

        public void ReleaseCommittedFish(AI_Fish fish)
        {
            if (fish == null)
                return;

            if (_committedFish == fish)
            {
                _committedFish = null;
                ApplyOutlineState();
            }
        }

        public bool TryHookFish(AI_Fish fish, float hookSetWindow = -1f)
        {
            if (fish == null || _hookedFish != null || _state != LureState.Floating || _shouldCancelCast || _shouldCompleteReel)
                return false;

            if (_committedFish != null && _committedFish != fish)
                return false;

            if (!fish.TryHook(this))
                return false;

            _committedFish = fish;
            _hookedFish = fish;
            _biteState = FishBiteState.HookPending;
            float window = hookSetWindow > 0f ? hookSetWindow : _hookSetWindow;
            _hookSetDeadline = Time.time + window;
            BeginDip(HookYankDipDepth, HookYankDuration);
            AudioService.Instance?.PlaySfx(AudioEvent.FishingBite, transform.position);

            if (_reelInputActive)
                ConfirmHookSet();

            ApplyOutlineState();
            UpdateHookedFishPose();
            return true;
        }

        public void ConfirmHookSet()
        {
            if (_biteState != FishBiteState.HookPending || _hookedFish == null)
                return;

            _biteState = FishBiteState.Hooked;
            _hookSetDeadline = 0f;
            _state = LureState.Fighting;
            SetKinematicState(true);
            SetCollidersEnabled(false);
            _hookedFish.SetHookedEscapeDirection(this, _fightEscapeDirection);
            if (_hookEscapeWindow > 0f)
                _hookEscapeDeadline = Time.time + _hookEscapeWindow;
            AudioService.Instance?.PlaySfx(AudioEvent.FishingHookSet, transform.position);
            ApplyOutlineState();
        }

        public AI_Fish ConsumeHookedFish()
        {
            if (_hookedFish == null)
                return null;

            AI_Fish fish = _hookedFish;
            _hookedFish = null;
            _committedFish = null;
            _hookEscapeDeadline = 0f;
            _hookSetDeadline = 0f;
            _biteState = FishBiteState.None;
            _lineTension = 0f;
            _lineSnapTimer = 0f;
            _struggleIntensity = 0f;
            _struggleNotifiedUntil = 0f;
            _fishPullIntensity = 0f;
            _fishPullNotifiedUntil = 0f;
            _fishPullDirection = Vector3.zero;
            ApplyOutlineState();
            fish.PrepareForCatchHandoff();
            return fish;
        }

        public void SetFloatAnchor(
            Vector3 anchorPosition,
            float slackDistance,
            float settleSpeed,
            float tensionStrength,
            float driftDistance,
            float driftFrequency)
        {
            _floatAnchorPosition = anchorPosition;
            _floatSlackDistance = Mathf.Max(0.05f, slackDistance);
            _floatSettleSpeed = Mathf.Max(0.05f, settleSpeed);
            _floatTensionStrength = Mathf.Max(0f, tensionStrength);


            _hasFloatAnchor = true;
        }

        private void Update()
        {
            switch (_state)
            {
                case LureState.Flying:
                    UpdateFlight();
                    break;
                case LureState.Floating:
                    UpdateFloat();
                    break;
                case LureState.Reeling:
                    UpdateReel();
                    break;
                case LureState.Fighting:
                    UpdateFight();
                    break;
            }

            UpdateCommittedFishState();
            UpdateNibble();
            UpdateHookedFishState();
            UpdateBiteFailFlash();
            UpdateHookPendingOutlinePulse();
        }

        private void UpdateFlight()
        {
            _timer += Time.deltaTime;

            if (!_collidersEnabled && Time.time >= _collisionEnableTime)
                SetCollidersEnabled(true);

            Vector3 currentPosition = _rigidbody != null ? _rigidbody.position : transform.position;
            UpdateFlightRotation(currentPosition);
            _lastPosition = currentPosition;

            if (TryGetSurfaceHeight(currentPosition, out float surfaceHeight))
            {
                bool hasTouchedSurface = currentPosition.y <= surfaceHeight
                    && HasReachedCastDistance(currentPosition)
                    && (_rigidbody == null || _rigidbody.linearVelocity.y <= 0.05f);
                if (hasTouchedSurface)
                {
                    BeginFloating(surfaceHeight);
                    return;
                }
            }

            if (_timer >= (_duration * 2f))
                BeginFloating();

            if (_timer >= _waterContactTimeout)
                SetTerminalOutcome(FishingLureOutcome.InvalidCast);
        }

        private void UpdateFloat()
        {
            SnapToSurface();
        }

        private bool HasReachedCastDistance(Vector3 currentPosition)
        {
            if (_targetHorizontalDistance <= 0.05f)
                return true;

            Vector2 startXZ = new(_startPosition.x, _startPosition.z);
            Vector2 currentXZ = new(currentPosition.x, currentPosition.z);
            return Vector2.Distance(startXZ, currentXZ) >= _targetHorizontalDistance * 0.92f;
        }

        private void UpdateReel()
        {
            UpdateManualReel();
        }

        private void SnapToSurface()
        {
            Vector3 position = transform.position;
            Vector3 desiredPosition = GetDesiredFloatPosition(position);
            float settleSpeed = Mathf.Max(0.05f, _floatSettleSpeed);
            float smoothTime = 1f / settleSpeed;

            if (_hasFloatAnchor)
            {
                Vector3 anchorOffset = desiredPosition - _floatAnchorPosition;
                anchorOffset.y = 0f;
                float anchorDistance = anchorOffset.magnitude;
                if (anchorDistance > _floatSlackDistance)
                {
                    Vector3 constrained = _floatAnchorPosition + (anchorOffset.normalized * _floatSlackDistance);
                    float tensionBlend = 1f - Mathf.Exp(-_floatTensionStrength * Time.deltaTime);
                    desiredPosition = Vector3.Lerp(desiredPosition, constrained, tensionBlend);
                    smoothTime /= 1f + _floatTensionStrength;
                }
            }

            _floatRestPosition = desiredPosition;

            Vector3 currentHorizontal = new(position.x, 0f, position.z);
            Vector3 targetHorizontal = new(desiredPosition.x, 0f, desiredPosition.z);
            Vector3 smoothedHorizontal = Vector3.SmoothDamp(
                currentHorizontal,
                targetHorizontal,
                ref _floatHorizontalVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime);

            position.x = smoothedHorizontal.x;
            position.z = smoothedHorizontal.z;
            if (!TryGetSurfaceHeight(position, out float surfaceHeight))
                return;

            position.y = surfaceHeight + GetActiveDipOffset();
            transform.position = position;
        }

        private Vector3 GetFightEscapeDirection(Vector3 fallbackDirection)
        {
            Vector3 escape = _fightEscapeDirection;
            escape.y = 0f;

            if (escape.sqrMagnitude < 0.001f)
            {
                escape = fallbackDirection;
                escape.y = 0f;
            }

            if (escape.sqrMagnitude < 0.001f)
                escape = Vector3.forward;

            return escape.normalized;
        }

        private Vector3 GetFightLateralDirection(Vector3 direction)
        {
            Vector3 escape = GetFightEscapeDirection(Vector3.forward);
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                return Vector3.zero;

            Vector3 lateral = Vector3.ProjectOnPlane(direction, escape);
            lateral.y = 0f;
            return lateral.sqrMagnitude > 0.001f ? lateral.normalized : Vector3.zero;
        }

        private void UpdateFight()
        {
            if (_hookedFish == null || _biteState != FishBiteState.Hooked)
                return;

            Vector3 currentPosition = transform.position;
            Vector3 toTip = _manualReelTipTarget - currentPosition;
            Vector3 horizontalToTip = new Vector3(toTip.x, 0f, toTip.z);
            float horizontalDistance = horizontalToTip.magnitude;
            bool shouldLift = horizontalDistance <= _manualReelLiftDistance;
            bool struggleActive = Time.time < _struggleNotifiedUntil && _struggleIntensity > 0f;
            bool fishPullActive = Time.time < _fishPullNotifiedUntil && _fishPullIntensity > 0f;
            float fishPressure = GetHookedFishPressure();
            float fishPullIntensity = fishPullActive ? Mathf.Max(fishPressure, _fishPullIntensity) : fishPressure;

            Vector3 nextPosition = currentPosition;

            if (_reelInputActive)
            {
                float reelSpeed = _manualReelSurfacePullStrength * GetReelSafetyFactor();
                float reelResistance = FightBaseReelResistance + fishPullIntensity;
                if (struggleActive)
                    reelResistance += FightStruggleReelResistance * _struggleIntensity;
                reelSpeed *= 1f - Mathf.Min(FightMaxReelResistance, reelResistance);
                reelSpeed = Mathf.Max(0f, reelSpeed);
                float reelStep = reelSpeed * Time.deltaTime;

                if (shouldLift)
                {
                    // Final phase: lift lure out of water toward rod tip. This is the
                    // only path that completes the catch, so we must not block it with
                    // an out-of-water guard.
                    nextPosition = Vector3.MoveTowards(currentPosition, _manualReelTipTarget, reelStep);
                }
                else if (horizontalDistance > 0.001f)
                {
                    Vector3 dir = horizontalToTip / horizontalDistance;
                    nextPosition = currentPosition + dir * reelStep;
                    if (fishPullActive)
                        nextPosition += _fishPullDirection * (FightFishDragSpeed * fishPullIntensity * 0.35f * Time.deltaTime);
                }
            }
            else if (!shouldLift && horizontalDistance > 0.001f)
            {
                Vector3 awayDir = fishPullActive
                    ? GetFightEscapeDirection(_fishPullDirection)
                    : GetFightEscapeDirection(-horizontalToTip);
                float dragSpeed = FightFishDragSpeed * (0.75f + fishPullIntensity);
                if (struggleActive)
                    dragSpeed *= 1f + FightStruggleDragMultiplier * _struggleIntensity;
                nextPosition = currentPosition + awayDir * dragSpeed * Time.deltaTime;
            }

            if (struggleActive && !shouldLift)
            {
                Vector3 lateralDirection = GetFightLateralDirection(_struggleDirection);
                Vector3 lateral = lateralDirection * _struggleIntensity * FightStruggleLateralKick * Time.deltaTime;
                nextPosition += lateral;
            }

            // Surface-phase: lock to water height. Lift-phase: free Y so lure can rise
            // out of water toward the rod tip. If we can't find surface during the surface
            // phase (very edge of water volume) keep the previous Y to avoid sinking � the
            // fight continues until line snap or successful lift completion.
            if (!shouldLift)
            {
                if (TryGetSurfaceHeight(nextPosition, out float surfaceHeight))
                    nextPosition.y = surfaceHeight;
                else
                    nextPosition.y = currentPosition.y;
            }

            transform.position = nextPosition;

            // The lure owns the visible fish pose during the surface fight, while
            // AI_Fish owns the pull direction and resistance notifications.
            if (!shouldLift)
            {
                float followOffset = Mathf.Max(0.15f, _hookedFish.Size * 0.15f);
                Vector3 fishPose = nextPosition - (transform.up * followOffset);
                Vector3 fishMoveDelta = fishPose - _hookedFish.transform.position;
                fishMoveDelta.y = 0f;
                Quaternion fishRot = fishMoveDelta.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(fishMoveDelta.normalized, Vector3.up)
                    : _hookedFish.transform.rotation;
                _hookedFish.SetHookedPose(fishPose, fishRot);
            }

            if (shouldLift && (nextPosition - _manualReelTipTarget).sqrMagnitude <= FightCompleteHorizontalDistance * FightCompleteHorizontalDistance)
                SetTerminalOutcome(FishingLureOutcome.Caught);
        }

        private void UpdateManualReel()
        {
            if (!TryGetSurfaceHeight(out float surfaceHeight))
            {
                SetTerminalOutcome(FishingLureOutcome.Retrieved);
                return;
            }

            Vector3 currentPosition = transform.position;
            Vector3 surfaceTarget = _manualReelSurfaceTarget;
            surfaceTarget.y = surfaceHeight;

            Vector3 flatToTip = _manualReelTipTarget - currentPosition;
            flatToTip.y = 0f;
            bool shouldLift = flatToTip.magnitude <= _manualReelLiftDistance;
            float reelStep = _manualReelSurfacePullStrength * GetReelSafetyFactor() * Time.deltaTime;

            if (!shouldLift)
            {
                Vector3 planarDelta = surfaceTarget - currentPosition;
                planarDelta.y = 0f;
                Vector3 nextPlanar = Vector3.MoveTowards(currentPosition, currentPosition + planarDelta, reelStep);
                if (TryGetSurfaceHeight(nextPlanar, out float nextSurfaceHeight))
                    nextPlanar.y = nextSurfaceHeight;
                else
                    nextPlanar.y = surfaceHeight;
                transform.position = nextPlanar;
                return;
            }

            Vector3 nextPosition = Vector3.MoveTowards(currentPosition, _manualReelTipTarget, reelStep);
            transform.position = nextPosition;
            if ((nextPosition - _manualReelTipTarget).sqrMagnitude <= 0.01f)
                SetTerminalOutcome(FishingLureOutcome.Retrieved);
        }

        private void UpdateFlightRotation(Vector3 currentPosition)
        {
            Vector3 velocity = _rigidbody != null && !_rigidbody.isKinematic
                ? _rigidbody.linearVelocity
                : currentPosition - _lastPosition;
            if (velocity.sqrMagnitude < 0.000001f)
                return;

            transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        }

        private void EnsurePhysicsComponents()
        {
            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();

            if (_rigidbody == null)
                _rigidbody = GetComponentInChildren<Rigidbody>();

            if (_rigidbody == null)
                _rigidbody = gameObject.AddComponent<Rigidbody>();

            _rigidbody.mass = Mathf.Max(0.01f, _rigidbody.mass);
            _rigidbody.linearDamping = 0.35f;
            _rigidbody.angularDamping = 1.25f;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (_colliders == null || _colliders.Length == 0)
                _colliders = GetComponentsInChildren<Collider>(true);
        }

        private void CacheOutlineComponents()
        {
            _outlineComponents = GetComponentsInChildren<OutlineComponent>(true);
            if (_outlineComponents == null || _outlineComponents.Length == 0)
            {
                _outlineBaseColors = null;
                return;
            }

            _outlineBaseColors = new Color[_outlineComponents.Length];
            for (int i = 0; i < _outlineComponents.Length; i++)
            {
                _outlineBaseColors[i] = _outlineComponents[i] != null
                    ? _outlineComponents[i].outlineColor
                    : Color.clear;
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (_colliders == null)
                _colliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider collider = _colliders[i];
                if (collider != null)
                    collider.enabled = enabled;
            }

            _collidersEnabled = enabled;
        }

        private void SetKinematicState(bool isKinematic)
        {
            EnsurePhysicsComponents();

            bool wasKinematic = _rigidbody.isKinematic;
            if (isKinematic && !wasKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            if (wasKinematic != isKinematic)
                _rigidbody.isKinematic = isKinematic;

            _rigidbody.useGravity = !isKinematic;
            _rigidbody.detectCollisions = !isKinematic;
        }

        private void UpdateCommittedFishState()
        {
            if (_committedFish == null || _committedFish == _hookedFish)
                return;

            if (_committedFish.isActiveAndEnabled && !_committedFish.IsCaught && _committedFish.IsCommittedTo(this))
                return;

            _committedFish = null;
            _fishAtBaitUntil = 0f;
            if (_biteState == FishBiteState.Nibbling)
                _biteState = FishBiteState.None;
            ApplyOutlineState();
        }

        private void UpdateNibble()
        {
            // Nibbles only happen while a committed fish is at the bait and not yet hooked.
            bool hasFishAtBait = _committedFish != null
                && _hookedFish == null
                && Time.time < _fishAtBaitUntil
                && _state == LureState.Floating;

            if (!hasFishAtBait)
            {
                if (_biteState == FishBiteState.Nibbling)
                    _biteState = FishBiteState.None;
                _nibbleNextTime = 0f;
                return;
            }

            if (_biteState != FishBiteState.Nibbling)
            {
                _biteState = FishBiteState.Nibbling;
                _nibbleNextTime = Time.time + Random.Range(NibbleIntervalRange.x, NibbleIntervalRange.y) * 0.5f;
            }

            // Skip if a dip is currently animating (avoid stacking).
            if (Time.time < _dipStartTime + _dipDuration)
                return;

            if (Time.time >= _nibbleNextTime)
            {
                BeginDip(NibbleDipDepthDefault, NibbleDipDuration);
                _nibbleNextTime = Time.time + Random.Range(NibbleIntervalRange.x, NibbleIntervalRange.y);
                AudioService.Instance?.PlaySfx(AudioEvent.FishingNibble, transform.position);
            }
        }

        private void BeginDip(float depth, float duration)
        {
            _dipDepth = Mathf.Max(0f, depth);
            _dipDuration = Mathf.Max(0.05f, duration);
            _dipStartTime = Time.time;
        }

        private float GetActiveDipOffset()
        {
            if (_dipDuration <= 0f)
                return 0f;

            float elapsed = Time.time - _dipStartTime;
            if (elapsed < 0f || elapsed >= _dipDuration)
                return 0f;

            // Sine pulse: 0 ? -depth ? 0 across the duration.
            float t = elapsed / _dipDuration;
            return -_dipDepth * Mathf.Sin(t * Mathf.PI);
        }

        private void UpdateHookPendingOutlinePulse()
        {
            if (_biteState == FishBiteState.HookPending)
                ApplyOutlineState();
        }

        private void BeginFloating()
        {
            if (TryGetSurfaceHeight(out float surfaceHeight))
            {
                BeginFloating(surfaceHeight);
                return;
            }

            _state = LureState.Floating;
            _floatStartTime = Time.time;
            SetTerminalOutcome(FishingLureOutcome.InvalidCast);
            SetKinematicState(true);
            _floatRestPosition = transform.position;
            _floatHorizontalVelocity = Vector3.zero;


            OnEnteredFloating?.Invoke(false);
        }

        private void BeginFloating(float surfaceHeight)
        {
            _state = LureState.Floating;
            _floatStartTime = Time.time;
            _shouldCancelCast = false;
            _lastSurfaceHeight = surfaceHeight;
            _hasLastSurfaceHeight = true;
            SetKinematicState(true);
            SetCollidersEnabled(true);

            Vector3 position = transform.position;
            position.y = surfaceHeight;
            transform.position = position;
            _floatRestPosition = position;
            _floatHorizontalVelocity = Vector3.zero;


            OnEnteredFloating?.Invoke(true);
        }

        private void UpdateHookedFishState()
        {
            if (_hookedFish == null)
                return;

            if (!_hookedFish.isActiveAndEnabled)
            {
                _hookedFish = null;
                _committedFish = null;
                _hookEscapeDeadline = 0f;
                _hookSetDeadline = 0f;
                _biteState = FishBiteState.None;
                _lineTension = 0f;
                _lineSnapTimer = 0f;
                SetTerminalOutcome(FishingLureOutcome.FishEscaped);
                ApplyOutlineState();
                return;
            }

            // Player missed the hook-set window.
            if (_biteState == FishBiteState.HookPending && Time.time >= _hookSetDeadline)
            {
                ReleaseHookedFish(FishingLureOutcome.FishEscaped);
                return;
            }

            if (_biteState == FishBiteState.Hooked)
            {
                UpdateLineTension();

                if (_lineTension >= 0.999f)
                {
                    _lineSnapTimer += Time.deltaTime;
                    if (_lineSnapTimer >= LineSnapHoldTime)
                    {
                        AudioService.Instance?.PlaySfx(AudioEvent.FishingLineSnap, transform.position);
                        ReleaseHookedFish(FishingLureOutcome.LineSnapped, playEscapeAudio: false);
                        return;
                    }
                }
                else
                {
                    _lineSnapTimer = 0f;
                }
            }

            UpdateHookedFishPose();
        }

        private void UpdateLineTension()
        {
            float effectiveStruggle = Time.time < _struggleNotifiedUntil ? _struggleIntensity : 0f;

            if (_reelInputActive)
            {
                float rise = LineTensionRiseRate + (LineTensionStruggleBonus * effectiveStruggle);
                _lineTension = Mathf.Clamp01(_lineTension + rise * Time.deltaTime);
            }
            else
            {
                float fall = LineTensionFallRate * (1f + effectiveStruggle * 0.4f);
                _lineTension = Mathf.Clamp01(_lineTension - fall * Time.deltaTime);
            }
        }

        private float GetReelSafetyFactor()
        {
            if (_biteState != FishBiteState.Hooked)
                return 1f;
            return Mathf.Clamp01(1f - Mathf.InverseLerp(LineTensionDangerStart, 1f, _lineTension));
        }

        private float GetHookedFishPressure()
        {
            if (_hookedFish == null)
                return 0f;

            float difficultyPressure = Mathf.Clamp01(_hookedFish.CatchDifficulty * 0.12f);
            float weightPressure = Mathf.Clamp01(_hookedFish.Weight * 0.035f);
            return Mathf.Clamp01(difficultyPressure + weightPressure);
        }

        private void UpdateHookedFishPose()
        {
            if (_hookedFish == null)
                return;

            if (_state == LureState.Fighting)
            {
                Vector3 toTip = _manualReelTipTarget - transform.position;
                toTip.y = 0f;
                if (toTip.magnitude > _manualReelLiftDistance)
                    return;
            }

            float followOffset = Mathf.Max(0.15f, _hookedFish.Size * 0.15f);
            Vector3 followPosition = transform.position - (transform.up * followOffset);
            _hookedFish.SetHookedPose(followPosition, transform.rotation);
        }

        private void ReleaseHookedFish(FishingLureOutcome outcome = FishingLureOutcome.FishEscaped, bool playEscapeAudio = true)
        {
            if (_hookedFish == null)
                return;

            AI_Fish fish = _hookedFish;
            _hookedFish = null;
            _committedFish = null;
            _hookEscapeDeadline = 0f;
            _hookSetDeadline = 0f;
            _biteState = FishBiteState.None;
            _lineTension = 0f;
            _lineSnapTimer = 0f;
            _struggleIntensity = 0f;
            _struggleNotifiedUntil = 0f;
            _fishPullIntensity = 0f;
            _fishPullNotifiedUntil = 0f;
            _fishPullDirection = Vector3.zero;
            _biteFailFlashUntilTime = Time.time + 0.2f;
            if (outcome != FishingLureOutcome.None)
                SetTerminalOutcome(outcome);
            if (playEscapeAudio)
                AudioService.Instance?.PlaySfx(AudioEvent.FishingFishEscaped, transform.position);
            ApplyOutlineState();
            fish.ReleaseFromHook();
        }

        private void UpdateBiteFailFlash()
        {
            if (_biteFailFlashUntilTime <= 0f || Time.time < _biteFailFlashUntilTime)
                return;

            _biteFailFlashUntilTime = 0f;
            ApplyOutlineState();
        }

        private void ApplyOutlineState()
        {
            if (_outlineComponents == null || _outlineComponents.Length == 0)
                CacheOutlineComponents();

            if (_outlineComponents == null || _outlineComponents.Length == 0)
                return;

            for (int i = 0; i < _outlineComponents.Length; i++)
            {
                OutlineComponent outline = _outlineComponents[i];
                if (outline == null)
                    continue;

                if (_biteFailFlashUntilTime > Time.time)
                {
                    outline.outlineColor = BiteFailOutlineColor;
                    continue;
                }

                if (_hookedFish != null)
                {
                    if (_biteState == FishBiteState.HookPending)
                    {
                        float pulse = (Mathf.Sin(Time.time * 12f) + 1f) * 0.5f;
                        outline.outlineColor = Color.Lerp(HookPendingOutlineColor, HookedOutlineColor, pulse * 0.4f);
                    }
                    else
                    {
                        outline.outlineColor = HookedOutlineColor;
                    }
                    continue;
                }

                if (_committedFish != null)
                {
                    outline.outlineColor = InterestedOutlineColor;
                    continue;
                }

                outline.outlineColor = CastOutlineColor;
            }
        }

        private bool TryGetSurfaceHeight(out float surfaceHeight)
        {
            return TryGetSurfaceHeight(transform.position, out surfaceHeight);
        }

        private bool TryGetSurfaceHeight(Vector3 worldPosition, out float surfaceHeight)
        {
            if (_waterVolume == null || !_waterVolume.isActiveAndEnabled)
                _waterVolume = WaterVolume.FindVolumeXZ(worldPosition);

            if (_waterVolume == null)
            {
                if (_hasLastSurfaceHeight)
                {
                    surfaceHeight = _lastSurfaceHeight;
                    return true;
                }

                surfaceHeight = worldPosition.y;
                return false;
            }

            surfaceHeight = _waterVolume.GetSurfaceHeight(worldPosition) + _surfaceOffset;
            _lastSurfaceHeight = surfaceHeight;
            _hasLastSurfaceHeight = true;
            return true;
        }

        private void SetTerminalOutcome(FishingLureOutcome outcome)
        {
            if (outcome == FishingLureOutcome.None || _terminalOutcome != FishingLureOutcome.None)
                return;

            _terminalOutcome = outcome;
            _shouldCancelCast = outcome is FishingLureOutcome.InvalidCast or FishingLureOutcome.FishEscaped or FishingLureOutcome.LineSnapped;
            _shouldCompleteReel = outcome is FishingLureOutcome.Retrieved or FishingLureOutcome.Caught;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_state != LureState.Flying || !_collidersEnabled)
                return;

            if (collision == null || collision.collider == null || collision.collider.isTrigger)
                return;

            SetTerminalOutcome(FishingLureOutcome.InvalidCast);
        }

        private static Vector3 GetLaunchDirection(Vector3 startPosition, Vector3 targetPosition, Vector3 launchForward)
        {
            Vector3 toTarget = targetPosition - startPosition;
            Vector3 horizontalToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            Vector3 horizontalLaunchForward = Vector3.ProjectOnPlane(launchForward, Vector3.up);
            if (horizontalLaunchForward.sqrMagnitude < 0.0001f)
                horizontalLaunchForward = horizontalToTarget;
            if (horizontalLaunchForward.sqrMagnitude < 0.0001f)
                horizontalLaunchForward = Vector3.forward;

            Vector3 targetDirection = horizontalToTarget.sqrMagnitude > 0.0001f
                ? horizontalToTarget.normalized
                : horizontalLaunchForward.normalized;

            return Vector3.Slerp(horizontalLaunchForward.normalized, targetDirection, 0.35f);
        }

        private static Vector3 CalculateLaunchVelocity(
            Vector3 startPosition,
            Vector3 targetPosition,
            float flightDuration,
            float arcHeight,
            Vector3 launchForward,
            float launchForwardDistance,
            float launchUpwardLift)
        {
            float duration = Mathf.Max(0.1f, flightDuration);
            Vector3 displacement = targetPosition - startPosition;
            Vector3 horizontalDisplacement = Vector3.ProjectOnPlane(displacement, Vector3.up);
            Vector3 horizontalVelocity = horizontalDisplacement / duration;

            Vector3 launchDirection = GetLaunchDirection(startPosition, targetPosition, launchForward);
            horizontalVelocity += launchDirection * (launchForwardDistance / duration);

            float gravity = Physics.gravity.y;
            float verticalVelocity = (displacement.y - (0.5f * gravity * duration * duration)) / duration;
            verticalVelocity += Mathf.Max(0f, arcHeight + launchUpwardLift);

            return horizontalVelocity + (Vector3.up * verticalVelocity);
        }

        private Vector3 GetDesiredFloatPosition(Vector3 currentPosition)
        {
            Vector3 desiredPosition = _floatRestPosition;
            desiredPosition += SolWaterSurfaceSampler.GetSurfaceDriftVelocity(_waterVolume) * Time.deltaTime;

            desiredPosition.y = currentPosition.y;
            return desiredPosition;
        }
        public Vector3 GetFishInterestPoint(float depthOffset)
        {
            Vector3 point = transform.position;
            point.y -= Mathf.Abs(depthOffset);
            return point;
        }

        private void OnDestroy()
        {
            ReleaseHookedFish(FishingLureOutcome.None, playEscapeAudio: false);
            if (_committedFish != null)
            {
                AI_Fish committedFish = _committedFish;
                _committedFish = null;
                committedFish.ReleaseFromHook();
            }
        }
    }
}
