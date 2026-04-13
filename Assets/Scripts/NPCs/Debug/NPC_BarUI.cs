using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Sol.Locomotion;

namespace Sol.AI
{
    [DisallowMultipleComponent]
    public class NPC_BarUI : MonoBehaviour
    {
        [SerializeField] private bool showWhenDebugEnabled = true;
        [SerializeField] private bool faceActiveCamera = true;
        [SerializeField] private bool showDetailedDebug = true;
        [SerializeField, Min(0.05f)] private float debugRefreshInterval = 0.1f;
        [SerializeField] private bool compactAtDistance = true;
        [SerializeField, Min(0f)] private float compactDistance = 8f;
        [SerializeField] private bool forceExpandWhenLookedAt = true;
        [SerializeField, Min(0f)] private float lookTowardMaxDistance = 40f;
        [SerializeField] private LayerMask lookTowardRayMask = ~0;
        [SerializeField] private Slider healthBar;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Slider hungerBar;
        [SerializeField] private TextMeshProUGUI hungerText;
        [SerializeField] private Slider thirstBar;
        [SerializeField] private TextMeshProUGUI thirstText;
        [SerializeField] private Slider staminaBar;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI staminaText;
        [SerializeField] private TextMeshProUGUI moodText;
        [SerializeField] private TextMeshProUGUI debugDetailsText;

        private NPCSoul soul;
        private AI_NPC npc;
        private LocomotionController locomotionController;
        private LocomotionState locomotionState;
        private Animator animator;
        private NavMeshAgent navAgent;
        private float debugRefreshTimer;
        private bool isCompactMode;
        private bool isLookedAtByCrosshair;
        private readonly StringBuilder debugBuilder = new(256);
        private int cachedHealthDisplay = int.MinValue;
        private int cachedHealthMaxDisplay = int.MinValue;

        private void Awake()
        {
            CacheReferences();
            CacheSoul();
            CacheAIReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            CacheSoul();
            CacheAIReferences();

            if (soul != null && !soul.IsAlive)
            {
                gameObject.SetActive(false);
                return;
            }

            if (soul != null)
            {
                soul.OnVitalsChanged += HandleSoulChanged;
                soul.OnDeath += HandleSoulDeath;
            }

            AI_DebugUISettings.VisibilityChanged += HandleVisibilityChanged;
            isCompactMode = IsCompactForCamera();
            ApplyVisibility();
            RefreshAll(force: true);
        }

        private void OnDisable()
        {
            if (soul != null)
            {
                soul.OnVitalsChanged -= HandleSoulChanged;
                soul.OnDeath -= HandleSoulDeath;
            }

            AI_DebugUISettings.VisibilityChanged -= HandleVisibilityChanged;
        }

        private void Update()
        {
            if (!showWhenDebugEnabled || !AI_DebugUISettings.IsVisible)
                return;

            bool compactNow = IsCompactForCamera();
            if (compactNow != isCompactMode)
            {
                isCompactMode = compactNow;
                ApplyVisibility();
                RefreshAll(force: true);
            }

            debugRefreshTimer -= Time.deltaTime;
            if (debugRefreshTimer <= 0f)
            {
                debugRefreshTimer = debugRefreshInterval;
                RefreshDebugDetails();
            }
        }

        private void LateUpdate()
        {
            if (!faceActiveCamera)
                return;

            Camera activeCamera = AI_DebugCameraUtility.GetActiveGameplayCamera();
            if (activeCamera == null)
                return;

            Vector3 forward = transform.position - activeCamera.transform.position;
            if (forward.sqrMagnitude <= 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(forward.normalized, activeCamera.transform.up);
        }

        private void CacheSoul()
        {
            if (soul == null)
                soul = GetComponentInParent<NPCSoul>();
        }

        private void CacheAIReferences()
        {
            if (npc == null)
                npc = GetComponentInParent<AI_NPC>();
            if (locomotionController == null)
                locomotionController = GetComponentInParent<LocomotionController>();
            if (locomotionState == null)
                locomotionState = GetComponentInParent<LocomotionState>();
            if (animator == null)
                animator = GetComponentInParent<Animator>();
            if (navAgent == null)
                navAgent = GetComponentInParent<NavMeshAgent>();
        }

        private void CacheReferences()
        {
            healthBar ??= FindSlider("HealthBar");
            hungerBar ??= FindSlider("hungerBar");
            thirstBar ??= FindSlider("thirstBar");
            staminaBar ??= FindSlider("EnergyBar", "staminaBar");
            healthText ??= FindText("hpText", "xpText", "HealthText");
            hungerText ??= FindText("hungerText", "HungerText");
            thirstText ??= FindText("thirstText", "ThirstText");
            nameText ??= FindText("nameText", "NameText");
            staminaText ??= FindText("NRGText", "staminaText", "EnergyText");
            moodText ??= FindText("moodText", "MoodText");
            debugDetailsText ??= FindText("debugText", "DebugText", "debugDetailsText", "AIDebugText");

            ConfigureBar(healthBar);
            HideLegacyUi(hungerBar);
            HideLegacyUi(thirstBar);
            HideLegacyUi(staminaBar);
            HideLegacyUi(hungerText);
            HideLegacyUi(thirstText);
            HideLegacyUi(staminaText);
            HideLegacyUi(moodText);
            EnsureDebugDetailsLabel();
            ConfigureDebugDetailsLabel();
        }

        private void HandleSoulChanged(NPCSoul _)
        {
            RefreshAll();
        }

        private void HandleVisibilityChanged(bool visible)
        {
            ApplyVisibility();
            if (visible)
                RefreshAll(force: true);
        }

        private void HandleSoulDeath()
        {
            gameObject.SetActive(false);
        }

        private void ApplyVisibility()
        {
            bool alive = soul == null || soul.IsAlive;
            bool visible = showWhenDebugEnabled && AI_DebugUISettings.IsVisible && alive;

            if (healthBar != null) healthBar.gameObject.SetActive(visible);
            if (healthText != null) healthText.gameObject.SetActive(visible);
            if (nameText != null) nameText.gameObject.SetActive(visible);
            if (debugDetailsText != null) debugDetailsText.gameObject.SetActive(visible && showDetailedDebug);

            HideLegacyUi(hungerBar);
            HideLegacyUi(thirstBar);
            HideLegacyUi(staminaBar);
            HideLegacyUi(hungerText);
            HideLegacyUi(thirstText);
            HideLegacyUi(staminaText);
            HideLegacyUi(moodText);
        }

        private void RefreshAll(bool force = false)
        {
            if (soul == null || !showWhenDebugEnabled || !AI_DebugUISettings.IsVisible)
                return;

            if (nameText != null && (force || nameText.text != soul.NPCName))
                nameText.text = soul.NPCName;

            RefreshBar(healthBar, soul.HealthNorm);
            RefreshHealthText(force);

            if (force)
                RefreshDebugDetails();
        }

        private static void RefreshBar(Slider bar, float norm)
        {
            if (bar == null || Mathf.Approximately(bar.value, norm))
                return;

            bar.value = norm;
        }

        private static void ConfigureBar(Slider bar)
        {
            if (bar == null)
                return;

            float normalized = bar.maxValue > bar.minValue ? bar.normalizedValue : 0f;
            bool needsClamp = !Mathf.Approximately(bar.minValue, 0f)
                || !Mathf.Approximately(bar.maxValue, 1f)
                || bar.wholeNumbers;

            if (!needsClamp)
                return;

            bar.minValue = 0f;
            bar.maxValue = 1f;
            bar.wholeNumbers = false;
            bar.SetValueWithoutNotify(Mathf.Clamp01(normalized));
        }

        private void RefreshHealthText(bool force)
        {
            if (healthText == null)
                return;

            int current = Mathf.CeilToInt(soul.Health);
            int max = Mathf.CeilToInt(soul.MaxHealth);
            if (!force && current == cachedHealthDisplay && max == cachedHealthMaxDisplay)
                return;

            cachedHealthDisplay = current;
            cachedHealthMaxDisplay = max;
            healthText.text = $"Health: {current}/{max}";
        }

        private void RefreshDebugDetails()
        {
            if (debugDetailsText == null || !showDetailedDebug || !showWhenDebugEnabled || !AI_DebugUISettings.IsVisible)
                return;

            CacheAIReferences();
            debugBuilder.Clear();

            if (npc == null)
            {
                debugDetailsText.text = "AI: missing";
                return;
            }

            debugBuilder.Append("State: ").Append(npc.CurrentState)
                        .Append(" (prev: ").Append(npc.PreviousState).Append(')').AppendLine();

            debugBuilder.Append("RotateGate: ").Append(npc.RotateGateTimer.ToString("F2")).AppendLine();

            if (isCompactMode)
            {
                debugBuilder.Append("Health: ").Append(Mathf.CeilToInt(soul.Health)).Append('/')
                            .Append(Mathf.CeilToInt(soul.MaxHealth));
                debugDetailsText.text = debugBuilder.ToString();
                return;
            }

            if (navAgent != null && navAgent.isOnNavMesh)
            {
                debugBuilder.Append("Nav: path=").Append(navAgent.hasPath)
                            .Append(" remainingDistance=").Append(navAgent.remainingDistance.ToString("F2"))
                            .Append(" stop=").Append(navAgent.isStopped)
                            .AppendLine();
            }
            else
            {
                debugBuilder.AppendLine("Nav: unavailable");
            }

            if (locomotionState != null)
            {
                debugBuilder.Append("Move: ").Append(locomotionState.CurrentMovementState);
                if (locomotionController != null)
                {
                    debugBuilder.Append(" speed=").Append(locomotionController.CurrentSpeed.ToString("F2"))
                                .Append(" swim=").Append(npc.IsSwimming);
                }
                debugBuilder.AppendLine();
            }
            else
            {
                debugBuilder.AppendLine("Move: unavailable");
            }

            if (animator != null)
            {
                debugBuilder.Append("Anim: aiState=").Append(animator.GetInteger("aiState"))
                            .Append(" speed=").Append(animator.GetFloat("speed").ToString("F2"));
            }
            else
            {
                debugBuilder.Append("Anim: unavailable");
            }

            debugDetailsText.text = debugBuilder.ToString();
        }

        private bool IsCompactForCamera()
        {
            Camera activeCamera = AI_DebugCameraUtility.GetActiveGameplayCamera();
            if (activeCamera == null)
            {
                isLookedAtByCrosshair = false;
                return false;
            }

            isLookedAtByCrosshair = IsLookedAtByCrosshair(activeCamera);
            if (forceExpandWhenLookedAt && isLookedAtByCrosshair)
                return false;

            if (!compactAtDistance || compactDistance <= 0f)
                return false;

            float sqrDistance = (transform.position - activeCamera.transform.position).sqrMagnitude;
            return sqrDistance > compactDistance * compactDistance;
        }

        private bool IsLookedAtByCrosshair(Camera activeCamera)
        {
            float rayDistance = lookTowardMaxDistance > 0f ? lookTowardMaxDistance : Mathf.Infinity;
            Ray ray = activeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, lookTowardRayMask, QueryTriggerInteraction.Collide))
                return false;

            AI_NPC hitNpc = hit.collider.GetComponentInParent<AI_NPC>();
            return hitNpc == npc;
        }

        private void EnsureDebugDetailsLabel()
        {
            if (debugDetailsText != null)
                return;

            TextMeshProUGUI template = healthText != null ? healthText : nameText;
            if (template == null)
                return;

            Transform parent = FindChild("Background");
            if (parent == null)
                parent = transform;

            GameObject labelObject = new("debugDetailsText", typeof(RectTransform));
            labelObject.layer = parent.gameObject.layer;
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -34f);
            rect.sizeDelta = new Vector2(0f, 82f);
            rect.localScale = Vector3.one;

            debugDetailsText = labelObject.AddComponent<TextMeshProUGUI>();
            ConfigureDebugDetailsLabel();
        }

        private void ConfigureDebugDetailsLabel()
        {
            if (debugDetailsText == null)
                return;

            TextMeshProUGUI template = healthText != null ? healthText : nameText;
            if (template == null)
                return;

            debugDetailsText.font = template.font;
            debugDetailsText.fontSharedMaterial = template.fontSharedMaterial;
            debugDetailsText.color = template.color;
            debugDetailsText.fontStyle = template.fontStyle;
            debugDetailsText.alignment = TextAlignmentOptions.TopLeft;
            debugDetailsText.textWrappingMode = TextWrappingModes.NoWrap;
            debugDetailsText.overflowMode = TextOverflowModes.Overflow;
            debugDetailsText.raycastTarget = false;
            debugDetailsText.fontSize = Mathf.Max(8f, template.fontSize * 0.42f);
            if (string.IsNullOrEmpty(debugDetailsText.text))
                debugDetailsText.text = string.Empty;
        }

        private Slider FindSlider(params string[] candidateNames)
        {
            foreach (string candidateName in candidateNames)
            {
                Transform candidate = transform.Find(candidateName);
                if (candidate == null)
                    candidate = FindChild(candidateName);

                if (candidate != null && candidate.TryGetComponent(out Slider slider))
                    return slider;
            }

            return null;
        }

        private TextMeshProUGUI FindText(params string[] candidateNames)
        {
            foreach (string candidateName in candidateNames)
            {
                Transform candidate = transform.Find(candidateName);
                if (candidate == null)
                    candidate = FindChild(candidateName);

                if (candidate != null && candidate.TryGetComponent(out TextMeshProUGUI text))
                    return text;
            }

            return null;
        }

        private Transform FindChild(string childName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                    return child;
            }

            return null;
        }

        private static void HideLegacyUi(Component component)
        {
            if (component != null)
                component.gameObject.SetActive(false);
        }
    }
}
