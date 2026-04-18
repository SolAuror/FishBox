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
        [SerializeField] private float raycastDistance = 100f;

        [Tooltip("Layer mask for raycast (optional)")]
        [SerializeField] private LayerMask raycastLayerMask = ~0;

        [Tooltip("Player root for CanInteract checks (auto-detected if null)")]
        [SerializeField] private GameObject player;

        private OutlineComponent _currentOutlinedObject;
        private Interactor _playerInteractor;

        private Collider _lastHitCollider;
        private IInteractable _cachedInteractable;
        private OutlineComponent _cachedOutlineComponent;

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

            // Resolve player reference at runtime if not set
            if (player == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    player = playerObj;
            }
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
                    // Try to find player by tag as final fallback
                    var playerObj = GameObject.FindGameObjectWithTag("Player");
                    player = playerObj != null ? playerObj : activeCamera.gameObject;
                }
                _playerInteractor = new Interactor(player, true);
            }

            // Skip outline detection while any blocking UI is open.
            if (Sol.HUD.UIStateOwnership.IsBlockingUiOpen())
            {
                if (_currentOutlinedObject != null)
                {
                    if (!_currentOutlinedObject.alwaysVisible)
                        _currentOutlinedObject.HideOutline();
                    _currentOutlinedObject = null;
                }
                _lastHitCollider = null;
                return;
            }

            // Raycast from screen center (works in both first and third person).
            Ray ray = activeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

            OutlineComponent hoveredComponent = null;

            // Raycast to find what's being hovered over
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastLayerMask))
            {
                // Re-query components only when the hit collider changes.
                if (hit.collider != _lastHitCollider)
                {
                    _lastHitCollider = hit.collider;
                    _cachedInteractable = hit.collider.GetComponent<IInteractable>()
                                       ?? hit.collider.GetComponentInParent<IInteractable>();
                    _cachedOutlineComponent = hit.collider.GetComponent<OutlineComponent>()
                                           ?? hit.collider.GetComponentInParent<OutlineComponent>();
                }

                if (_cachedInteractable != null && _cachedInteractable.CanInteract(_playerInteractor))
                {
                    if (_cachedOutlineComponent != null && !_cachedOutlineComponent.alwaysVisible)
                        hoveredComponent = _cachedOutlineComponent;
                }
            }
            else
            {
                _lastHitCollider = null;
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
