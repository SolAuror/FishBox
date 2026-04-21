using UnityEngine;

namespace Sol.Locomotion
{
    /// <summary>
    /// Maps PhysicsMaterials to FootstepSurface assets.
    /// One library per project. Assign to FootstepPlayer.
    /// Create via Assets ? Create ? Sol/Locomotion/Footstep Library.
    /// </summary>
    [CreateAssetMenu(menuName = "Sol/Locomotion/Footstep Library", fileName = "FootstepLibrary")]
    public class FootstepLibrary : ScriptableObject
    {
        [System.Serializable]
        public struct SurfaceMapping
        {
            [Tooltip("The PhysicsMaterial assigned to a collider. Null = default/fallback.")]
            public PhysicsMaterial material;
            public FootstepSurface surface;
        }
#region Inspector Settings

        [Tooltip("Default surface used when no material match is found.")]
        [SerializeField] private FootstepSurface _defaultSurface;

        [Tooltip("Material ? Surface mappings. First match wins.")]
        [SerializeField] private SurfaceMapping[] _mappings;
#endregion

        public FootstepSurface DefaultSurface => _defaultSurface;

        /// <summary>
        /// Resolve a PhysicsMaterial to a FootstepSurface, falling back to the default.
        /// </summary>
        public FootstepSurface Resolve(PhysicsMaterial mat)
        {
            if (_mappings != null)
            {
                for (int i = 0; i < _mappings.Length; i++)
                {
                    if (_mappings[i].material == mat && _mappings[i].surface != null)
                        return _mappings[i].surface;
                }
            }
            return _defaultSurface;
        }
    }
}
