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

        [Header("Definition")]
        [SerializeField] private InteractionDefinition _definition;
        [SerializeField] private bool _overrideDefinitionSettings;

        [Header("Interaction Identity")]
        [SerializeField] private string _interactionPointId = string.Empty;
        [SerializeField] private string _prompt = "Use";
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private InteractionPointType _type = InteractionPointType.Utility;

        [Header("Ownership")]
        [OwnerIdDropdown]
        [SerializeField] private string _ownerId = string.Empty;
        [SerializeField] private InteractionOwnershipPolicy _ownershipPolicy = InteractionOwnershipPolicy.OwnerOnly;
        [OwnerIdDropdown]
        [SerializeField] private string[] _guestOwnerIds;
        [SerializeField] private string _unauthorizedPrompt = "Owned";

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
        [SerializeField] private UnityEvent _onReady;
        [SerializeField] private UnityEvent _onUsed;

        private bool _inUse;
        private Interactor _activeInteractor;
        private Animator _activeAnimator;
        private Transform _activeAlignPoint;
        private Interactor _reservedInteractor;
        private float _reservationExpiresAt;
        private bool _unsupportedVitalsWarningLogged;
        private InteractionSession _activeSession;
        private InteractionEffect[] _interactionEffects;
        private Interactor _lastPromptInteractor;

        public InteractionDefinition Definition => _definition;
        public string InteractionPointId => _interactionPointId;
        public string DisplayName => EffectiveDisplayName;
        public InteractionPointType Type => EffectiveType;
        public InteractionPointAnimationType AnimationType => EffectiveAnimationType;
        public string OwnerId => EffectiveOwnerId;
        public bool IsOwned => !string.IsNullOrWhiteSpace(OwnerId);
        public InteractionSession ActiveSession => _activeSession;
        public Transform AlignPoint => _activeAlignPoint != null ? _activeAlignPoint : GetFirstAlignPoint();
        public bool InUse => _inUse;
        public bool IsAvailable
        {
            get
            {
                ClearExpiredReservationIfNeeded();
                return !_inUse && (!EffectiveSingleOccupancy || _reservedInteractor == null);
            }
        }
        public float NpcArrivalTolerance => EffectiveNpcArrivalTolerance;
        public float HealthDelta => _healthDelta;
        public float StaminaDelta => _staminaDelta;
        public float HungerDelta => _hungerDelta;
        public float ThirstDelta => _thirstDelta;

        public string InteractionPrompt =>
            IsInteractionOwnedButUnavailableToLastInteractor()
                ? EffectiveUnauthorizedPrompt
                : string.IsNullOrWhiteSpace(EffectiveDisplayName)
                    ? EffectivePrompt
                    : $"{EffectivePrompt} {EffectiveDisplayName}";

        public virtual bool CanInteract(Interactor interactor)
        {
            _lastPromptInteractor = interactor;

            if (interactor == null || interactor.Owner == null || !interactor.Owner.activeInHierarchy)
                return false;

            ClearExpiredReservationIfNeeded();

            if (!CanOwnerUse(interactor))
                return false;

            if (!EffectiveAllowPlayer && interactor.IsPlayer)
                return false;

            if (!EffectiveAllowNPC && !interactor.IsPlayer)
                return false;

            if (EffectiveMaxInteractionRange > 0f)
            {
                Vector3 delta = AlignPoint.position - interactor.Transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > EffectiveMaxInteractionRange * EffectiveMaxInteractionRange)
                    return false;
            }

            if (EffectiveSingleOccupancy && _inUse)
                return false;

            if (EffectiveSingleOccupancy && IsReservedByAnother(interactor))
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

            return TryBeginSession(interactor, out _);
        }

        public virtual bool TryBeginSession(Interactor interactor, out InteractionSession session)
        {
            session = null;
            if (!CanInteract(interactor))
                return false;

            _inUse = true;
            _activeInteractor = interactor;
            _activeAlignPoint = ResolveNearestAlignPoint(interactor.Transform.position);
            if (IsReservedBy(interactor))
                ReleaseReservation(interactor);

            _activeAnimator = ResolveAnimator(interactor);
            _activeSession = new InteractionSession(this, interactor);
            _activeSession.SetState(InteractionSessionState.Reserved);
            session = _activeSession;

            AlignInteractorToPoint(interactor);
            _activeSession.SetState(InteractionSessionState.Aligning);
            if (EffectiveSuppressCombat)
                ApplyInteractionCombatSuppression(interactor);
            if (EffectiveLockMovement)
                ApplyPlayerInteractionLocomotionLock(interactor);
            if (EffectiveForceThirdPerson)
                ApplyPlayerInteractionCameraOverride(interactor);
            _activeSession.SetState(InteractionSessionState.Animating);
            BeginInteractionAnimatorState(_activeAnimator);
            _onUseStarted?.Invoke();
            OnUseStarted(interactor);
            DispatchEffectStarted(_activeSession);

            if (!RequiresReadyGate())
                MarkActiveSessionReady();

            return true;
        }

        public virtual void EndUse(bool completed)
        {
            if (!_inUse && _activeInteractor == null && _activeAnimator == null)
                return;

            Interactor interactor = _activeInteractor;
            InteractionSession session = _activeSession;
            if (session != null)
                session.SetState(completed ? InteractionSessionState.Completing : InteractionSessionState.Cancelling);

            EndInteractionAnimatorState(_activeAnimator);

            if (completed)
            {
                ApplyActorEffects(interactor);
                _onUsed?.Invoke();
                OnUseCompleted(interactor);
                DispatchEffectCompleted(session);
            }
            else
            {
                OnUseCancelled(interactor);
                DispatchEffectCancelled(session);
            }

            RestorePlayerInteractionCameraOverride();
            ReleasePlayerInteractionLocomotionLock();

            _activeAnimator = null;
            _activeInteractor = null;
            _activeAlignPoint = null;
            _inUse = false;
            if (session != null)
                session.SetState(InteractionSessionState.Cleanup);
            _activeSession = null;
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
            return EffectiveUseDuration;
        }

        public virtual bool ShouldHoldUntilCancelled(Interactor interactor)
        {
            return EffectiveCompletionMode == InteractionCompletionMode.HoldUntilCancelled
                || EffectiveCompletionMode == InteractionCompletionMode.WaitForReadyThenExternalCompletion
                || _holdUntilCancelled
                || EffectiveAnimationType == InteractionPointAnimationType.RestSit;
        }

        public bool ShouldWaitForReadyBeforeDuration(Interactor interactor)
        {
            return EffectiveCompletionMode == InteractionCompletionMode.WaitForReadyThenTimed
                || EffectiveCompletionMode == InteractionCompletionMode.WaitForReadyThenExternalCompletion
                || EffectiveAnimationType == InteractionPointAnimationType.RestSleep;
        }

        public void RequestActiveCompletion()
        {
            _activeSession?.RequestCompletion();
        }

        public void CancelActiveSession()
        {
            if (_inUse)
                EndUse(completed: false);
        }

        public void MarkActiveSessionReady()
        {
            if (_activeSession == null || _activeSession.IsReady)
                return;

            _activeSession.MarkReady();
            _activeSession.WaitForCompletion();
            _onReady?.Invoke();
            OnUseReady(_activeInteractor);
            DispatchEffectReady(_activeSession);
        }

        public bool CanOwnerUse(Interactor interactor)
        {
            string ownerId = OwnerId;
            if (string.IsNullOrWhiteSpace(ownerId))
                return true;

            InteractionOwnershipPolicy policy = EffectiveOwnershipPolicy;
            if (policy == InteractionOwnershipPolicy.Public)
                return true;

            if (interactor?.Owner == null)
                return false;

            if (ItemOwnershipUtility.IsOwnedBy(ownerId, interactor.Owner))
                return true;

            if (policy != InteractionOwnershipPolicy.OwnerOrGuests)
                return false;

            string actorOwnerId = ItemOwnershipUtility.ResolveActorOwnerIdOrEmpty(interactor.Owner);
            if (string.IsNullOrWhiteSpace(actorOwnerId) || _guestOwnerIds == null)
                return false;

            for (int i = 0; i < _guestOwnerIds.Length; i++)
            {
                string guest = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(_guestOwnerIds[i]);
                if (!string.IsNullOrWhiteSpace(guest)
                    && string.Equals(guest, actorOwnerId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        protected virtual void OnUseStarted(Interactor interactor) { }
        protected virtual void OnUseReady(Interactor interactor) { }
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
            _ownerId = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(_ownerId);
            _unauthorizedPrompt = string.IsNullOrWhiteSpace(_unauthorizedPrompt) ? "Owned" : _unauthorizedPrompt.Trim();
            if (_guestOwnerIds != null)
            {
                for (int i = 0; i < _guestOwnerIds.Length; i++)
                    _guestOwnerIds[i] = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(_guestOwnerIds[i]);
            }

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
            TickActiveSessionReadyGate();
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

        private bool IsInteractionOwnedButUnavailableToLastInteractor()
        {
            return IsOwned && _lastPromptInteractor != null && !CanOwnerUse(_lastPromptInteractor);
        }

        private bool UsesDefinitionDefaults => _definition != null && !_overrideDefinitionSettings;
        private string EffectivePrompt => UsesDefinitionDefaults ? _definition.Prompt : (string.IsNullOrWhiteSpace(_prompt) ? "Use" : _prompt.Trim());
        private string EffectiveDisplayName => UsesDefinitionDefaults ? _definition.DisplayName : (_displayName?.Trim() ?? string.Empty);
        private InteractionPointType EffectiveType => UsesDefinitionDefaults ? _definition.Type : _type;
        private InteractionPointAnimationType EffectiveAnimationType => UsesDefinitionDefaults ? _definition.AnimationType : _animationType;
        private AnimationClip EffectiveCustomClip => UsesDefinitionDefaults ? _definition.CustomClip : _customClip;
        private bool EffectiveAllowPlayer => UsesDefinitionDefaults ? _definition.AllowPlayer : _allowPlayer;
        private bool EffectiveAllowNPC => UsesDefinitionDefaults ? _definition.AllowNPC : _allowNPC;
        private bool EffectiveSingleOccupancy => UsesDefinitionDefaults ? _definition.SingleOccupancy : _singleOccupancy;
        private float EffectiveMaxInteractionRange => UsesDefinitionDefaults ? _definition.MaxInteractionRange : Mathf.Max(0f, _maxInteractionRange);
        private float EffectiveNpcArrivalTolerance => UsesDefinitionDefaults ? _definition.NpcArrivalTolerance : Mathf.Max(0.1f, _npcArrivalTolerance);
        private float EffectiveUseDuration => UsesDefinitionDefaults ? _definition.UseDuration : Mathf.Max(0f, _useDuration);
        private InteractionCompletionMode EffectiveCompletionMode => UsesDefinitionDefaults ? _definition.CompletionMode : (_holdUntilCancelled ? InteractionCompletionMode.HoldUntilCancelled : InteractionCompletionMode.Timed);
        private InteractionOwnershipPolicy EffectiveOwnershipPolicy => UsesDefinitionDefaults ? _definition.OwnershipPolicy : _ownershipPolicy;
        private string EffectiveUnauthorizedPrompt => UsesDefinitionDefaults ? _definition.UnauthorizedPrompt : (string.IsNullOrWhiteSpace(_unauthorizedPrompt) ? "Owned" : _unauthorizedPrompt.Trim());
        private bool EffectiveForceThirdPerson => UsesDefinitionDefaults ? _definition.ForceThirdPerson : ShouldForceThirdPersonForPlayerInteraction();
        private bool EffectiveSuppressCombat => !UsesDefinitionDefaults || _definition.SuppressCombat;
        private bool EffectiveLockMovement => !UsesDefinitionDefaults || _definition.LockMovement;
        private string EffectiveReadyStatePath => UsesDefinitionDefaults ? _definition.ReadyStatePath : string.Empty;
        private float EffectiveReadyNormalizedTime => UsesDefinitionDefaults ? _definition.ReadyNormalizedTime : 0.85f;
        private string EffectiveOwnerId => ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(_ownerId);

        private bool RequiresReadyGate()
        {
            return EffectiveCompletionMode == InteractionCompletionMode.WaitForReadyThenTimed
                || EffectiveCompletionMode == InteractionCompletionMode.WaitForReadyThenExternalCompletion
                || EffectiveAnimationType == InteractionPointAnimationType.RestSleep;
        }

        private void TickActiveSessionReadyGate()
        {
            if (_activeSession == null || _activeSession.IsReady || _activeAnimator == null || !RequiresReadyGate())
                return;

            if (IsAnimatorAtReadyPoint(_activeAnimator))
                MarkActiveSessionReady();
        }

        private void DispatchEffectStarted(InteractionSession session)
        {
            foreach (InteractionEffect effect in GetInteractionEffects())
                effect.OnInteractionStarted(session);
        }

        private void DispatchEffectReady(InteractionSession session)
        {
            foreach (InteractionEffect effect in GetInteractionEffects())
                effect.OnInteractionReady(session);
        }

        private void DispatchEffectCompleted(InteractionSession session)
        {
            foreach (InteractionEffect effect in GetInteractionEffects())
                effect.OnInteractionCompleted(session);
        }

        private void DispatchEffectCancelled(InteractionSession session)
        {
            foreach (InteractionEffect effect in GetInteractionEffects())
                effect.OnInteractionCancelled(session);
        }

        private InteractionEffect[] GetInteractionEffects()
        {
            if (_interactionEffects == null)
                _interactionEffects = GetComponents<InteractionEffect>();
            return _interactionEffects;
        }
    }
}
