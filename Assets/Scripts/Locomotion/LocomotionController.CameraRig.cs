using Unity.Cinemachine;
using UnityEngine;

namespace Sol.Locomotion
{
    public partial class LocomotionController
    {
#region Camera Rig
        public bool IsPlayerControlled()
        {
            return _locomotionInput != null && _locomotionInput.IsControlledByPlayer;
        }

        public void SetMovementLock(float duration)
        {
            if (duration <= 0f)
                return;

            _externalMovementLockTimer = Mathf.Max(_externalMovementLockTimer, duration);
        }

        public void ClearMovementLock()
        {
            _externalMovementLockTimer = 0f;
        }

        public bool TryGetCameraForMode(CameraMode mode, out CinemachineCamera camera)
        {
            if (_cameraModes != null)
            {
                for (int i = 0; i < _cameraModes.Count; i++)
                {
                    CameraModeBinding binding = _cameraModes[i];
                    if (binding == null || binding.mode != mode || binding.camera == null) continue;
                    camera = binding.camera;
                    return true;
                }
            }

            camera = FindCameraForModeFallback(mode);
            return camera != null;
        }

        public bool TryGetHeadMeshRenderer(out Renderer renderer)
        {
            if (_headMeshRenderer != null)
            {
                renderer = _headMeshRenderer;
                return true;
            }

            renderer = FindHeadRendererFallback();
            return renderer != null;
        }

        private CinemachineCamera FindCameraForModeFallback(CameraMode mode)
        {
            CinemachineCamera[] cameras = GetComponentsInChildren<CinemachineCamera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                CinemachineCamera candidate = cameras[i];
                if (candidate == null) continue;

                if (mode == CameraMode.FirstPerson && candidate.GetComponent<CinemachinePanTilt>() != null)
                    return candidate;

                if (mode == CameraMode.ThirdPerson && candidate.GetComponent<CinemachineOrbitalFollow>() != null)
                    return candidate;
            }

            return null;
        }

        private Renderer FindHeadRendererFallback()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer candidate = renderers[i];
                if (candidate == null) continue;

                string objectName = candidate.gameObject.name;
                if (objectName == "Head")
                    return candidate;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer candidate = renderers[i];
                if (candidate == null) continue;

                string objectName = candidate.gameObject.name;
                if (objectName == "head")
                    return candidate;
            }

            return null;
        }
#endregion
    }
}
