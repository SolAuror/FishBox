using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Sol.AI;
using Sol.Grab;
using Sol.Player;
using Sol.ToD;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol.SaveLoad
{
    public partial class SaveManager : MonoBehaviour
    {

        private GameSaveData CollectSaveData(int slotIndex, string saveName) // Collects all relevant game state data into a GameSaveData object for saving. This includes player data, NPC states, world containers, in-game time, and more.
        {
            TimeSaveData timeData = CollectTimeData();

            return new GameSaveData
            {
                SaveVersion = GameSaveData.CurrentVersion,
                Metadata = new SaveMetadata
                {

                    SlotIndex = slotIndex,
                    SaveName = string.IsNullOrWhiteSpace(saveName) ? GetDefaultSaveName(slotIndex) : saveName.Trim(),
                    Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    TimestampTicks = DateTime.UtcNow.Ticks,
                    InGameDate = FormatInGameDate(timeData),
                    PlaytimeSeconds = Time.realtimeSinceStartup - _sessionStartTime,
                    ScreenshotFileName = $"slot_{slotIndex}.png"
                },
                Player = CollectPlayerData(),
                Time = timeData,
                Containers = CollectContainerData(),
                NPCs = CollectNpcData(),
                CaughtFish = new List<CaughtFishData>(FishRegistry.Instance.GetAllFishData()),
                WorldItems = CollectWorldItemData(),
                Quests = Sol.Quests.QuestManager.Instance != null
                    ? Sol.Quests.QuestManager.Instance.CollectSaveData()
                    : new List<Sol.Quests.QuestSaveData>()
            };
        }


        private PlayerSaveData CollectPlayerData() // Collects data about the player character, including position, rotation, health, inventory items, equipped items, and more, into a PlayerSaveData object for saving.
        {
            PlayerSaveData data = new();
            GameObject playerRoot = FindPlayerRoot();
            if (playerRoot == null)
                return data;

            data.Position = playerRoot.transform.position;
            data.Rotation = playerRoot.transform.rotation;

            PlayerSoul soul = playerRoot.GetComponent<PlayerSoul>();
            if (soul != null)
            {
                data.Health = soul.Health;
                data.MaxHealth = soul.MaxHealth;
                data.Stamina = soul.Stamina;
                data.MaxStamina = soul.MaxStamina;
            }

            Inventory inventory = playerRoot.GetComponent<Inventory>();
            Equipment equipment = playerRoot.GetComponent<Equipment>();
            HashSet<ItemComponent> equippedItems = BuildEquippedItemSet(equipment);

            if (inventory != null)
            {
                int slottedGold;
                data.InventoryItems = CollectInventoryItems(inventory, equippedItems, out slottedGold);
                data.Gold = inventory.Gold + slottedGold;
            }

            data.EquippedItems = CollectEquippedItems(equipment);
            return data;
        }


        private TimeSaveData CollectTimeData() // Collects in-game time and date information from TimeOfDay and Calendar objects into a TimeSaveData object for saving.
        {
            TimeSaveData data = new();
            TimeOfDay timeOfDay = FindFirstObjectByType<TimeOfDay>();
            if (timeOfDay != null)
                data.CurrentTime = timeOfDay.CurrentTime;

            Calendar calendar = timeOfDay != null ? timeOfDay.Calendar : FindFirstObjectByType<Calendar>();
            if (calendar != null)
            {
                data.Day = calendar.Day;
                data.Month = calendar.Month;
                data.Year = calendar.Year;
                data.TotalDaysElapsed = calendar.TotalDaysElapsed;
            }

            return data;
        }


        private List<ContainerSaveData> CollectContainerData() // Collects data for all world containers (chests, barrels, etc.) that can hold items, including their position, rotation, lock state, inventory contents, and more, into a list of ContainerSaveData objects for saving.
        {
            List<ContainerSaveData> containers = new();
            ContainerInteractable[] interactables = FindObjectsByType<ContainerInteractable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < interactables.Length; i++)
            {
                ContainerInteractable interactable = interactables[i];
                if (interactable == null)
                    continue;

                Inventory inventory = interactable.GetComponent<Inventory>();
                if (inventory == null || !inventory.IsWorldContainer)
                    continue;

                int slottedGold;
                List<ItemInstanceSaveData> items = CollectInventoryItems(inventory, null, out slottedGold);

                containers.Add(new ContainerSaveData
                {
                    ContainerId = interactable.ContainerId,
                    HierarchyPath = GetHierarchyPath(interactable.transform),
                    GameObjectName = interactable.gameObject.name,
                    Position = interactable.transform.position,
                    Rotation = interactable.transform.rotation,
                    IsLocked = inventory.IsLocked,
                    LockLevel = inventory.LockLevel,
                    Gold = inventory.Gold + slottedGold,
                    Items = items
                });
            }

            return containers;
        }


        private List<ItemInstanceSaveData> CollectInventoryItems(Inventory inventory, HashSet<ItemComponent> excludedItems, out int goldValue) // Collects data for all items in the specified inventory into a list of ItemInstanceSaveData objects for saving. Also calculates the total gold value of any slotted gold items and returns it via the out parameter.
        {
            List<ItemInstanceSaveData> items = new();
            goldValue = 0;

            if (inventory == null)
                return items;

            IReadOnlyList<InventorySlot> slots = inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot == null)
                    continue;

                foreach (ItemComponent item in slot.EnumerateItems())
                {
                    if (item == null)
                        continue;

                    if (excludedItems != null && excludedItems.Contains(item))
                        continue;

                    if (item.Type == ItemType.Gold)
                    {
                        goldValue += Mathf.Max(1, item.Value);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(item.ItemId))
                    {
 Debug.LogWarning($"[SaveManager] Item '{item.ItemName}' in '{inventory.name}' has no ItemId - it will not be saved. Set an ItemId on the prefab.");
                        continue;
                    }

                    items.Add(CaptureItemState(item));
                }
            }

            return items;
        }


        private List<EquippedItemSaveData> CollectEquippedItems(Equipment equipment) // Collects data for all equipped items on the player character, including their equipment slot and item instance data, into a list of EquippedItemSaveData objects for saving.
        {
            List<EquippedItemSaveData> items = new();
            if (equipment == null)
                return items;

            HashSet<ItemComponent> capturedItems = new();
            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> pair in equipment.Equipped)
            {
                if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Value.ItemId))
                    continue;

                if (!capturedItems.Add(pair.Value))
                    continue;

                EquipmentSlotType slot = equipment.TryGetPrimarySlot(pair.Value, out EquipmentSlotType primarySlot)
                    ? primarySlot
                    : pair.Key;

                items.Add(new EquippedItemSaveData
                {
                    SlotType = slot.ToString(),
                    Item = CaptureItemState(pair.Value)
                });
            }

            return items;
        }


        private List<NPCSaveData> CollectNpcData() // Collects data for all AI NPCs in the scene, including their position, rotation, health, inventory items, and more, into a list of NPCSaveData objects for saving.
        {
            List<NPCSaveData> npcs = new();
            AI_NPC[] allNpcs = FindObjectsByType<AI_NPC>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allNpcs.Length; i++)
            {
                AI_NPC npc = allNpcs[i];
                if (npc == null)
                    continue;

                string npcId = GetHierarchyPath(npc.transform);
                if (string.IsNullOrWhiteSpace(npcId))
                    continue;

                NPCSoul soul = npc.Soul;
                NPCSaveData data = new()
                {
                    NpcId = npcId,
                    Position = npc.transform.position,
                    Rotation = npc.transform.rotation,
                    Health = soul != null ? soul.Health : 0f,
                    MaxHealth = soul != null ? soul.MaxHealth : 0f,
                    Stamina = soul != null ? soul.Stamina : 0f,
                    MaxStamina = soul != null ? soul.MaxStamina : 0f
                };

                Inventory npcInventory = npc.Inventory;
                if (npcInventory != null)
                {
                    int slottedGold;
                    data.InventoryItems = CollectInventoryItems(npcInventory, null, out slottedGold);
                    data.Gold = npcInventory.Gold + slottedGold;
                }

                npcs.Add(data);
            }

            return npcs;
        }


        private List<WorldItemSaveData> CollectWorldItemData() // Collects data for items placed in the world (e.g. dropped items), including position and rotation to restore them on load, into a list of WorldItemSaveData objects for saving.
        {
            List<WorldItemSaveData> worldItems = new();
            ItemComponent[] allItems = FindObjectsByType<ItemComponent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < allItems.Length; i++)
            {
                ItemComponent item = allItems[i];
                if (item == null || !item.gameObject.activeInHierarchy)
                    continue;

                // Anything with an Inventory ancestor (player, NPC, container, equipment bone
 // on an inventoried actor) is captured by Collect*InventoryItems - skip here.
                if (IsOwnedByInventory(item))
                    continue;

                if (string.IsNullOrWhiteSpace(item.ItemId))
                {
 Debug.LogWarning($"[SaveManager] World item '{item.gameObject.name}' has no ItemId - skipped.");
                    continue;
                }

                CaughtFishItem fishItem = item.GetComponent<CaughtFishItem>();
                worldItems.Add(new WorldItemSaveData
                {
                    ItemId = item.ItemId,
                    OwnerId = item.ItemOwnerId,
                    IsStolen = item.IsStolen,
                    FishCode = fishItem != null ? fishItem.FishCode : string.Empty,
                    Position = item.transform.position,
                    Rotation = item.transform.rotation
                });
            }

            return worldItems;
        }


        private static bool IsOwnedByInventory(ItemComponent item) // Returns true if the specified item is parented under an Inventory (e.g. in a player/NPC inventory, container, or equipped on an actor), false otherwise. This is used to determine whether an item should be captured by Collect*InventoryItems or treated as a loose world item.
        {
            return item != null && item.GetComponentInParent<Inventory>(true) != null;
        }


        private static string GetHierarchyPath(Transform transform) // Returns a string representing the hierarchy path of the specified transform (e.g. "Root/Child/Subchild"). Used to uniquely identify NPCs and containers in the scene for saving/loading purposes. If the transform is null, returns an empty string.
        {
            if (transform == null)
                return string.Empty;

            System.Text.StringBuilder sb = new(transform.name);
            Transform parent = transform.parent;
            while (parent != null)
            {
                sb.Insert(0, parent.name + "/");
                parent = parent.parent;
            }

            return sb.ToString();
        }


        private static ItemInstanceSaveData CaptureItemState(ItemComponent item)
        {
            var data = new ItemInstanceSaveData
            {
                ItemId = item.ItemId,
                OwnerId = item.ItemOwnerId,
                IsStolen = item.IsStolen
            };

            CaughtFishItem fishItem = item.GetComponent<CaughtFishItem>();
            if (fishItem != null && !string.IsNullOrWhiteSpace(fishItem.FishCode))
                data.FishCode = fishItem.FishCode;

            return data;
        }


        private static HashSet<ItemComponent> BuildEquippedItemSet(Equipment equipment) // Builds a HashSet of currently equipped ItemComponents from the specified Equipment component for quick lookup. This is used to exclude equipped items when collecting inventory items during saving, since equipped items are captured separately. Returns the HashSet of equipped items, or null if the Equipment component is null or has no equipped items.
        {
            if (equipment == null || equipment.Equipped.Count == 0)
                return null;

            HashSet<ItemComponent> items = new();
            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> pair in equipment.Equipped)
            {
                if (pair.Value != null)
                    items.Add(pair.Value);
            }

            return items;
        }
    }
}
