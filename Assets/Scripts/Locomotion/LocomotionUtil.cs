using UnityEngine;

namespace Sol.Locomotion
{
    public static class LocomotionUtil
    {
        public static Vector3 GetNormalWithSphereCast(CharacterController characterController, LayerMask layerMask = default)
        {
            Vector3 normal = Vector3.up;
            Vector3 center = characterController.transform.position + characterController.center;
            float distance = characterController.height / 2f;

            RaycastHit hit;
            if (Physics.SphereCast(center, characterController.radius, Vector3.down, out hit, distance, layerMask))
            {
                normal = hit.normal;
            };

            return normal;
        }

        public static bool DetectNearbyWall(CharacterController characterController, Transform transform, LayerMask groundLayers, float checkDistance, float angleTolerance = 0.01f)
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3[] dirs = { transform.forward, -transform.forward, transform.right, -transform.right };
            RaycastHit hit;
            for (int i = 0; i < dirs.Length; i++)
            {
                if (Physics.SphereCast(origin, characterController.radius, dirs[i], out hit, checkDistance, groundLayers, QueryTriggerInteraction.Ignore))
                {
                    float angle = Vector3.Angle(hit.normal, Vector3.up);
                    if (angle > characterController.slopeLimit + angleTolerance)
                    {
                        if (Vector3.Dot(characterController.velocity, -hit.normal) > 0.1f)
                            return true;
                    }
                }
            }

            return false;
        }

        public static Vector3 AdjustVelocityForSteepGround(CharacterController characterController, Vector3 velocity, LayerMask groundLayers, float frictionMultiplier = 0.5f)
        {
            Vector3 normal = GetNormalWithSphereCast(characterController, groundLayers);
            float angleGround = Vector3.Angle(normal, Vector3.up);
            bool validAngle = angleGround <= characterController.slopeLimit;

            if (!validAngle && velocity.y <= 0f)
            {
                float originalY = velocity.y;
                Vector3 lateralOnly = Vector3.ProjectOnPlane(velocity, normal) * frictionMultiplier;
                lateralOnly.y = originalY;
                return lateralOnly;
            }

            return velocity;
        }
    }
} 