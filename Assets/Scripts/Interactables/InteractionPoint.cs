using Sol.Actions;
using UnityEngine;
using UnityEngine.Events;

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
        [InspectorName("Water.Draw")] DrawWater = 7
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Interactables/Interaction Point")]
    public class InteractionPoint : MonoBehaviour, IInteractable
    {
        [Header("Interaction Identity")]
        [SerializeField] private string _interactionPointId = string.Empty;
        [SerializeField] private string _prompt = "Use";
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private InteractionPointType _type = InteractionPointType.Utility;

        [Header("Use")]
        [SerializeField] private InteractionPointAnimationType _animationType = InteractionPointAnimationType.None;
        [SerializeField] private Transform _alignPoint;
        [Min(0f)]
        [SerializeField] private float _useDuration = 2f;
        [SerializeField] private bool _holdUntilCancelled;
        [SerializeField] private bool _singleOccupancy = true;
        [SerializeField] private bool _allowPlayer = true;
        [SerializeField] private bool _allowNPC = true;

        [Header("Actor Effects")]
        [SerializeField] private float _healthDelta;
        [SerializeField] private float _staminaDelta;
        [SerializeField] private float _hungerDelta;
        [SerializeField] private float _thirstDelta;

        [Header("Events")]
        [SerializeField] private UnityEvent _onUseStarted;
        [SerializeField] private UnityEvent _onUsed;

        private const string AnimatorParamInteractionType = "interactionType";
        private const string AnimatorParamInteractionActive = "interactionActive";
        private const string AnimatorParamIsInteracting = "isInteracting";

        private bool _inUse;
        private Interactor _activeInteractor;
        private Animator _activeAnimator;
        private bool _unsupportedVitalsWarningLogged;

        public string InteractionPointId => _interactionPointId;
        public string DisplayName => _displayName;
        public InteractionPointType Type => _type;
        public InteractionPointAnimationType AnimationType => _animationType;
        public Transform AlignPoint => _alignPoint != null ? _alignPoint : transform;
        public bool InUse => _inUse;
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

            if (!_allowPlayer && interactor.IsPlayer)
                return false;

            if (!_allowNPC && !interactor.IsPlayer)
                return false;

            if (_singleOccupancy && _inUse)
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
            _activeAnimator = ResolveAnimator(interactor);

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

            _activeAnimator = null;
            _activeInteractor = null;
            _inUse = false;
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

        private void BeginInteractionAnimatorState(Animator animator)
        {
            if (animator == null)
                return;

            if (HasAnimatorParameter(animator, AnimatorParamInteractionType, AnimatorControllerParameterType.Int))
                animator.SetInteger(AnimatorParamInteractionType, (int)_animationType);

            if (HasAnimatorParameter(animator, AnimatorParamInteractionActive, AnimatorControllerParameterType.Bool))
                animator.SetBool(AnimatorParamInteractionActive, true);

            if (_animationType == InteractionPointAnimationType.None
                && HasAnimatorParameter(animator, AnimatorParamIsInteracting, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(AnimatorParamIsInteracting);
            }
        }

        private void EndInteractionAnimatorState(Animator animator)
        {
            if (animator == null)
                return;

            if (HasAnimatorParameter(animator, AnimatorParamInteractionActive, AnimatorControllerParameterType.Bool))
                animator.SetBool(AnimatorParamInteractionActive, false);

            if (HasAnimatorParameter(animator, AnimatorParamInteractionType, AnimatorControllerParameterType.Int))
                animator.SetInteger(AnimatorParamInteractionType, (int)InteractionPointAnimationType.None);

            if (HasAnimatorParameter(animator, AnimatorParamIsInteracting, AnimatorControllerParameterType.Trigger))
                animator.ResetTrigger(AnimatorParamIsInteracting);
        }

        private static Animator ResolveAnimator(Interactor interactor)
        {
            if (interactor?.Owner == null)
                return null;

            return interactor.Owner.GetComponent<Animator>()
                ?? interactor.Owner.GetComponentInChildren<Animator>(true);
        }

        private static bool HasAnimatorParameter(
            Animator animator,
            string parameterName,
            AnimatorControllerParameterType requiredType)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
                return false;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == requiredType && parameter.name == parameterName)
                    return true;
            }

            return false;
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
        }
    }
}
