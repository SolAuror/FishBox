using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Outline;

namespace Sol.Fishing
{
    [DisallowMultipleComponent]
    public class FishingTackleInstance : MonoBehaviour
    {
        private static readonly List<FishingTackleInstance> s_ActiveInstances = new();
        private static readonly Color CastOutlineColor = new(1f, 0.5f, 0f, 1f);
        private static readonly Color InterestedOutlineColor = new(1f, 0.92f, 0.2f, 1f);
        private static readonly Color HookedOutlineColor = new(0.2f, 1f, 0.35f, 1f);
        private static readonly Color BiteFailOutlineColor = new(1f, 0.2f, 0.2f, 1f);

        private enum TackleState
        {
            Flying = 0,
            Floating = 1,
            Reeling = 2
        }

        private TackleState _state;
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
        private float _floatBobAmplitude;
        private float _floatBobFrequency;
        private float _floatDriftDistance;
        private float _floatDriftFrequency;
        private float _floatSlackDistance;
        private float _floatSettleSpeed;
        private float _floatTensionStrength;
        private float _floatStartTime;
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
        private float _timer;
        private float _duration;
        private bool _shouldCancelCast;
        private bool _shouldCompleteReel;
        private bool _manualReelControl;
        private bool _reelInputActive;
        private bool _collidersEnabled;
        private bool _hasFloatAnchor;
        private float _driftPhaseX;
        private float _driftPhaseZ;

        public static IReadOnlyList<FishingTackleInstance> ActiveInstances => s_ActiveInstances;
        public bool ShouldCancelCast => _shouldCancelCast;
        public bool ShouldCompleteReel => _shouldCompleteReel;
        public bool CanAttractFish => _state == TackleState.Floating && !_shouldCancelCast && !_shouldCompleteReel && _hookedFish == null;
        public float InterestMultiplier => _interestMultiplier;
        public float InterestRadius => _interestRadius;
        public string BaitItemId => _baitItemId;
        public string BaitName => _baitName;
        public bool HasBait => !string.IsNullOrWhiteSpace(_baitItemId) || !string.IsNullOrWhiteSpace(_baitName);
        public WaterVolume WaterVolume => _waterVolume;
        public AI_Fish CommittedFish => _committedFish;
        public bool HasHookedFish => _hookedFish != null;

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
            string baitName)
        {
            EnsurePhysicsComponents();

            _state = TackleState.Flying;
            _waterVolume = waterVolume;
            _startPosition = startPosition;
            _targetPosition = targetPosition;
            _surfaceOffset = surfaceOffset;
            _floatBobAmplitude = floatBobAmplitude;
            _floatBobFrequency = floatBobFrequency;
            _floatStartTime = Time.time;
            _timer = 0f;
            _duration = Mathf.Max(0.05f, flightDuration);
            _waterContactTimeout = Mathf.Max(0.1f, waterContactTimeout);
            _interestMultiplier = Mathf.Max(0.1f, interestMultiplier);
            _interestRadius = Mathf.Max(0.5f, interestRadius);
            _baitItemId = string.IsNullOrWhiteSpace(baitItemId) ? string.Empty : baitItemId.Trim();
            _baitName = string.IsNullOrWhiteSpace(baitName) ? string.Empty : baitName.Trim();
            _shouldCancelCast = false;
            _shouldCompleteReel = false;
            _manualReelControl = false;
            _reelInputActive = false;
            _hookEscapeDeadline = 0f;
            _biteFailFlashUntilTime = 0f;

            transform.position = _startPosition;
            transform.rotation = Quaternion.LookRotation(GetLaunchDirection(_startPosition, _targetPosition, launchForward), Vector3.up);
            _lastPosition = transform.position;
            SetCollidersEnabled(false);
            _collisionEnableTime = Time.time + Mathf.Max(0f, collisionEnableDelay);
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
            _state = TackleState.Reeling;
            _manualReelControl = true;
            _manualReelSurfaceTarget = surfaceTarget;
            _manualReelTipTarget = tipTarget;
            _manualReelLiftDistance = Mathf.Max(0.05f, liftDistance);
            _manualReelSurfacePullStrength = Mathf.Max(0.05f, surfacePullStrength);
            _shouldCompleteReel = false;
            SetKinematicState(true);
            SetCollidersEnabled(false);
        }

        public void SetReelInputActive(bool isReeling)
        {
            _reelInputActive = isReeling;
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

        public bool TryHookFish(AI_Fish fish)
        {
            if (fish == null || _hookedFish != null || _state != TackleState.Floating || _shouldCancelCast || _shouldCompleteReel)
                return false;

            if (_committedFish != null && _committedFish != fish)
                return false;

            if (!fish.TryHook(this))
                return false;

            _committedFish = fish;
            _hookedFish = fish;
            _hookEscapeDeadline = Time.time + 2f;
            ApplyOutlineState();
            UpdateHookedFishPose();
            return true;
        }

        public AI_Fish ConsumeHookedFish()
        {
            if (_hookedFish == null)
                return null;

            AI_Fish fish = _hookedFish;
            _hookedFish = null;
            _committedFish = null;
            _hookEscapeDeadline = 0f;
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
            _floatDriftDistance = Mathf.Max(0f, driftDistance);
            _floatDriftFrequency = Mathf.Max(0f, driftFrequency);
            _hasFloatAnchor = true;
        }

        private void Update()
        {
            switch (_state)
            {
                case TackleState.Flying:
                    UpdateFlight();
                    break;
                case TackleState.Floating:
                    UpdateFloat();
                    break;
                case TackleState.Reeling:
                    UpdateReel();
                    break;
            }

            UpdateCommittedFishState();
            UpdateHookedFishState();
            UpdateBiteFailFlash();
        }

        private void UpdateFlight()
        {
            _timer += Time.deltaTime;

            if (!_collidersEnabled && Time.time >= _collisionEnableTime)
                SetCollidersEnabled(true);

            Vector3 currentPosition = _rigidbody != null ? _rigidbody.position : transform.position;
            UpdateFlightRotation(currentPosition);
            _lastPosition = currentPosition;

            if (TryGetSurfaceHeight(out float surfaceHeight))
            {
                bool hasTouchedSurface = currentPosition.y <= surfaceHeight
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
                _shouldCancelCast = true;
        }

        private void UpdateFloat()
        {
            SnapToSurface();
        }

        private void UpdateReel()
        {
            UpdateManualReel();
        }

        private void SnapToSurface()
        {
            if (!TryGetSurfaceHeight(out float surfaceHeight))
                return;

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
            float bobOffset = _floatBobAmplitude > 0f
                ? Mathf.Sin((Time.time - _floatStartTime) * _floatBobFrequency) * _floatBobAmplitude
                : 0f;
            position.y = surfaceHeight + bobOffset;
            transform.position = position;
        }

        private void UpdateManualReel()
        {
            if (!TryGetSurfaceHeight(out float surfaceHeight))
            {
                _shouldCompleteReel = true;
                return;
            }

            Vector3 currentPosition = transform.position;
            Vector3 surfaceTarget = _manualReelSurfaceTarget;
            surfaceTarget.y = surfaceHeight;

            Vector3 flatToTip = _manualReelTipTarget - currentPosition;
            flatToTip.y = 0f;
            bool shouldLift = flatToTip.magnitude <= _manualReelLiftDistance;
            float reelStep = _manualReelSurfacePullStrength * Time.deltaTime;

            if (!shouldLift)
            {
                Vector3 planarDelta = surfaceTarget - currentPosition;
                planarDelta.y = 0f;
                Vector3 nextPlanar = Vector3.MoveTowards(currentPosition, currentPosition + planarDelta, reelStep);
                nextPlanar.y = surfaceHeight;
                transform.position = nextPlanar;
                return;
            }

            Vector3 nextPosition = Vector3.MoveTowards(currentPosition, _manualReelTipTarget, reelStep);
            transform.position = nextPosition;
            if ((nextPosition - _manualReelTipTarget).sqrMagnitude <= 0.01f)
                _shouldCompleteReel = true;
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
            ApplyOutlineState();
        }

        private void BeginFloating()
        {
            if (TryGetSurfaceHeight(out float surfaceHeight))
            {
                BeginFloating(surfaceHeight);
                return;
            }

            _state = TackleState.Floating;
            _floatStartTime = Time.time;
            _shouldCancelCast = true;
            SetKinematicState(true);
            _floatRestPosition = transform.position;
            _floatHorizontalVelocity = Vector3.zero;
            _driftPhaseX = Random.Range(0f, Mathf.PI * 2f);
            _driftPhaseZ = Random.Range(0f, Mathf.PI * 2f);
        }

        private void BeginFloating(float surfaceHeight)
        {
            _state = TackleState.Floating;
            _floatStartTime = Time.time;
            _shouldCancelCast = false;
            SetKinematicState(true);
            SetCollidersEnabled(true);

            Vector3 position = transform.position;
            position.y = surfaceHeight;
            transform.position = position;
            _floatRestPosition = position;
            _floatHorizontalVelocity = Vector3.zero;
            _driftPhaseX = Random.Range(0f, Mathf.PI * 2f);
            _driftPhaseZ = Random.Range(0f, Mathf.PI * 2f);
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
                ApplyOutlineState();
                return;
            }

            if (!_reelInputActive && _hookEscapeDeadline > 0f && Time.time >= _hookEscapeDeadline)
            {
                ReleaseHookedFish();
                return;
            }

            UpdateHookedFishPose();
        }

        private void UpdateHookedFishPose()
        {
            if (_hookedFish == null)
                return;

            float followOffset = Mathf.Max(0.15f, _hookedFish.Size * 0.15f);
            Vector3 followPosition = transform.position - (transform.up * followOffset);
            Quaternion followRotation = transform.rotation;
            _hookedFish.SetHookedPose(followPosition, followRotation);
        }

        private void ReleaseHookedFish()
        {
            if (_hookedFish == null)
                return;

            AI_Fish fish = _hookedFish;
            _hookedFish = null;
            _committedFish = null;
            _hookEscapeDeadline = 0f;
            _biteFailFlashUntilTime = Time.time + 0.2f;
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
                    outline.outlineColor = HookedOutlineColor;
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
            if (_waterVolume == null || !_waterVolume.isActiveAndEnabled)
                _waterVolume = WaterVolume.FindVolumeXZ(transform.position);

            if (_waterVolume == null)
            {
                surfaceHeight = transform.position.y;
                return false;
            }

            surfaceHeight = _waterVolume.GetSurfaceHeight(transform.position) + _surfaceOffset;
            return true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_state != TackleState.Flying || !_collidersEnabled)
                return;

            if (collision == null || collision.collider == null || collision.collider.isTrigger)
                return;

            _shouldCancelCast = true;
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
            if (_floatDriftDistance > 0f && _floatDriftFrequency > 0f)
            {
                float elapsed = Time.time - _floatStartTime;
                float driftX = Mathf.Sin((elapsed * _floatDriftFrequency) + _driftPhaseX) * _floatDriftDistance;
                float driftZ = Mathf.Cos((elapsed * (_floatDriftFrequency * 0.87f)) + _driftPhaseZ) * _floatDriftDistance;
                desiredPosition += new Vector3(driftX, 0f, driftZ);
            }

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
            ReleaseHookedFish();
            if (_committedFish != null)
            {
                AI_Fish committedFish = _committedFish;
                _committedFish = null;
                committedFish.ReleaseFromHook();
            }
        }
    }
}
