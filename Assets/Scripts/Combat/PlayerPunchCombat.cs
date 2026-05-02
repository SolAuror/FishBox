using Sol.AI;
using Sol.Fishing;
using Sol.Locomotion;
using UnityEngine;

namespace Sol.Combat
{
    [AddComponentMenu("Sol/Combat/Player Punch Combat")]
    [DefaultExecutionOrder(-1)]
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

        private readonly Collider[] _hits = new Collider[16];
        private float _nextPunchTime;

        private void Awake()
        {
            if (_input == null)
                _input = GetComponent<LocomotionInput>();

            if (_controller == null)
                _controller = GetComponent<LocomotionController>();

            if (_fishingState == null)
                _fishingState = GetComponent<FishingState>();
        }

        private void Update()
        {
            if (_input == null || !_input.AttackPressed)
                return;

            if (Time.time < _nextPunchTime)
                return;

            if (_fishingState != null && _fishingState.ShouldBlockDefaultAttack)
                return;

            _nextPunchTime = Time.time + Mathf.Max(0.01f, _cooldown);
            TryPunch();
        }

        private void TryPunch()
        {
            Transform origin = ResolveOrigin();
            Vector3 originPosition = origin.position;
            Vector3 forward = origin.forward;
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                originPosition,
                originPosition + forward * _range,
                _radius,
                _hits,
                _targetLayers,
                QueryTriggerInteraction.Ignore);

            NPCSoul target = FindBestTarget(hitCount, originPosition, forward);
            if (target == null)
                return;

            target.TakeDamage(_damage);

            if (!target.IsAlive)
                return;

            Vector3 hitDirection = target.transform.position - transform.position;
            if (hitDirection.sqrMagnitude < 0.001f)
                hitDirection = forward;

            HitReaction hitReaction = target.GetComponent<HitReaction>();
            if (hitReaction == null)
                hitReaction = target.GetComponentInChildren<HitReaction>();

            if (hitReaction != null)
                hitReaction.PlayHitReaction(hitDirection.normalized);
        }

        private NPCSoul FindBestTarget(int hitCount, Vector3 originPosition, Vector3 forward)
        {
            NPCSoul bestTarget = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _hits[i];
                _hits[i] = null;

                if (hit == null || hit.transform.IsChildOf(transform))
                    continue;

                NPCSoul soul = hit.GetComponentInParent<NPCSoul>();
                if (soul == null || !soul.IsAlive)
                    continue;

                Vector3 targetPoint = soul.transform.position + Vector3.up * _hitHeightOffset;
                Vector3 toTarget = targetPoint - originPosition;
                if (Vector3.Dot(forward, toTarget.normalized) < 0.2f)
                    continue;

                float score = toTarget.sqrMagnitude;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestTarget = soul;
            }

            return bestTarget;
        }

        private Transform ResolveOrigin()
        {
            if (_originOverride != null)
                return _originOverride;

            if (_controller != null && _controller.CamTransform != null)
                return _controller.CamTransform;

            return transform;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform origin = _originOverride != null
                ? _originOverride
                : _controller != null && _controller.CamTransform != null
                    ? _controller.CamTransform
                    : transform;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(origin.position, _radius);
            Gizmos.DrawWireSphere(origin.position + origin.forward * _range, _radius);
            Gizmos.DrawLine(origin.position, origin.position + origin.forward * _range);
        }
#endif
    }
}
