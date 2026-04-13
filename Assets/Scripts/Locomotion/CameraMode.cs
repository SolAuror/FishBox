using Unity.Cinemachine;
using UnityEngine;

namespace Sol.Locomotion
{
    public enum CameraMode
    {
        FirstPerson = 0,
        ThirdPerson = 1,
    }

    [System.Serializable]
    public class CameraModeBinding
    {
        public CameraMode mode = CameraMode.FirstPerson;
        public CinemachineCamera camera;
    }

    public readonly struct CameraContext
    {
        public CameraContext(CameraMode mode, Camera unityCamera, Transform cameraTransform, CinemachineCamera rig)
        {
            Mode = mode;
            UnityCamera = unityCamera;
            CameraTransform = cameraTransform;
            Rig = rig;
        }

        public CameraMode Mode { get; }
        public Camera UnityCamera { get; }
        public Transform CameraTransform { get; }
        public CinemachineCamera Rig { get; }
        public bool IsThirdPerson => Mode == CameraMode.ThirdPerson;
    }
}
