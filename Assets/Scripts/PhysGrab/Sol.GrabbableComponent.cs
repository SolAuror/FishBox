using UnityEngine;

namespace Sol.Grab
{

    /// Attach to any object that should be grabbable.
    /// Requires a Collider for raycasting and optionally a Rigidbody for physics-based movement.
    [RequireComponent(typeof(Collider))]
    public class GrabbableComponent : MonoBehaviour
    {
        [Tooltip("Hold distance from the camera when grabbed")]
        [Range(0.5f, 20f)]
        public float holdDistance = 3f;

        [Tooltip("How quickly the object follows the target position (higher = snappier)")]
        [Range(1f, 50f)]
        public float followSpeed = 15f;

        private Rigidbody _rb;
        private bool _hadGravity;
        private bool _wasKinematic;
        private bool _isGrabbed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Called when the object is picked up.
        /// </summary>
        public void OnGrab()
        {
            _isGrabbed = true;

            if (_rb != null)
            {
                _hadGravity = _rb.useGravity;
                _wasKinematic = _rb.isKinematic;
                _rb.useGravity = false;
                _rb.linearDamping = 10f;
                _rb.angularDamping = 10f;
            }
        }

        /// <summary>
        /// Called when the object is released.
        /// </summary>
        public void OnRelease()
        {
            _isGrabbed = false;

            if (_rb != null)
            {
                _rb.useGravity = _hadGravity;
                _rb.isKinematic = _wasKinematic;
                _rb.linearDamping = 0f;
                _rb.angularDamping = 0.05f;
            }
        }

        public void RefreshStoredPhysicsState()
        {
            if (_rb == null)
                return;

            _hadGravity = _rb.useGravity;
            _wasKinematic = _rb.isKinematic;
        }

        /// <summary>
        /// Move toward the target position. Called each FixedUpdate by GrabManager.
        /// </summary>
        public void MoveToward(Vector3 targetPosition)
        {
            if (_rb != null && !_rb.isKinematic)
            {
                Vector3 direction = targetPosition - _rb.position;
                _rb.linearVelocity = direction * followSpeed;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
            }
        }

        public bool IsGrabbed => _isGrabbed;
    }
}
