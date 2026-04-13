using UnityEngine;
using Sol;
using Sol.Locomotion;

namespace Sol.Outline
{
    /// Singleton manager - handles hover detection and controls which object's outline is active.
    /// Performs raycasting from mouse position and activates the OutlineComponent on the hovered object.
    /// Place one instance of this in the scene to enable hover-based outlining.
    public class OutlineManager : MonoBehaviour
    {
        [Tooltip("Max raycast distance for hover detection")]
        public float raycastDistance = 100f;

        [Tooltip("Layer mask for raycast (optional)")]
        public LayerMask raycastLayerMask = ~0;

        [Tooltip("Player root for CanInteract checks (auto-detected if null)")]
        public GameObject player;

        private OutlineComponent _currentOutlinedObject;
        private Interactor _playerInteractor;

        public static OutlineManager Instance { get; private set; }

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple OutlineManagers detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            Camera activeCamera = GetActiveGameplayCamera();
            if (activeCamera == null) return;

            // Lazy-init player interactor.
            if (_playerInteractor == null)
            {
                if (player == null)
                {
                    var playerObj = GameObject.FindGameObjectWithTag("Player");
                    player = playerObj != null ? playerObj : activeCamera.gameObject;
                }
                _playerInteractor = new Interactor(player, true);
            }

            // Raycast from screen center (works in both first and third person).
            Ray ray = activeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

            OutlineComponent hoveredComponent = null;

            // Raycast to find what's being hovered over
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastLayerMask))
            {
                // Only outline objects that are interactable and can be interacted with
                var interactable = hit.collider.GetComponent<IInteractable>()
                                ?? hit.collider.GetComponentInParent<IInteractable>();

                if (interactable != null && interactable.CanInteract(_playerInteractor))
                {
                    hoveredComponent = hit.collider.GetComponent<OutlineComponent>();
                    if (hoveredComponent == null)
                        hoveredComponent = hit.collider.GetComponentInParent<OutlineComponent>();

                    // Skip objects that are set to alwaysVisible (they manage themselves)
                    if (hoveredComponent != null && hoveredComponent.alwaysVisible)
                        hoveredComponent = null;
                }
            }

            // Update outline state
            if (hoveredComponent != _currentOutlinedObject)
            {
                // Hide previous outline
                if (_currentOutlinedObject != null && !_currentOutlinedObject.alwaysVisible)
                    _currentOutlinedObject.HideOutline();

                // Show new outline
                if (hoveredComponent != null)
                    hoveredComponent.ShowOutline();

                _currentOutlinedObject = hoveredComponent;
            }
        }

        /// Manually set which object should have an outline. Pass null to clear.
        public void SetOutlinedObject(OutlineComponent component)
        {
            if (_currentOutlinedObject != null)
                _currentOutlinedObject.HideOutline();

            _currentOutlinedObject = component;

            if (_currentOutlinedObject != null)
                _currentOutlinedObject.ShowOutline();
        }

        /// Returns the currently outlined object, if any.
        public OutlineComponent CurrentOutlinedObject => _currentOutlinedObject;

        private Camera GetActiveGameplayCamera()
        {
            if (LocomotionInputManager.Instance != null &&
                LocomotionInputManager.Instance.TryGetCameraContext(out CameraContext context) &&
                context.UnityCamera != null)
            {
                return context.UnityCamera;
            }

            return Camera.main;
        }
    }
}
