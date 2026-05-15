using UnityEngine;
using UnityEngine.AI;
using Sol.Actions;

namespace Sol.AI
{
    public sealed class AIState_Travel : AIStateBase
    {
        private NavMeshAgent agent => npc.Agent;

        public AIState_Travel(AI_NPC npc) : base(npc) { }

        public override void Enter()
        {
            SetDestination();
        }

        public override void Exit()
        {
        }

        public override AI_NPC.State Tick()
        {
            if (npc.CanChasePlayer())
                return AI_NPC.State.Chase;

            npc.RefreshSchedule(force: false);
            if (!npc.HasActiveSchedule)
                return AI_NPC.State.Idle;

            if (!SetDestination())
                return AI_NPC.State.Travel;

            if (npc.IsAtCurrentScheduleTarget())
            {
                npc.ApplyScheduleFacing(npc.TryGetCurrentScheduleTarget(out _, out NpcScheduleLocation location) ? location : null);
                return npc.GetDesiredScheduleState();
            }

            return AI_NPC.State.Travel;
        }

        public override Shared.AI.LocomotionIntent GetIntent()
        {
            Vector3 target = npc.transform.position;
            if (agent != null && agent.isOnNavMesh && agent.hasPath)
                target = agent.steeringTarget;
            else if (npc.TryGetCurrentScheduleTarget(out Vector3 scheduleTarget, out _))
                target = scheduleTarget;

            return new Shared.AI.LocomotionIntent
            {
                TargetPosition = target,
                DesiredSpeed = npc.GetCurrentTargetSpeed(),
                ActionType = Shared.AI.LocomotionActionType.Walk
            };
        }

        private bool SetDestination()
        {
            if (agent == null || !agent.enabled)
                return false;

            if (!agent.isOnNavMesh)
                return false;

            if (!npc.TryGetCurrentScheduleTarget(out Vector3 target, out _))
            {
                agent.ResetPath();
                agent.isStopped = true;
                return false;
            }

            agent.isStopped = false;
            agent.SetDestination(target);
            return true;
        }
    }

    public sealed class AIState_ScheduleActivity : AIStateBase
    {
        private readonly AI_NPC.State _state;
        private readonly NpcScheduleActivity _activity;
        private float _waitTimer;
        private Interactor _interactor;
        private InteractionPoint _activeInteractionPoint;
        private InteractAction _interactAction;
        private UseInteractionPointAction _useInteractionAction;
        private bool _interactionCompleted;

        public AIState_ScheduleActivity(AI_NPC npc, AI_NPC.State state, NpcScheduleActivity activity) : base(npc)
        {
            _state = state;
            _activity = activity;
        }

        public override void Enter()
        {
            _waitTimer = npc.CurrentScheduleEntry != null ? npc.CurrentScheduleEntry.WaitSeconds : 0f;
            _interactor = new Interactor(npc.gameObject, false);
            _activeInteractionPoint = null;
            _interactAction = null;
            _useInteractionAction = null;
            _interactionCompleted = false;

            if (npc.Agent != null && npc.Agent.isOnNavMesh)
            {
                npc.Agent.ResetPath();
                npc.Agent.isStopped = true;
            }

            if (npc.TryGetCurrentScheduleTarget(out _, out NpcScheduleLocation location))
                npc.ApplyScheduleFacing(location);
        }

        public override void Exit()
        {
            EndActiveInteraction(completed: false);
            _activeInteractionPoint = null;
            _interactAction = null;
            _useInteractionAction = null;
            _interactionCompleted = false;
        }

        public override AI_NPC.State Tick()
        {
            if (npc.CanChasePlayer())
                return AI_NPC.State.Chase;

            npc.RefreshSchedule(force: false);
            if (!npc.HasActiveSchedule)
                return AI_NPC.State.Idle;

            if (npc.CurrentScheduleActivity != _activity)
                return npc.GetDesiredScheduleState();

            if (!npc.IsAtCurrentScheduleTarget())
                return AI_NPC.State.Travel;

            if (_waitTimer > 0f)
                _waitTimer -= Time.deltaTime;

            if (npc.TryGetCurrentScheduleTarget(out _, out NpcScheduleLocation location)
                && TickScheduleInteraction(location))
            {
                return _state;
            }

            return _state;
        }

        public override Shared.AI.LocomotionIntent GetIntent()
        {
            return new Shared.AI.LocomotionIntent
            {
                TargetPosition = npc.transform.position,
                DesiredSpeed = 0f,
                ActionType = Shared.AI.LocomotionActionType.Idle
            };
        }

        private bool TickScheduleInteraction(NpcScheduleLocation location)
        {
            InteractionPoint interactionPoint = location != null ? location.InteractionPoint : null;
            if (interactionPoint == null)
                return false;

            if (_interactionCompleted)
                return true;

            if (_useInteractionAction != null)
            {
                if (_useInteractionAction.HasResolved)
                {
                    _activeInteractionPoint = null;
                    _interactAction = null;
                    _useInteractionAction = null;
                    _interactionCompleted = true;
                }

                return true;
            }

            if (_interactAction != null && !_interactAction.HasResolved)
                return true;

            if (!interactionPoint.CanInteract(_interactor))
                return false;

            if (!interactionPoint.TryReserve(_interactor, Mathf.Max(ReservationDuration(), 0.5f)))
                return false;

            _activeInteractionPoint = interactionPoint;
            _interactAction = new InteractAction(interactionPoint, _interactor);
            bool started = ActionSystem.Instance != null
                && ActionSystem.Instance.DispatchImmediate(_interactAction, npc.gameObject, interactionPoint.gameObject);

            if (!started || _interactAction.Failed)
            {
                interactionPoint.ReleaseReservation(_interactor);
                _activeInteractionPoint = null;
                _interactAction = null;
                return false;
            }

            _useInteractionAction = _interactAction.DispatchedAction as UseInteractionPointAction;
            if (_useInteractionAction == null)
            {
                interactionPoint.ReleaseReservation(_interactor);
                _activeInteractionPoint = null;
                return false;
            }

            return true;
        }

        private float ReservationDuration()
        {
            float wait = npc.CurrentScheduleEntry != null ? npc.CurrentScheduleEntry.WaitSeconds : _waitTimer;
            return Mathf.Max(wait, 1f) + 1f;
        }

        private void EndActiveInteraction(bool completed)
        {
            if (_activeInteractionPoint != null)
            {
                if (_activeInteractionPoint.IsInUseBy(_interactor))
                    _activeInteractionPoint.EndUse(completed);

                _activeInteractionPoint.ReleaseReservation(_interactor);
            }
        }
    }
}
