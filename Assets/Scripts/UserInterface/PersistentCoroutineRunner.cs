using System.Collections;
using UnityEngine;

namespace Sol.HUD
{
    /// <summary>
    /// Runs coroutines on a persistent, always-active GameObject.
    /// Use this instead of StartCoroutine() when the caller's GameObject might be
    /// deactivated before the coroutine finishes (coroutines stop with their host).
    /// </summary>
    internal class PersistentCoroutineRunner : MonoBehaviour
    {
        private static PersistentCoroutineRunner _instance;

        private static PersistentCoroutineRunner Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[PersistentCoroutineRunner]") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<PersistentCoroutineRunner>();
                return _instance;
            }
        }

        /// <summary>Starts <paramref name="routine"/> on the persistent runner and returns the Coroutine.</summary>
        public static Coroutine Run(IEnumerator routine) => Instance.StartCoroutine(routine);

        /// <summary>Stops a coroutine previously started via <see cref="Run"/>.</summary>
        public static void Stop(Coroutine routine)
        {
            if (_instance == null || routine == null)
                return;

            _instance.StopCoroutine(routine);
        }
    }
}
