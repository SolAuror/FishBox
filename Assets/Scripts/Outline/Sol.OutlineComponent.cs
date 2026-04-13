using UnityEngine;

namespace Sol.Outline
{
    /// Attach to any object that should be outlineable.
    /// The SolOutlineRendererFeature handles all rendering — this component
    /// just registers/unregisters itself and exposes the renderers.
    public class OutlineComponent : MonoBehaviour
    {
        [Tooltip("Outline color for this object")]
        public Color outlineColor = new Color(1f, 0.5f, 0f, 1f);

        [Tooltip("Outline width in pixels for this object")]
        [Range(1f, 20f)]
        public float outlineWidth = 3f;

        [Tooltip("Keep the outline visible at all times, regardless of hover")]
        public bool alwaysVisible = false;

        [Tooltip("Outline renders on top of everything, ignoring depth")]
        public bool priority = false;

        private Renderer[] _renderers;
        private bool _outlineActive;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
        }

        private void OnEnable()
        {
            if (alwaysVisible)
                ShowOutline();
        }

        private void OnDisable()
        {
            HideOutline();
        }

        public void ShowOutline()
        {
            if (_outlineActive) return;
            _outlineActive = true;
            SolOutlineRendererFeature.Register(this);
        }

        public void HideOutline()
        {
            if (!_outlineActive) return;
            _outlineActive = false;
            SolOutlineRendererFeature.Unregister(this);
        }

        public bool IsOutlineActive => _outlineActive;

        public Renderer[] GetRenderers() => _renderers;
    }
}
