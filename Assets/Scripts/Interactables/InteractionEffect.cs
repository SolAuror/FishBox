using UnityEngine;

namespace Sol
{
    public abstract class InteractionEffect : MonoBehaviour
    {
        public virtual void OnInteractionStarted(InteractionSession session) { }
        public virtual void OnInteractionReady(InteractionSession session) { }
        public virtual void OnInteractionCompleted(InteractionSession session) { }
        public virtual void OnInteractionCancelled(InteractionSession session) { }
    }
}
