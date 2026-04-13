using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Sol.Locomotion;
using Sol;

namespace Sol.Grab
{
    /// Singleton manager that handles grabbing objects with GrabbableComponent.
    /// Raycasts from the mouse position on click, grabs the object and moves it with the cursor.
    /// Middle click to toggle rotation mode - objects will rotate instead of moving.
    /// Use scroll wheel to adjust distance. Right click to freeze/unfreeze individual objects (when isLockingEnabled).
    /// Toggle isFrozen to freeze/unfreeze all objects at once.
    /// Place one instance in your scene alongside the OutlineManager.
    public class GrabManager : BaseStateSystem
    {
        [Tooltip("Max raycast distance for grab detection")]
        public float raycastDistance = 100f;

        [Tooltip("Layer mask for grab raycast")]
        public LayerMask raycastLayerMask = ~0;

        [Tooltip("Enable grabbing functionality")]
        public bool isGrabbingEnabled = true;

        [Tooltip("Scroll wheel sensitivity for adjusting hold distance")]
        public float scrollSensitivity = 0.5f;

        [Tooltip("Allow objects to be frozen with right click")]
        public bool isLockingEnabled = true;

        [Tooltip("Master toggle: freeze/unfreeze all objects")]
        public bool isFrozen = false;

        [Tooltip("Enable rotation mode - objects will rotate instead of move")]
        public bool rotationMode = false;

        [Tooltip("Rotation sensitivity for mouse movement")]
        public float rotationSensitivity = 2f;

        private GrabbableComponent _heldObject;
        private GrabbableComponent _hoveredObject;
        private readonly List<FrozenObjectData> _frozenObjects = new();
        private float _currentHoldDistance;
        private Vector2 _scrollInput;
        private Vector2 _lastMousePosition;
        
        // Structure to track frozen object state
        private struct FrozenObjectData
        {
            public GrabbableComponent component;
            public Rigidbody rb;
            public bool wasKinematic;
            public bool hadGravity;
            public RigidbodyConstraints constraints;
        }

        private SolControls _controls;
        private SolControls.DefaultActions _defaultActions;
        private bool _previousFrozenState = false;

        public static GrabManager Instance { get; private set; }

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple GrabManagers detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            base.Awake();

            _controls = new SolControls();
            _defaultActions = _controls.Default;
        }

        private void OnEnable()
        {
            _defaultActions.Enable();
            _defaultActions.Scroll.performed += OnScroll;
            _defaultActions.Scroll.canceled += OnScroll;
            _defaultActions.RClick.started += OnRClick;
            _defaultActions.MClick.started += OnMClick;
        }

        private void OnDisable()
        {
            _defaultActions.Scroll.performed -= OnScroll;
            _defaultActions.Scroll.canceled -= OnScroll;
            _defaultActions.RClick.started -= OnRClick;
            _defaultActions.MClick.started -= OnMClick;
            _defaultActions.Disable();
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            _controls?.Dispose();
            base.OnDestroy();
        }

        private void Update()
        {
            Camera activeCamera = GetActiveGameplayCamera();
            if (Mouse.current == null || activeCamera == null) return;

            // Handle master freeze toggle
            if (isFrozen != _previousFrozenState)
            {
                _previousFrozenState = isFrozen;
                if (isFrozen)
                {
                    FreezeAllObjects();
                }
                else
                {
                    UnfreezeAllObjects();
                }
            }

            // Suppress all grab interaction while UI is open.
            bool uiOpen = LocomotionInputManager.Instance != null && LocomotionInputManager.Instance.UIInputBlocked;
            if (uiOpen)
            {
                _hoveredObject = null;
                return;
            }

            // Update hovered object (used for freeze/unfreeze via RClick)
            UpdateHoveredObject();

            // Scroll adjusts hold distance while holding an object.
            if (_heldObject != null && Mathf.Abs(_scrollInput.y) > 0.01f)
            {
                _currentHoldDistance = Mathf.Clamp(_currentHoldDistance + _scrollInput.y * scrollSensitivity, 0.5f, raycastDistance);
                _scrollInput = Vector2.zero;
            }
        }

        private void FixedUpdate()
        {
            Camera activeCamera = GetActiveGameplayCamera();
            if (_heldObject == null || activeCamera == null) return;
            if (IsFrozen(_heldObject) || isFrozen) return; // Don't move frozen objects

            if (rotationMode)
            {
                // Rotate the object based on mouse delta
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Vector2 mouseDelta = mousePos - _lastMousePosition;
                _lastMousePosition = mousePos;

                if (mouseDelta.sqrMagnitude > 0.01f)
                {
                    // Horizontal movement = rotation around world up axis
                    // Vertical movement = rotation around camera right axis
                    Vector3 cameraRight = activeCamera.transform.right;
                    Vector3 worldUp = Vector3.up;

                    Quaternion yawRotation = Quaternion.AngleAxis(mouseDelta.x * rotationSensitivity, worldUp);
                    Quaternion pitchRotation = Quaternion.AngleAxis(-mouseDelta.y * rotationSensitivity, cameraRight);

                    _heldObject.transform.rotation = yawRotation * pitchRotation * _heldObject.transform.rotation;
                }
            }
            else
            {
                // Normal movement mode
                Vector3 targetPos = GetHoldPosition();
                _heldObject.MoveToward(targetPos);
            }
        }

        private void UpdateHoveredObject()
        {
            Camera activeCamera = GetActiveGameplayCamera();
            if (activeCamera == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = activeCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastLayerMask))
            {
                var grabbable = hit.collider.GetComponent<GrabbableComponent>();
                if (grabbable == null)
                    grabbable = hit.collider.GetComponentInParent<GrabbableComponent>();

                _hoveredObject = grabbable;
            }
            else
            {
                _hoveredObject = null;
            }
        }

        private void TryGrab()
        {
            Camera activeCamera = GetActiveGameplayCamera();
            if (activeCamera == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = activeCamera.ScreenPointToRay(mousePos);

            if (!Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastLayerMask))
                return;

            // Yield to the interaction pipeline: if the hit object has an IInteractable
            // that isn't a GrabInteractable, let CrosshairUI handle it (e.g. item pickup).
            var interactable = hit.collider.GetComponent<IInteractable>()
                            ?? hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null && interactable is not GrabInteractable)
                return;

            var grabbable = hit.collider.GetComponent<GrabbableComponent>();
            if (grabbable == null)
                grabbable = hit.collider.GetComponentInParent<GrabbableComponent>();

            if (grabbable == null || grabbable.IsGrabbed) return;

            GrabObject(grabbable);
        }

        protected override void OnExit()
        {
            if (_heldObject != null && !IsFrozen(_heldObject))
                _heldObject.OnRelease();
            _heldObject = null;
        }

        private Vector3 GetHoldPosition()
        {
            Camera activeCamera = GetActiveGameplayCamera();
            if (activeCamera == null)
                return _heldObject != null ? _heldObject.transform.position : Vector3.zero;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = activeCamera.ScreenPointToRay(mousePos);
            return ray.GetPoint(_currentHoldDistance);
        }

        private void OnRClick(InputAction.CallbackContext context)
        {
            if (!isLockingEnabled) return;
            if (LocomotionInputManager.Instance != null && LocomotionInputManager.Instance.UIInputBlocked) return;
            
            // Try to lock/unlock hovered or held object
            GrabbableComponent target = _heldObject != null ? _heldObject : _hoveredObject;
            
            if (target != null)
            {
                if (IsFrozen(target))
                {
                    // Unfreeze this specific object
                    UnfreezeObject(target);
                }
                else
                {
                    // Freeze new target
                    FreezeObject(target);
                }
            }
        }
        
        private void OnMClick(InputAction.CallbackContext context) 
        {
            // Toggle rotation mode
            rotationMode = !rotationMode;
        }

        private bool IsFrozen(GrabbableComponent obj)
        {
            for (int i = 0; i < _frozenObjects.Count; i++)
                if (_frozenObjects[i].component == obj) return true;
            return false;
        }

        private void FreezeObject(GrabbableComponent obj)
        {
            if (IsFrozen(obj)) return; // Already frozen
            
            var rb = obj.GetComponent<Rigidbody>();
            
            // If object is currently grabbed, release it first via state lifecycle
            if (obj == _heldObject)
            {
                Exit();
            }
            
            if (rb != null)
            {
                var frozenData = new FrozenObjectData
                {
                    component = obj,
                    rb = rb,
                    wasKinematic = rb.isKinematic,
                    hadGravity = rb.useGravity,
                    constraints = rb.constraints
                };
                
                _frozenObjects.Add(frozenData);
                
                // Set velocities to zero BEFORE making kinematic to avoid warnings
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }
        }

        private void UnfreezeObject(GrabbableComponent obj)
        {
            int index = -1;
            for (int i = 0; i < _frozenObjects.Count; i++)
            {
                if (_frozenObjects[i].component == obj) { index = i; break; }
            }
            if (index < 0) return;
            
            var frozenData = _frozenObjects[index];
            
            if (frozenData.rb != null)
            {
                frozenData.rb.isKinematic = frozenData.wasKinematic;
                frozenData.rb.useGravity = frozenData.hadGravity;
                frozenData.rb.constraints = frozenData.constraints;
            }
            
            _frozenObjects.RemoveAt(index);
        }

        private void FreezeAllObjects()
        {
            // Find all GrabbableComponent objects in the scene and freeze them
            var allGrabbables = FindObjectsByType<GrabbableComponent>(FindObjectsSortMode.None);
            foreach (var grabbable in allGrabbables)
            {
                if (!IsFrozen(grabbable))
                {
                    var rb = grabbable.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        // Release if currently held
                        if (grabbable == _heldObject)
                        {
                            Exit();
                        }
                        
                        var frozenData = new FrozenObjectData
                        {
                            component = grabbable,
                            rb = rb,
                            wasKinematic = rb.isKinematic,
                            hadGravity = rb.useGravity,
                            constraints = rb.constraints
                        };
                        
                        _frozenObjects.Add(frozenData);
                        
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.isKinematic = true;
                        rb.useGravity = false;
                        rb.constraints = RigidbodyConstraints.FreezeAll;
                    }
                }
            }
        }

        private void UnfreezeAllObjects()
        {
            for (int i = 0; i < _frozenObjects.Count; i++)
            {
                var frozenData = _frozenObjects[i];
                if (frozenData.rb != null)
                {
                    frozenData.rb.isKinematic = frozenData.wasKinematic;
                    frozenData.rb.useGravity = frozenData.hadGravity;
                    frozenData.rb.constraints = frozenData.constraints;
                }
            }
            _frozenObjects.Clear();
        }


        /// Force-drop the currently held object, if any.
        public void ForceRelease() => Exit();

        /// <summary>
        /// Grab a specific object and begin tracking it. Called by GrabInteractable
        /// so the interaction pipeline goes through GrabManager state rather than
        /// calling GrabbableComponent.OnGrab() directly.
        /// </summary>
        public void GrabObject(GrabbableComponent grabbable)
        {
            if (grabbable == null || grabbable.IsGrabbed || IsActive) return;

            if (IsFrozen(grabbable))
                UnfreezeObject(grabbable);

            _heldObject = grabbable;

            // Estimate hold distance from camera to object.
            Camera cam = GetActiveGameplayCamera();
            _currentHoldDistance = cam != null
                ? Vector3.Distance(cam.transform.position, grabbable.transform.position)
                : 3f;

            if (Mouse.current != null)
                _lastMousePosition = Mouse.current.position.ReadValue();

            Enter();
        }

        protected override void OnEnter()
        {
            _heldObject.OnGrab();
        }

        private void OnScroll(InputAction.CallbackContext context)
        {
            _scrollInput = context.ReadValue<Vector2>();
        }

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

        /// Returns the currently grabbed object, if any.
        public GrabbableComponent HeldObject => _heldObject;
        
        /// Returns the currently hovered object, if any.
        public GrabbableComponent HoveredObject => _hoveredObject;
        
        /// Returns the number of currently frozen objects.
        public int FrozenObjectCount => _frozenObjects.Count;

        /// Returns the frozen object at the given index.
        public GrabbableComponent GetFrozenObject(int index) => _frozenObjects[index].component;
    }
}
