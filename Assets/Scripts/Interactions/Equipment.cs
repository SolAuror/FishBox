using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.Grab;
using Sol.Locomotion;

namespace Sol
{
    /// <summary>
    /// Minimal equipment system. Attach to any character with a skeleton.
    /// Uses EquipBone, EquipOffset, and EquipRotation from ItemComponent.
    /// When a MainHand weapon is equipped the IK system is activated automatically.
    /// </summary>
    public class Equipment : MonoBehaviour
    {
        private readonly Dictionary<EquipmentSlotType, ItemComponent> _equipped = new();
        private Dictionary<string, Transform> _boneCache;
        private LocomotionIK _locoIK;

        /// <summary>Read-only view of equipped items.</summary>
        public IReadOnlyDictionary<EquipmentSlotType, ItemComponent> Equipped => _equipped;

        public event Action OnChanged;

        private void Awake()
        {
            _locoIK = GetComponent<LocomotionIK>();
        }

        /// <summary>Equip an item. Determines slot from ItemType. Returns false if slot occupied.</summary>
        public bool Equip(ItemComponent item)
        {
            if (item == null) return false;

            if (!ItemTypeRules.TryGetEquipmentSlot(item.Type, out EquipmentSlotType slot))
                return false;

            if (_equipped.ContainsKey(slot)) return false;

            var bone = FindBone(item.EquipBone);
            if (bone == null)
            {
                Debug.LogWarning($"[Equipment] Bone '{item.EquipBone}' not found on {name}.");
                return false;
            }

            // Attach to bone.
            var t = item.transform;
            
            // Preserve the item's original world scale when reparenting to the bone.
            var worldScale = t.lossyScale;
            t.SetParent(bone);
            t.localPosition = item.EquipOffset;
            t.localRotation = Quaternion.Euler(item.EquipRotation);
            
            // Convert the saved world scale back to local space under the new parent.
            var ps = bone.lossyScale;
            t.localScale = new Vector3(
                ps.x != 0f ? worldScale.x / ps.x : 1f,
                ps.y != 0f ? worldScale.y / ps.y : 1f,
                ps.z != 0f ? worldScale.z / ps.z : 1f);
            item.gameObject.SetActive(true);

            // Disable physics while equipped.
            var rb = item.GetComponent<Rigidbody>();
            if (rb != null) 
            { 
                rb.isKinematic = true; 
                rb.detectCollisions = false; 
                rb.interpolation = RigidbodyInterpolation.None;   
                rb.position = t.position;                         
                rb.rotation = t.rotation;
            }
            var col = item.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Disable any Light components the item was SetActive(false) in inventory,
            // so its lights were off. Re-enabling them on equip would change scene lighting.
            foreach (var lt in item.GetComponentsInChildren<Light>(includeInactive: true))
                lt.enabled = false;

            _equipped[slot] = item;
            OnChanged?.Invoke();

            // Activate hand IK for held weapons.
            if (slot == EquipmentSlotType.MainHand && _locoIK != null)
                _locoIK.SetHandIKTarget(false, bone.position, bone.rotation);

            return true;
        }

        /// <summary>
 /// Unequip and hide the item - it returns to the inventory slot.
        /// Physics stays disabled and the GameObject is deactivated.
        /// </summary>
        public ItemComponent Unequip(EquipmentSlotType slot)
        {
            if (!_equipped.TryGetValue(slot, out var item)) return null;
            _equipped.Remove(slot);

            // Detach from bone.
            item.transform.SetParent(null);

 // Keep physics disabled - item is back in inventory, not in the world.
            var rb = item.GetComponent<Rigidbody>();
            if (rb != null) 
            { 
                rb.isKinematic = true; 
                rb.detectCollisions = false; 
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
            var col = item.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Hide from the game world; the inventory slot still references it.
            item.gameObject.SetActive(false);

            // Deactivate hand IK when unequipping a held weapon.
            if (slot == EquipmentSlotType.MainHand && _locoIK != null)
                _locoIK.ClearHandIKTarget(false);

            OnChanged?.Invoke();
            return item;
        }

        /// <summary>
        /// Detaches the item from its bone and re-enables physics so it can be
        /// dropped into the world. Does NOT deactivate the GameObject.
        /// Called by ItemActionSystem.ExecuteDrop when the item is equipped.
        /// </summary>
        public ItemComponent DetachForDrop(ItemComponent item)
        {
            // Find the slot without modifying the dictionary during enumeration.
            EquipmentSlotType? foundSlot = null;
            foreach (var kv in _equipped)
            {
                if (kv.Value == item) { foundSlot = kv.Key; break; }
            }
            if (foundSlot == null) return null;

            var equipSlot = foundSlot.Value;
            _equipped.Remove(equipSlot);

            item.transform.SetParent(null);

            // Re-enable physics so gravity takes over after the drop.
            var rb = item.GetComponent<Rigidbody>();
            if (rb != null) 
            { 
                rb.isKinematic = false; 
                rb.detectCollisions = true; 
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.position = item.transform.position; 
                rb.rotation = item.transform.rotation;
            }
            var col = item.GetComponent<Collider>();
            if (col != null) col.enabled = true;

            if (equipSlot == EquipmentSlotType.MainHand && _locoIK != null)
                _locoIK.ClearHandIKTarget(false);

            OnChanged?.Invoke();
            return item;
        }

        public bool IsSlotOccupied(EquipmentSlotType slot) => _equipped.ContainsKey(slot);

        /// <summary>Returns true if this exact item instance is currently equipped.</summary>
        public bool IsEquipped(ItemComponent item)
        {
            foreach (var kv in _equipped)
                if (kv.Value == item) return true;
            return false;
        }

        /// <summary>Unequips the slot holding this item instance, if any.</summary>
        public bool UnequipItem(ItemComponent item)
        {
            foreach (var kv in _equipped)
            {
                if (kv.Value == item)
                {
                    Unequip(kv.Key);
                    return true;
                }
            }
            return false;
        }

        private Transform FindBone(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return null;

            if (_boneCache == null)
            {
                var bones = GetComponentsInChildren<Transform>();
                _boneCache = new Dictionary<string, Transform>(bones.Length);
                for (int i = 0; i < bones.Length; i++)
                    _boneCache[bones[i].name] = bones[i];
            }

            return _boneCache.TryGetValue(boneName, out var bone) ? bone : null;
        }

    }
}
