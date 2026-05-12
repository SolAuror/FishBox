using UnityEngine;

namespace Sol
{
    public sealed class InteractionAnimationEventRelay : MonoBehaviour
    {
        public void InteractionReady()
        {
            InteractionPoint point = GetComponentInParent<InteractionPoint>();
            if (point == null)
                point = FindActiveInteractionPointForActor(gameObject);

            point?.MarkActiveSessionReady();
        }

        public void OnInteractionReady()
        {
            InteractionReady();
        }

        private static InteractionPoint FindActiveInteractionPointForActor(GameObject actor)
        {
            if (actor == null)
                return null;

            InteractionPoint[] points = FindObjectsByType<InteractionPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
            {
                InteractionPoint point = points[i];
                if (point != null && point.ActiveSession?.Interactor?.Owner == actor)
                    return point;
            }

            return null;
        }
    }
}
