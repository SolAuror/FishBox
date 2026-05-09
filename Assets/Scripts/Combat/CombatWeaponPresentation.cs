using System.Collections.Generic;
using Sol.Grab;
using UnityEngine;

namespace Sol.Combat
{
    [AddComponentMenu("Sol/Combat/Combat Weapon Presentation")]
    [DisallowMultipleComponent]
    public class CombatWeaponPresentation : MonoBehaviour
    {
        [Header("Socket Names")]
        [SerializeField] private string _rightHandSocket = "RightHand";
        [SerializeField] private string _leftHandSocket = "LeftHand";
        [SerializeField] private string _rightHipSocket = "RightHip";
        [SerializeField] private string _leftHipSocket = "LeftHip";
        [SerializeField] private string _twoHandedBackSocket = "RightBack";
        [SerializeField] private string _backFallbackSocket = "Back";
        [SerializeField] private string _waistFallbackSocket = "BackWaist";

        private readonly Dictionary<string, Transform> _socketCache = new();
        private Combatant _combatant;

        public bool PresentSheathed()
        {
            if (!TryGetWeapon(out ItemComponent weapon, out EquipmentSlotType slot))
                return true;

            Transform socket = ResolveSheathSocket(weapon, slot);
            if (socket == null)
                return false;

            Attach(weapon, socket);
            return true;
        }

        public bool PresentUnsheathed()
        {
            if (!TryGetWeapon(out ItemComponent weapon, out EquipmentSlotType slot))
                return true;

            Transform socket = ResolveHandSocket(weapon, slot);
            if (socket == null)
                return false;

            Attach(weapon, socket);
            return true;
        }

        private bool TryGetWeapon(out ItemComponent weapon, out EquipmentSlotType slot)
        {
            if (_combatant == null)
                _combatant = Combatant.ResolveOrAdd(gameObject);

            if (_combatant != null && _combatant.TryGetEquippedWeapon(out weapon, out slot))
                return true;

            weapon = null;
            slot = default;
            return false;
        }

        private Transform ResolveHandSocket(ItemComponent weapon, EquipmentSlotType slot)
        {
            if (weapon != null && !string.IsNullOrWhiteSpace(weapon.EquipBone))
            {
                Transform authored = FindSocket(weapon.EquipBone);
                if (authored != null)
                    return authored;
            }

            if (slot == EquipmentSlotType.LeftHand)
                return FindSocket(_leftHandSocket) ?? FindSocket(_rightHandSocket);

            return FindSocket(_rightHandSocket) ?? FindSocket(_leftHandSocket);
        }

        private Transform ResolveSheathSocket(ItemComponent weapon, EquipmentSlotType slot)
        {
            if (weapon != null && weapon.WeaponHanding == WeaponHanding.TwoHanded)
                return FindSocket(_twoHandedBackSocket)
                    ?? FindSocket(_backFallbackSocket)
                    ?? FindSocket(_waistFallbackSocket);

            if (slot == EquipmentSlotType.LeftHand || slot == EquipmentSlotType.LeftHip)
                return FindSocket(_leftHipSocket) ?? FindSocket(_rightHipSocket);

            return FindSocket(_rightHipSocket) ?? FindSocket(_leftHipSocket);
        }

        private Transform FindSocket(string socketName)
        {
            if (string.IsNullOrWhiteSpace(socketName))
                return null;

            if (_socketCache.TryGetValue(socketName, out Transform cached) && cached != null)
                return cached;

            Transform[] transforms = GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == socketName)
                {
                    _socketCache[socketName] = candidate;
                    return candidate;
                }
            }

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && string.Equals(candidate.name, socketName, System.StringComparison.OrdinalIgnoreCase))
                {
                    _socketCache[socketName] = candidate;
                    return candidate;
                }
            }

            _socketCache[socketName] = null;
            return null;
        }

        private static void Attach(ItemComponent item, Transform socket)
        {
            if (item == null || socket == null)
                return;

            Transform itemTransform = item.transform;
            Vector3 worldScale = itemTransform.lossyScale;
            itemTransform.SetParent(socket);
            itemTransform.localPosition = Vector3.zero;
            itemTransform.localRotation = Quaternion.identity;

            Vector3 parentScale = socket.lossyScale;
            itemTransform.localScale = new Vector3(
                parentScale.x != 0f ? worldScale.x / parentScale.x : 1f,
                parentScale.y != 0f ? worldScale.y / parentScale.y : 1f,
                parentScale.z != 0f ? worldScale.z / parentScale.z : 1f);
            item.gameObject.SetActive(true);
        }
    }
}
