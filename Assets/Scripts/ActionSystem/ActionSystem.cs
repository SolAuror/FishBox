using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.Actions
{
    /// <summary>
    /// Central dispatcher for all actions. Maintains per-actor priority queues and executes
    /// one action at a time per actor. Higher-priority actions preempt lower ones.
    /// All intent must flow through Dispatch -- no system may execute actions outside this pipeline.
    /// </summary>
    public class ActionSystem : MonoBehaviour
    {
        public static ActionSystem Instance { get; private set; }

        // ----------------------------------------------------------------
        //  Per-actor state
        // ----------------------------------------------------------------

        private sealed class ActorState
        {
            public GameAction Current;
            public readonly List<GameAction> Queue = new();   // sorted by priority desc
            public ActionContext Context;
            public readonly List<ActionHistoryEntry> History = new();
        }

        private const int MaxInstantChain = 16;
        private const int MaxHistoryPerActor = 32;

        private readonly Dictionary<GameObject, ActorState> _actors = new();
        private readonly List<GameObject> _stale = new();

        // ----------------------------------------------------------------
        //  Events
        // ----------------------------------------------------------------

        /// <summary>Fired when an action begins execution.</summary>
        public event Action<GameObject, GameAction> OnActionStarted;

        /// <summary>Fired when an action finishes successfully.</summary>
        public event Action<GameObject, GameAction> OnActionCompleted;

        /// <summary>Fired when an action is cancelled (self or external).</summary>
        public event Action<GameObject, GameAction> OnActionCancelled;

        // ----------------------------------------------------------------
        //  Lifecycle
        // ----------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("[ActionSystem]");
            go.AddComponent<ActionSystem>();
            Debug.Log("[ActionSystem] Auto-created. Add an ActionSystem component to your scene to suppress this message.");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ActionSystem] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _stale.Clear();

            foreach (var kvp in _actors)
            {
                if (kvp.Key == null)
                {
                    CleanupDestroyedActor(kvp.Value);
                    _stale.Add(kvp.Key);
                    continue;
                }
                try
                {
                    TickActor(kvp.Key, kvp.Value);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    ForceCancelCorrupted(kvp.Value);
                }
            }

            for (int i = 0; i < _stale.Count; i++)
                _actors.Remove(_stale[i]);
        }

        // ----------------------------------------------------------------
        //  Core tick
        // ----------------------------------------------------------------

        private void TickActor(GameObject actor, ActorState state)
        {
            // --- Tick current ---
            if (state.Current != null)
            {
                if (state.Current.IsCancelled)
                {
                    Finish(actor, state, cancelled: true);
                }
                else if (state.Current.IsComplete)
                {
                    Finish(actor, state, cancelled: false);
                }
                else
                {
                    state.Current.OnUpdate();

                    if (state.Current != null)
                    {
                        if (state.Current.IsCancelled)
                            Finish(actor, state, cancelled: true);
                        else if (state.Current.IsComplete)
                            Finish(actor, state, cancelled: false);
                    }
                }
            }

            // --- Dequeue next (highest priority first). Instant actions chain in one frame. ---
            int chainCount = 0;
            while (state.Current == null && state.Queue.Count > 0 && chainCount < MaxInstantChain)
            {
                chainCount++;
                var next = PopHighest(state.Queue);

                if (!next.CanExecute())
                {
                    Reject(actor, state, next);
                    continue;
                }

                state.Current = next;
                OnActionStarted?.Invoke(actor, next);
                next.OnStart();

                if (state.Current == null) break;

                if (state.Current.IsCancelled)
                {
                    Finish(actor, state, cancelled: true);
                    continue;
                }
                if (state.Current.IsComplete)
                {
                    Finish(actor, state, cancelled: false);
                    continue;
                }

                break;
            }

            if (chainCount >= MaxInstantChain && state.Queue.Count > 0)
                Debug.LogWarning($"[ActionSystem] Instant-chain limit ({MaxInstantChain}) hit for {actor.name}. {state.Queue.Count} actions deferred.");
        }

        private void Reject(GameObject actor, ActorState state, GameAction action)
        {
            action.ForceCancelInternal();

            try
            {
                action.OnCancel();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            RecordHistory(state, action, wasCancelled: true);

            try
            {
                OnActionCancelled?.Invoke(actor, action);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        private void Finish(GameObject actor, ActorState state, bool cancelled)
        {
            var action = state.Current;
            state.Current = null;

            try
            {
                if (cancelled) action.OnCancel();
                else           action.OnComplete();
            }
            catch (Exception e) { Debug.LogException(e); }

            // Record history.
            RecordHistory(state, action, cancelled);

            try
            {
                if (cancelled) OnActionCancelled?.Invoke(actor, action);
                else           OnActionCompleted?.Invoke(actor, action);
            }
            catch (Exception e) { Debug.LogException(e); }

            // Action chaining -- enqueue Next if the action completed successfully.
            if (!cancelled && action.Next != null)
                EnqueueInternal(state, action.Next, actor);
        }

        // ----------------------------------------------------------------
        //  Priority queue helpers (small list, sorted insert + pop-last)
        // ----------------------------------------------------------------

        private static void InsertSorted(List<GameAction> queue, GameAction action)
        {
            // Ascending order: lowest priority at index 0, highest at end.
            int pri = (int)action.Priority;
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if ((int)queue[i].Priority >= pri)
                {
                    queue.Insert(i + 1, action);
                    return;
                }
            }
            queue.Insert(0, action);
        }

        private static GameAction PopHighest(List<GameAction> queue)
        {
            int last = queue.Count - 1;
            var action = queue[last];
            queue.RemoveAt(last);
            return action;
        }

        private static GameAction PeekHighest(List<GameAction> queue)
        {
            return queue[queue.Count - 1];
        }

        // ----------------------------------------------------------------
        //  History
        // ----------------------------------------------------------------

        private static void RecordHistory(ActorState state, GameAction action, bool wasCancelled)
        {
            state.History.Add(new ActionHistoryEntry
            {
                ActionType = action.GetType().Name,
                Priority = action.Priority,
                WasCancelled = wasCancelled,
                Timestamp = Time.time
            });

            if (state.History.Count > MaxHistoryPerActor)
                state.History.RemoveAt(0);
        }

        // ----------------------------------------------------------------
        //  Cleanup
        // ----------------------------------------------------------------

        private void CleanupDestroyedActor(ActorState state)
        {
            if (state.Current != null && !state.Current.IsComplete && !state.Current.IsCancelled)
            {
                state.Current.ForceCancelInternal();
                try { state.Current.OnCancel(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            state.Current = null;
            state.Queue.Clear();
        }

        private void ForceCancelCorrupted(ActorState state)
        {
            if (state.Current != null && !state.Current.IsComplete && !state.Current.IsCancelled)
            {
                state.Current.ForceCancelInternal();
                try { state.Current.OnCancel(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            state.Current = null;
        }

        // ----------------------------------------------------------------
        //  Internal enqueue (shared by Dispatch + chaining)
        // ----------------------------------------------------------------

        private void EnqueueInternal(ActorState state, GameAction action, GameObject actor)
        {
            action.Initialize(state.Context, action.Target);
            InsertSorted(state.Queue, action);
        }

        // ----------------------------------------------------------------
        //  Public API
        // ----------------------------------------------------------------

        /// <summary>
        /// Queue an action for the given actor. If the action's priority is higher than
        /// the currently running action, the current action is cancelled (preempted).
        /// Returns true if the action was enqueued (or triggered preemption).
        /// </summary>
        public bool Dispatch(GameAction action, GameObject actor, GameObject target = null)
        {
            if (action == null || actor == null) return false;

            if (!_actors.TryGetValue(actor, out var state))
            {
                state = new ActorState { Context = new ActionContext(actor) };
                _actors[actor] = state;
            }

            action.Initialize(state.Context, target);

            // Priority preemption: if incoming action outranks the running one, interrupt it.
            if (state.Current != null
                && !state.Current.IsComplete
                && !state.Current.IsCancelled
                && action.Priority > state.Current.Priority)
            {
                state.Current.ForceCancelInternal();
                Finish(actor, state, cancelled: true);
            }

            InsertSorted(state.Queue, action);
            return true;
        }

        /// <summary>Cancel the currently running action for the given actor.</summary>
        public void CancelCurrent(GameObject actor)
        {
            if (actor == null) return;
            if (!_actors.TryGetValue(actor, out var state)) return;
            if (state.Current == null || state.Current.IsComplete || state.Current.IsCancelled) return;

            state.Current.ForceCancelInternal();
            Finish(actor, state, cancelled: true);
        }

        /// <summary>Cancel current and discard all queued actions for the actor.</summary>
        public void CancelAll(GameObject actor)
        {
            if (actor == null) return;
            if (!_actors.TryGetValue(actor, out var state)) return;

            if (state.Current != null && !state.Current.IsComplete && !state.Current.IsCancelled)
            {
                state.Current.ForceCancelInternal();
                Finish(actor, state, cancelled: true);
            }

            state.Queue.Clear();
        }

        /// <summary>Returns the running action for the actor, or null.</summary>
        public GameAction GetCurrent(GameObject actor)
        {
            if (actor == null) return null;
            return _actors.TryGetValue(actor, out var state) ? state.Current : null;
        }

        /// <summary>True if the actor has a running or queued action.</summary>
        public bool IsBusy(GameObject actor)
        {
            if (actor == null) return false;
            if (!_actors.TryGetValue(actor, out var state)) return false;
            return state.Current != null || state.Queue.Count > 0;
        }

        // ----------------------------------------------------------------
        //  Debug API (read-only)
        // ----------------------------------------------------------------

        /// <summary>
        /// Returns a snapshot of the actor's action state for debug display.
        /// Allocates -- use only for editor/debug tools, not per-frame gameplay.
        /// </summary>
        public ActionDebugSnapshot GetDebugSnapshot(GameObject actor)
        {
            if (actor == null || !_actors.TryGetValue(actor, out var state))
                return default;

            var queued = new string[state.Queue.Count];
            // Read highest-priority-first (reverse of internal order).
            for (int i = 0; i < queued.Length; i++)
                queued[i] = $"{state.Queue[queued.Length - 1 - i].GetType().Name} [{state.Queue[queued.Length - 1 - i].Priority}]";

            var history = new ActionHistoryEntry[state.History.Count];
            state.History.CopyTo(history);

            return new ActionDebugSnapshot
            {
                ActorName = actor.name,
                CurrentAction = state.Current?.GetType().Name,
                CurrentPriority = state.Current?.Priority ?? ActionPriority.Normal,
                QueuedActions = queued,
                ActiveStates = BaseStateSystem.GetActiveStateNames(actor),
                History = history
            };
        }

        /// <summary>Returns all tracked actor GameObjects. Debug only.</summary>
        public IEnumerable<GameObject> GetTrackedActors() => _actors.Keys;
    }

    // ----------------------------------------------------------------
    //  Debug data structures
    // ----------------------------------------------------------------

    /// <summary>Single entry in per-actor action history.</summary>
    public struct ActionHistoryEntry
    {
        public string ActionType;
        public ActionPriority Priority;
        public bool WasCancelled;
        public float Timestamp;

        public override string ToString()
        {
            string status = WasCancelled ? "CANCELLED" : "OK";
            return $"[{Timestamp:F1}s] {ActionType} [{Priority}] {status}";
        }
    }

    /// <summary>Read-only snapshot of an actor's action state for debug UI.</summary>
    public struct ActionDebugSnapshot
    {
        public string ActorName;
        public string CurrentAction;
        public ActionPriority CurrentPriority;
        public string[] QueuedActions;
        public System.Collections.Generic.List<string> ActiveStates;
        public ActionHistoryEntry[] History;
    }
}
