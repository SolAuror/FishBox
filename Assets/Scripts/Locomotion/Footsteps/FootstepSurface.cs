using UnityEngine;

namespace Sol.Locomotion
{
    /// <summary>
    /// Defines the footstep sounds for a single surface type (e.g. Grass, Stone, Wood).
    /// Create one asset per surface via Assets ? Create ? Sol/Locomotion/Footstep Surface.
    /// </summary>
    [CreateAssetMenu(menuName = "Sol/Locomotion/Footstep Surface", fileName = "NewFootstepSurface")]
    public class FootstepSurface : ScriptableObject
    {
        #region Inspector Settings
        [Tooltip("Clips played when walking or running on this surface.")]
        [SerializeField] private AudioClip[] _footsteps;

 [Tooltip("Clips played on hard landing (optional - falls back to footsteps).")]
        [SerializeField] private AudioClip[] _landings;

        [Tooltip("Volume range for footsteps.")]
        [SerializeField] private float _volumeMin = 0.15f;
        [SerializeField] private float _volumeMax = 0.3f;

        [Tooltip("Pitch variation range.")]
        [SerializeField] private float _pitchMin = 0.9f;
        [SerializeField] private float _pitchMax = 1.1f;
        #endregion

        public AudioClip[] Footsteps => _footsteps;
        public AudioClip[] Landings => _landings;
        public float VolumeMin => _volumeMin;
        public float VolumeMax => _volumeMax;
        public float PitchMin => _pitchMin;
        public float PitchMax => _pitchMax;

        public AudioClip GetRandomFootstep()
        {
            if (_footsteps == null || _footsteps.Length == 0) return null;
            return _footsteps[Random.Range(0, _footsteps.Length)];
        }

        public AudioClip GetRandomLanding()
        {
            if (_landings != null && _landings.Length > 0)
                return _landings[Random.Range(0, _landings.Length)];
            return GetRandomFootstep();
        }
    }
}
