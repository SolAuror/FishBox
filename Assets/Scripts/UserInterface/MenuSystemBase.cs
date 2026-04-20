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

            // Each menu must own a CanvasGroup on its own GameObject. If the prefab
            // wires _canvasGroup to a shared parent group (e.g. UI_System), closing
            // one menu would hide its siblings by zeroing their shared alpha.
            if (_canvasGroup == null || _canvasGroup.gameObject != gameObject)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
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
