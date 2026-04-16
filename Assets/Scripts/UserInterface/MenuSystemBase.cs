using UnityEngine;

namespace Sol.HUD
{
    /// <summary>
    /// Generic singleton base for all modal menu panels.
    /// Handles instance lifecycle, CanvasGroup, SetOpen, and UIStateOwnership registration.
    ///
    /// Usage: public sealed class MyMenu : MenuSystemBase&lt;MyMenu&gt; { ... }
    /// </summary>
    public abstract class MenuSystemBase<T> : MonoBehaviour where T : MenuSystemBase<T>
    {
        [SerializeField] protected CanvasGroup _canvasGroup;

        protected static T _instance;
        public static T Instance => _instance;
        public bool IsOpen => gameObject.activeSelf;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = (T)this;
            if (_canvasGroup == null) _canvasGroup = MenuUiUtility.EnsureCanvasGroup(gameObject);
            if (_canvasGroup == null)
                Debug.LogWarning($"[{typeof(T).Name}] CanvasGroup is not assigned on '{name}'. Author it in the prefab instead of relying on runtime creation.", this);
            if (_canvasGroup != null) _canvasGroup.ignoreParentGroups = true;
            UIStateOwnership.Register<T>(_instance);
        }

        protected virtual void OnDestroy()
        {
            UIStateOwnership.Unregister<T>();
            if (_instance == this) _instance = null;
        }

        protected void SetOpen(bool open)
        {
            MenuUiUtility.SetVisible(gameObject, open);
            if (_canvasGroup == null) _canvasGroup = MenuUiUtility.EnsureCanvasGroup(gameObject);
            if (_canvasGroup == null)
                return;

            if (_canvasGroup != null) _canvasGroup.ignoreParentGroups = true;
            MenuUiUtility.SetCanvasGroupVisible(_canvasGroup, open);
        }

        /// <summary>
        /// Returns the singleton instance, activating it if needed.
        /// Override PostResolve() to run system-specific wiring after resolve.
        /// </summary>
        public static T ResolveInstance(bool activateIfInactive = true)
        {
            T resolved = Instance ?? UIStateOwnership.Resolve<T>(activateIfInactive);
            (resolved as MenuSystemBase<T>)?.PostResolve();
            return resolved;
        }

        /// <summary>Called after ResolveInstance finds an instance. Override to run AutoWire etc.</summary>
        protected virtual void PostResolve() { }
    }
}
