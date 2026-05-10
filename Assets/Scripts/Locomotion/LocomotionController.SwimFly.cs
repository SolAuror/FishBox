using UnityEngine;

namespace Sol.Locomotion
{
    public partial class LocomotionController
    {
#region Swimming & Flying
        private void HandleSwimMovement()
        {
            if (_camTransform == null) return; // NPCs do not swim via camera-relative input
            // Use full 3D camera direction so looking up/down steers vertically.
            Vector3 camForward = _camTransform.forward;
            Vector3 camRight   = _camTransform.right;

            // Flatten for lateral-only component used by surface swimming.
            Vector3 flatForward = new Vector3(camForward.x, 0f, camForward.z).normalized;
            Vector3 flatRight   = new Vector3(camRight.x,   0f, camRight.z).normalized;

            Vector2 input   = _locomotionInput.MovementInput;

            // Lateral direction is always camera-relative on XZ.
            Vector3 wishDir = flatRight * input.x + flatForward * input.y;

            // Camera pitch drives vertical swim: looking up while moving
            // forward makes the player ascend, looking down makes them dive.
            float cameraPitchVertical = 0f;
            if (input.y > 0.1f) // Only when pushing forward
                cameraPitchVertical = camForward.y * input.y;

            // Explicit controls: hold Up/Space = rise, hold Down/Ctrl = sink (override camera pitch).
            float vertical = cameraPitchVertical;
            float heldVertical = 0f;
            if (_locomotionInput.SwimUpHeld) heldVertical += 1f;
            if (_locomotionInput.SwimDownHeld) heldVertical -= 1f;
            if (Mathf.Abs(heldVertical) > 0.01f)
                vertical = Mathf.Clamp(heldVertical, -1f, 1f);

            float effectiveSwimMult = GetSwimSpeedMultiplier?.Invoke() ?? SwimSpeedMultiplier;

            // Build target velocity - lateral driven by input, vertical independent.
            Vector3 targetVelocity = wishDir * swimSpeed * effectiveSwimMult;
            targetVelocity.y = vertical * swimVerticalSpeed;

            // Lerp current swim velocity toward target.  swimAcceleration controls
            // how quickly the player reaches full speed (and how quickly they stop).
            // Using a time-corrected lerp keeps feel consistent regardless of framerate.
            float lerpT = 1f - Mathf.Pow(1f - Mathf.Clamp01(swimAcceleration), Time.deltaTime * 60f);
            _swimVelocity = Vector3.Lerp(_swimVelocity, targetVelocity, lerpT);

            _characterController.Move(_swimVelocity * Time.deltaTime);

            // Clamp to water surface: the player can only rise to chest
            // height (swimWaterLine).  Uses XZ-only lookup so the clamp still
            // works even if Move() pushed the player momentarily above the surface.
            // Uses CharacterController.Move for the correction so terrain
            // collision is respected (raw position assignment clips through ground).
            float surface = _waterSystem != null
                ? _waterSystem.GetWaterline(transform.position)
                : WaterVolume.FindVolumeXZ(transform.position)?.GetSurfaceHeight(transform.position)
                  ?? (SolWaterManager.Instance != null ? SolWaterManager.Instance.waterLevel : transform.position.y);

            float headRoom = _characterController.height * swimWaterLine;
            float maxY = surface - headRoom;
            if (transform.position.y > maxY)
            {
                float correction = maxY - transform.position.y;
                _characterController.Move(new Vector3(0f, correction, 0f));
                if (_swimVelocity.y > 0f) _swimVelocity.y = 0f;
            }

            // Rotate to face camera forward - consistent with land movement so strafing doesn't pivot the body.
            // Suppress rotation while conversing
            if (!IsConversing)
            {
                Vector3 lateralVel = new Vector3(_swimVelocity.x, 0f, _swimVelocity.z);
                if (lateralVel.sqrMagnitude > movementThreshold * movementThreshold)
                {
                    Vector3 camForwardXZ = new Vector3(_camTransform.forward.x, 0f, _camTransform.forward.z).normalized;
                    if (camForwardXZ.sqrMagnitude > 0.0001f)
                    {
                        Quaternion toRot = Quaternion.LookRotation(camForwardXZ, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, toRot, playerModelRotationSpeed * Time.deltaTime);
                    }
                }
            }

        }

        private void HandleFlyMovement()
        {
            if (_camTransform == null) return; // NPCs do not fly via camera-relative input
            Vector3 forward = _camTransform.forward;
            Vector3 right = _camTransform.right;
            Vector3 moveDir = right * _locomotionInput.MovementInput.x + forward * _locomotionInput.MovementInput.y;

            // Vertical: Space = up, Crouch key = down
            float vertical = 0f;
            if (_locomotionInput.JumpPressed) vertical = 1f;
            if (_locomotionInput.CrouchToggle) vertical = -1f;
            moveDir.y += vertical * flyVerticalSpeed;

            float effectiveSpeedMult = GetSpeedMultiplier?.Invoke() ?? SpeedMultiplier;
            Vector3 targetVelocity = moveDir * flySpeed * effectiveSpeedMult;
            _characterController.Move(targetVelocity * flyAcceleration * Time.deltaTime);

            // Face movement direction
            Vector3 lateralDir = new Vector3(moveDir.x, 0f, moveDir.z);
            if (lateralDir.sqrMagnitude > movementThreshold)
            {
                Quaternion toRot = Quaternion.LookRotation(lateralDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, toRot, playerModelRotationSpeed * Time.deltaTime);
            }

        }

        private bool CanStandUp()
        {
            float headRadius = Mathf.Max(0.05f, _characterController.radius * 0.9f);
            Vector3 start = transform.position + _characterController.center + Vector3.up * (_characterController.height * 0.5f);
            Vector3 end = transform.position + _standingCenter + Vector3.up * (_standingHeight * 0.5f);
            return !Physics.CheckCapsule(start, end, headRadius, _groundLayers, QueryTriggerInteraction.Ignore);
        }
#endregion
    }
}
