using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Sol.Locomotion
{
    [Serializable]
    public struct ExtractTransformConstraintData : IAnimationJobData
    {
        [SyncSceneToStream] public Transform bone;

        [HideInInspector] public Vector3 position;
        [HideInInspector] public Quaternion rotation;

        public bool IsValid() => bone != null;

        public void SetDefaultValues()
        {
            bone = null;
            position = Vector3.zero;
            rotation = Quaternion.identity;
        }
    }
}
