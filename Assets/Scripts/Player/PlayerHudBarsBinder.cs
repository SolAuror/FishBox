using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sol.Player
{
    /// <summary>
    /// Binds the player HUD bars prefab to PlayerSoul vitals.
    /// Keeps UI values authoritative to the runtime soul state.
    /// </summary>
    public class PlayerHudBarsBinder : MonoBehaviour
    {
        #region Inspector Settings
        [Header("Player")]
        [Tooltip("Inspector: tunes player root.")]
        [SerializeField] private GameObject playerRoot;
        [Tooltip("Inspector: tunes player soul.")]
        [SerializeField] private PlayerSoul playerSoul;

        [Header("UI")]
        [Tooltip("Inspector: tunes health bar.")]
        [SerializeField] private Slider healthBar;
        [SerializeField] private Slider staminaBar;
        [Tooltip("Inspector: tunes name text.")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text healthText;
        [Tooltip("Inspector: tunes stamina text.")]
        [SerializeField] private TMP_Text staminaText;

        [Header("Auto-Wire")]
        [Tooltip("Inspector: tunes auto find by name.")]
        [SerializeField] private bool autoFindByName = true;
        #endregion

        private PlayerSoul _boundSoul;
        private PlayerSoul _subscribedSoul;

        private void Awake()
        {
            ResolveReferences();
            Refresh(forceText: true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindSoul();
            Refresh(forceText: true);
        }

        private void OnDisable()
        {
            UnbindSoul();
        }

        private void Update()
        {
            // Late-resolve in case player is spawned/replaced after HUD.
            if (_boundSoul == null || (playerRoot != null && _boundSoul.gameObject != playerRoot))
            {
                ResolveReferences();
                BindSoul();
                Refresh(forceText: true);
            }
        }

        private void OnValidate()
        {
            if (!autoFindByName)
                return;

            AutoAssignUiReferences();
        }

        private void ResolveReferences()
        {
            if (playerRoot == null)
            {
                var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null)
                    playerRoot = taggedPlayer;
            }

            if (playerSoul == null && playerRoot != null)
                playerSoul = playerRoot.GetComponent<PlayerSoul>();

            if (playerSoul == null)
                playerSoul = FindFirstObjectByType<PlayerSoul>();

            if (playerSoul != null && playerRoot == null)
                playerRoot = playerSoul.gameObject;

            PlayerSoul resolvedSoul = playerSoul;
            if (resolvedSoul == null && playerRoot != null)
                resolvedSoul = playerRoot.GetComponent<PlayerSoul>();

            if (resolvedSoul != null)
            {
                _boundSoul = resolvedSoul;
                if (playerRoot == null)
                    playerRoot = resolvedSoul.gameObject;
            }
            else
            {
                _boundSoul = null;
            }

            if (autoFindByName)
                AutoAssignUiReferences();
        }

        private void BindSoul()
        {
            if (_boundSoul == null || _subscribedSoul == _boundSoul)
                return;

            UnbindSoul();
            _boundSoul.OnVitalsChanged += HandleVitalsChanged;
            _subscribedSoul = _boundSoul;
        }

        private void UnbindSoul()
        {
            if (_subscribedSoul == null)
                return;

            _subscribedSoul.OnVitalsChanged -= HandleVitalsChanged;
            _subscribedSoul = null;
        }

        private void HandleVitalsChanged(PlayerSoul _)
        {
            Refresh(forceText: false);
        }

        private void Refresh(bool forceText)
        {
            if (_boundSoul == null)
                return;

            SetBarNormalized(healthBar, _boundSoul.HealthNorm);
            if (staminaBar != null)
                staminaBar.gameObject.SetActive(false);

            if (nameText != null)
            {
                string displayName = !string.IsNullOrWhiteSpace(_boundSoul.CharacterName)
                    ? _boundSoul.CharacterName
                    : _boundSoul.gameObject.name;
                if (forceText || nameText.text != displayName)
                    nameText.text = displayName;
            }

            if (healthText != null)
            {
                int current = Mathf.CeilToInt(_boundSoul.Health);
                int max = Mathf.CeilToInt(_boundSoul.MaxHealth);
                healthText.text = $"Health: {current}/{max}";
            }

            if (staminaText != null)
                staminaText.gameObject.SetActive(false);
        }

        private static void SetBarNormalized(Slider bar, float normalized)
        {
            if (bar == null)
                return;

            float clamped = Mathf.Clamp01(normalized);
            float value = Mathf.Lerp(bar.minValue, bar.maxValue, clamped);
            bar.SetValueWithoutNotify(value);
        }

        private void AutoAssignUiReferences()
        {
            healthBar ??= FindSlider("HealthBar", "healthBar");
            staminaBar ??= FindSlider("staminaBar", "StaminaBar", "EnergyBar");
            nameText ??= FindText("nameText", "NameText");
            healthText ??= FindText("hpText", "HealthText", "HPText");
            staminaText ??= FindText("NRGText", "staminaText", "EnergyText");
        }

        private Slider FindSlider(params string[] candidateNames)
        {
            for (int i = 0; i < candidateNames.Length; i++)
            {
                Transform found = FindChild(candidateNames[i]);
                if (found != null && found.TryGetComponent(out Slider slider))
                    return slider;
            }

            return null;
        }

        private TMP_Text FindText(params string[] candidateNames)
        {
            for (int i = 0; i < candidateNames.Length; i++)
            {
                Transform found = FindChild(candidateNames[i]);
                if (found != null && found.TryGetComponent(out TMP_Text text))
                    return text;
            }

            return null;
        }

        private Transform FindChild(string childName)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == childName)
                    return child;

                Transform deep = FindDeep(child, childName);
                if (deep != null)
                    return deep;
            }

            return null;
        }

        private static Transform FindDeep(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child;

                Transform deep = FindDeep(child, childName);
                if (deep != null)
                    return deep;
            }

            return null;
        }
    }
}
