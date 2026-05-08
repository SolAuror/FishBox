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
        #region Inspector Settings
        [Header("Setup")]
        [Tooltip("Inspector: tunes preview camera.")]
        [SerializeField] private Camera _previewCamera;
        [SerializeField] private Transform _anchor;
        [Tooltip("Inspector: tunes pivot.")]
        [SerializeField] private Transform _pivot;
        [Tooltip("Inspector: tunes preview layer.")]
        [SerializeField] private int _previewLayer = 31;

        [Header("Rendering")]
        [Tooltip("Inspector: tunes texture size.")]
        [SerializeField] private int _textureSize = 256;
        [SerializeField] private float _rotationSpeed = 20f;
        [Tooltip("Inspector: tunes camera distance.")]
        [SerializeField] private float _cameraDistance = 2f;
        [Tooltip("Inspector: tunes preview light intensity.")]
        [SerializeField] private float _previewLightIntensity = 1.15f;
        #endregion

        private static readonly Color ClearColor = new(0f, 0f, 0f, 0f);

        public RenderTexture RenderTexture { get; private set; }
        public static ItemPreviewRenderer Instance { get; private set; }

        private GameObject _currentInstance;
        private bool _isActive;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            UIStateOwnership.Register<ItemPreviewRenderer>(this);

            // The MasterCanvas prefab has historically nested this renderer under the UI
            // PreviewImage RawImage (a RectTransform in screen space, with non-unit scale).
            // That breaks 3D previewing: the camera inherits UI-space transforms and items
            // parented under the pivot get squashed or clipped. Detach to the scene root and
            // normalize local scale so the renderer is robust to prefab misplacement.
            if (transform.parent != null)
                transform.SetParent(null, worldPositionStays: false);
            transform.localScale = Vector3.one;

            if (_anchor == null)
                _anchor = transform;
            else
                _anchor.localScale = Vector3.one;

            if (_pivot == null)
                _pivot = _anchor;
            else
                _pivot.localScale = Vector3.one;

            RenderTexture = new RenderTexture(_textureSize, _textureSize, 16);
            RenderTexture.antiAliasing = 2;

            if (_previewCamera != null)
            {
                _previewCamera.targetTexture = RenderTexture;
                _previewCamera.cullingMask = 1 << _previewLayer;
                _previewCamera.clearFlags = CameraClearFlags.SolidColor;
                _previewCamera.backgroundColor = ClearColor;
                _previewCamera.enabled = false;
            }

            EnsurePreviewLight();

            ClearRenderTarget();
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
            _currentInstance.transform.localScale = Vector3.one;

            PrepareRenderers(_currentInstance);
            StripGameplayComponents(_currentInstance);
            SetLayerRecursive(_currentInstance, _previewLayer);
            _isActive = FrameObject(_currentInstance);

            ClearRenderTarget();

            if (_previewCamera != null)
            {
                _previewCamera.enabled = _isActive;
                if (_isActive)
                    _previewCamera.Render();
            }
        }

        public void ShowItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Clear();
                return;
            }

            ItemComponent prefab = Sol.ItemRegistry.Get()?.GetVisualPrefab(itemId);
            if (prefab == null)
            {
                Clear();
                return;
            }

            Show(prefab);
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

            ClearRenderTarget();
        }

        private static void PrepareRenderers(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.gameObject.SetActive(true);
                renderer.enabled = true;
                renderer.forceRenderingOff = false;

                if (renderer is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
            }
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

        private bool FrameObject(GameObject go)
        {
            if (go == null || _pivot == null || _anchor == null || _previewCamera == null)
                return false;

            // Calculate combined bounds of all renderers.
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return false;

            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
                return false;

 // Center the object on the pivot. Do NOT rescale the clone - previews must
            // preserve real-world item scale so that a small fish looks smaller than a large
            // fish in the tooltip, matching how they appear in the world and inventory.
            Vector3 centerOffset = _pivot.position - bounds.center;
            go.transform.position += centerOffset;

            // Position the camera at a fixed authoring distance. A large item will fill more
 // of the preview frame, a small item will fill less - that's the consistency we
            // want across every tooltip.
            float distance = Mathf.Max(0.1f, _cameraDistance);
            _previewCamera.transform.position = _anchor.position + _anchor.forward * -distance;
            _previewCamera.transform.LookAt(_anchor.position);
            _previewCamera.nearClipPlane = 0.01f;
            _previewCamera.farClipPlane = Mathf.Max(10f, distance + bounds.extents.magnitude * 4f);

            return true;
        }

        private void ClearRenderTarget()
        {
            if (RenderTexture == null)
                return;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = RenderTexture;
            GL.Clear(true, true, ClearColor);
            RenderTexture.active = previous;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        private void EnsurePreviewLight()
        {
            // Previous implementation used a Directional light filtered via Light.cullingMask.
            // URP's forward renderer does not reliably honour cullingMask for dynamic lights,
            // so a directional preview light spills into the main scene and washes out the
            // environment lighting. Use a short-range Point light instead: its physical range
            // is capped below _cameraDistance so it cannot reach any real scene geometry even
            // if URP ignores the culling mask entirely.
            Light light = GetComponentInChildren<Light>(true);
            if (light == null)
            {
                GameObject lightGo = new("PreviewLight", typeof(Light));
                lightGo.transform.SetParent(_pivot != null ? _pivot : transform, false);
                lightGo.transform.localPosition = Vector3.zero;
                lightGo.transform.localRotation = Quaternion.identity;
                light = lightGo.GetComponent<Light>();
            }

            if (light == null)
                return;

            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.range = Mathf.Max(0.1f, _cameraDistance * 0.5f);
            light.intensity = Mathf.Max(0f, _previewLightIntensity);
            light.cullingMask = 1 << _previewLayer;
            light.renderingLayerMask = 1 << _previewLayer;
            light.enabled = true;
        }
    }
}
