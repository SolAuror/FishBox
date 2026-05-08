using System.Collections.Generic;
using Sol.AI;
using Sol.Player;
using UnityEngine;

namespace Sol.Combat
{
    [AddComponentMenu("Sol/Combat/Basic Melee Attack")]
    [DisallowMultipleComponent]
    public class BasicMeleeAttack : MonoBehaviour
    {
        [Header("Melee")]
        [SerializeField] private float _damage = 10f;
        [SerializeField] private float _range = 1.6f;
        [SerializeField] private float _radius = 0.55f;
        [SerializeField] private float _cooldown = 0.45f;
        [SerializeField] private float _minForwardDot = 0.2f;
        [SerializeField] private float _hitHeightOffset = 1f;
        [SerializeField] private LayerMask _targetLayers = ~0;
        [SerializeField] private Transform _originOverride;

        private readonly Collider[] _hits = new Collider[24];
        private readonly HashSet<int> _hitTargetsThisSwing = new();
        private float _nextAttackTime;
        private bool _attackActive;

        private PlayerSoul _playerSoul;
        private NPCSoul _npcSoul;
        private Combatant _combatant;
        private Transform _cachedTransform;

        public float EngageDistance => Mathf.Max(0.1f, _range + _radius);

        private void Awake()
        {
            _cachedTransform = transform;
            RefreshSoulReferences();
            _combatant = Combatant.ResolveOrAdd(gameObject);
        }

        public bool CanStartAttack()
        {
            RefreshSoulReferences();

            if (Time.time < _nextAttackTime)
                return false;

            if (_playerSoul != null)
            {
                if (!_playerSoul.IsAlive)
                    return false;
            }
            else if (_npcSoul != null && !_npcSoul.IsAlive)
            {
                return false;
            }

            Combatant combatant = ResolveCombatant();
            if (combatant == null)
                return true;

            combatant.TryGetEquippedWeapon(out Sol.Grab.ItemComponent weapon, out _);
            float staminaCost = combatant.GetAttackStaminaCost(weapon);
            return combatant.CanSpendStamina(staminaCost);
        }

        public void BeginAttack()
        {
            TryBeginAttack();
        }

        public bool TryBeginAttack()
        {
            RefreshSoulReferences();

            if (!CanStartAttack())
            {
                Combatant rejectedCombatant = ResolveCombatant();
                CombatResolver.RaiseAttackRejected(new CombatHit(
                    rejectedCombatant,
                    null,
                    null,
                    _damage,
                    _damage,
                    0f,
                    CombatAttackKind.Unarmed,
                    _cachedTransform != null ? _cachedTransform.forward : transform.forward));
                return false;
            }

            Combatant combatant = ResolveCombatant();
            if (combatant != null)
            {
                combatant.TryGetEquippedWeapon(out Sol.Grab.ItemComponent weapon, out _);
                float staminaCost = combatant.GetAttackStaminaCost(weapon);
                if (!combatant.TrySpendStamina(staminaCost))
                {
                    CombatResolver.RaiseAttackRejected(new CombatHit(
                        combatant,
                        null,
                        weapon,
                        combatant.GetAttackBaseDamage(weapon, _damage),
                        _damage,
                        staminaCost,
                        combatant.GetAttackKind(weapon),
                        _cachedTransform != null ? _cachedTransform.forward : transform.forward));
                    return false;
                }

                CombatResolver.RaiseAttackStarted(new CombatHit(
                    combatant,
                    null,
                    weapon,
                    combatant.GetAttackBaseDamage(weapon, _damage),
                    _damage,
                    staminaCost,
                    combatant.GetAttackKind(weapon),
                    _cachedTransform != null ? _cachedTransform.forward : transform.forward));
            }

            _nextAttackTime = Time.time + Mathf.Max(0.01f, _cooldown);
            _hitTargetsThisSwing.Clear();
            _attackActive = true;
            return true;
        }

        public void ResolveHitFrame()
        {
            RefreshSoulReferences();

            if (!_attackActive)
                return;

            Transform origin = _originOverride != null ? _originOverride : _cachedTransform;
            Vector3 originPosition = origin.position;
            Vector3 forward = origin.forward;

            int hitCount = Physics.OverlapCapsuleNonAlloc(
                originPosition,
                originPosition + forward * _range,
                _radius,
                _hits,
                _targetLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _hits[i];
                _hits[i] = null;
                if (hit == null)
                    continue;

                Transform hitTransform = hit.transform;
                if (hitTransform == null || hitTransform.IsChildOf(_cachedTransform))
                    continue;

                if (TryDamageTarget(hitTransform, originPosition, forward))
                    continue;
            }
        }

        public void EndAttack()
        {
            _attackActive = false;
            _hitTargetsThisSwing.Clear();
        }

        private bool TryDamageTarget(Transform targetTransform, Vector3 originPosition, Vector3 forward)
        {
            RefreshSoulReferences();

            if (_playerSoul != null)
            {
                NPCSoul targetNpcSoul = targetTransform.GetComponentInParent<NPCSoul>();
                if (targetNpcSoul == null || !targetNpcSoul.IsAlive || !targetNpcSoul.IsHostile)
                    return false;

                if (!IsInForwardArc(targetNpcSoul.transform.position, originPosition, forward))
                    return false;

                if (!TryMarkHit(targetNpcSoul.GetInstanceID()))
                    return false;

                Combatant attacker = ResolveCombatant();
                Combatant target = Combatant.ResolveOrAdd(targetNpcSoul.transform);
                if (attacker == null || target == null)
                    return false;

                Vector3 hitDirection = ResolveHitDirection(targetNpcSoul.transform, forward);
                CombatResolver.ApplyHit(attacker.CreateMeleeHit(target, _damage, hitDirection));
                return true;
            }

            if (_npcSoul != null && _npcSoul.IsHostile)
            {
                PlayerSoul targetPlayerSoul = targetTransform.GetComponentInParent<PlayerSoul>();
                if (targetPlayerSoul == null || !targetPlayerSoul.IsAlive)
                    return false;

                if (!IsInForwardArc(targetPlayerSoul.transform.position, originPosition, forward))
                    return false;

                if (!TryMarkHit(targetPlayerSoul.GetInstanceID()))
                    return false;

                Combatant attacker = ResolveCombatant();
                Combatant target = Combatant.ResolveOrAdd(targetPlayerSoul.transform);
                if (attacker == null || target == null)
                    return false;

                Vector3 hitDirection = ResolveHitDirection(targetPlayerSoul.transform, forward);
                CombatResolver.ApplyHit(attacker.CreateMeleeHit(target, _damage, hitDirection));
                return true;
            }

            return false;
        }

        private bool TryMarkHit(int id)
        {
            if (_hitTargetsThisSwing.Contains(id))
                return false;

            _hitTargetsThisSwing.Add(id);
            return true;
        }

        private bool IsInForwardArc(Vector3 targetPosition, Vector3 originPosition, Vector3 forward)
        {
            Vector3 targetPoint = targetPosition + Vector3.up * _hitHeightOffset;
            Vector3 toTarget = targetPoint - originPosition;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return true;

            return Vector3.Dot(forward, toTarget.normalized) >= _minForwardDot;
        }

        private Combatant ResolveCombatant()
        {
            if (_combatant == null)
                _combatant = Combatant.ResolveOrAdd(gameObject);

            return _combatant;
        }

        private void RefreshSoulReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;
            if (_playerSoul == null)
                _playerSoul = GetComponent<PlayerSoul>();
            if (_npcSoul == null)
                _npcSoul = GetComponent<NPCSoul>();
        }

        private Vector3 ResolveHitDirection(Transform target, Vector3 fallbackForward)
        {
            if (target == null)
                return fallbackForward;

            Vector3 hitDirection = target.position - _cachedTransform.position;
            return hitDirection.sqrMagnitude > 0.0001f
                ? hitDirection.normalized
                : fallbackForward;
        }

        public void ApplyLegacyConfig(PlayerPunchCombat.SerializedConfig config)
        {
            _damage = config.Damage;
            _range = config.Range;
            _radius = config.Radius;
            _cooldown = config.Cooldown;
            _hitHeightOffset = config.HitHeightOffset;
            _targetLayers = config.TargetLayers;
            _originOverride = config.OriginOverride;
        }
    }
}
