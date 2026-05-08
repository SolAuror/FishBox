using Sol.AI;
using Sol.Fishing;
using Sol.Locomotion;
using UnityEngine;

namespace Sol.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BasicMeleeAttack))]
    [AddComponentMenu("Sol/Combat/Player Punch Combat")]
    [DefaultExecutionOrder(-2)]
    public class PlayerPunchCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LocomotionInput _input;
        [SerializeField] private LocomotionController _controller;
        [SerializeField] private FishingState _fishingState;
        [SerializeField] private Transform _originOverride;

        [Header("Punch")]
        [SerializeField] private float _damage = 10f;
        [SerializeField] private float _range = 1.6f;
        [SerializeField] private float _radius = 0.55f;
        [SerializeField] private float _cooldown = 0.45f;
        [SerializeField] private float _hitHeightOffset = 1f;
        [SerializeField] private LayerMask _targetLayers = ~0;

        private BasicMeleeAttack _basicMeleeAttack;

        private void Awake()
        {
            _basicMeleeAttack = GetComponent<BasicMeleeAttack>();
            if (_basicMeleeAttack == null)
                _basicMeleeAttack = gameObject.AddComponent<BasicMeleeAttack>();
            if (_input == null)
                _input = GetComponent<LocomotionInput>();

            if (_controller == null)
                _controller = GetComponent<LocomotionController>();

            if (_fishingState == null)
                _fishingState = GetComponent<FishingState>();
            SyncIntoBasicMelee();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                SyncIntoBasicMelee();
        }

        private void OnDrawGizmosSelected()
        {
            if (_basicMeleeAttack == null)
                _basicMeleeAttack = GetComponent<BasicMeleeAttack>();

            if (_basicMeleeAttack == null)
                return;
        }
#endif

        private void SyncIntoBasicMelee()
        {
            if (_basicMeleeAttack == null)
                _basicMeleeAttack = GetComponent<BasicMeleeAttack>();

            if (_basicMeleeAttack == null)
                return;

            // Keep legacy prefab values alive while routing runtime behavior through BasicMeleeAttack.
            SerializedConfig config = new SerializedConfig
            {
                Damage = _damage,
                Range = _range,
                Radius = _radius,
                Cooldown = _cooldown,
                HitHeightOffset = _hitHeightOffset,
                TargetLayers = _targetLayers,
                OriginOverride = _originOverride
            };

            _basicMeleeAttack.ApplyLegacyConfig(config);
        }

        public struct SerializedConfig
        {
            public float Damage;
            public float Range;
            public float Radius;
            public float Cooldown;
            public float HitHeightOffset;
            public LayerMask TargetLayers;
            public Transform OriginOverride;
        }
    }
}
