using System;
using UnityEngine;

namespace Sol
{
    public enum InteractionSessionState
    {
        Requested = 0,
        Reserved = 1,
        Aligning = 2,
        Animating = 3,
        Ready = 4,
        WaitingForCompletion = 5,
        Completing = 6,
        Cancelling = 7,
        Cleanup = 8
    }

    public sealed class InteractionSession
    {
        public InteractionPoint Point { get; }
        public Interactor Interactor { get; }
        public InteractionSessionState State { get; private set; }
        public bool IsReady => State == InteractionSessionState.Ready || State == InteractionSessionState.WaitingForCompletion;
        public bool CompletionRequested { get; private set; }
        public float StartedAt { get; }
        public float ReadyAt { get; private set; }

        public event Action<InteractionSession> Ready;

        public InteractionSession(InteractionPoint point, Interactor interactor)
        {
            Point = point;
            Interactor = interactor;
            State = InteractionSessionState.Requested;
            StartedAt = Time.time;
        }

        public void SetState(InteractionSessionState state)
        {
            State = state;
        }

        public void MarkReady()
        {
            if (IsReady)
                return;

            ReadyAt = Time.time;
            State = InteractionSessionState.Ready;
            Ready?.Invoke(this);
        }

        public void WaitForCompletion()
        {
            if (State == InteractionSessionState.Ready)
                State = InteractionSessionState.WaitingForCompletion;
        }

        public void RequestCompletion()
        {
            CompletionRequested = true;
        }
    }
}
