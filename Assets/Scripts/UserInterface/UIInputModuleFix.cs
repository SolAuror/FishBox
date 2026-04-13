using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Sol.HUD
{
    /// <summary>
    /// Wires the InputSystemUIInputModule to use the SolControls "UI" action map
    /// instead of the default actions or the misconfigured Default map references.
    ///
    /// Assigns:
    ///   Point       ? UI/MousePoint  (<Mouse>/position)
    ///   LeftClick   ? UI/LClick      (*/{PrimaryAction})
    ///   RightClick  ? UI/RClick      (*/{SecondaryAction})
    ///   ScrollWheel ? UI/Scroll      (<Mouse>/scroll)
    ///
    /// Place this component on the EventSystem GameObject. Assign the SolControls
    /// InputActionAsset in the Inspector (drag the SolControls.InputActions asset).
    /// </summary>
    [AddComponentMenu("Sol/HUD/UI Input Module Fix")]
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public class UIInputModuleFix : MonoBehaviour
    {
        [Tooltip("Drag the SolControls.InputActions asset here.")]
        [SerializeField] private InputActionAsset _solControls;

        private void Awake()
        {
            var module = GetComponent<InputSystemUIInputModule>();

            // Auto-find asset if not assigned.
            if (_solControls == null)
                _solControls = FindSolControlsAsset();

            if (_solControls == null)
            {
                Debug.LogWarning("[UIInputModuleFix] SolControls asset not found. Falling back to built-in defaults.", this);
                module.AssignDefaultActions();
                return;
            }

            var uiMap      = _solControls.FindActionMap("UI",      throwIfNotFound: false);
            var defaultMap = _solControls.FindActionMap("Default", throwIfNotFound: false); // fallback for Scroll if not in UI map

            if (uiMap == null)
            {
                Debug.LogWarning("[UIInputModuleFix] 'UI' action map not found in SolControls. Falling back to built-in defaults.", this);
                module.AssignDefaultActions();
                return;
            }

            // Enable the UI map so the actions fire.
            uiMap.Enable();

            var point      = uiMap.FindAction("MousePoint", throwIfNotFound: false);
            var leftClick  = uiMap.FindAction("LClick",     throwIfNotFound: false);
            var rightClick = uiMap.FindAction("RClick",     throwIfNotFound: false);
            var scroll     = uiMap.FindAction("Scroll",     throwIfNotFound: false)
                          ?? defaultMap?.FindAction("Scroll", throwIfNotFound: false);

            if (point      != null) module.point       = InputActionReference.Create(point);
            if (leftClick  != null) module.leftClick   = InputActionReference.Create(leftClick);
            if (rightClick != null) module.rightClick  = InputActionReference.Create(rightClick);
            if (scroll     != null) module.scrollWheel = InputActionReference.Create(scroll);

            Debug.Log("[UIInputModuleFix] InputSystemUIInputModule wired to SolControls UI map. UI pointer events active.", this);
        }

        private static InputActionAsset FindSolControlsAsset()
        {
            foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
            {
                if (asset.name == "SolControls")
                    return asset;
            }
            return null;
        }
    }
}
