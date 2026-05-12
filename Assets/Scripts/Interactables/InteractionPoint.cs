using Sol.Actions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Sol
{
    public enum InteractionPointType
    {
        Rest,
        Work,
        Food,
        Water,
        Utility
    }

    public enum InteractionPointAnimationType
    {
        None = 0,
        [InspectorName("Work.Forge")] WorkForge = 1,
        [InspectorName("Work.Anvil")] WorkAnvil = 2,
        [InspectorName("Rest.Sit")] RestSit = 3,
        [InspectorName("Rest.Sleep")] RestSleep = 4,
        [InspectorName("Work.Fishing")] WorkFishing = 5,
        [InspectorName("Gather.Fruit")] GatherFruit = 6,
        [InspectorName("Water.Draw")] DrawWater = 7,
        [InspectorName("Custom Clip")] CustomClip = 100
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Interactables/Interaction Point")]
    public partial class InteractionPoint : MonoBehaviour, IInteractable
    {
        private const float ReservationGraceDuration = 4f;

        [Header("Interaction Identity")]
        [SerializeField] private string _interactionPointId = string.Empty;
        [SerializeField] private string _prompt = "Use";
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private InteractionPointType _type = InteractionPointType.Utility;

        [Header("Use")]
        [SerializeField] private InteractionPointAnimationType _animationType = InteractionPointAnimationType.None;
        [SerializeField] private AnimationClip _customClip;
        [FormerlySerializedAs("_alignPoint")]
        [SerializeField] private Transform _alignPoint;
        [SerializeField] private Transform[] _alignPoints;
        [Min(0f)]
        [SerializeField] private float _useDuration = 2f;
        [SerializeField] private bool _holdUntilCancelled;
        [SerializeField] private bool _singleOccupancy = true;
        [SerializeField] private bool _allowPlayer = true;
        [SerializeField] private bool _allowNPC = true;
        [Min(0f)]
        [SerializeField] private float _maxInteractionRange = 0f;

        [Header("NPC Alignment")]
        [Min(0.1f)]
        [SerializeField] private float _npcArrivalTolerance = 0.55f;
        [SerializeField] private bool _allowNpcPositionSnapOnUse = false;
        [Min(0f)]
        [SerializeField] private float _npcPositionSnapDistance = 0.05f;
        [SerializeField] private bool _alignNpcRotationWhileUsing = true;
        [Min(0f)]
        [SerializeField] private float _npcAlignRotationSpeed = 8f;

        [Header("Actor Effects")]
        [SerializeField] private float _healthDelta;
        [SerializeField] private float _staminaDelta;
        [SerializeField] private float _hungerDelta;
        [SerializeField] private float _thirstDelta;

        [Header("Events")]
        [SerializeField] private UnityEvent _onUseStarted;
        [SerializeField] private UnityEvent _onUsed;

        private bool _inUse;
        private Interactor _activeInteractor;
        private Animator _activeAnimator;
        private Transform _activeAlignPoint;
        private Interactor _reservedInteractor;
        private float _reservationExpiresAt;
        private bool _unsupportedVitalsWarningLogged;

        public string InteractionPointId => _interactionPointId;
        public string DisplayName => _displayName;
        public InteractionPointType Type => _type;
        public InteractionPointAnimationType AnimationType => _animationType;
        public Transform AlignPoint => _activeAlignPoint != null ? _activeAlignPoint : GetFirstAlignPoint();
        public bool InUse => _inUse;
        public bool IsAvailable
        {
            get
            {
                ClearExpiredReservationIfNeeded();
                return !_inUse && (!_singleOccupancy || _reservedInteractor == null);
            }
        }
        public float NpcArrivalTolerance => Mathf.Max(0.1f, _npcArrivalTolerance);
        public float HealthDelta => _healthDelta;
        public float StaminaDelta => _staminaDelta;
        public float HungerDelta => _hungerDelta;
        public float ThirstDelta => _thirstDelta;

        public string InteractionPrompt =>
            string.IsNullOrWhiteSpace(_displayName)
                ? _prompt
                : $"{_prompt} {_displayName}";

        public virtual bool CanInteract(Interactor interactor)
        {
            if (interactor == null || interactor.Owner == null || !interactor.Owner.activeInHierarchy)
                return false;

            ClearExpiredReservationIfNeeded();

            if (!_allowPlayer && interactor.IsPlayer)
                return false;

            if (!_allowNPC && !interactor.IsPlayer)
                return false;

            if (_maxInteractionRange > 0f)
            {
                Vector3 delta = AlignPoint.position - interactor.Transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > _maxInteractionRange * _maxInteractionRange)
                    return false;
            }

            if (_singleOccupancy && _inUse)
                return false;

            if (_singleOccupancy && IsReservedByAnother(interactor))
                return false;

            return true;
        }

        public virtual GameAction GetInteraction(Interactor interactor)
        {
            return new UseInteractionPointAction(this, interactor);
        }

        public virtual bool BeginUse(Interactor interactor)
        {
            if (!CanInteract(interactor))
                return false;

            _inUse = true;
            _activeInteractor = interactor;
            _activeAlignPoint = ResolveNearestAlignPoint(interactor.Transform.position);
            if (IsReservedBy(interactor))
                ReleaseReservation(interactor);

            _activeAnimator = ResolveAnimator(interactor);

            AlignInteractorToPoint(interactor);
            ApplyInteractionCombatSuppression(interactor);
            ApplyPlayerInteractionLocomotionLock(interactor);
            ApplyPlayerInteractionCameraOverride(interactor);
            BeginInteractionAnimatorState(_activeAnimator);
            _onUseStarted?.Invoke();
            OnUseStarted(interactor);
            return true;
        }

        public virtual void EndUse(bool completed)
        {
            if (!_inUse && _activeInteractor == null && _activeAnimator == null)
                return;

            Interactor interactor = _activeInteractor;
            EndInteractionAnimatorState(_activeAnimator);

            if (completed)
            {
                ApplyActorEffects(interactor);
                _onUsed?.Invoke();
                OnUseCompleted(interactor);
            }
            else
            {
                OnUseCancelled(interactor);
            }

            RestorePlayerInteractionCameraOverride();
            ReleasePlayerInteractionLocomotionLock();

            _activeAnimator = null;
            _activeInteractor = null;
            _activeAlignPoint = null;
            _inUse = false;
            ReleaseReservation();
        }

        public bool IsInUseBy(Interactor interactor)
        {
            if (!_inUse || interactor == null || interactor.Owner == null)
                return false;

            return _activeInteractor != null && _activeInteractor.Owner == interactor.Owner;
        }

        public virtual float GetUseDuration(Interactor interactor)
        {
            return Mathf.Max(0f, _useDuration);
        }

        public virtual bool ShouldHoldUntilCancelled(Interactor interactor)
        {
            return _holdUntilCancelled || _animationType == InteractionPointAnimationType.RestSit;
        }

        protected virtual void OnUseStarted(Interactor interactor) { }
        protected virtual void OnUseCompleted(Interactor interactor) { }
        protected virtual void OnUseCancelled(Interactor interactor) { }

        protected void ApplyActorEffects(Interactor interactor)
        {
            if (interactor == null)
                return;

            ApplyHealthDelta(interactor);
            ApplyStaminaDelta(interactor);
            WarnIfUnsupportedVitalsConfigured();
        }

        protected void WarnIfUnsupportedVitalsConfigured()
        {
            if (_unsupportedVitalsWarningLogged)
                return;

            if (Mathf.Approximately(_hungerDelta, 0f) && Mathf.Approximately(_thirstDelta, 0f))
                return;

            _unsupportedVitalsWarningLogged = true;
            Debug.LogWarning(
                $"[{nameof(InteractionPoint)}] '{name}' has hunger/thirst deltas configured, but FishBox souls currently expose health/stamina only. The hunger/thirst values were not applied.",
                this);
        }

        private void ApplyHealthDelta(Interactor interactor)
        {
            if (Mathf.Approximately(_healthDelta, 0f))
                return;

            if (interactor.PlayerSoul != null)
            {
                if (_healthDelta > 0f)
                    interactor.PlayerSoul.Heal(_healthDelta);
                else
                    interactor.PlayerSoul.TakeDamage(-_healthDelta);
                return;
            }

            if (interactor.NpcSoul != null)
            {
                if (_healthDelta > 0f)
                    interactor.NpcSoul.Heal(_healthDelta);
                else
                    interactor.NpcSoul.TakeDamage(-_healthDelta);
            }
        }

        private void ApplyStaminaDelta(Interactor interactor)
        {
            if (Mathf.Approximately(_staminaDelta, 0f))
                return;

            if (interactor.PlayerSoul != null)
            {
                if (_staminaDelta > 0f)
                    interactor.PlayerSoul.RestoreStamina(_staminaDelta);
                else
                    interactor.PlayerSoul.SpendStamina(-_staminaDelta);
                return;
            }

            if (interactor.NpcSoul != null)
            {
                if (_staminaDelta > 0f)
                    interactor.NpcSoul.RestoreStamina(_staminaDelta);
                else
                    interactor.NpcSoul.SpendStamina(-_staminaDelta);
            }
        }

        protected virtual void OnValidate()
        {
            _interactionPointId = EntityCodeUtility.NormalizeOrEmpty(
                _interactionPointId,
                EntityCodeUtility.InteractionPointPrefix);

#if UNITY_EDITOR
            _interactionPointId = EntityCodeUtility.EnsureAssignedCode(
                this,
                _interactionPointId,
                EntityCodeUtility.InteractionPointPrefix,
                static point => point._interactionPointId);
#endif
        }

        private void OnDisable()
        {
            if (_inUse)
                EndUse(completed: false);

            ReleaseReservation();
        }

        private void Update()
        {
            TickNpcActiveAlignment();
        }

        private Transform GetFirstAlignPoint()
        {
            if (_alignPoint != null)
                return _alignPoint;

            if (_alignPoints != null)
            {
                for (int i = 0; i < _alignPoints.Length; i++)
                {
                    if (_alignPoints[i] != null)
                        return _alignPoints[i];
                }
            }

            return transform;
        }

        private Transform ResolveNearestAlignPoint(Vector3 fromPosition)
        {
            Transform best = _alignPoint;
            float bestSqr = best != null ? PlanarSqrDistance(best.position, fromPosition) : float.MaxValue;

            if (_alignPoints != null)
            {
                for (int i = 0; i < _alignPoints.Length; i++)
                {
                    Transform candidate = _alignPoints[i];
                    if (candidate == null)
                        continue;

                    float sqr = PlanarSqrDistance(candidate.position, fromPosition);
                    if (sqr < bestSqr)
                    {
                        best = candidate;
                        bestSqr = sqr;
                    }
                }
            }

            return best != null ? best : transform;
        }

        private static float PlanarSqrDistance(Vector3 a, Vector3 b)
        {
            Vector3 delta = a - b;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }
    }
}
