using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Sol.Actions;
using Sol.Grab;

namespace Sol
{
    /// <summary>
    /// Shared clamped current/max stat used by actor souls.
    /// Keeps health, stamina, and future actor attributes on the same rules.
    /// </summary>
    [Serializable]
    public sealed class SoulStat
    {
        [SerializeField] private float _current;
        [SerializeField] private float _max;

        public SoulStat() : this(100f, 100f)
        {
        }

        public SoulStat(float current, float max)
        {
            Set(current, max);
        }

        public float Current => _current;
        public float Max => _max;
        public float Normalized => _max > 0f ? _current / _max : 0f;
        public bool IsEmpty => _current <= 0f;

        public bool Set(float current, float max)
        {
            float clampedMax = Mathf.Max(1f, max);
            float clampedCurrent = Mathf.Clamp(current, 0f, clampedMax);
            if (Mathf.Approximately(_max, clampedMax)
                && Mathf.Approximately(_current, clampedCurrent))
            {
                return false;
            }

            _max = clampedMax;
            _current = clampedCurrent;
            return true;
        }

        public bool SetMax(float value)
        {
            return Set(_current, value);
        }

        public bool SetCurrent(float value)
        {
            return Set(value, _max);
        }

        public bool Add(float amount)
        {
            return SetCurrent(_current + Mathf.Abs(amount));
        }

        public bool Subtract(float amount)
        {
            return SetCurrent(_current - Mathf.Abs(amount));
        }

        public bool Clamp()
        {
            return Set(_current, _max);
        }
    }

    public static class EntityCodeUtility
    {
        public const string ItemPrefix = "ITM";
        public const string NpcPrefix = "NPC";
        public const string ContainerPrefix = "CNT";
        public const string OwnerPrefix = "OWN";
        public const string DefaultPlayerOwnerId = "PLY00001";

        private const int CodeWidth = 5;
        private const int MaxCodeValue = 99999;

        public static string NormalizeOrEmpty(string rawCode, string prefix)
        {
            if (!TryParse(rawCode, prefix, out int numeric))
                return string.Empty;

            return Format(prefix, numeric);
        }

        public static string Format(string prefix, int numeric)
        {
            return $"{prefix}{numeric.ToString($"D{CodeWidth}")}";
        }

        public static bool TryParse(string rawCode, string prefix, out int numeric)
        {
            numeric = 0;
            if (string.IsNullOrWhiteSpace(rawCode) || string.IsNullOrWhiteSpace(prefix))
                return false;

            string trimmed = rawCode.Trim().ToUpperInvariant();
            string normalizedPrefix = prefix.Trim().ToUpperInvariant();
            if (!trimmed.StartsWith(normalizedPrefix, StringComparison.Ordinal))
                return false;

            string suffix = trimmed.Substring(normalizedPrefix.Length);
            if (suffix.Length != CodeWidth || !int.TryParse(suffix, out int parsed))
                return false;

            if (parsed <= 0 || parsed > MaxCodeValue)
                return false;

            numeric = parsed;
            return true;
        }

#if UNITY_EDITOR
        public static string EnsureAssignedCode<T>(
            T self,
            string currentCode,
            string prefix,
            Func<T, string> codeSelector)
            where T : Component
        {
            string normalized = NormalizeOrEmpty(currentCode, prefix);
            if (!string.IsNullOrEmpty(normalized))
                return normalized;

            int nextCode = FindNextAvailableCode(prefix, codeSelector);
            if (nextCode <= 0)
            {
                Debug.LogWarning(
                    $"[EntityCodeUtility] Unable to assign '{prefix}' code to '{self?.name ?? "Unknown"}'.",
                    self);
                return string.Empty;
            }

            return Format(prefix, nextCode);
        }

        private static int FindNextAvailableCode<T>(string prefix, Func<T, string> codeSelector)
            where T : Component
        {
            bool[] used = new bool[MaxCodeValue + 1];
            T[] loaded = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < loaded.Length; i++)
            {
                T component = loaded[i];
                if (component == null)
                    continue;

                RegisterCode(codeSelector(component), prefix, used);
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                    continue;

                T[] prefabComponents = prefabRoot.GetComponentsInChildren<T>(true);
                for (int j = 0; j < prefabComponents.Length; j++)
                    RegisterCode(codeSelector(prefabComponents[j]), prefix, used);
            }

            for (int i = 1; i <= MaxCodeValue; i++)
            {
                if (!used[i])
                    return i;
            }

            return 0;
        }

        private static void RegisterCode(string rawCode, string prefix, bool[] used)
        {
            if (!TryParse(rawCode, prefix, out int numeric))
                return;

            if (numeric > 0 && numeric < used.Length)
                used[numeric] = true;
        }
#endif
    }

    public static class ItemTypeRules
    {
        public static bool IsConsumableType(ItemType itemType)
        {
            return itemType == ItemType.Consumable
                || itemType == ItemType.Food
                || itemType == ItemType.Drink
                || itemType == ItemType.Potion;
        }

        public static bool IsEquipableType(ItemType itemType)
        {
            return itemType == ItemType.Weapon
                || itemType == ItemType.Armor
                || itemType == ItemType.Equipable;
        }

        public static bool UsesWeaponStats(ItemType itemType) => itemType == ItemType.Weapon;
        public static bool UsesArmorStats(ItemType itemType) => itemType == ItemType.Armor;
        public static bool UsesEquipmentSettings(ItemType itemType) => IsEquipableType(itemType);

        public static bool SupportsAction(ItemType itemType, bool isConsumable, ItemActionType actionType)
        {
            return actionType switch
            {
                ItemActionType.Use => isConsumable || IsConsumableType(itemType),
                ItemActionType.Equip => IsEquipableType(itemType),
                ItemActionType.Drop => true,
                _ => false
            };
        }

        public static bool IsArmorSlot(EquipmentSlotType slotType)
        {
            return slotType == EquipmentSlotType.Head
                || slotType == EquipmentSlotType.Hands
                || slotType == EquipmentSlotType.Chest
                || slotType == EquipmentSlotType.Legs
                || slotType == EquipmentSlotType.Feet
                || slotType == EquipmentSlotType.Back;
        }

        public static bool IsHandSlot(EquipmentSlotType slotType)
        {
            return slotType == EquipmentSlotType.LeftHand
                || slotType == EquipmentSlotType.RightHand;
        }

        public static bool IsSheathSlot(EquipmentSlotType slotType)
        {
            return slotType == EquipmentSlotType.LeftHip
                || slotType == EquipmentSlotType.RightHip
                || slotType == EquipmentSlotType.LeftBack
                || slotType == EquipmentSlotType.RightBack
                || slotType == EquipmentSlotType.BackWaist;
        }

        public static EquipDomain ResolveEquipDomain(ItemComponent item)
        {
            if (item == null)
                return EquipDomain.WeaponTool;

            if (item.EquipCategory != EquipDomain.Auto)
                return item.EquipCategory;

            return item.Type == ItemType.Armor ? EquipDomain.Armor : EquipDomain.WeaponTool;
        }

        public static bool CanItemUseSlot(ItemComponent item, EquipmentSlotType slotType)
        {
            if (item == null || !IsEquipableType(item.Type))
                return false;

            EquipDomain domain = ResolveEquipDomain(item);
            if (domain == EquipDomain.Armor)
                return IsArmorSlot(slotType);

            return IsHandSlot(slotType) || IsSheathSlot(slotType);
        }

        public static bool TryResolveDefaultSlot(ItemComponent item, out EquipmentSlotType slotType)
        {
            slotType = default;
            if (item == null || !IsEquipableType(item.Type))
                return false;

            IReadOnlyList<EquipmentSlotType> allowed = item.AllowedEquipSlots;
            if (allowed != null)
            {
                for (int i = 0; i < allowed.Count; i++)
                {
                    EquipmentSlotType candidate = allowed[i];
                    if (!CanItemUseSlot(item, candidate))
                        continue;

                    slotType = candidate;
                    return true;
                }
            }

            EquipDomain domain = ResolveEquipDomain(item);
            if (domain == EquipDomain.Armor)
            {
                slotType = item.Type switch
                {
                    ItemType.Equipable => EquipmentSlotType.Back,
                    _ => EquipmentSlotType.Chest
                };
                return true;
            }

            if (TryResolveHandSlotFromEquipBone(item, out EquipmentSlotType inferredHandSlot))
            {
                slotType = inferredHandSlot;
                return true;
            }

            slotType = EquipmentSlotType.RightHand;
            return true;
        }

        private static bool TryResolveHandSlotFromEquipBone(ItemComponent item, out EquipmentSlotType slotType)
        {
            slotType = default;
            if (item == null)
                return false;

            string equipBone = item.EquipBone;
            if (string.IsNullOrWhiteSpace(equipBone))
                return false;

            string normalized = equipBone.Trim().ToLowerInvariant();
            if (normalized.Contains("left")
                || normalized.StartsWith("l_")
                || normalized.Contains("_l_")
                || normalized.EndsWith("_l"))
            {
                slotType = EquipmentSlotType.LeftHand;
                return true;
            }

            if (normalized.Contains("right")
                || normalized.StartsWith("r_")
                || normalized.Contains("_r_")
                || normalized.EndsWith("_r"))
            {
                slotType = EquipmentSlotType.RightHand;
                return true;
            }

            return false;
        }

        public static bool GetRequiredSlotsForEquip(ItemComponent item, EquipmentSlotType desiredSlot, List<EquipmentSlotType> outputSlots)
        {
            if (outputSlots == null)
                return false;

            outputSlots.Clear();
            if (!CanItemUseSlot(item, desiredSlot))
                return false;

            IReadOnlyList<EquipmentSlotType> allowed = item.AllowedEquipSlots;
            if (allowed != null && allowed.Count > 0)
            {
                bool explicitlyAllowed = false;
                for (int i = 0; i < allowed.Count; i++)
                {
                    if (allowed[i] != desiredSlot)
                        continue;

                    explicitlyAllowed = true;
                    break;
                }

                if (!explicitlyAllowed)
                    return false;
            }

            if (ResolveEquipDomain(item) == EquipDomain.WeaponTool
                && IsHandSlot(desiredSlot)
                && item.WeaponHanding == WeaponHanding.TwoHanded)
            {
                outputSlots.Add(EquipmentSlotType.LeftHand);
                outputSlots.Add(EquipmentSlotType.RightHand);
                return true;
            }

            outputSlots.Add(desiredSlot);
            return true;
        }
    }

    public static class ItemOwnershipUtility
    {
        public static string NormalizeOwnerIdOrEmpty(string rawOwnerId)
        {
            if (string.IsNullOrWhiteSpace(rawOwnerId))
                return string.Empty;

            string trimmed = rawOwnerId.Trim();
            if (string.Equals(trimmed, EntityCodeUtility.DefaultPlayerOwnerId, StringComparison.OrdinalIgnoreCase))
                return EntityCodeUtility.DefaultPlayerOwnerId;

            return EntityCodeUtility.NormalizeOrEmpty(trimmed, EntityCodeUtility.OwnerPrefix);
        }

        public static string ResolveActorOwnerIdOrEmpty(GameObject actor)
        {
            return NormalizeOwnerIdOrEmpty(OwnerRegistry.ResolveOwnerId(actor));
        }

        public static bool IsOwnedBy(string ownerId, GameObject actor)
        {
            string normalizedOwnerId = NormalizeOwnerIdOrEmpty(ownerId);
            if (string.IsNullOrEmpty(normalizedOwnerId))
                return true;

            string actorOwnerId = ResolveActorOwnerIdOrEmpty(actor);
            return !string.IsNullOrEmpty(actorOwnerId)
                && string.Equals(normalizedOwnerId, actorOwnerId, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemIdDropdownAttribute : PropertyAttribute
    {
        public bool AllowEmpty { get; }

        public ItemIdDropdownAttribute(bool allowEmpty = true)
        {
            AllowEmpty = allowEmpty;
        }
    }

    public sealed class OwnerIdDropdownAttribute : PropertyAttribute
    {
        public bool AllowEmpty { get; }

        public OwnerIdDropdownAttribute(bool allowEmpty = true)
        {
            AllowEmpty = allowEmpty;
        }
    }

    /// <summary>
    /// Draws a string field as an NPC owner-id dropdown (OWN#####).
    /// </summary>
    public sealed class NpcIdDropdownAttribute : PropertyAttribute
    {
        public bool AllowEmpty { get; }

        public NpcIdDropdownAttribute(bool allowEmpty = true)
        {
            AllowEmpty = allowEmpty;
        }
    }

    /// <summary>
    /// Draws a string field as a QuestId dropdown (QST#####).
    /// </summary>
    public sealed class QuestIdDropdownAttribute : PropertyAttribute
    {
        public bool AllowEmpty { get; }

        public QuestIdDropdownAttribute(bool allowEmpty = true)
        {
            AllowEmpty = allowEmpty;
        }
    }

    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(Interactor interactor);

        GameAction GetInteraction(Interactor interactor);
    }
}

