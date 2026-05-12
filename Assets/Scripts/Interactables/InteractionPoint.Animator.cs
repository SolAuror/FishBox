using UnityEngine;

namespace Sol
{
    public partial class InteractionPoint
    {
        private const string AnimatorParamIsInteracting = "isInteracting";
        private const string AnimatorParamInteractionType = "interactionType";
        private const string AnimatorParamInteractionActive = "interactionActive";
        private const string SitEntryStatePath = "Base Layer.Interactions.SitState.SitChairStart";
        private const string SleepEntryStatePath = "Base Layer.Interactions.SleepState.Sleep";
        private const string CustomClipEntryStatePath = "Base Layer.Interactions.CustomClipState.CustomClipPlay";
        private const string CustomClipPlaceholderName = "CustomClipPlaceholder";

        private static readonly string[] WorkEntryStatePaths =
        {
            "Base Layer.Interactions.WorkState.Work_Smith",
            "Base Layer.Interactions.Work_Smith",
            "Base Layer.Work_Smith"
        };

        private RuntimeAnimatorController _originalController;
        private AnimatorOverrideController _clipOverride;

        private void BeginInteractionAnimatorState(Animator animator)
        {
            if (animator == null)
                return;

            if (EffectiveAnimationType == InteractionPointAnimationType.CustomClip && EffectiveCustomClip != null)
            {
                _originalController = animator.runtimeAnimatorController;
                _clipOverride = new AnimatorOverrideController(animator.runtimeAnimatorController);
                _clipOverride[CustomClipPlaceholderName] = EffectiveCustomClip;
                animator.runtimeAnimatorController = _clipOverride;
            }

            if (HasAnimatorParameter(animator, AnimatorParamInteractionType, AnimatorControllerParameterType.Int))
                animator.SetInteger(AnimatorParamInteractionType, (int)EffectiveAnimationType);

            if (HasAnimatorParameter(animator, AnimatorParamInteractionActive, AnimatorControllerParameterType.Bool))
                animator.SetBool(AnimatorParamInteractionActive, true);

            if (EffectiveAnimationType == InteractionPointAnimationType.None
                && HasAnimatorParameter(animator, AnimatorParamIsInteracting, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(AnimatorParamIsInteracting);
            }

            TryForceEnterInteractionState(animator);
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

            if (_originalController != null)
            {
                animator.runtimeAnimatorController = _originalController;
                _originalController = null;
                _clipOverride = null;
            }
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

        private void TryForceEnterInteractionState(Animator animator)
        {
            if (animator == null)
                return;

            if (!TryResolveForcedEntryState(animator, out int targetStateHash))
                return;

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.fullPathHash == targetStateHash)
                return;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.fullPathHash == targetStateHash)
                    return;
            }

            animator.CrossFadeInFixedTime(targetStateHash, 0.05f, 0, 0f);
        }

        private bool TryResolveForcedEntryState(Animator animator, out int stateHash)
        {
            stateHash = 0;

            if (!string.IsNullOrWhiteSpace(EffectiveReadyStatePath)
                && TryResolveAnimatorState(animator, EffectiveReadyStatePath, out stateHash))
            {
                return true;
            }

            string statePath = EffectiveAnimationType switch
            {
                InteractionPointAnimationType.RestSit => SitEntryStatePath,
                InteractionPointAnimationType.RestSleep => SleepEntryStatePath,
                InteractionPointAnimationType.CustomClip => CustomClipEntryStatePath,
                _ => null
            };

            if (!string.IsNullOrEmpty(statePath))
                return TryResolveAnimatorState(animator, statePath, out stateHash);

            if (EffectiveAnimationType == InteractionPointAnimationType.WorkForge
                || EffectiveAnimationType == InteractionPointAnimationType.WorkAnvil
                || EffectiveAnimationType == InteractionPointAnimationType.WorkFishing)
            {
                for (int i = 0; i < WorkEntryStatePaths.Length; i++)
                {
                    if (TryResolveAnimatorState(animator, WorkEntryStatePaths[i], out stateHash))
                        return true;
                }
            }

            return false;
        }

        private static bool TryResolveAnimatorState(Animator animator, string statePath, out int stateHash)
        {
            stateHash = Animator.StringToHash(statePath);
            if (animator.HasState(0, stateHash))
                return true;

            stateHash = 0;
            return false;
        }

        private bool IsAnimatorAtReadyPoint(Animator animator)
        {
            if (animator == null)
                return false;

            if (!TryResolveForcedEntryState(animator, out int targetStateHash))
                return true;

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.fullPathHash == targetStateHash && current.normalizedTime >= EffectiveReadyNormalizedTime)
                return true;

            if (!animator.IsInTransition(0))
                return false;

            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            return next.fullPathHash == targetStateHash && next.normalizedTime >= EffectiveReadyNormalizedTime;
        }
    }
}
