using UnityEngine;
using Sol.Player;
using Sol.Outline;
using Sol;
using Sol.Grab;
using Sol.Fishing;

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
        [Header("Definition")]
        [SerializeField] private FishDefinition _definition;

        [Header("Fish Identity")]
        [SerializeField] private string _fishName = "Fish";
        [SerializeField] private FishRarity _rarity = FishRarity.Common;
        [SerializeField] private int _baseValue = 10;
        [SerializeField] private GameObject _catchItemPrefab;

        [Header("Fish Stats")]
        [SerializeField] private float _minSize = 0.5f;
        [SerializeField] private float _maxSize = 1.5f;
        [SerializeField] private float _minWeight = 0.2f;
        [SerializeField] private float _maxWeight = 5f;
        [SerializeField] private float _catchDifficulty = 1f;

        [Header("Visuals")]
        [SerializeField] private Transform _modelRoot;
        [SerializeField] private GameObject[] _possibleModels;
        [SerializeField, Min(0.01f)] private float _modelImportScaleCompensation = 10f;

        [Header("Outline")]
        [SerializeField] private Color _commonOutlineColor = new(0.7f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color _uncommonOutlineColor = new(0.45f, 1f, 0.45f, 1f);
        [SerializeField] private Color _rareOutlineColor = new(0.35f, 0.7f, 1f, 1f);
        [SerializeField] private Color _legendaryOutlineColor = new(1f, 0.75f, 0.2f, 1f);
        [SerializeField] private Color _mythicalOutlineColor = new(1f, 0.35f, 0.95f, 1f);

        [Header("Prefixes")]
        [SerializeField, Range(0.01f, 0.25f)] private float _sizeOutlierThreshold = 0.1f;
        [SerializeField] private string _smallFishPrefix = "Teeny";
        [SerializeField] private string _largeFishPrefix = "Lofty";

        [Header("Debug")]
        [SerializeField] private bool _showDebugLabel;

        [Header("Tackle Interest")]
        [SerializeField, Range(0f, 1f)] private float _baseTackleInterestChance = 0.35f;
        [SerializeField] private float _interestDepthOffset = 0.35f;
        [SerializeField] private float _interestResolveInterval = 0.35f;
        [SerializeField] private float _interestLoseDistanceMultiplier = 1.5f;
        [SerializeField] private float _interestSwimSpeedMultiplier = 1.2f;
        [SerializeField] private float _interestRetargetMinTime = 0.25f;
        [SerializeField] private float _interestRetargetMaxTime = 0.65f;

        [Header("Movement")]
        [SerializeField] private float _swimSpeed = 1.5f;
        [SerializeField] private float _fleeSpeed = 3f;
        [SerializeField] private float _turnSpeed = 3f;

        [Header("Wander")]
        [SerializeField] private float _wanderRadius = 5f;
        [SerializeField] private float _wanderRetargetMinTime = 2f;
        [SerializeField] private float _wanderRetargetMaxTime = 5f;

        [Header("Player Avoidance")]
        [SerializeField] private Transform _player;
        [SerializeField] private float _fleeRadius = 6f;
        [SerializeField] private float _fleeDistance = 4f;
        [SerializeField] private float _playerMovementThreshold = 0.2f;
        [SerializeField] private float _playerPredictionTime = 0.35f;
        [SerializeField] private float _fleeRetargetMinTime = 0.35f;
        [SerializeField] private float _fleeRetargetMaxTime = 1f;

        [Header("Water Bounds")]
        [SerializeField] private WaterVolume _waterVolume;
        [SerializeField] private float _edgePadding = 0.75f;
        [SerializeField] private float _surfacePadding = 0.6f;
        [SerializeField] private float _bottomPadding = 0.5f;

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

        private float _size;
        private float _weight;
        private float _rarityPercent;
        private bool _isCaught;
        private string _prefix = string.Empty;
        private FishingTackleInstance _interestTackle;

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
        public float CatchDifficulty => _catchDifficulty * (1f + _weight * 0.1f);
        public bool IsCaught => _isCaught;
        public GameObject CatchItemPrefab => _catchItemPrefab;

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
            if (Time.time >= _nextContextResolveTime)
                ResolveContext(force: false);

            UpdatePlayerVelocity();

            bool shouldFlee = ShouldFleeFromPlayer();
            FishingTackleInstance interestTackle = shouldFlee ? null : ResolveInterestTackle();
            _retargetTimer -= Time.deltaTime;

            if (interestTackle != null)
            {
                _isFleeing = false;
                _interestTackle = interestTackle;

                if (_retargetTimer <= 0f || HasReachedTarget() || !IsCurrentInterestTackleValid(_interestTackle))
                {
                    _target = GetInterestTarget(_interestTackle);
                    _retargetTimer = Random.Range(_interestRetargetMinTime, _interestRetargetMaxTime);
                }

                MoveTowardsTarget(_swimSpeed * _interestSwimSpeedMultiplier);
                return;
            }

            _interestTackle = null;
            if (shouldFlee != _isFleeing || _retargetTimer <= 0f || HasReachedTarget())
                PickNewTarget(shouldFlee);

            MoveTowardsTarget(shouldFlee ? _fleeSpeed : _swimSpeed);
        }

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
                (_interestTackle != null ? "\nInterested" : string.Empty);

            GUI.Label(labelRect, label);
        }

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
                        // Scene has no Player tag configured yet; fall back to another
                        // resolve attempt later instead of throwing every second.
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
            _size = Random.Range(_minSize, _maxSize);
            _weight = Random.Range(_minWeight, _maxWeight);
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

        private string GetPrefixForCurrentRoll()
        {
            if (_maxSize <= _minSize)
                return string.Empty;

            float sizePercent = Mathf.InverseLerp(_minSize, _maxSize, _size);
            float upperThreshold = 1f - _sizeOutlierThreshold;

            if (sizePercent <= _sizeOutlierThreshold)
                return _smallFishPrefix;

            if (sizePercent >= upperThreshold)
                return _largeFishPrefix;

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

        private FishingTackleInstance ResolveInterestTackle()
        {
            if (_interestTackle != null && IsCurrentInterestTackleValid(_interestTackle))
                return _interestTackle;

            if (Time.time < _nextInterestResolveTime)
                return null;

            _nextInterestResolveTime = Time.time + Mathf.Max(0.05f, _interestResolveInterval);

            FishingTackleInstance bestTackle = null;
            float bestChance = 0f;
            var activeTackles = FishingTackleInstance.ActiveInstances;
            for (int i = 0; i < activeTackles.Count; i++)
            {
                FishingTackleInstance tackle = activeTackles[i];
                if (!IsCurrentInterestTackleValid(tackle))
                    continue;

                float chance = CalculateInterestChance(tackle);
                if (chance <= 0f || Random.value > chance || chance <= bestChance)
                    continue;

                bestChance = chance;
                bestTackle = tackle;
            }

            return bestTackle;
        }

        private bool IsCurrentInterestTackleValid(FishingTackleInstance tackle)
        {
            if (tackle == null || !tackle.isActiveAndEnabled || !tackle.CanAttractFish)
                return false;

            if (_waterVolume != null && tackle.WaterVolume != null && tackle.WaterVolume != _waterVolume)
                return false;

            float loseDistance = tackle.InterestRadius * Mathf.Max(1f, _interestLoseDistanceMultiplier);
            return (tackle.transform.position - transform.position).sqrMagnitude <= loseDistance * loseDistance;
        }

        private float CalculateInterestChance(FishingTackleInstance tackle)
        {
            Vector3 toTackle = tackle.transform.position - transform.position;
            float distance = toTackle.magnitude;
            if (distance > tackle.InterestRadius)
                return 0f;

            float speciesAffinity = _definition != null ? _definition.baitInterestMultiplier : 1f;
            float distanceFactor = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, tackle.InterestRadius));
            float tackleFactor = Mathf.Max(0.1f, tackle.InterestMultiplier);
            return Mathf.Clamp01(_baseTackleInterestChance * speciesAffinity * tackleFactor * Mathf.Lerp(0.35f, 1f, distanceFactor));
        }

        private Vector3 GetInterestTarget(FishingTackleInstance tackle)
        {
            if (tackle == null)
                return GetWanderTarget();

            Vector3 target = tackle.GetFishInterestPoint(_interestDepthOffset);
            return ClampToWater(target);
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

            _isCaught = true;
            ItemComponent caughtItem = CreateCaughtItem(inventory, dropParent);
            Destroy(gameObject);
            return caughtItem;
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

            if (inventory == null)
                return itemComponent;

            bool addedToInventory = inventory.Add(itemComponent);
            if (addedToInventory)
            {
                itemInstance.SetActive(false);
                return itemComponent;
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
