using UnityEngine;

namespace Sol.Locomotion
{
    public partial class LocomotionController
    {
#region Physics Pushing
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!enablePushing) return;
            var body = hit.rigidbody;
            // No rigidbody or kinematic rigidbody - cannot push
            if (body == null || body.isKinematic) return;

            // Don't push objects below us
            if (hit.moveDirection.y < -0.3f) return;

            // Check if the object is on a pushable layer
            int objLayer = hit.gameObject.layer;
            if (((1 << objLayer) & pushLayers) == 0) return;

            // Calculate push direction from move direction
            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

            // Apply force proportional to player's speed and push power
            float pushForce = pushPower * CurrentSpeed;
            var soul = body.GetComponentInParent<Sol.AI.NPCSoul>();
            if (soul != null && !soul.IsAlive)
            {
                body.AddForce(pushDir * (pushForce * 0.2f), ForceMode.VelocityChange);
                return;
            }

            body.linearVelocity = pushDir * pushForce;
        }
#endregion
    }
}
