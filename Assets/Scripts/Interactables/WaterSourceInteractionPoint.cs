using UnityEngine;

namespace Sol
{
    [AddComponentMenu("Sol/Interactables/Water Source Interaction Point")]
    public class WaterSourceInteractionPoint : InteractionPoint
    {
        [Header("Water Source")]
        [Tooltip("Future item id for filling a container. FishBox has no filled-container flow yet.")]
        [ItemIdDropdown]
        [SerializeField] private string _filledContainerItemId = string.Empty;

        protected override void OnUseCompleted(Interactor interactor)
        {
            if (!Mathf.Approximately(ThirstDelta, 0f))
                WarnIfUnsupportedVitalsConfigured();

            if (!string.IsNullOrWhiteSpace(_filledContainerItemId))
            {
                Debug.LogWarning(
                    $"[{nameof(WaterSourceInteractionPoint)}] '{name}' has filled-container item '{_filledContainerItemId}' configured, but FishBox does not yet expose a container-fill interaction flow. TODO: wire this to the future filled-container item API.",
                    this);
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _filledContainerItemId = EntityCodeUtility.NormalizeOrEmpty(_filledContainerItemId, EntityCodeUtility.ItemPrefix);
        }
    }
}
