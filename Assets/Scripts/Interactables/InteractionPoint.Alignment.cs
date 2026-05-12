using UnityEngine;

namespace Sol
{
    public partial class InteractionPoint
    {
        private void AlignInteractorToPoint(Interactor interactor)
        {
            if (interactor?.Owner == null || AlignPoint == null || AlignPoint == transform)
                return;

            Transform ownerTransform = interactor.Owner.transform;
            if (interactor.IsPlayer)
            {
                ownerTransform.SetPositionAndRotation(AlignPoint.position, AlignPoint.rotation);
                return;
            }

            if (!_allowNpcPositionSnapOnUse)
                return;

            float snapDistance = Mathf.Max(0f, _npcPositionSnapDistance);
            if (snapDistance <= 0f)
                return;

            const float VerticalTolerance = 0.2f;
            Vector3 delta = AlignPoint.position - ownerTransform.position;
            float verticalDelta = Mathf.Abs(delta.y);
            delta.y = 0f;
            if (delta.sqrMagnitude <= snapDistance * snapDistance && verticalDelta <= VerticalTolerance)
                ownerTransform.position = AlignPoint.position;
        }

        private void TickNpcActiveAlignment()
        {
            if (!_inUse || !_alignNpcRotationWhileUsing || AlignPoint == null || _activeInteractor == null || _activeInteractor.IsPlayer)
                return;

            GameObject owner = _activeInteractor.Owner;
            if (owner == null || !owner.activeInHierarchy)
                return;

            Vector3 forward = AlignPoint.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            float step = Mathf.Clamp01(Time.deltaTime * Mathf.Max(0f, _npcAlignRotationSpeed));
            if (step > 0f)
                owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation, targetRotation, step);
        }
    }
}
