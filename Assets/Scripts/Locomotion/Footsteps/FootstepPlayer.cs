using UnityEngine;

namespace Sol.Locomotion
{
    /// <summary>
    /// Receives OnFootstep animation events and plays surface-aware footstep audio.
    /// Attach to the same GameObject that has the Animator (the character root or model).
    ///
    /// Requires:
    ///   - An AudioSource (added automatically if missing)
    ///   - A FootstepLibrary asset assigned in the Inspector
    /// Optional:
    ///   - A LocomotionController in parents for movement-state volume scaling and landing events.
    ///     When absent (e.g. on NPCs) footsteps play at default volume using the library default surface.
    /// </summary>
    [AddComponentMenu("Sol/Locomotion/Footstep Player")]
    [RequireComponent(typeof(AudioSource))]
    public class FootstepPlayer : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Drag a FootstepLibrary asset here.")]
        [SerializeField] private FootstepLibrary _library;

        [Header("Volume Scaling")]
        [Tooltip("Volume multiplier when crouching.")]
        [SerializeField] private float _crouchVolumeScale = 0.4f;
        [Tooltip("Volume multiplier when sprinting.")]
        [SerializeField] private float _sprintVolumeScale = 1.5f;
        [Tooltip("Volume multiplier for landing impacts.")]
        [SerializeField] private float _landingVolumeScale = 2f;

        [Header("Cooldown")]
        [Tooltip("Minimum seconds between footstep sounds to prevent spam.")]
        [SerializeField] private float _minInterval = 0.15f;

        private AudioSource _audio;
        private LocomotionController _controller;
        private CharacterController _cc;
        private float _lastStepTime;

        private void Awake()
        {
            _audio = GetComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f; // 3D sound
            _audio.loop = false;

            _controller = GetComponentInParent<LocomotionController>();
            if (_controller != null)
            {
                _cc = _controller.GetComponent<CharacterController>();
                _controller.OnLanded += OnLanded;
            }
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.OnLanded -= OnLanded;
        }

        /// <summary>
        /// Called by animation events named "OnFootstep".
        /// </summary>
        public void OnFootstep()
        {
            if (_library == null) return;
            if (Time.time - _lastStepTime < _minInterval) return;

            // Only play when grounded (skipped if no CharacterController — e.g. NPC).
            if (_cc != null && !_cc.isGrounded) return;

            PhysicsMaterial groundMat = _controller != null ? _controller.CurrentGroundMaterial : null;
            var surface = _library.Resolve(groundMat);
            if (surface == null) return;

            var clip = surface.GetRandomFootstep();
            if (clip == null) return;

            float volume = Random.Range(surface.VolumeMin, surface.VolumeMax);
            volume *= GetMovementVolumeScale();

            _audio.pitch = Random.Range(surface.PitchMin, surface.PitchMax);
            _audio.PlayOneShot(clip, volume);

            _lastStepTime = Time.time;
        }

        private void OnLanded(float impactNormalized)
        {
            if (_library == null || _controller == null) return;
            if (impactNormalized <= 0f) return;

            var surface = _library.Resolve(_controller.CurrentGroundMaterial);
            if (surface == null) return;

            var clip = surface.GetRandomLanding();
            if (clip == null) return;

            float volume = Mathf.Lerp(surface.VolumeMin, surface.VolumeMax, impactNormalized);
            volume *= _landingVolumeScale;

            _audio.pitch = Random.Range(surface.PitchMin, surface.PitchMax);
            _audio.PlayOneShot(clip, volume);

            _lastStepTime = Time.time;
        }

        private float GetMovementVolumeScale()
        {
            if (_controller == null) return 1f;

            return _controller.CurrentMovementState switch
            {
                MovementState.Crouching => _crouchVolumeScale,
                MovementState.Sprinting => _sprintVolumeScale,
                _ => 1f
            };
        }
    }
}
