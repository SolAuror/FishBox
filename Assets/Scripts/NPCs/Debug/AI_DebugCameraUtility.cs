using UnityEngine;
using Sol.Locomotion;

namespace Sol.AI
{
    public static class AI_DebugCameraUtility
    {
        public static Camera GetActiveGameplayCamera()
        {
            if (LocomotionInputManager.Instance != null &&
                LocomotionInputManager.Instance.TryGetCameraContext(out CameraContext context) &&
                context.UnityCamera != null)
            {
                return context.UnityCamera;
            }

            return Camera.main;
        }
    }
}
