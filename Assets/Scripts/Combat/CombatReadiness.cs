using System.Collections.Generic;
using Sol.Grab;
using UnityEngine;

namespace Sol.Combat
{
    public enum CombatReadinessState
    {
        Relaxed = 0,
        Readying = 1,
        Ready = 2,
        Sheathing = 3
    }

    [AddComponentMenu("Sol/Combat/Combat Readiness")]
    [DisallowMultipleComponent]
    public class CombatReadiness : MonoBehaviour
    {
        [Header("Readiness")]
        [SerializeField] private CombatReadinessState _initialState = CombatReadinessState.Relaxed;
        [SerializeField, Min(0f)] private float _readyFallbackDuration = 0.35f;
        [SerializeField, Min(0f)] private float _sheatheFallbackDuration = 0.35f;

        [Header("Animator")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _combatReadyParameter = "combatReady";
        [SerializeField] private string _weaponKindParameter = "weaponKind";
        [SerializeField] private string _readyTriggerParameter = "readyTrigger";
        [SerializeField] private string _sheatheTriggerParameter = "sheatheTrigger";

        private readonly HashSet<int> _animatorParameters = new();
        private CombatReadinessState _state;
        private Combatant _combatant;
        private CombatWeaponPresentation _presentation;
        private Equipment _equipment;
        private float _pendingCompleteTime = -1f;
        private int _combatReadyHash;
        private int _weaponKindHash;
        private int _readyTriggerHash;
        private int _sheatheTriggerHash;

        public CombatReadinessState State => _state;
        public bool IsRelaxed => _state == CombatReadinessState.Relaxed;
        public bool IsReadying => _state == CombatReadinessState.Readying;
        public bool IsReady => _state == CombatReadinessState.Ready;
        public bool IsSheathing => _state == CombatReadinessState.Sheathing;
        public bool IsBusy => IsReadying || IsSheathing;
        public bool CanAttack => IsReady;
        public CombatAttackKind CurrentWeaponKind => ResolveWeaponKind();

        private void Awake()
        {
            ResolveReferences(addPresentation: true);
            CacheAnimatorParameters();
            _state = _initialState;
            ApplyPresentationForCurrentState();
            UpdateAnimatorState();
        }

        private void OnEnable()
        {
            ResolveReferences(addPresentation: true);
            if (_equipment != null)
                _equipment.OnChanged += HandleEquipmentChanged;
        }

        private void OnDisable()
        {
            if (_equipment != null)
                _equipment.OnChanged -= HandleEquipmentChanged;
        }

        private void Update()
        {
            if (_pendingCompleteTime >= 0f && Time.time >= _pendingCompleteTime)
            {
                if (_state == CombatReadinessState.Readying)
                    CompleteReady();
                else if (_state == CombatReadinessState.Sheathing)
                    CompleteSheathe();
            }

            UpdateAnimatorState();
        }

        public bool RequestReady()
        {
            ResolveReferences(addPresentation: true);

            if (_state == CombatReadinessState.Ready)
                return true;

            if (_state == CombatReadinessState.Readying)
                return false;

            BeginReadying();
            return _state == CombatReadinessState.Ready;
        }

        public bool RequestSheathe()
        {
            ResolveReferences(addPresentation: true);

            if (_state == CombatReadinessState.Relaxed)
                return true;

            if (_state == CombatReadinessState.Sheathing)
                return false;

            BeginSheathing();
            return _state == CombatReadinessState.Relaxed;
        }

        public bool ToggleReady()
        {
            return _state == CombatReadinessState.Ready || _state == CombatReadinessState.Readying
                ? RequestSheathe()
                : RequestReady();
        }

        public void OnCombatReadyComplete()
        {
            if (_state == CombatReadinessState.Readying)
                CompleteReady();
        }

        public void OnCombatSheatheComplete()
        {
            if (_state == CombatReadinessState.Sheathing)
                CompleteSheathe();
        }

        public static CombatReadiness ResolveOrAdd(GameObject actor)
        {
            if (actor == null)
                return null;

            CombatReadiness readiness = actor.GetComponent<CombatReadiness>();
            if (readiness == null)
                readiness = actor.AddComponent<CombatReadiness>();

            readiness.ResolveReferences(addPresentation: true);
            return readiness;
        }

        private void BeginReadying()
        {
            _state = CombatReadinessState.Readying;
            _pendingCompleteTime = Time.time + Mathf.Max(0f, _readyFallbackDuration);
            _presentation?.PresentUnsheathed();
            SetAnimatorTrigger(_readyTriggerHash);
            UpdateAnimatorState();

            if (_readyFallbackDuration <= 0f)
                CompleteReady();
        }

        private void BeginSheathing()
        {
            _state = CombatReadinessState.Sheathing;
            _pendingCompleteTime = Time.time + Mathf.Max(0f, _sheatheFallbackDuration);
            SetAnimatorTrigger(_sheatheTriggerHash);
            UpdateAnimatorState();

            if (_sheatheFallbackDuration <= 0f)
                CompleteSheathe();
        }

        private void CompleteReady()
        {
            _state = CombatReadinessState.Ready;
            _pendingCompleteTime = -1f;
            _presentation?.PresentUnsheathed();
            UpdateAnimatorState();
        }

        private void CompleteSheathe()
        {
            _state = CombatReadinessState.Relaxed;
            _pendingCompleteTime = -1f;
            _presentation?.PresentSheathed();
            UpdateAnimatorState();
        }

        private void ResolveReferences(bool addPresentation)
        {
            if (_combatant == null)
                _combatant = GetComponent<Combatant>();

            if (_equipment == null)
                _equipment = GetComponent<Equipment>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            if (_presentation == null)
            {
                _presentation = GetComponent<CombatWeaponPresentation>();
                if (_presentation == null && addPresentation)
                    _presentation = gameObject.AddComponent<CombatWeaponPresentation>();
            }
        }

        private void CacheAnimatorParameters()
        {
            _combatReadyHash = Animator.StringToHash(_combatReadyParameter);
            _weaponKindHash = Animator.StringToHash(_weaponKindParameter);
            _readyTriggerHash = Animator.StringToHash(_readyTriggerParameter);
            _sheatheTriggerHash = Animator.StringToHash(_sheatheTriggerParameter);

            _animatorParameters.Clear();
            if (_animator == null)
                return;

            foreach (AnimatorControllerParameter parameter in _animator.parameters)
                _animatorParameters.Add(parameter.nameHash);
        }

        private void UpdateAnimatorState()
        {
            if (_animator == null)
                return;

            if (_animatorParameters.Count == 0)
                CacheAnimatorParameters();

            if (_animatorParameters.Contains(_combatReadyHash))
                _animator.SetBool(_combatReadyHash, _state == CombatReadinessState.Ready || _state == CombatReadinessState.Readying);

            if (_animatorParameters.Contains(_weaponKindHash))
                _animator.SetInteger(_weaponKindHash, (int)CurrentWeaponKind);
        }

        private void SetAnimatorTrigger(int hash)
        {
            if (_animator == null)
                return;

            if (_animatorParameters.Count == 0)
                CacheAnimatorParameters();

            if (_animatorParameters.Contains(hash))
                _animator.SetTrigger(hash);
        }

        private CombatAttackKind ResolveWeaponKind()
        {
            ResolveReferences(addPresentation: false);
            if (_combatant == null)
                _combatant = Combatant.ResolveOrAdd(gameObject);

            if (_combatant != null && _combatant.TryGetEquippedWeapon(out ItemComponent weapon, out _))
                return _combatant.GetAttackKind(weapon);

            return CombatAttackKind.Unarmed;
        }

        private void ApplyPresentationForCurrentState()
        {
            if (_presentation == null)
                return;

            if (_state == CombatReadinessState.Ready || _state == CombatReadinessState.Readying)
                _presentation.PresentUnsheathed();
            else
                _presentation.PresentSheathed();
        }

        private void HandleEquipmentChanged()
        {
            ResolveReferences(addPresentation: true);
            ApplyPresentationForCurrentState();
            UpdateAnimatorState();
        }
    }
}
