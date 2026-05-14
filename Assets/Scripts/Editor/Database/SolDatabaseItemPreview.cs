using Sol.Grab;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal sealed class SolDatabaseItemPreview : System.IDisposable
    {
        private const float CameraDistance = 2.4f;

        private readonly PreviewRenderUtility _previewUtility;
        private ItemComponent _source;
        private GameObject _instance;
        private Bounds _bounds;
        private bool _hasBounds;
        private float _rotationDegrees;

        public SolDatabaseItemPreview()
        {
            _previewUtility = new PreviewRenderUtility();
            _previewUtility.cameraFieldOfView = 28f;
            _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            _previewUtility.camera.backgroundColor = new Color(0.11f, 0.12f, 0.13f, 1f);
            _previewUtility.camera.nearClipPlane = 0.01f;
            _previewUtility.camera.farClipPlane = 100f;
            _previewUtility.lights[0].intensity = 1.1f;
            _previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            _previewUtility.lights[1].intensity = 0.45f;
        }

        public bool HasRenderablePreview => _instance != null && _hasBounds;

        public void SetItem(ItemComponent item)
        {
            if (_source == item)
                return;

            ClearInstance();
            _source = item;
            _rotationDegrees = 0f;

            if (_source == null)
                return;

            _instance = Object.Instantiate(_source.gameObject);
            _instance.name = $"{_source.name} Preview";
            _instance.hideFlags = HideFlags.HideAndDontSave;
            _instance.SetActive(true);
            _instance.transform.position = Vector3.zero;
            _instance.transform.rotation = Quaternion.identity;
            _instance.transform.localScale = Vector3.one;

            PreparePreviewObject(_instance);
            _previewUtility.AddSingleGO(_instance);
            _hasBounds = TryCalculateBounds(_instance, out _bounds);
            FrameCamera();
        }

        public void ResetRotation()
        {
            _rotationDegrees = 0f;
            ApplyRotation();
        }

        public void Rotate(float deltaDegrees)
        {
            _rotationDegrees = Mathf.Repeat(_rotationDegrees + deltaDegrees, 360f);
            ApplyRotation();
        }

        public Texture Render(Rect rect)
        {
            if (!HasRenderablePreview)
                return null;

            FrameCamera();
            _previewUtility.BeginPreview(rect, GUIStyle.none);
            _previewUtility.camera.Render();
            return _previewUtility.EndPreview();
        }

        public void Dispose()
        {
            ClearInstance();
            _previewUtility.Cleanup();
        }

        private void ClearInstance()
        {
            if (_instance != null)
                Object.DestroyImmediate(_instance);

            _source = null;
            _instance = null;
            _hasBounds = false;
            _bounds = default;
        }

        private void ApplyRotation()
        {
            if (_instance == null || !_hasBounds)
                return;

            _instance.transform.rotation = Quaternion.Euler(0f, _rotationDegrees, 0f);
            _hasBounds = TryCalculateBounds(_instance, out _bounds);
            FrameCamera();
        }

        private void FrameCamera()
        {
            if (!_hasBounds)
                return;

            Vector3 center = _bounds.center;
            float radius = Mathf.Max(0.1f, _bounds.extents.magnitude);
            float distance = Mathf.Max(CameraDistance, radius * 2.6f);
            Transform cameraTransform = _previewUtility.camera.transform;
            cameraTransform.position = center + new Vector3(0f, radius * 0.18f, -distance);
            cameraTransform.rotation = Quaternion.LookRotation(center - cameraTransform.position, Vector3.up);
            _previewUtility.camera.farClipPlane = Mathf.Max(20f, distance + radius * 4f);
        }

        private static void PreparePreviewObject(GameObject go)
        {
            SetLayerRecursive(go, 31);

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

            MonoBehaviour[] behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                    behaviours[i].enabled = false;
            }

            Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }

            Rigidbody[] rigidbodies = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody rb = rigidbodies[i];
                if (rb == null)
                    continue;

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        private static bool TryCalculateBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
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

            return hasBounds;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
