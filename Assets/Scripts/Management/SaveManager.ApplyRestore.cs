using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Sol.AI;
using Sol.Grab;
using Sol.Player;
using Sol.Rpg;
using Sol.ToD;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol.SaveLoad
{
    public partial class SaveManager : MonoBehaviour
    {

        private void ApplySaveData(GameSaveData data) // Applies the provided GameSaveData to restore game state, including player position, NPC states, world containers, in-game time, and more. This is called during loading to restore the saved game state.
        {
            if (data == null)
                return;

            ApplyTimeData(data.Time);
            // FishRegistry must be loaded before any inventories/world items so fishCode lookups
            // during item restoration resolve to the saved CaughtFishData.
            FishRegistry.Instance.LoadRegistry(data.CaughtFish);
            // Re-link each saved fish's modelPrefab via its FishDefinition (matched by speciesAssetName)
            FishRegistry.Instance.RepopulateRuntimeRefs();
            ApplyPlayerData(data.Player);
            ApplyContainerData(data.Containers);
            ApplyNpcData(data.NPCs);
            ApplyWorldItemData(data.WorldItems);
            ApplyInteractionPointData(data.InteractionPoints);
            Sol.Quests.QuestManager.Instance?.ApplySaveData(data.Quests);
            ApplyShopData(data.Shops);
        }


        private void ApplyTimeData(TimeSaveData data) // Applies in-game time and date information from the provided TimeSaveData to the TimeOfDay and Calendar objects in the scene to restore the saved in-game time state during loading.
        {
            if (data == null)
                return;

            TimeOfDay timeOfDay = TimeOfDay.ResolveInstance();
            if (timeOfDay != null)
            {
                timeOfDay.RestoreTimeSnapshot(
                    data.CurrentTime,
                    data.Day,
                    data.Month,
                    data.Year,
                    data.TotalDaysElapsed,
                    this,
                    "Save Load");
            }
            else
            {
                Calendar calendar = FindFirstObjectByType<Calendar>();
                if (calendar != null)
                    calendar.SetDate(data.Day, data.Month, data.Year, data.TotalDaysElapsed);
            }
        }


        private void ApplyPlayerData(PlayerSaveData data) // Applies data about the player character from the provided PlayerSaveData, including position, rotation, health, inventory items, equipped items, and more, to restore the player's state during loading.
        {
            if (data == null)
                return;

            GameObject playerRoot = FindPlayerRoot();
            if (playerRoot == null)
                return;

            CharacterController controller = playerRoot.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;

            playerRoot.transform.position = data.Position;
            playerRoot.transform.rotation = data.Rotation;

            if (controller != null)
                controller.enabled = true;

            PlayerSoul soul = playerRoot.GetComponent<PlayerSoul>();
            if (soul != null)
            {
                if (data.MaxHealth > 0f)
                {
                    soul.MaxHealth = data.MaxHealth;
                    soul.Health = data.Health;
                }
                else if (data.Health > 0f)
                {
                    soul.Health = data.Health;
                }

                if (data.MaxStamina > 0f)
                {
                    soul.MaxStamina = data.MaxStamina;
                    soul.Stamina = data.Stamina;
                }

                if (data.TagPaths != null && data.TagPaths.Count > 0)
                    soul.ApplySavedTagPaths(data.TagPaths);
            }

            RestoreInventoryAndEquipment(
                playerRoot,
                data.Gold,
                data.InventoryItems,
                data.EquippedItems);
        }


        private void ApplyContainerData(List<ContainerSaveData> containers) // Applies data for world containers from the provided list of ContainerSaveData, including their position, rotation, lock state, inventory contents, and more, to restore the state of world containers during loading.
        {
            if (containers == null || containers.Count == 0)
                return;

            ContainerInteractable[] interactables = FindObjectsByType<ContainerInteractable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < interactables.Length; i++)
            {
                ContainerInteractable interactable = interactables[i];
                if (interactable == null)
                    continue;

                ContainerSaveData saved = FindMatchingContainer(containers, interactable);
                if (saved == null)
                    continue;

                Inventory inventory = interactable.GetComponent<Inventory>();
                if (inventory == null)
                    continue;

                Rigidbody rb = interactable.GetComponent<Rigidbody>();

                if (rb != null)
                {
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.position = saved.Position;
                    rb.rotation = saved.Rotation;
                    Physics.SyncTransforms();
                }
                else
                {
                    interactable.transform.SetPositionAndRotation(saved.Position, saved.Rotation);
                }

                if (saved.IsLocked)
                    inventory.Lock(saved.LockLevel);
                else
                    inventory.Unlock();
                if (saved.TagPaths != null && saved.TagPaths.Count > 0)
                    inventory.ApplySavedTagPaths(saved.TagPaths);

                RestoreInventory(inventory, saved.Gold, saved.Items);
            }
        }


        private void ApplyNpcData(List<NPCSaveData> npcs) // Applies data for AI NPCs from the provided list of NPCSaveData, including their position, rotation, health, inventory items, and more, to restore the state of NPCs during loading.
        {
            if (npcs == null || npcs.Count == 0)
                return;

            AI_NPC[] allNpcs = FindObjectsByType<AI_NPC>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allNpcs.Length; i++)
            {
                AI_NPC npc = allNpcs[i];
                if (npc == null)
                    continue;

                string npcId = GetHierarchyPath(npc.transform);
                NPCSaveData saved = null;
                for (int j = 0; j < npcs.Count; j++)
                {
                    if (string.Equals(npcs[j].NpcId, npcId, StringComparison.Ordinal))
                    {
                        saved = npcs[j];
                        break;
                    }
                }

                if (saved == null)
                    continue;

                NavMeshAgent agent = npc.Agent;
                if (agent != null)
                    agent.Warp(saved.Position);
                else
                    npc.transform.position = saved.Position;

                npc.transform.rotation = saved.Rotation;

                NPCSoul soul = npc.Soul;
                if (soul != null)
                {
                    if (saved.MaxHealth > 0f)
                    {
                        soul.MaxHealth = saved.MaxHealth;
                        soul.Health = saved.Health;
                    }
                    else if (saved.Health > 0f)
                    {
                        soul.Health = saved.Health;
                    }

                    if (saved.MaxStamina > 0f)
                    {
                        soul.MaxStamina = saved.MaxStamina;
                        soul.Stamina = saved.Stamina;
                    }

                    if (saved.HasRuntimeFlags)
                    {
                        if (saved.TagPaths != null && saved.TagPaths.Count > 0)
                            soul.ApplySavedTagPaths(saved.TagPaths);
                        else
                        {
                            soul.CanTrade = saved.CanTrade;
                            soul.IsHostile = saved.IsHostile;
                        }
                    }
                }

                if (saved.HasRuntimeFlags)
                    npc.ApplyConversationFlags(saved.ConversationFlags);

                Inventory npcInventory = npc.Inventory;
                if (npcInventory != null)
                    RestoreInventory(npcInventory, saved.Gold, saved.InventoryItems);

                if (npc.ScheduleDefinition != null)
                    npc.SnapToCurrentScheduleTarget();
            }
        }


        private void ApplyWorldItemData(List<WorldItemSaveData> worldItems) // Applies data for items placed in the world (e.g. dropped items) from the provided list of WorldItemSaveData, including position and rotation to restore them on load.
        {
            // Destroy all current loose world items before restoring saved state.
            // Items owned by an Inventory were already reset by ApplyPlayerData /
            // ApplyContainerData / ApplyNpcData, so we leave those alone.
            ItemComponent[] existing = FindObjectsByType<ItemComponent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                ItemComponent item = existing[i];
                if (item != null && item.gameObject.activeInHierarchy && !IsOwnedByInventory(item))
                    Destroy(item.gameObject);
            }

            if (worldItems == null || worldItems.Count == 0)
                return;

            for (int i = 0; i < worldItems.Count; i++)
            {
                WorldItemSaveData saved = worldItems[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.ItemId))
                    continue;

                ItemInstanceSaveData instanceData = new()
                {
                    ItemId = saved.ItemId,
                    OwnerId = saved.OwnerId,
                    IsStolen = saved.IsStolen,
                    FishCode = saved.FishCode,
                    TagPaths = saved.TagPaths
                };

                ItemComponent item = CreateItemInstance(instanceData, null);
                if (item == null)
                    continue;

                item.transform.position = saved.Position;
                item.transform.rotation = saved.Rotation;
                item.gameObject.SetActive(true);

                Rigidbody rb = item.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.detectCollisions = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                Collider col = item.GetComponent<Collider>();
                if (col != null)
                    col.enabled = true;
            }
        }

        private void ApplyShopData(List<ShopSaveData> shops)
        {
            ShopRuntimeStore.Clear();
            if (shops == null || shops.Count == 0)
                return;

            for (int i = 0; i < shops.Count; i++)
            {
                ShopSaveData shop = shops[i];
                if (shop == null || string.IsNullOrWhiteSpace(shop.ShopId))
                    continue;

                ShopRuntimeStore.RestoreSession(
                    shop.ShopId,
                    shop.Gold,
                    shop.Stock,
                    shop.LastRestockRealtime,
                    shop.LastRestockInGameDay);
            }
        }


        private void RestoreInventoryAndEquipment(
            GameObject owner,
            int gold,
            List<ItemInstanceSaveData> inventoryItems,
            List<EquippedItemSaveData> equippedItems)  // Restores the player's inventory and equipped items from the provided save data. This first clears the player's current inventory and equipment, then restores inventory items (excluding equipped items), and finally restores equipped items to ensure they are properly equipped after being added to the inventory.
        {
            if (owner == null)
                return;

            Inventory inventory = owner.GetComponent<Inventory>();
            if (inventory == null)
                return;

            Equipment equipment = owner.GetComponent<Equipment>();
            UnequipAll(equipment);
            RestoreInventory(inventory, gold, inventoryItems);

            if (equipment == null || equippedItems == null)
                return;

            for (int i = 0; i < equippedItems.Count; i++)
            {
                EquippedItemSaveData saved = equippedItems[i];
                if (saved?.Item == null)
                    continue;

                ItemComponent item = CreateItemInstance(saved.Item, inventory.transform);
                if (item == null)
                    continue;

                EquipmentSlotType? preferredSlot = null;
                if (TryParseEquipmentSlot(saved.SlotType, out EquipmentSlotType restoredSlot))
                    preferredSlot = ResolveRestoredEquipmentSlot(item, restoredSlot);

                if (preferredSlot.HasValue
                    && !equipment.CanEquip(item, preferredSlot.Value)
                    && HasEquivalentEquippedItem(equipment, saved.Item))
                {
                    Destroy(item.gameObject);
                    continue;
                }

                if (!inventory.Add(item, InventoryAddOwnershipMode.PreserveExistingOwner))
                {
                    Destroy(item.gameObject);
                    continue;
                }

                if (preferredSlot.HasValue)
                    equipment.Equip(item, preferredSlot.Value);
                else
                    equipment.Equip(item);
            }
        }


        private void RestoreInventory(Inventory inventory, int gold, List<ItemInstanceSaveData> items) // Restores the contents of the specified inventory from the provided gold amount and list of item save data. This first clears the inventory, then adds the specified gold and items back into it. Used for restoring player, NPC, and container inventories during loading.
        {
            if (inventory == null)
                return;

            ClearInventory(inventory);
            inventory.BeginBulkUpdate();
            inventory.Gold = Mathf.Max(0, gold);

            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ItemComponent item = CreateItemInstance(items[i], inventory.transform);
                    if (item == null)
                        continue;

                    if (!inventory.Add(item, InventoryAddOwnershipMode.PreserveExistingOwner))
                        Destroy(item.gameObject);
                }
            }

            inventory.EndBulkUpdate();
        }


        private ItemComponent CreateItemInstance(ItemInstanceSaveData data, Transform parent) // Creates a new instance of an item based on the provided ItemInstanceSaveData, using the ItemId to find the corresponding prefab. The new item is parented under the specified transform (e.g. an inventory) if provided. Returns the created ItemComponent, or null if creation failed (e.g. due to missing prefab).
        {
            if (data == null || string.IsNullOrWhiteSpace(data.ItemId))
                return null;

            ItemComponent prefab = ResolveItemPrefab(data.ItemId);
            if (prefab == null)
            {
                Debug.LogWarning($"[SaveManager] Item prefab not found for '{data.ItemId}'.");
                return null;
            }

            ItemComponent item = Instantiate(prefab, parent);
            item.gameObject.SetActive(false);
            item.SetOwnerId(data.OwnerId);
            item.SetStolen(data.IsStolen);
            if (data.TagPaths != null && data.TagPaths.Count > 0)
                item.ApplySavedTagPaths(data.TagPaths);

            if (!string.IsNullOrWhiteSpace(data.FishCode))
            {
                CaughtFishItem fishItem = item.GetComponent<CaughtFishItem>();
                CaughtFishData fishData = FishRegistry.Instance.GetFish(data.FishCode);
                if (fishItem != null && fishData != null)
                    fishItem.ConfigureFromData(fishData);
            }

            return item;
        }

        private void ApplyInteractionPointData(List<InteractionPointSaveData> points)
        {
            if (points == null || points.Count == 0)
                return;

            InteractionPoint[] scenePoints = FindObjectsByType<InteractionPoint>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < scenePoints.Length; i++)
            {
                InteractionPoint point = scenePoints[i];
                if (point == null)
                    continue;

                InteractionPointSaveData saved = FindMatchingInteractionPoint(points, point);
                if (saved?.TagPaths == null || saved.TagPaths.Count == 0)
                    continue;

                point.ApplySavedTagPaths(saved.TagPaths);
            }
        }


        private ItemComponent ResolveItemPrefab(string itemId) // Resolves the item prefab corresponding to the specified ItemId. This first checks the ItemRegistry for a prefab match, then falls back to an editor-only search through all prefabs in the project for an ItemComponent with a matching ItemId. Returns the resolved ItemComponent prefab, or null if no matching prefab is found.
        {
            ItemRegistry registry = ItemRegistry.Get();
            ItemComponent prefab = registry != null ? registry.GetPrefab(itemId) : null;
            if (prefab != null)
                return prefab;

#if UNITY_EDITOR
            string normalizedId = itemId.Trim();
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                    continue;

                ItemComponent[] candidates = prefabRoot.GetComponentsInChildren<ItemComponent>(true);
                for (int j = 0; j < candidates.Length; j++)
                {
                    ItemComponent candidate = candidates[j];
                    if (candidate == null)
                        continue;

                    if (string.Equals(candidate.ItemId, normalizedId, StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }
#endif

            return null;
        }


        private void ClearInventory(Inventory inventory) // Clears all items and gold from the specified inventory, destroying item GameObjects as needed. Used before restoring inventory contents during loading to ensure a clean slate.
        {
            if (inventory == null)
                return;

            inventory.BeginBulkUpdate();
            while (inventory.Slots.Count > 0)
            {
                InventorySlot slot = inventory.Slots[inventory.Slots.Count - 1];
                if (slot == null)
                    break;

                foreach (ItemComponent item in slot.EnumerateItems())
                {
                    if (item != null)
                        Destroy(item.gameObject);
                }

                inventory.Remove(slot, slot.Count);
            }
            inventory.Gold = 0;
            inventory.EndBulkUpdate();
        }


        private static void UnequipAll(Equipment equipment) // Unequips all currently equipped items from the specified Equipment component, if any. This is used before restoring equipped items during loading to ensure a clean slate.
        {
            if (equipment == null || equipment.Equipped.Count == 0)
                return;

            List<EquipmentSlotType> occupiedSlots = new();
            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> pair in equipment.Equipped)
            {
                if (pair.Value != null)
                    occupiedSlots.Add(pair.Key);
            }

            for (int i = 0; i < occupiedSlots.Count; i++)
                equipment.Unequip(occupiedSlots[i]);
        }


        private static bool TryParseEquipmentSlot(string rawSlotType, out EquipmentSlotType slot)
        {
            if (Enum.TryParse(rawSlotType, true, out slot))
                return true;

            if (string.IsNullOrWhiteSpace(rawSlotType))
                return false;

            if (string.Equals(rawSlotType, "MainHand", StringComparison.OrdinalIgnoreCase))
            {
                slot = EquipmentSlotType.RightHand;
                return true;
            }

            if (string.Equals(rawSlotType, "OffHand", StringComparison.OrdinalIgnoreCase))
            {
                slot = EquipmentSlotType.LeftHand;
                return true;
            }

            if (string.Equals(rawSlotType, "Back", StringComparison.OrdinalIgnoreCase))
            {
                slot = EquipmentSlotType.Back;
                return true;
            }

            return false;
        }

        private static EquipmentSlotType ResolveRestoredEquipmentSlot(ItemComponent item, EquipmentSlotType restoredSlot)
        {
            if (ItemTypeRules.IsHandSlot(restoredSlot)
                && TryInferHandSlotFromEquipBone(item, out EquipmentSlotType boneSlot))
            {
                return boneSlot;
            }

            return restoredSlot;
        }

        private static bool TryInferHandSlotFromEquipBone(ItemComponent item, out EquipmentSlotType slot)
        {
            slot = default;
            if (item == null || string.IsNullOrWhiteSpace(item.EquipBone))
                return false;

            string normalized = item.EquipBone.Trim().ToLowerInvariant();
            if (normalized.Contains("left")
                || normalized.StartsWith("l_")
                || normalized.Contains("_l_")
                || normalized.EndsWith("_l"))
            {
                slot = EquipmentSlotType.LeftHand;
                return true;
            }

            if (normalized.Contains("right")
                || normalized.StartsWith("r_")
                || normalized.Contains("_r_")
                || normalized.EndsWith("_r"))
            {
                slot = EquipmentSlotType.RightHand;
                return true;
            }

            return false;
        }

        private static bool HasEquivalentEquippedItem(Equipment equipment, ItemInstanceSaveData savedItem)
        {
            if (equipment == null || savedItem == null || string.IsNullOrWhiteSpace(savedItem.ItemId))
                return false;

            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> pair in equipment.Equipped)
            {
                ItemComponent item = pair.Value;
                if (item == null || !string.Equals(item.ItemId, savedItem.ItemId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(savedItem.FishCode))
                    return true;

                CaughtFishItem fishItem = item.GetComponent<CaughtFishItem>();
                if (fishItem != null && string.Equals(fishItem.FishCode, savedItem.FishCode, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static ContainerSaveData FindMatchingContainer(List<ContainerSaveData> containers, ContainerInteractable interactable) // Finds the best matching ContainerSaveData from the provided list of containers for the specified ContainerInteractable. This first tries to match by hierarchy path, then falls back to matching by ContainerId (if unique), and finally falls back to matching by name and proximity. Returns the best matching ContainerSaveData, or null if no match is found.
        {
            if (containers == null || interactable == null)
                return null;

            // Preferred: hierarchy path. Stable per scene-placed instance, survives the
            // prefab-wide shared ContainerId problem where every crate authored from the
            // same prefab gets CNT##### baked in.
            string hierarchyPath = GetHierarchyPath(interactable.transform);
            if (!string.IsNullOrWhiteSpace(hierarchyPath))
            {
                for (int i = 0; i < containers.Count; i++)
                {
                    ContainerSaveData candidate = containers[i];
                    if (candidate != null
                        && string.Equals(candidate.HierarchyPath, hierarchyPath, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            // Fallback for legacy saves: ContainerId is unique only if authored that way.
            string containerId = interactable.ContainerId;
            if (!string.IsNullOrWhiteSpace(containerId)) // Only use ContainerId if it's non-empty, to avoid matching every crate with the default CNT##### ID.
            {
                int matchCount = 0;
                ContainerSaveData firstIdMatch = null;
                for (int i = 0; i < containers.Count; i++)
                {
                    ContainerSaveData candidate = containers[i];
                    if (candidate != null
                        && string.Equals(candidate.ContainerId, containerId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (firstIdMatch == null) firstIdMatch = candidate;
                        matchCount++;
                    }
                }
                if (matchCount == 1)
                    return firstIdMatch;
            }

            // Final fallback: name + proximity (works when the crate hasn't been moved).
            for (int i = 0; i < containers.Count; i++) // Iterate in order, so the first match wins if there are multiple with the same name.
            {
                ContainerSaveData candidate = containers[i];
                if (candidate == null)
                    continue;

                bool nameMatches = string.Equals(candidate.GameObjectName, interactable.gameObject.name, StringComparison.Ordinal);
                bool positionMatches = Vector3.Distance(candidate.Position, interactable.transform.position) <= 0.5f;
                if (nameMatches && positionMatches)
                    return candidate;
            }

            return null;
        }

        private static InteractionPointSaveData FindMatchingInteractionPoint(List<InteractionPointSaveData> points, InteractionPoint point)
        {
            if (points == null || point == null)
                return null;

            string hierarchyPath = GetHierarchyPath(point.transform);
            if (!string.IsNullOrWhiteSpace(hierarchyPath))
            {
                for (int i = 0; i < points.Count; i++)
                {
                    InteractionPointSaveData candidate = points[i];
                    if (candidate != null
                        && string.Equals(candidate.HierarchyPath, hierarchyPath, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            string pointId = point.InteractionPointId;
            if (!string.IsNullOrWhiteSpace(pointId))
            {
                int matchCount = 0;
                InteractionPointSaveData firstIdMatch = null;
                for (int i = 0; i < points.Count; i++)
                {
                    InteractionPointSaveData candidate = points[i];
                    if (candidate != null
                        && string.Equals(candidate.InteractionPointId, pointId, StringComparison.OrdinalIgnoreCase))
                    {
                        firstIdMatch ??= candidate;
                        matchCount++;
                    }
                }

                if (matchCount == 1)
                    return firstIdMatch;
            }

            for (int i = 0; i < points.Count; i++)
            {
                InteractionPointSaveData candidate = points[i];
                if (candidate != null
                    && string.Equals(candidate.GameObjectName, point.gameObject.name, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
