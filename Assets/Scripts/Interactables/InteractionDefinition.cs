using UnityEngine;
using Sol.Rpg;

namespace Sol
{
    public enum InteractionOwnershipPolicy
    {
        Public = 0,
        OwnerOnly = 1,
        OwnerOrGuests = 2
    }

    public enum InteractionCompletionMode
    {
        Timed = 0,
        HoldUntilCancelled = 1,
        WaitForReadyThenTimed = 2,
        WaitForReadyThenExternalCompletion = 3
    }

    [CreateAssetMenu(menuName = "Sol/Interactions/Interaction Definition", fileName = "InteractionDefinition")]
    public sealed class InteractionDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _definitionId = "Interaction.New";
        [SerializeField] private string _prompt = "Use";
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private InteractionPointType _type = InteractionPointType.Utility;

        [Header("Actor Rules")]
        [SerializeField] private GameplayTagSet _tags = new();
        [SerializeField] private GameplayTagSet _allowedActorTags = new();
        [SerializeField] private GameplayTagSet _forbiddenActorTags = new();
        [SerializeField] private GameplayTagSet _requiredItemTags = new();
        [SerializeField] private GameplayTagSet _producedItemTags = new();
        [SerializeField] private bool _allowPlayer = true;
        [SerializeField] private bool _allowNPC = true;
        [SerializeField] private bool _singleOccupancy = true;
        [Min(0f)]
        [SerializeField] private float _maxInteractionRange;
        [Min(0.1f)]
        [SerializeField] private float _npcArrivalTolerance = 0.55f;

        [Header("Ownership")]
        [SerializeField] private InteractionOwnershipPolicy _ownershipPolicy = InteractionOwnershipPolicy.OwnerOnly;
        [SerializeField] private string _unauthorizedPrompt = "Owned";

        [Header("Animation")]
        [SerializeField] private InteractionPointAnimationType _animationType = InteractionPointAnimationType.None;
        [SerializeField] private AnimationClip _customClip;
        [SerializeField] private string _readyStatePath = string.Empty;
        [Range(0f, 1f)]
        [SerializeField] private float _readyNormalizedTime = 0.85f;

        [Header("Timing")]
        [SerializeField] private InteractionCompletionMode _completionMode = InteractionCompletionMode.Timed;
        [Min(0f)]
        [SerializeField] private float _useDuration = 2f;

        [Header("Locking")]
        [SerializeField] private bool _forceThirdPerson = true;
        [SerializeField] private bool _suppressCombat = true;
        [SerializeField] private bool _lockMovement = true;

        public string DefinitionId => string.IsNullOrWhiteSpace(_definitionId) ? name : _definitionId.Trim();
        public string Prompt => string.IsNullOrWhiteSpace(_prompt) ? "Use" : _prompt.Trim();
        public string DisplayName => _displayName?.Trim() ?? string.Empty;
        public InteractionPointType Type => _type;
        public GameplayTagSet Tags => _tags ?? GameplayTagSet.Empty;
        public GameplayTagSet AllowedActorTags => _allowedActorTags ?? GameplayTagSet.Empty;
        public GameplayTagSet ForbiddenActorTags => _forbiddenActorTags ?? GameplayTagSet.Empty;
        public GameplayTagSet RequiredItemTags => _requiredItemTags ?? GameplayTagSet.Empty;
        public GameplayTagSet ProducedItemTags => _producedItemTags ?? GameplayTagSet.Empty;
        public bool AllowPlayer => _allowPlayer;
        public bool AllowNPC => _allowNPC;
        public bool SingleOccupancy => _singleOccupancy;
        public float MaxInteractionRange => Mathf.Max(0f, _maxInteractionRange);
        public float NpcArrivalTolerance => Mathf.Max(0.1f, _npcArrivalTolerance);
        public InteractionOwnershipPolicy OwnershipPolicy => _ownershipPolicy;
        public string UnauthorizedPrompt => string.IsNullOrWhiteSpace(_unauthorizedPrompt) ? "Owned" : _unauthorizedPrompt.Trim();
        public InteractionPointAnimationType AnimationType => _animationType;
        public AnimationClip CustomClip => _customClip;
        public string ReadyStatePath => _readyStatePath?.Trim() ?? string.Empty;
        public float ReadyNormalizedTime => Mathf.Clamp01(_readyNormalizedTime);
        public InteractionCompletionMode CompletionMode => _completionMode;
        public float UseDuration => Mathf.Max(0f, _useDuration);
        public bool ForceThirdPerson => _forceThirdPerson;
        public bool SuppressCombat => _suppressCombat;
        public bool LockMovement => _lockMovement;

        private void OnValidate()
        {
            _definitionId = string.IsNullOrWhiteSpace(_definitionId) ? name : _definitionId.Trim();
            _prompt = string.IsNullOrWhiteSpace(_prompt) ? "Use" : _prompt.Trim();
            _displayName = _displayName?.Trim() ?? string.Empty;
            _tags ??= new GameplayTagSet();
            _tags.Normalize();
            _allowedActorTags ??= new GameplayTagSet();
            _allowedActorTags.Normalize();
            _forbiddenActorTags ??= new GameplayTagSet();
            _forbiddenActorTags.Normalize();
            _requiredItemTags ??= new GameplayTagSet();
            _requiredItemTags.Normalize();
            _producedItemTags ??= new GameplayTagSet();
            _producedItemTags.Normalize();
            _unauthorizedPrompt = string.IsNullOrWhiteSpace(_unauthorizedPrompt) ? "Owned" : _unauthorizedPrompt.Trim();
            _maxInteractionRange = Mathf.Max(0f, _maxInteractionRange);
            _npcArrivalTolerance = Mathf.Max(0.1f, _npcArrivalTolerance);
            _readyNormalizedTime = Mathf.Clamp01(_readyNormalizedTime);
            _useDuration = Mathf.Max(0f, _useDuration);
        }
    }
}
