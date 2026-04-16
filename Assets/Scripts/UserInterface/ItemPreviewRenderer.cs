using UnityEngine;
using Sol.Grab;

namespace Sol.HUD
{
    /// <summary>
    /// Global 3D item preview renderer. Uses a dedicated camera rendering to a RenderTexture.
    /// ONE instance in scene. Shown inside TooltipUI via RawImage.
    ///
    /// Hierarchy at runtime:
    ///   ItemPreviewRenderer (this)
    ///     +-- PreviewCamera
    ///     +-- PreviewAnchor
    ///           +-- Pivot (rotates)
    ///                 +-- [ItemInstance] (spawned)
    /// </summary>
    public class ItemPreviewRenderer : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private Camera _previewCamera;
        [SerializeField] private Transform _anchor;
        [SerializeField] private Transform _pivot;
        [SerializeField] private int _previewLayer = 31;

        [Header("Rendering")]
        [SerializeField] private int _textureSize = 256;
        [SerializeField] private float _rotationSpeed = 20f;
        [SerializeField] private float _cameraDistance = 2f;

        public RenderTexture RenderTexture { get; private set; }
        public static ItemPreviewRenderer Instance { get; private set; }

        private GameObject _currentInstance;
        private bool _isActive;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            UIStateOwnership.Register<ItemPreviewRenderer>(this);

            RenderTexture = new RenderTexture(_textureSize, _textureSize, 16);
            RenderTexture.antiAliasing = 2;

            if (_previewCamera != null)
            {
                _previewCamera.targetTexture = RenderTexture;
                _previewCamera.cullingMask = 1 << _previewLayer;
                _previewCamera.enabled = false;
            }
        }

        private void OnDestroy()
        {
            UIStateOwnership.Unregister<ItemPreviewRenderer>();
            if (Instance == this) Instance = null;
            if (_currentInstance != null) Destroy(_currentInstance);
            if (RenderTexture != null) RenderTexture.Release();
        }

        private void Update()
        {
            if (!_isActive || _pivot == null) return;
            _pivot.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.Self);

            if (_previewCamera != null)
                _previewCamera.Render();
        }

        public void Show(ItemComponent item)
        {
            if (item == null) return;

            Clear();

            // Instantiate a visual copy under the pivot.
            _currentInstance = Instantiate(item.gameObject, _pivot);
            _currentInstance.SetActive(true);   // Source may be inactive (in inventory).
            _currentInstance.transform.localPosition = Vector3.zero;
            _currentInstance.transform.localRotation = Quaternion.identity;

            StripGameplayComponents(_currentInstance);
            SetLayerRecursive(_currentInstance, _previewLayer);
            FrameObject(_currentInstance);

            _isActive = true;
            if (_previewCamera != null)
            {
                _previewCamera.enabled = true;
                _previewCamera.Render();
            }
        }

        public void Clear()
        {
            _isActive = false;

            if (_currentInstance != null)
            {
                Destroy(_currentInstance);
                _currentInstance = null;
            }

            if (_pivot != null)
                _pivot.localRotation = Quaternion.identity;

            if (_previewCamera != null)
                _previewCamera.enabled = false;
        }

        private void StripGameplayComponents(GameObject go)
        {
            // Disable all MonoBehaviours except renderers.
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                mb.enabled = false;

            // Disable colliders.
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            // Make rigidbodies kinematic.
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }
        }

        private void FrameObject(GameObject go)
        {
            // Calculate combined bounds of all renderers.
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // Center the object on the pivot.
            var offset = _pivot.position - bounds.center;
            go.transform.localPosition += go.transform.InverseTransformVector(offset);

            // Normalize scale so the object fits in view.
            float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            if (maxExtent > 0.001f)
            {
                float desiredSize = _cameraDistance * 0.35f;
                float scaleFactor = desiredSize / maxExtent;
                go.transform.localScale *= scaleFactor;
            }

            // Position camera to frame the object.
            if (_previewCamera != null && _anchor != null)
            {
                _previewCamera.transform.position = _anchor.position + _anchor.forward * -_cameraDistance;
                _previewCamera.transform.LookAt(_anchor.position);
            }
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
