using Sol.Combat;
using Sol.Locomotion;
using UnityEngine;

namespace Sol
{
    public partial class InteractionPoint
    {
        private const float IndefiniteInteractionMovementLockSeconds = 999999f;

        private bool _restoreFirstPersonOnInteractionEnd;
        private LocomotionInput _activeLocomotionInput;
        private LocomotionController _activeLocomotionController;

        protected void ApplyPlayerInteractionCameraOverride(Interactor interactor)
        {
            _restoreFirstPersonOnInteractionEnd = false;

            if (interactor == null || !interactor.IsPlayer || !ShouldForceThirdPersonForPlayerInteraction())
                return;

            LocomotionInputManager inputManager = LocomotionInputManager.Instance;
            if (inputManager == null || inputManager.CurrentMode != CameraMode.FirstPerson)
                return;

            if (inputManager.TrySetCameraMode(CameraMode.ThirdPerson, useTransition: true))
                _restoreFirstPersonOnInteractionEnd = true;
        }

        protected void RestorePlayerInteractionCameraOverride()
        {
            if (!_restoreFirstPersonOnInteractionEnd)
                return;

            _restoreFirstPersonOnInteractionEnd = false;
            LocomotionInputManager.Instance?.TrySetCameraMode(CameraMode.FirstPerson, useTransition: true);
        }

        private void ApplyPlayerInteractionLocomotionLock(Interactor interactor)
        {
            _activeLocomotionInput = null;
            _activeLocomotionController = null;

            if (interactor?.Owner == null)
                return;

            _activeLocomotionController = ResolveLocomotionController(interactor.Owner);
            if (_activeLocomotionController != null)
                _activeLocomotionController.SetMovementLock(IndefiniteInteractionMovementLockSeconds);

            if (!interactor.IsPlayer)
                return;

            _activeLocomotionInput = interactor.Owner.GetComponent<LocomotionInput>();
            if (_activeLocomotionInput != null)
            {
                _activeLocomotionInput.MovementInput = Vector2.zero;
                _activeLocomotionInput.AttackPressed = false;
                _activeLocomotionInput.ReadyTogglePressed = false;
            }
        }

        private void ReleasePlayerInteractionLocomotionLock()
        {
            if (_activeLocomotionController != null)
                _activeLocomotionController.ClearMovementLock();

            if (_activeLocomotionInput != null)
            {
                _activeLocomotionInput.MovementInput = Vector2.zero;
                _activeLocomotionInput.AttackPressed = false;
                _activeLocomotionInput.ReadyTogglePressed = false;
            }

            _activeLocomotionInput = null;
            _activeLocomotionController = null;
        }

        private void ApplyInteractionCombatSuppression(Interactor interactor)
        {
            if (interactor?.Owner == null)
                return;

            BasicMeleeAttack meleeAttack = interactor.Owner.GetComponent<BasicMeleeAttack>();
            if (meleeAttack != null)
                meleeAttack.EndAttack();

            LocomotionInput locomotionInput = interactor.Owner.GetComponent<LocomotionInput>();
            if (locomotionInput != null)
            {
                locomotionInput.AttackPressed = false;
                locomotionInput.ReadyTogglePressed = false;
            }

            CombatReadiness readiness = interactor.Owner.GetComponent<CombatReadiness>();
            if (readiness != null && (readiness.IsReady || readiness.IsReadying))
                readiness.RequestSheathe();
        }

        protected virtual bool ShouldForceThirdPersonForPlayerInteraction()
        {
            return EffectiveType == InteractionPointType.Work
                || EffectiveType == InteractionPointType.Rest
                || EffectiveType == InteractionPointType.Utility;
        }

        private static LocomotionController ResolveLocomotionController(GameObject owner)
        {
            if (owner == null)
                return null;

            return owner.GetComponent<LocomotionController>()
                ?? owner.GetComponentInChildren<LocomotionController>(true)
                ?? owner.GetComponentInParent<LocomotionController>();
        }
    }
}
