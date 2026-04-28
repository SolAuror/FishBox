using UnityEngine;
using Sol.Player;
using Sol.Outline;
using Sol;
using Sol.Grab;
using Sol.Audio;
using Sol.Fishing;
using Sol.AI;

namespace Sol.AI
{
    public enum FishRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Legendary = 3,
        Mythical = 4
    }

    /// <summary>
    /// Simple ambient fish behaviour. Fish wander inside a water volume and
    /// perform short flee bursts when a nearby player is moving through the water.
    /// </summary>
    [DisallowMultipleComponent]
    public class AI_Fish : MonoBehaviour
    {
        #region Inspector Settings
        [Header("Definition")]
        [Tooltip("Inspector: tunes definition.")]
        [SerializeField] private FishDefinition _definition;

        [Header("Fish Identity")]
        [Tooltip("Inspector: tunes fish name.")]
        [SerializeField] private string _fishName = "Fish";
        [SerializeField] private FishRarity _rarity = FishRarity.Common;
        [Tooltip("Inspector: tunes base value.")]
        [SerializeField] private int _baseValue = 10;
        [Tooltip("Inspector: tunes catch item prefab.")]
        [SerializeField] private GameObject _catchItemPrefab;

        [Header("Fish Stats")]
        [Tooltip("Inspector: tunes min size.")]
        [SerializeField] private float _minSize = 0.5f;
        [SerializeField] private float _maxSize = 1.5f;
        [Tooltip("Inspector: tunes min weight.")]
        [SerializeField] private float _minWeight = 0.2f;
        [SerializeField] private float _maxWeight = 5f;
        [Tooltip("Inspector: tunes catch difficulty.")]
        [SerializeField] private float _catchDifficulty = 1f;

        [Header("Visuals")]
        [Tooltip("Inspector: tunes model root.")]
        [SerializeField] private Transform _modelRoot;
        [Tooltip("Inspector: tunes possible models.")]
        [SerializeField] private GameObject[] _possibleModels;
        [SerializeField, Min(0.01f)] private float _modelImportScaleCompensation = 1f;

        [Header("Outline")]
        [Tooltip("Inspector: tunes common outline color.")]
        [SerializeField] private Color _commonOutlineColor = new(0.7f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color _uncommonOutlineColor = new(0.45f, 1f, 0.45f, 1f);
        [Tooltip("Inspector: tunes rare outline color.")]
        [SerializeField] private Color _rareOutlineColor = new(0.35f, 0.7f, 1f, 1f);
        [SerializeField] private Color _legendaryOutlineColor = new(1f, 0.75f, 0.2f, 1f);
        [Tooltip("Inspector: tunes mythical outline color.")]
        [SerializeField] private Color _mythicalOutlineColor = new(1f, 0.35f, 0.95f, 1f);

        [Header("Prefixes")]
        [SerializeField, Range(0.01f, 0.25f)]
        private float _sizeOutlierThreshold = 0.1f;

        [Tooltip("Inspector: tunes small fish prefix.")]
        [SerializeField] private string[] _smallFishPrefixes =
        {
            "Scrap",
            "Fryling",
            "Tidebit",
            "Nibbler",
            "Dockling"
        };

        [Tooltip("Inspector: tunes large fish prefix.")]
        [SerializeField] private string[] _largeFishPrefixes =
        {
            "Leviathan",
            "Deepborn",
            "Hullbreaker",
            "Tidemaw",
            "Old One"
        };

        [Tooltip("Inspector: tunes lightweight fish prefix.")]
        [SerializeField] private string[] _lightFishPrefixes =
        {
            "Hollow",
            "Drift",
            "Bone-light",
            "Windried",
            "Thinwater"
        };

        [Tooltip("Inspector: tunes heavy fish prefix.")]
        [SerializeField] private string[] _heavyFishPrefixes =
        {
            "Brinefat",
            "Ironbelly",
            "Anchorweight",
            "Barnacle-back",
            "Mudswollen"
        };

        [Tooltip("Inspector: prefixes for fish that are double outliers (size + weight mismatch).")]
        [SerializeField] private string[] _outlierFishPrefixes =
        {
            "Odd",
            "Offsize",
            "Misfit",
            "Wrongbuilt",
            "Bent",
            "Roughcut",
            "Scrapgrown",
            "Patchwork",
            "Junk-bred",
            "Outcast"
        };

        [Header("Debug")]
        [Tooltip("Inspector: tunes show debug label.")]
        [SerializeField] private bool _showDebugLabel;

        [Header("Lure Interest")]
        [SerializeField, Range(0f, 1f)] private float _baseLureInterestChance = 0.35f;
        [Tooltip("Inspector: tunes interest depth offset.")]
        [SerializeField] private float _interestDepthOffset = 0.35f;
        [SerializeField] private float _interestResolveInterval = 0.35f;
        [Tooltip("Inspector: tunes interest lose distance multiplier.")]
        [SerializeField] private float _interestLoseDistanceMultiplier = 1.5f;
        [SerializeField] private float _interestSwimSpeedMultiplier = 1.2f;
        [Tooltip("Inspector: tunes interest retarget min time.")]
        [SerializeField] private float _interestRetargetMinTime = 0.25f;
        [Tooltip("Inspector: tunes interest retarget max time.")]
        [SerializeField] private float _interestRetargetMaxTime = 0.65f;
        [SerializeField, Min(1f)] private float _maxInterestDuration = 60f;
        [Tooltip("Inspector: tunes interest retry delay range.")]
        [SerializeField] private Vector2 _interestRetryDelayRange = new(4f, 7f);
        [SerializeField, Min(1f)] private float _maxCommitTravelDistance = 50f;
        [SerializeField, Range(0.05f, 1f)] private float _baseBiteChance = 0.9f;
        [Tooltip("Inspector: tunes bite retry delay range.")]
        [SerializeField] private Vector2 _biteRetryDelayRange = new(3f, 5f);
        [Tooltip("Inspector: tunes max hook-set window (seconds) on the easiest fish.")]
        [SerializeField, Min(0.25f)] private float _hookSetWindowMax = 1.2f;
        [Tooltip("Inspector: tunes min hook-set window (seconds) on the hardest fish.")]
        [SerializeField, Min(0.1f)] private float _hookSetWindowMin = 0.4f;

        [Header("Movement")]
        [Tooltip("Inspector: tunes swim speed.")]
        [SerializeField] private float _swimSpeed = 1.5f;
        [SerializeField] private float _fleeSpeed = 3f;
        [Tooltip("Inspector: tunes turn speed.")]
        [SerializeField] private float _turnSpeed = 3f;

        [Header("Wander")]
        [Tooltip("Inspector: tunes wander radius.")]
        [SerializeField] private float _wanderRadius = 5f;
        [SerializeField] private float _wanderRetargetMinTime = 2f;
        [Tooltip("Inspector: tunes wander retarget max time.")]
        [SerializeField] private float _wanderRetargetMaxTime = 5f;

        [Header("Player Avoidance")]
        [Tooltip("Inspector: tunes player.")]
        [SerializeField] private Transform _player;
        [SerializeField] private float _fleeRadius = 6f;
        [Tooltip("Inspector: tunes flee distance.")]
        [SerializeField] private float _fleeDistance = 4f;
        [SerializeField] private float _playerMovementThreshold = 0.2f;
        [Tooltip("Inspector: tunes player prediction time.")]
        [SerializeField] private float _playerPredictionTime = 0.35f;
        [SerializeField] private float _fleeRetargetMinTime = 0.35f;
        [Tooltip("Inspector: tunes flee retarget max time.")]
        [SerializeField] private float _fleeRetargetMaxTime = 1f;

        [Header("Water Bounds")]
        [Tooltip("Inspector: tunes water volume.")]
        [SerializeField] private WaterVolume _waterVolume;
        [SerializeField] private float _edgePadding = 0.75f;
        [Tooltip("Inspector: tunes surface padding.")]
        [SerializeField] private float _surfacePadding = 0.6f;
        [Tooltip("Inspector: tunes bottom padding.")]
        [SerializeField] private float _bottomPadding = 0.5f;
        #endregion

        private Vector3 _origin;
        private Vector3 _target;
        private Vector3 _playerVelocity;
        private Vector3 _previousPlayerPosition;
        private float _retargetTimer;
        private float _nextContextResolveTime;
        private bool _hasPlayerPosition;
        private bool _isFleeing;
        private BoxCollider _waterCollider;
        private GameObject _activeModelInstance;
        private Vector3 _initialModelRootScale = Vector3.one;
        private bool _hasInitialModelRootScale;
        private bool _hasStarted;
        private float _spawnChanceNormalized = 1f;
        private float _rarestSpeciesChanceNormalized = 1f;
        private float _mostCommonSpeciesChanceNormalized = 1f;
        private float _nextInterestResolveTime;
        private float _interestStartTime = -1f;
        private float _nextInterestAllowedTime;
        private float _nextBiteAttemptTime;

        private float _size;
        private float _weight;
        private float _rarityPercent;
        private bool _isCaught;
        private string _prefix = string.Empty;
        private FishingLureInstance _interestLure;
        private FishingLureInstance _hookedLure;
        private float _fightStamina = 1f;
        private float _nextStruggleTime;
        private float _struggleEndTime;
        private bool _isStruggling;
        private Vector3 _struggleDirection;
        private float _currentStruggleIntensity;
        private Vector3 _hookedPullDirection;

        // --- Public accessors ---
        public FishDefinition Definition => _definition;
        public string SpeciesName => _fishName;
        public string Prefix => _prefix;
        public string FishName => string.IsNullOrWhiteSpace(_prefix) ? _fishName : $"{_prefix} {_fishName}";
        public FishRarity Rarity => _rarity;
        public float RarityPercent => _rarityPercent;
        public bool IsPrefixed => !string.IsNullOrWhiteSpace(_prefix);
        public bool IsPredator => _definition != null && _definition.isPredator;
        public float SpawnChancePercent => _spawnChanceNormalized * 100f;
        public float Size => _size;
        public float Weight => _weight;
        public int Value => Mathf.RoundToInt(_baseValue * Mathf.Max(_size, 0.1f) * GetRarityValueMultiplier());
        public float CatchDifficulty => (_catchDifficulty * (1f + _weight * 0.1f)) / GetFavoriteBaitCatchMultiplier();
        public bool IsCaught => _isCaught;
        public bool IsHooked => _hookedLure != null;
        public GameObject CatchItemPrefab => _catchItemPrefab;
        public GameObject CatchVisualPrefab => _definition != null && _definition.modelPrefab != null
            ? _definition.modelPrefab
            : (_possibleModels != null && _possibleModels.Length > 0 ? _possibleModels[0] : null);
        public Vector3 CatchVisualLocalScale => _modelRoot != null ? _modelRoot.localScale : Vector3.one;

        private void Awake()
        {
            if (_modelRoot != null)
            {
                _initialModelRootScale = _modelRoot.localScale;
                _hasInitialModelRootScale = true;
            }

            ApplyDefinitionToRuntimeFields();
        }

        private void Start()
        {
            _hasStarted = true;
            InitializeFish();
        }

        public void ApplyDefinition(
            FishDefinition definition,
            float spawnChanceNormalized = 1f,
            float rarestSpeciesChanceNormalized = 1f,
            float mostCommonSpeciesChanceNormalized = 1f)
        {
            _definition = definition;
            _spawnChanceNormalized = Mathf.Clamp01(spawnChanceNormalized);
            _rarestSpeciesChanceNormalized = Mathf.Clamp01(rarestSpeciesChanceNormalized);
            _mostCommonSpeciesChanceNormalized = Mathf.Clamp01(mostCommonSpeciesChanceNormalized);
            ApplyDefinitionToRuntimeFields();

            if (!_hasStarted)
                return;

            InitializeFish();
        }

        private void Update()
        {
            if (_hookedLure != null)
            {
                UpdateHookedFight();
                return;
            }

            if (Time.time >= _nextContextResolveTime)
                ResolveContext(force: false);

            UpdatePlayerVelocity();

            bool shouldFlee = ShouldFleeFromPlayer();
            FishingLureInstance interestLure = shouldFlee ? null : ResolveInterestLure();
            _retargetTimer -= Time.deltaTime;

            if (interestLure != null)
            {
                if (HasInterestTimedOut())
                {
                    AbandonInterest();
                    PickNewTarget(shouldFlee: false);
                    MoveTowardsTarget(_swimSpeed);
                    return;
                }

                _isFleeing = false;
                _interestLure = interestLure;

                Vector3 biteTarget = interestLure.GetFishInterestPoint(_interestDepthOffset);
                float biteDistance = Mathf.Max(0.3f, _size * 0.35f);
                if ((biteTarget - transform.position).sqrMagnitude <= biteDistance * biteDistance)
                {
                    interestLure.NotifyFishAtBait();
                    HoldAtBitePoint(biteTarget);
                    if (TryBiteLure(interestLure))
                        return;

                    return;
                }

                if (_retargetTimer <= 0f || HasReachedTarget() || !IsCurrentInterestLureValid(_interestLure))
                {
                    _target = GetInterestTarget(_interestLure);
                    _retargetTimer = Random.Range(_interestRetargetMinTime, _interestRetargetMaxTime);
                }

                MoveTowardsTarget(_swimSpeed * _interestSwimSpeedMultiplier);
                return;
            }

            ClearInterestLure();
            if (shouldFlee != _isFleeing || _retargetTimer <= 0f || HasReachedTarget())
                PickNewTarget(shouldFlee);

            MoveTowardsTarget(shouldFlee ? _fleeSpeed : _swimSpeed);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!_showDebugLabel || !Application.isPlaying)
                return;

            Camera cameraToUse = Camera.main;
            if (cameraToUse == null)
                return;

            Vector3 worldPosition = transform.position + Vector3.up * 0.35f;
            Vector3 screenPosition = cameraToUse.WorldToScreenPoint(worldPosition);
            if (screenPosition.z <= 0f)
                return;

            const float width = 210f;
            const float height = 54f;
            Rect labelRect = new(
                screenPosition.x - (width * 0.5f),
                Screen.height - screenPosition.y - height,
                width,
                height);

            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            GUI.Box(labelRect, GUIContent.none);
            GUI.color = Color.white;

            string label =
                $"{FishName}\n" +
                $"{_rarity} {RarityPercent:0.##}% | Spawn {SpawnChancePercent:0.##}%\n" +
                $"Value {Value}" +
                (_interestLure != null ? "\nInterested" : string.Empty);

            GUI.Label(labelRect, label);
        }
#endif

        private void ResolveContext(bool force)
        {
            if (!force && Time.time < _nextContextResolveTime)
                return;

            if (_waterVolume == null || !_waterVolume.isActiveAndEnabled)
            {
                _waterVolume = WaterVolume.FindVolumeXZ(transform.position)
                             ?? WaterVolume.FindVolumeXZ(_origin);
                CacheWaterCollider();
            }

            if (_player == null)
            {
                PlayerSoul playerSoul = FindFirstObjectByType<PlayerSoul>();
                if (playerSoul != null)
                    _player = playerSoul.transform;
                else
                {
                    try
                    {
                        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                        if (taggedPlayer != null)
                            _player = taggedPlayer.transform;
                    }
                    catch (UnityException)
                    {
                        Debug.LogWarning("[AI_Fish] 'Player' tag is not registered in the Tag Manager.");
                    }
                }

                if (_player != null)
                {
                    _previousPlayerPosition = _player.position;
                    _hasPlayerPosition = true;
                }
            }

            _nextContextResolveTime = Time.time + 1f;
        }

        private void SelectModelVariant()
        {
            if (_possibleModels == null || _possibleModels.Length == 0)
                return;

            Transform parent = _modelRoot != null ? _modelRoot : transform;
            int chosenIndex = Random.Range(0, _possibleModels.Length);

            for (int i = 0; i < _possibleModels.Length; i++)
            {
                GameObject model = _possibleModels[i];
                if (model == null || !model.scene.IsValid() || !model.transform.IsChildOf(transform))
                    continue;

                model.SetActive(i == chosenIndex);
            }

            GameObject chosenModel = _possibleModels[chosenIndex];
            if (chosenModel == null)
                return;

            if (chosenModel.scene.IsValid() && chosenModel.transform.IsChildOf(transform))
                return;

            if (_activeModelInstance != null)
                Destroy(_activeModelInstance);

            _activeModelInstance = Instantiate(chosenModel, parent);
            _activeModelInstance.transform.localPosition = Vector3.zero;
            _activeModelInstance.transform.localRotation = Quaternion.identity;
            _activeModelInstance.transform.localScale = Vector3.one;
        }

        private void InitializeFish()
        {
            float roll = Random.value;
            float expectedSize = Mathf.Max(0.01f, (_minSize + _maxSize) * 0.5f);
            float minS = Mathf.Min(_minSize, _maxSize);
            float maxS = Mathf.Max(_minSize, _maxSize);
            _size = Mathf.Lerp(minS, maxS, roll);

            float sizeMultiplier = Mathf.Clamp(_size / expectedSize, 0.5f, 1.5f);
            float expectedWeight = Mathf.Lerp(_minWeight, _maxWeight, roll);
            _weight = Mathf.Max(0.01f, expectedWeight * sizeMultiplier);
            _prefix = GetPrefixForCurrentRoll();
            _rarityPercent = CalculateRarityPercent();
            _rarity = GetRarityTierFromPercent(_rarityPercent);

            ApplyModelScale();
            SelectModelVariant();
            ApplyOutlineColor();
            ResolveContext(force: true);
            transform.position = ClampToWater(transform.position);
            _origin = transform.position;
            PickNewTarget(ShouldFleeFromPlayer());
        }

        private void ApplyDefinitionToRuntimeFields()
        {
            if (_definition == null)
                return;

            _fishName = _definition.fishName;
            _rarity = _definition.rarity;
            _baseValue = _definition.baseValue;
            _catchItemPrefab = _definition.catchItemPrefab;
            _minSize = _definition.minSize;
            _maxSize = _definition.maxSize;
            _minWeight = _definition.minWeight;
            _maxWeight = _definition.maxWeight;
            _catchDifficulty = _definition.catchDifficulty;

            if (_definition.modelPrefab != null)
                _possibleModels = new[] { _definition.modelPrefab };
        }

        private void ApplyModelScale()
        {
            if (_modelRoot == null)
                return;

            Vector3 baseScale = _hasInitialModelRootScale ? _initialModelRootScale : _modelRoot.localScale;
            float scaleMultiplier = _size * Mathf.Max(0.01f, _modelImportScaleCompensation);

            if (_definition != null)
                scaleMultiplier *= Mathf.Max(0.01f, _definition.modelScaleMultiplier);

            _modelRoot.localScale = baseScale * scaleMultiplier;
        }

        private string GetRandomPrefix(string[] prefixes)
        {
            if (prefixes == null || prefixes.Length == 0)
                return string.Empty;

            return prefixes[UnityEngine.Random.Range(0, prefixes.Length)];
        }

        private static string CombineComboPrefixes(string firstPrefix, string secondPrefix)
        {
            firstPrefix = firstPrefix?.Trim() ?? string.Empty;
            secondPrefix = secondPrefix?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(firstPrefix))
                return secondPrefix;

            if (string.IsNullOrWhiteSpace(secondPrefix))
                return firstPrefix;

            return $"{firstPrefix} {secondPrefix}";
        }

        private string GetPrefixForCurrentRoll()
        {
            if (_maxSize <= _minSize || _maxWeight <= _minWeight)
                return string.Empty;

            float sizePercent = Mathf.InverseLerp(_minSize, _maxSize, _size);
            float weightPercent = Mathf.InverseLerp(_minWeight, _maxWeight, _weight);

            float upperThreshold = 1f - _sizeOutlierThreshold;

            bool isSizeOutlier = sizePercent <= _sizeOutlierThreshold || sizePercent >= upperThreshold;
            bool isWeightOutlier = weightPercent <= _sizeOutlierThreshold || weightPercent >= upperThreshold;

            // Strange combos: small+heavy OR large+light
            if ((sizePercent <= _sizeOutlierThreshold && weightPercent >= upperThreshold) ||
                (sizePercent >= upperThreshold && weightPercent <= _sizeOutlierThreshold))
            {
                return GetRandomPrefix(_outlierFishPrefixes);
            }

            // Normal combos only if BOTH dimensions are outliers
            if (!isSizeOutlier || !isWeightOutlier)
                return string.Empty;

            // small+light combo
            if (sizePercent <= _sizeOutlierThreshold && weightPercent <= _sizeOutlierThreshold)
            {
                string sizePrefix = GetRandomPrefix(_smallFishPrefixes);
                string weightPrefix = GetRandomPrefix(_lightFishPrefixes);
                return CombineComboPrefixes(sizePrefix, weightPrefix);
            }

            // large+heavy combo
            if (sizePercent >= upperThreshold && weightPercent >= upperThreshold)
            {
                string sizePrefix = GetRandomPrefix(_largeFishPrefixes);
                string weightPrefix = GetRandomPrefix(_heavyFishPrefixes);
                return CombineComboPrefixes(sizePrefix, weightPrefix);
            }

            return string.Empty;
        }

        private float CalculateRarityPercent()
        {
            float outlierChance = Mathf.Clamp(_sizeOutlierThreshold, 0.01f, 0.49f);
            float normalChance = Mathf.Max(0.01f, 1f - (outlierChance * 2f));
            float prefixChance = IsPrefixed ? outlierChance : normalChance;

            float speciesChance = Mathf.Max(0.0001f, _spawnChanceNormalized);
            float rarestSpeciesChance = Mathf.Max(0.0001f, _rarestSpeciesChanceNormalized);
            float mostCommonSpeciesChance = Mathf.Max(rarestSpeciesChance, _mostCommonSpeciesChanceNormalized);

            float occurrenceChance = speciesChance * prefixChance;
            float rarestOccurrenceChance = rarestSpeciesChance * outlierChance;
            float mostCommonOccurrenceChance = mostCommonSpeciesChance * normalChance;

            if (IsPrefixed && Mathf.Approximately(speciesChance, rarestSpeciesChance))
                return 101f;

            if (mostCommonOccurrenceChance <= rarestOccurrenceChance)
                return occurrenceChance <= rarestOccurrenceChance ? 101f : 0f;

            float t = Mathf.Clamp01(
                (mostCommonOccurrenceChance - occurrenceChance) /
                (mostCommonOccurrenceChance - rarestOccurrenceChance));

            return Mathf.Lerp(0f, 100.99f, t);
        }

        private FishRarity GetRarityTierFromPercent(float rarityPercent)
        {
            if (rarityPercent >= 101f)
                return FishRarity.Mythical;

            if (rarityPercent >= 95f)
                return FishRarity.Legendary;

            if (rarityPercent >= 80f)
                return FishRarity.Rare;

            if (rarityPercent >= 55f)
                return FishRarity.Uncommon;

            return FishRarity.Common;
        }

        private float GetRarityValueMultiplier()
        {
            float tierMultiplier = _rarity switch
            {
                FishRarity.Uncommon => 1.35f,
                FishRarity.Rare => 1.85f,
                FishRarity.Legendary => 2.6f,
                FishRarity.Mythical => 4f,
                _ => 1f
            };

            float percentRefinement = 1f + Mathf.Clamp01(_rarityPercent / 101f) * 0.25f;
            return tierMultiplier * percentRefinement;
        }

        private void ApplyOutlineColor()
        {
            OutlineComponent[] outlines = GetComponentsInChildren<OutlineComponent>(true);
            if (outlines == null || outlines.Length == 0)
                return;

            Color rarityColor = GetRarityOutlineColor();
            for (int i = 0; i < outlines.Length; i++)
            {
                if (outlines[i] != null)
                    outlines[i].outlineColor = rarityColor;
            }
        }

        private Color GetRarityOutlineColor()
        {
            return _rarity switch
            {
                FishRarity.Mythical => _mythicalOutlineColor,
                FishRarity.Uncommon => _uncommonOutlineColor,
                FishRarity.Rare => _rareOutlineColor,
                FishRarity.Legendary => _legendaryOutlineColor,
                _ => _commonOutlineColor
            };
        }

        private void CacheWaterCollider()
        {
            _waterCollider = _waterVolume != null ? _waterVolume.GetComponent<BoxCollider>() : null;
        }

        private void UpdatePlayerVelocity()
        {
            if (_player == null)
            {
                _playerVelocity = Vector3.zero;
                _hasPlayerPosition = false;
                return;
            }

            if (!_hasPlayerPosition)
            {
                _previousPlayerPosition = _player.position;
                _playerVelocity = Vector3.zero;
                _hasPlayerPosition = true;
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            _playerVelocity = (_player.position - _previousPlayerPosition) / deltaTime;
            _previousPlayerPosition = _player.position;
        }

        private bool ShouldFleeFromPlayer()
        {
            if (_player == null)
                return false;

            Vector3 toPlayer = _player.position - transform.position;
            if (toPlayer.sqrMagnitude > _fleeRadius * _fleeRadius)
                return false;

            Vector3 horizontalVelocity = new Vector3(_playerVelocity.x, 0f, _playerVelocity.z);
            return horizontalVelocity.magnitude >= _playerMovementThreshold;
        }

        private bool HasReachedTarget()
        {
            return (_target - transform.position).sqrMagnitude < 0.09f;
        }

        private void PickNewTarget(bool shouldFlee)
        {
            _isFleeing = shouldFlee;
            _target = shouldFlee ? GetFleeTarget() : GetWanderTarget();
            _retargetTimer = shouldFlee
                ? Random.Range(_fleeRetargetMinTime, _fleeRetargetMaxTime)
                : Random.Range(_wanderRetargetMinTime, _wanderRetargetMaxTime);
        }

        private FishingLureInstance ResolveInterestLure()
        {
            if (_interestLure != null && IsCurrentInterestLureValid(_interestLure))
                return _interestLure;

            ClearInterestLure();

            if (Time.time < _nextInterestAllowedTime)
                return null;

            if (Time.time < _nextInterestResolveTime)
                return null;

            _nextInterestResolveTime = Time.time + Mathf.Max(0.05f, _interestResolveInterval);

            FishingLureInstance bestLure = null;
            float bestScore = 0f;
            FishingLureInstance fallbackLure = null;
            float fallbackScore = float.MinValue;
            var activeLures = FishingLureInstance.ActiveInstances;
            for (int i = 0; i < activeLures.Count; i++)
            {
                FishingLureInstance lure = activeLures[i];
                if (!IsAwareOfLure(lure))
                    continue;

                float awarenessChance = CalculateAwarenessChance(lure);
                if (awarenessChance <= 0f || Random.value > awarenessChance)
                    continue;

                float commitScore = CalculateCommitScore(lure, awarenessChance);
                if (commitScore > fallbackScore)
                {
                    fallbackScore = commitScore;
                    fallbackLure = lure;
                }

                if (commitScore <= bestScore)
                    continue;

                if (!lure.TryCommitFish(this))
                    continue;

                if (bestLure != null && bestLure != lure)
                    bestLure.ReleaseCommittedFish(this);

                bestScore = commitScore;
                bestLure = lure;
            }

            if (bestLure == null && fallbackLure != null && fallbackLure.TryCommitFish(this))
                bestLure = fallbackLure;

            _interestLure = bestLure;
            if (_interestLure != null)
            {
                _interestStartTime = Time.time;
                ClampCommittedDistanceToLure(_interestLure);
            }

            return bestLure;
        }

        private bool IsCurrentInterestLureValid(FishingLureInstance lure)
        {
            if (lure == null || !lure.isActiveAndEnabled || !lure.CanAttractFish)
                return false;

            if (!IsAwareOfLure(lure))
                return false;

            if (lure.CommittedFish != null && lure.CommittedFish != this)
                return false;

            float loseDistance = lure.InterestRadius * Mathf.Max(1f, _interestLoseDistanceMultiplier);
            float awarenessRange = Mathf.Max(loseDistance, GetWaterAwareRange());
            return (lure.transform.position - transform.position).sqrMagnitude <= awarenessRange * awarenessRange;
        }

        private bool IsAwareOfLure(FishingLureInstance lure)
        {
            if (lure == null || !lure.isActiveAndEnabled || !lure.CanAttractFish)
                return false;

            if (_waterVolume != null && lure.WaterVolume != null)
                return lure.WaterVolume == _waterVolume;

            float awarenessRange = Mathf.Max(lure.InterestRadius, GetWaterAwareRange());
            return (lure.transform.position - transform.position).sqrMagnitude <= awarenessRange * awarenessRange;
        }

        private float CalculateAwarenessChance(FishingLureInstance lure)
        {
            float speciesAffinity = _definition != null ? _definition.baitInterestMultiplier : 1f;
            float favoriteBaitBonus = GetFavoriteBaitLureMultiplier(lure);
            float lureFactor = Mathf.Max(0.1f, lure.InterestMultiplier);
            return Mathf.Clamp01(_baseLureInterestChance * speciesAffinity * lureFactor * favoriteBaitBonus);
        }

        private float CalculateCommitScore(FishingLureInstance lure, float awarenessChance)
        {
            Vector3 toLure = lure.transform.position - transform.position;
            float distance = toLure.magnitude;
            float nearRange = Mathf.Max(0.01f, lure.InterestRadius);
            float awarenessRange = Mathf.Max(nearRange, GetWaterAwareRange());
            float distanceWeight = distance <= nearRange
                ? 1f
                : Mathf.Lerp(1f, 0.2f, Mathf.InverseLerp(nearRange, awarenessRange, distance));

            return awarenessChance * distanceWeight;
        }

        private bool TryBiteLure(FishingLureInstance lure)
        {
            if (lure == null)
                return false;

            if (Time.time < _nextBiteAttemptTime)
                return false;

            if (Random.value <= CalculateBiteChance(lure))
                return lure.TryHookFish(this, CalculateHookSetWindow());

            lure.FlashBiteFail();
            _nextBiteAttemptTime = Time.time + Random.Range(
                Mathf.Min(_biteRetryDelayRange.x, _biteRetryDelayRange.y),
                Mathf.Max(_biteRetryDelayRange.x, _biteRetryDelayRange.y));
            return false;
        }

        private float CalculateHookSetWindow()
        {
            // Easier fish (low CatchDifficulty) get the full window. Hard fish get
            // closer to the floor — the player has to react fast.
            float minWindow = Mathf.Min(_hookSetWindowMin, _hookSetWindowMax);
            float maxWindow = Mathf.Max(_hookSetWindowMin, _hookSetWindowMax);
            float scaled = maxWindow / Mathf.Max(0.5f, CatchDifficulty);
            return Mathf.Clamp(scaled, minWindow, maxWindow);
        }

        private void HoldAtBitePoint(Vector3 biteTarget)
        {
            Vector3 clampedTarget = ClampToWater(biteTarget);
            float holdSpeed = Mathf.Max(0.25f, _swimSpeed * _interestSwimSpeedMultiplier * 0.4f);
            Vector3 nextPosition = Vector3.MoveTowards(transform.position, clampedTarget, holdSpeed * Time.deltaTime);
            Vector3 travel = nextPosition - transform.position;
            transform.position = nextPosition;

            Vector3 lookDirection = clampedTarget - transform.position;
            if (lookDirection.sqrMagnitude < 0.0001f)
                lookDirection = travel;

            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _turnSpeed * Time.deltaTime);
            }
        }

        private float CalculateBiteChance(FishingLureInstance lure)
        {
            float lureFactor = Mathf.Lerp(0.6f, 1.2f, Mathf.InverseLerp(0.1f, 2f, lure.InterestMultiplier));
            float difficultyFactor = 1f / Mathf.Max(0.35f, CatchDifficulty);
            return Mathf.Clamp01(_baseBiteChance * lureFactor * difficultyFactor);
        }

        private float GetFavoriteBaitLureMultiplier(FishingLureInstance lure)
        {
            if (!IsFavoriteBaitMatch(lure) || _definition == null)
                return 1f;

            return Mathf.Max(1f, _definition.favoriteBaitLureMultiplier);
        }

        private float GetFavoriteBaitCatchMultiplier()
        {
            if (!IsFavoriteBaitMatch(_interestLure) || _definition == null)
                return 1f;

            return Mathf.Max(1f, _definition.favoriteBaitCatchMultiplier);
        }

        private bool IsFavoriteBaitMatch(FishingLureInstance lure)
        {
            if (_definition == null || lure == null || !lure.HasBait)
                return false;

            bool hasFavoriteItemId = !string.IsNullOrWhiteSpace(_definition.favoriteBaitItemId);
            bool hasFavoriteName = !string.IsNullOrWhiteSpace(_definition.favoriteBaitName);
            if (!hasFavoriteItemId && !hasFavoriteName)
                return false;

            if (hasFavoriteItemId
                && string.Equals(
                    _definition.favoriteBaitItemId.Trim(),
                    lure.BaitItemId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return hasFavoriteName
                && string.Equals(
                    _definition.favoriteBaitName.Trim(),
                    lure.BaitName,
                    System.StringComparison.OrdinalIgnoreCase);
        }

        private Vector3 GetInterestTarget(FishingLureInstance lure)
        {
            if (lure == null)
                return GetWanderTarget();

            Vector3 target = lure.GetFishInterestPoint(_interestDepthOffset);
            return ClampToWater(target);
        }

        private void ClampCommittedDistanceToLure(FishingLureInstance lure)
        {
            if (lure == null)
                return;

            float maxTravelDistance = Mathf.Max(1f, _maxCommitTravelDistance);
            Vector3 lurePosition = lure.transform.position;
            Vector3 toFish = transform.position - lurePosition;
            float distance = toFish.magnitude;
            if (distance <= maxTravelDistance)
                return;

            Vector3 direction = distance > 0.001f
                ? toFish / distance
                : Random.onUnitSphere;
            direction.y *= 0.35f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;

            Vector3 clampedPosition = lurePosition + (direction.normalized * maxTravelDistance);
            transform.position = ClampToWater(clampedPosition);
        }

        private void ClearInterestLure()
        {
            if (_interestLure == null)
                return;

            _interestLure.ReleaseCommittedFish(this);
            _interestLure = null;
            _interestStartTime = -1f;
        }

        private bool HasInterestTimedOut()
        {
            return _interestLure != null
                && _interestStartTime >= 0f
                && Time.time - _interestStartTime >= Mathf.Max(1f, _maxInterestDuration);
        }

        private void AbandonInterest()
        {
            ClearInterestLure();
            _nextBiteAttemptTime = 0f;
            _nextInterestAllowedTime = Time.time + Random.Range(
                Mathf.Min(_interestRetryDelayRange.x, _interestRetryDelayRange.y),
                Mathf.Max(_interestRetryDelayRange.x, _interestRetryDelayRange.y));
        }

        private float GetWaterAwareRange()
        {
            if (_waterCollider == null)
                CacheWaterCollider();

            if (_waterCollider == null)
                return Mathf.Max(_wanderRadius, _fleeRadius, 10f);

            return _waterCollider.bounds.size.magnitude;
        }

        private Vector3 GetWanderTarget()
        {
            Vector3 candidate = _origin + Random.insideUnitSphere * _wanderRadius;

            if (_waterVolume == null || _waterCollider == null)
                return candidate;

            for (int i = 0; i < 6; i++)
            {
                candidate = _origin + Random.insideUnitSphere * _wanderRadius;
                candidate = ClampToWater(candidate);
                if (IsWithinWaterBounds(candidate))
                    return candidate;
            }

            return ClampToWater(candidate);
        }

        private Vector3 GetFleeTarget()
        {
            if (_player == null)
                return GetWanderTarget();

            Vector3 predictedPlayerPosition = _player.position + _playerVelocity * _playerPredictionTime;
            Vector3 away = transform.position - predictedPlayerPosition;
            away.y *= 0.5f;

            if (away.sqrMagnitude < 0.001f)
                away = -_player.forward;

            if (away.sqrMagnitude < 0.001f)
                away = Random.onUnitSphere;

            away.Normalize();

            Vector3 side = Vector3.Cross(Vector3.up, away);
            if (side.sqrMagnitude < 0.001f)
                side = Vector3.right;
            side.Normalize();

            Vector3 candidate = transform.position
                              + away * _fleeDistance
                              + side * Random.Range(-_fleeDistance * 0.35f, _fleeDistance * 0.35f)
                              + Vector3.up * Random.Range(-1f, 1f);

            return ClampToWater(candidate);
        }

        private void MoveTowardsTarget(float speed)
        {
            Vector3 toTarget = _target - transform.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                return;

            Vector3 direction = toTarget.normalized;
            Vector3 nextPosition = transform.position + direction * (speed * Time.deltaTime);
            nextPosition = ClampToWater(nextPosition);

            Vector3 actualDirection = nextPosition - transform.position;
            transform.position = nextPosition;

            if (actualDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(actualDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _turnSpeed * Time.deltaTime);
            }
        }

        public ItemComponent Catch(Inventory inventory = null, Transform dropParent = null)
        {
            if (_isCaught)
                return null;

            PrepareForCatchHandoff();
            _isCaught = true;
            ItemComponent caughtItem = CreateCaughtItem(inventory, dropParent);
            Destroy(gameObject);
            return caughtItem;
        }

        public bool TryHook(FishingLureInstance lure)
        {
            if (_isCaught || _hookedLure != null || lure == null)
                return false;

            _interestLure = lure;
            _hookedLure = lure;
            _retargetTimer = 0f;
            _isFleeing = false;
            _fightStamina = 1f;
            _isStruggling = false;
            _currentStruggleIntensity = 0f;
            ResolveContext(force: true);
            _hookedPullDirection = GetHookedEscapeDirection(lure);
            _nextStruggleTime = Time.time + GetStruggleInterval(initialDelay: true);
            return true;
        }

        public void SetHookedEscapeDirection(FishingLureInstance lure, Vector3 escapeDirection)
        {
            if (_isCaught || lure == null || _hookedLure != lure)
                return;

            ResolveContext(force: true);
            _hookedPullDirection = GetHookedEscapeDirection(lure, escapeDirection);
        }

        public void ReleaseFromHook()
        {
            if (_hookedLure == null && _interestLure == null)
                return;

            _hookedLure = null;
            _isStruggling = false;
            _currentStruggleIntensity = 0f;
            _hookedPullDirection = Vector3.zero;
            _fightStamina = 1f;
            ClearInterestLure();
            ResolveContext(force: true);
            PickNewTarget(shouldFlee: false);
        }

        private void UpdateHookedFight()
        {
            FishingLureInstance lure = _hookedLure;
            if (lure == null)
                return;

            // Struggles only happen once the player has confirmed the hook set.
            if (lure.BiteState != FishBiteState.Hooked)
                return;

            ResolveContext(force: false);
            UpdatePlayerVelocity();

            float staminaDuration = _definition != null ? _definition.fightStaminaDuration : 18f;
            _fightStamina = Mathf.Max(0f, _fightStamina - (Time.deltaTime / Mathf.Max(1f, staminaDuration)));

            if (_isStruggling)
            {
                if (Time.time >= _struggleEndTime)
                {
                    _isStruggling = false;
                    _currentStruggleIntensity = 0f;
                    _nextStruggleTime = Time.time + GetStruggleInterval();
                }
                else
                {
                    lure.NotifyStruggle(_struggleDirection, _currentStruggleIntensity);
                }
            }

            if (!_isStruggling && Time.time >= _nextStruggleTime)
                BeginStruggle();

            SwimWhileHooked(lure);
        }

        private void SwimWhileHooked(FishingLureInstance lure)
        {
            if (lure == null || !lure.IsSurfaceFightActive)
                return;

            if (!IsWithinWaterBounds(transform.position))
                transform.position = ClampToWater(transform.position);

            if (_hookedPullDirection.sqrMagnitude < 0.001f)
                _hookedPullDirection = GetHookedEscapeDirection(lure);

            Vector3 swimDirection = _hookedPullDirection.normalized;

            float difficultyPressure = Mathf.Clamp01(CatchDifficulty * 0.12f);
            float weightPressure = Mathf.Clamp01(Weight * 0.035f);
            float strugglePressure = _isStruggling ? _currentStruggleIntensity * 0.45f : 0f;
            float pullIntensity = Mathf.Clamp01(0.45f + difficultyPressure + weightPressure + strugglePressure);
            float swimSpeed = Mathf.Lerp(_swimSpeed * 0.85f, _fleeSpeed, Mathf.Clamp01(0.25f + pullIntensity * 0.55f));

            Vector3 currentPosition = transform.position;
            Vector3 nextPosition = currentPosition + swimDirection * (swimSpeed * Time.deltaTime);
            nextPosition = ClampToWater(nextPosition);

            Vector3 actualDirection = nextPosition - currentPosition;
            transform.position = nextPosition;

            if (actualDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(actualDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _turnSpeed * Time.deltaTime);
                lure.NotifyHookedFishPull(transform.position, actualDirection.normalized, pullIntensity);
            }
            else
            {
                lure.NotifyHookedFishPull(transform.position, swimDirection, pullIntensity * 0.5f);
            }
        }

        private Vector3 GetHookedEscapeDirection(FishingLureInstance lure, Vector3 preferredDirection = default)
        {
            preferredDirection.y = 0f;
            if (preferredDirection.sqrMagnitude >= 0.001f)
                return preferredDirection.normalized;

            if (_player != null)
            {
                Vector3 playerForward = _player.forward;
                playerForward.y = 0f;
                if (playerForward.sqrMagnitude >= 0.001f)
                    return playerForward.normalized;
            }

            Vector3 away = lure != null
                ? transform.position - lure.transform.position
                : transform.forward;
            away.y = 0f;

            if (away.sqrMagnitude < 0.001f)
                away = Vector3.forward;

            return away.normalized;
        }

        private void BeginStruggle()
        {
            float minDur = _definition != null ? _definition.struggleDurationMin : 0.8f;
            float maxDur = _definition != null ? _definition.struggleDurationMax : 2.2f;
            float duration = Random.Range(minDur, Mathf.Max(minDur, maxDur));
            // Tired fish struggle in shorter bursts.
            duration *= Mathf.Lerp(0.4f, 1f, _fightStamina);

            float speciesStrength = _definition != null ? _definition.struggleStrength : 1f;
            float weightFactor = 1f + Mathf.Clamp(_weight * 0.05f, 0f, 0.6f);
            // 0.6x global softener so a default fish (strength=1) caps around 0.65 intensity,
            // leaving headroom for predators/legendaries with strength>1 to feel meaningfully harder.
            _currentStruggleIntensity = Mathf.Clamp01(speciesStrength * weightFactor * 0.6f * Mathf.Lerp(0.35f, 1f, _fightStamina));

            float angle = Random.Range(0f, Mathf.PI * 2f);
            _struggleDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            _isStruggling = true;
            _struggleEndTime = Time.time + duration;

            AudioService.Instance?.PlaySfx(AudioEvent.FishingStruggle, transform.position);
        }

        private float GetStruggleInterval(bool initialDelay = false)
        {
            float minInt = _definition != null ? _definition.struggleIntervalMin : 3.5f;
            float maxInt = _definition != null ? _definition.struggleIntervalMax : 6.5f;
            float interval = Random.Range(minInt, Mathf.Max(minInt, maxInt));
            // Give the player time to settle into the fight before the first struggle hits.
            if (initialDelay)
                interval *= 1.4f;
            // As stamina drops, struggles become rarer (tired fish).
            interval *= Mathf.Lerp(1.6f, 1f, _fightStamina);
            return interval;
        }

        public void PrepareForCatchHandoff()
        {
            _hookedLure = null;
            ClearInterestLure();
        }

        public bool IsCommittedTo(FishingLureInstance lure)
        {
            return lure != null && (_interestLure == lure || _hookedLure == lure);
        }

        public void SetHookedPose(Vector3 position, Quaternion rotation)
        {
            if (_isCaught)
                return;

            transform.position = position;
            transform.rotation = rotation;
        }

        private ItemComponent CreateCaughtItem(Inventory inventory, Transform dropParent)
        {
            GameObject catchPrefab = CatchItemPrefab;
            if (catchPrefab == null)
                return null;

            Transform itemParent = inventory != null ? inventory.transform : dropParent;
            GameObject itemInstance = Instantiate(catchPrefab, transform.position, transform.rotation, itemParent);
            if (itemInstance == null)
                return null;

            ItemComponent itemComponent = itemInstance.GetComponent<ItemComponent>();
            if (itemComponent == null)
            {
                Destroy(itemInstance);
                return null;
            }

            CaughtFishItem caughtFishItem = itemInstance.GetComponent<CaughtFishItem>();
            if (caughtFishItem == null)
                caughtFishItem = itemInstance.AddComponent<CaughtFishItem>();

            caughtFishItem.ConfigureFromFish(this);

            var fishData = new CaughtFishData(
                null,
                _definition,
                _size,
                _weight,
                _rarity,
                _rarityPercent,
                _prefix,
                IsPredator
            );
            fishData.speciesDisplayName = _fishName;
            fishData.speciesAssetName = _definition != null ? _definition.name : string.Empty;
            fishData.cachedValue = Value;
            fishData.catchVisualScale = CatchVisualLocalScale;
            fishData.modelPrefab = CatchVisualPrefab;
            string fishCode = FishRegistry.Instance.RegisterFish(fishData);
            caughtFishItem.FishCode = fishCode;

            if (inventory != null)
            {
                bool addedToInventory = inventory.AddSlot(new InventorySlot(itemComponent, fishCode));
                if (addedToInventory)
                {
                    itemInstance.SetActive(false);
                    return itemComponent;
                }
            }

            itemInstance.transform.SetParent(dropParent, true);
            itemInstance.SetActive(true);
            return itemComponent;
        }

        private Vector3 ClampToWater(Vector3 position)
        {
            if (_waterVolume == null)
                return position;

            if (_waterCollider == null)
                CacheWaterCollider();

            if (_waterCollider == null)
                return position;

            Bounds bounds = _waterCollider.bounds;
            float minX = bounds.min.x + _edgePadding;
            float maxX = bounds.max.x - _edgePadding;
            float minZ = bounds.min.z + _edgePadding;
            float maxZ = bounds.max.z - _edgePadding;

            if (minX > maxX)
                minX = maxX = bounds.center.x;

            if (minZ > maxZ)
                minZ = maxZ = bounds.center.z;

            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.z = Mathf.Clamp(position.z, minZ, maxZ);

            float minY = bounds.min.y + _bottomPadding;
            float maxY = _waterVolume.GetSurfaceHeight(position) - _surfacePadding;
            if (maxY < minY)
                maxY = minY;

            position.y = Mathf.Clamp(position.y, minY, maxY);
            return position;
        }

        private bool IsWithinWaterBounds(Vector3 position)
        {
            if (_waterVolume == null || _waterCollider == null)
                return true;

            Bounds bounds = _waterCollider.bounds;
            if (position.x < bounds.min.x + _edgePadding || position.x > bounds.max.x - _edgePadding)
                return false;

            if (position.z < bounds.min.z + _edgePadding || position.z > bounds.max.z - _edgePadding)
                return false;

            float minY = bounds.min.y + _bottomPadding;
            float maxY = _waterVolume.GetSurfaceHeight(position) - _surfacePadding;
            return position.y >= minY && position.y <= maxY;
        }
    }
}
