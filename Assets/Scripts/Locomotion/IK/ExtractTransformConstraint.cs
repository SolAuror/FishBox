using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Sol.Locomotion
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Animation Rigging/Extract Transform Constraint")]
    public class ExtractTransformConstraint :
        RigConstraint<ExtractTransformConstraintJob, ExtractTransformConstraintData, ExtractTransformConstraintJobBinder>
    {
    }
}
