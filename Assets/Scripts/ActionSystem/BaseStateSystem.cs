using System.Collections.Generic;
using UnityEngine;

namespace Sol
{
    /// <summary>
    /// Abstract base for continuous state systems (Grab, Rest, Combat, etc.).
    /// Manages the IsActive lifecycle; subclasses override OnEnter/OnExit for domain logic.
    /// Ticks via Update/FixedUpdate on its own MonoBehaviour — never inside an action.
    /// </summary>
    public abstract class BaseStateSystem : MonoBehaviour, IStateSystem
    {
        private static readonly List<BaseStateSystem> _allInstances = new();

        public bool IsActive { get; private set; }

        /// <summary>Display name for debug UI. Defaults to type name.</summary>
        public virtual string DebugName => GetType().Name;

        protected virtual void Awake()
        {
            _allInstances.Add(this);
        }

        protected virtual void OnDestroy()
        {
            _allInstances.Remove(this);
        }

        public void Enter()
        {
            if (IsActive) return;
            IsActive = true;
            OnEnter();
        }

        public void Exit()
        {
            if (!IsActive) return;
            IsActive = false;
            OnExit();
        }

        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }

        // ----------------------------------------------------------------
        //  Debug API (read-only)
        // ----------------------------------------------------------------

        /// <summary>Returns all active state systems on an actor. Debug only — allocates.</summary>
        public static List<string> GetActiveStateNames(GameObject actor)
        {
            var result = new List<string>();
            for (int i = 0; i < _allInstances.Count; i++)
            {
                var s = _allInstances[i];
                if (s != null && s.IsActive && s.gameObject == actor)
                    result.Add(s.DebugName);
            }
            return result;
        }
    }
}
