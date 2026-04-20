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
    public class SaveManager : MonoBehaviour // Singleton responsible for saving and loading game state, including player data, NPC states, world containers, and more. Uses JSON serialization to write save files to disk, and captures screenshots for save previews.
    {
        public static SaveManager Instance { get; private set; } // Singleton instance, accessible via SaveManager.Instance. Set in Awake().

        public const int MaxSlots = 25;
        public const int AutoSaveSlot = 0;

        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");

        [Header("Player Reference")]
        [SerializeField] private GameObject _playerRoot;

        private float _sessionStartTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _sessionStartTime = Time.realtimeSinceStartup;

            // Resolve player reference at runtime
            if (_playerRoot == null)
            {
                PlayerSoul playerSoul = FindFirstObjectByType<PlayerSoul>();
                if (playerSoul != null)
                    _playerRoot = playerSoul.gameObject;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool SaveGame(int slotIndex, string saveName = null) // Returns true if save was successful, false if an error occurred.
        {
            if (!IsValidSlotIndex(slotIndex))
                return false;

            try
            {
                GameSaveData data = CollectSaveData(slotIndex, saveName);
                EnsureSaveDirectory();
                File.WriteAllText(GetSlotPath(slotIndex), JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to save slot {slotIndex}: {ex}");
                return false;
            }
        }

        public bool LoadGame(int slotIndex) // Returns true if load was successful, false if an error occurred or save file was invalid.
        {
            if (!IsValidSlotIndex(slotIndex))
                return false;

            if (!TryReadSaveData(slotIndex, out GameSaveData data) || data == null)
                return false;

            try
            {
                ApplySaveData(data);
                _sessionStartTime = Time.realtimeSinceStartup - Mathf.Max(0f, data.Metadata.PlaytimeSeconds);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to load slot {slotIndex}: {ex}");
                return false;
            }
        }

        public SaveMetadata GetSlotMetadata(int slotIndex) // Returns metadata for the specified slot, or null if the slot index is invalid or save file is missing/invalid. Metadata includes display information like save name, timestamp, in-game date, and playtime, but does not include actual game state data.
        {
            if (!IsValidSlotIndex(slotIndex))
                return null;

            return TryReadSaveData(slotIndex, out GameSaveData data) ? data?.Metadata : null;
        }

        public SaveMetadata[] GetAllSlotMetadata() // Returns an array of metadata for all save slots, with null entries for invalid slots or missing/invalid save files. Used to populate save/load menus with display information about each slot.
        {
            SaveMetadata[] slots = new SaveMetadata[MaxSlots];
            for (int i = 0; i < MaxSlots; i++)
                slots[i] = GetSlotMetadata(i);
            return slots;
        }

        public bool DeleteSave(int slotIndex) // Deletes the save file and associated screenshot for the specified slot. Returns true if deletion was successful, false if an error occurred or slot index is invalid.
        {
            if (!IsValidSlotIndex(slotIndex))
                return false;

            try
            {
                string savePath = GetSlotPath(slotIndex);
                string screenshotPath = GetScreenshotPath(slotIndex);
                if (File.Exists(savePath))
                    File.Delete(savePath);
                if (File.Exists(screenshotPath))
                    File.Delete(screenshotPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to delete slot {slotIndex}: {ex.Message}");
                return false;
            }
        }

        public bool SlotExists(int slotIndex) // Returns true if a valid save file exists for the specified slot index, false otherwise.
        {
            return IsValidSlotIndex(slotIndex) && File.Exists(GetSlotPath(slotIndex));
        }

        public Texture2D LoadScreenshot(int slotIndex) // Loads and returns the screenshot associated with the specified slot index, or null if the slot index is invalid or screenshot file is missing/invalid. The returned Texture2D is a new instance that the caller is responsible for destroying when no longer needed.
        {
            if (!IsValidSlotIndex(slotIndex))
                return null;

            string path = GetScreenshotPath(slotIndex);
            if (!File.Exists(path))
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(bytes);

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;

                return texture; // ALWAYS a new instance → caller must destroy
            }
            catch
            {
                return null;
            }
        }

        public string GetSlotInGameDate(int slotIndex) // Returns the in-game date string for the specified slot index, or an empty string if the slot index is invalid or save file is missing/invalid. Used to display in-game date information in save/load menus.
        {
            if (!TryReadSaveData(slotIndex, out GameSaveData data) || data == null)
                return string.Empty;

            if (data.Metadata != null && !string.IsNullOrWhiteSpace(data.Metadata.InGameDate))
                return data.Metadata.InGameDate.Trim();

            return FormatInGameDate(data.Time);
        }

        public static string FormatPlaytime(float seconds) // Formats a playtime duration in seconds into a human-readable string like "2h 15m" or "45m". Used to display playtime information in save/load menus.
        {
            seconds = Mathf.Max(0f, seconds);
            int totalMinutes = Mathf.FloorToInt(seconds / 60f);
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
        }

        public static string FormatInGameDate(TimeSaveData data) // Formats in-game date information from TimeSaveData into a human-readable string like "Day 5, Harvestmonth, Year 2". Used to display in-game date information in save/load menus.
        {
            if (data == null)
                return string.Empty;

            int day = Mathf.Max(1, data.Day);
            int month = Mathf.Max(1, data.Month);
            int year = data.Year;

            Calendar runtimeCalendar = FindFirstObjectByType<Calendar>();
            string monthName = runtimeCalendar != null
                ? runtimeCalendar.GetMonthName(month)
                : $"Month {month}";

            return $"Day {day}, {monthName}, Year {year}";
        }

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
                WorldItems = CollectWorldItemData()
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

            NPCSoul soul = playerRoot.GetComponent<NPCSoul>();
            if (soul != null)
            {
                data.Health = soul.Health;
                data.MaxHealth = soul.MaxHealth;
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
                        Debug.LogWarning($"[SaveManager] Item '{item.ItemName}' in '{inventory.name}' has no ItemId — it will not be saved. Set an ItemId on the prefab.");
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

            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> pair in equipment.Equipped)
            {
                if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Value.ItemId))
                    continue;

                items.Add(new EquippedItemSaveData
                {
                    SlotType = pair.Key.ToString(),
                    Item = CaptureItemState(pair.Value)
                });
            }

            return items;
        }

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
        }

        private void ApplyTimeData(TimeSaveData data) // Applies in-game time and date information from the provided TimeSaveData to the TimeOfDay and Calendar objects in the scene to restore the saved in-game time state during loading.
        {
            if (data == null)
                return;

            TimeOfDay timeOfDay = FindFirstObjectByType<TimeOfDay>();
            if (timeOfDay != null)
                timeOfDay.CurrentTime = data.CurrentTime;

            Calendar calendar = timeOfDay != null ? timeOfDay.Calendar : FindFirstObjectByType<Calendar>();
            if (calendar != null)
                calendar.SetDate(data.Day, data.Month, data.Year, data.TotalDaysElapsed);
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

            NPCSoul soul = playerRoot.GetComponent<NPCSoul>();
            if (soul != null)
            {
                soul.MaxHealth = data.MaxHealth;
                soul.Health = data.Health;
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
                bool hadKinematic = rb != null && rb.isKinematic;
                if (rb != null) rb.isKinematic = true;
                interactable.transform.SetPositionAndRotation(saved.Position, saved.Rotation);
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = hadKinematic;
                }

                if (saved.IsLocked)
                    inventory.Lock(saved.LockLevel);
                else
                    inventory.Unlock();

                RestoreInventory(inventory, saved.Gold, saved.Items);
            }
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
                    MaxHealth = soul != null ? soul.MaxHealth : 0f
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
                        soul.MaxHealth = saved.MaxHealth;
                    soul.Health = saved.Health;
                }

                Inventory npcInventory = npc.Inventory;
                if (npcInventory != null)
                    RestoreInventory(npcInventory, saved.Gold, saved.InventoryItems);
            }
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
                // on an inventoried actor) is captured by Collect*InventoryItems — skip here.
                if (IsOwnedByInventory(item))
                    continue;

                if (string.IsNullOrWhiteSpace(item.ItemId))
                {
                    Debug.LogWarning($"[SaveManager] World item '{item.gameObject.name}' has no ItemId — skipped.");
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
                    FishCode = saved.FishCode
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

                if (!inventory.Add(item))
                {
                    Destroy(item.gameObject);
                    continue;
                }

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

                    if (!inventory.Add(item))
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

            if (!string.IsNullOrWhiteSpace(data.FishCode))
            {
                CaughtFishItem fishItem = item.GetComponent<CaughtFishItem>();
                CaughtFishData fishData = FishRegistry.Instance.GetFish(data.FishCode);
                if (fishItem != null && fishData != null)
                    fishItem.ConfigureFromData(fishData);
            }

            return item;
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

        private GameObject FindPlayerRoot() // Finds and returns the root GameObject of the player character. This first checks a cached reference for efficiency, then falls back to searching for a PlayerSoul component in the scene if the cached reference is not set. Returns the player root GameObject, or null if it cannot be found.
        {
            // Use the cached reference
            if (_playerRoot != null)
                return _playerRoot;

            // Fallback to component lookup
            PlayerSoul playerSoul = FindFirstObjectByType<PlayerSoul>();
            return playerSoul != null ? playerSoul.gameObject : null;
        }

        private static bool IsValidSlotIndex(int slotIndex) // Returns true if the provided slot index is within the valid range of 0 to MaxSlots - 1, false otherwise. This is used to validate save slot indices for saving and loading operations.
        {
            return slotIndex >= 0 && slotIndex < MaxSlots;
        }

        private static void EnsureSaveDirectory() //    Ensures that the save directory exists on disk, creating it if necessary. This is called before saving to ensure that the save file can be written successfully.
        {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);
        }

        private static string GetSlotPath(int slotIndex) // Returns the file path for the save file corresponding to the specified slot index. This is used to determine where to read/write save data for each slot. The path is typically something like "Saves/slot_0.json" for slot index 0.
        {
            return Path.Combine(SaveDirectory, $"slot_{slotIndex}.json");
        }

        private static string GetScreenshotPath(int slotIndex) // Returns the file path for the screenshot image corresponding to the specified slot index. This is used to determine where to save the screenshot image for each slot. The path is typically something like "Saves/slot_0.png" for slot index 0.
        {
            return Path.Combine(SaveDirectory, $"slot_{slotIndex}.png");
        }

        public void CaptureScreenshotForSlot(int slotIndex) //  Captures a screenshot of the current game view and saves it to disk with a filename corresponding to the specified slot index. This is called after saving to capture a visual thumbnail for the save slot. The screenshot is captured at the end of the current frame to ensure that the UI has updated to reflect the saved state.
        {
            if (IsValidSlotIndex(slotIndex))
                TryCaptureScreenshot(slotIndex);
        }

        private static void TryCaptureScreenshot(int slotIndex) // Attempts to capture a screenshot of the current game view and save it to disk with a filename corresponding to the specified slot index. This is called after saving to capture a visual thumbnail for the save slot. The screenshot is captured at the end of the current frame to ensure that the UI has updated to reflect the saved state.
        {
            try
            {
                EnsureSaveDirectory();

                // Force end-of-frame capture
                string path = GetScreenshotPath(slotIndex);
                ScreenCapture.CaptureScreenshot(path);

                // NOTE:
                // This still happens end-of-frame,
                // but UI now hides correctly thanks to Canvas.ForceUpdateCanvases()
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Screenshot capture failed for slot {slotIndex}: {ex.Message}");
            }
        }

        private static void UpgradeSaveDataIfNeeded(GameSaveData data) // Upgrades the provided GameSaveData to the current save version if it is from an older version. This checks the SaveVersion field in the data and applies any necessary transformations to bring it up to date with the current version. This is called after deserializing save data to ensure compatibility with older saves.
        {
            if (data == null)
                return;

            // Legacy saves from before SaveVersion existed may deserialize as 0.
            if (data.SaveVersion <= 0)
                data.SaveVersion = GameSaveData.InitialVersion;

            if (data.Metadata == null)
                data.Metadata = new SaveMetadata();

            if (data.Player == null)
                data.Player = new PlayerSaveData();

            if (data.Time == null)
                data.Time = new TimeSaveData();

            data.Containers ??= new List<ContainerSaveData>();
            data.NPCs ??= new List<NPCSaveData>();
            data.CaughtFish ??= new List<CaughtFishData>();
            data.WorldItems ??= new List<WorldItemSaveData>();

            switch (data.SaveVersion)
            {
                case 1:
                    // Current version. Nothing to migrate yet.
                    break;
            }

            data.SaveVersion = GameSaveData.CurrentVersion;
        }

        private static bool TryReadSaveData(int slotIndex, out GameSaveData data) // Attempts to read and deserialize the save data for the specified slot index from disk. If successful, returns true and outputs the deserialized GameSaveData. If the slot index is invalid, the file does not exist, or deserialization fails, returns false and outputs null.
        {
            data = null;

            if (!IsValidSlotIndex(slotIndex))
                return false;

            string path = GetSlotPath(slotIndex);
            if (!File.Exists(path))
                return false;

            try
            {
                data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
                if (data == null)
                    return false;

                UpgradeSaveDataIfNeeded(data);

                if (data.SaveVersion > GameSaveData.CurrentVersion)
                {
                    Debug.LogWarning(
                        $"[SaveManager] Save slot {slotIndex} uses newer save version {data.SaveVersion} " +
                        $"than this build supports ({GameSaveData.CurrentVersion}).");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to read save slot {slotIndex}: {ex.Message}");
                return false;
            }
        }

        private static string GetDefaultSaveName(int slotIndex) // Returns a default display name for the specified save slot index. This is used in the UI when a save slot is empty or when displaying the name of the save slot. For the autosave slot, it returns "Autosave". For regular slots, it returns "Save X" where X is the slot index.
        {
            return slotIndex == AutoSaveSlot ? "Autosave" : $"Save {slotIndex}";
        }
    }
}
