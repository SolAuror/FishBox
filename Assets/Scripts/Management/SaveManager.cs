using System;
using System.Collections.Generic;
using System.IO;
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
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public const int MaxSlots = 10;
        public const int AutoSaveSlot = 0;

        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");

        private float _sessionStartTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _sessionStartTime = Time.realtimeSinceStartup;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool SaveGame(int slotIndex, string saveName = null)
        {
            if (!IsValidSlotIndex(slotIndex))
                return false;

            try
            {
                GameSaveData data = CollectSaveData(slotIndex, saveName);
                EnsureSaveDirectory();
                File.WriteAllText(GetSlotPath(slotIndex), JsonUtility.ToJson(data, true));
                TryCaptureScreenshot(slotIndex);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to save slot {slotIndex}: {ex}");
                return false;
            }
        }

        public bool LoadGame(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
                return false;

            string path = GetSlotPath(slotIndex);
            if (!File.Exists(path))
                return false;

            try
            {
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
                if (data == null)
                    return false;

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

        public SaveMetadata GetSlotMetadata(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
                return null;

            string path = GetSlotPath(slotIndex);
            if (!File.Exists(path))
                return null;

            try
            {
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
                return data?.Metadata;
            }
            catch
            {
                return null;
            }
        }

        public SaveMetadata[] GetAllSlotMetadata()
        {
            SaveMetadata[] slots = new SaveMetadata[MaxSlots];
            for (int i = 0; i < MaxSlots; i++)
                slots[i] = GetSlotMetadata(i);
            return slots;
        }

        public bool DeleteSave(int slotIndex)
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

        public bool SlotExists(int slotIndex)
        {
            return IsValidSlotIndex(slotIndex) && File.Exists(GetSlotPath(slotIndex));
        }

        public Texture2D LoadScreenshot(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
                return null;

            string path = GetScreenshotPath(slotIndex);
            if (!File.Exists(path))
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(bytes))
                    return texture;

                Destroy(texture);
            }
            catch
            {
            }

            return null;
        }

        private GameSaveData CollectSaveData(int slotIndex, string saveName)
        {
            return new GameSaveData
            {
                Metadata = new SaveMetadata
                {
                    SlotIndex = slotIndex,
                    SaveName = string.IsNullOrWhiteSpace(saveName) ? $"Save {slotIndex}" : saveName.Trim(),
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    PlaytimeSeconds = Time.realtimeSinceStartup - _sessionStartTime,
                    ScreenshotFileName = $"slot_{slotIndex}.png"
                },
                Player = CollectPlayerData(),
                Time = CollectTimeData(),
                Containers = CollectContainerData(),
                NPCs = CollectNpcData()
            };
        }

        private PlayerSaveData CollectPlayerData()
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

        private TimeSaveData CollectTimeData()
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

        private List<ContainerSaveData> CollectContainerData()
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
                    GameObjectName = interactable.gameObject.name,
                    Position = interactable.transform.position,
                    IsLocked = inventory.IsLocked,
                    LockLevel = inventory.LockLevel,
                    Gold = inventory.Gold + slottedGold,
                    Items = items
                });
            }

            return containers;
        }

        private List<ItemInstanceSaveData> CollectInventoryItems(Inventory inventory, HashSet<ItemComponent> excludedItems, out int goldValue)
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

        private List<EquippedItemSaveData> CollectEquippedItems(Equipment equipment)
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

        private void ApplySaveData(GameSaveData data)
        {
            if (data == null)
                return;

            ApplyTimeData(data.Time);
            ApplyPlayerData(data.Player);
            ApplyContainerData(data.Containers);
            ApplyNpcData(data.NPCs);
        }

        private void ApplyTimeData(TimeSaveData data)
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

        private void ApplyPlayerData(PlayerSaveData data)
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

        private void ApplyContainerData(List<ContainerSaveData> containers)
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

                if (saved.IsLocked)
                    inventory.Lock(saved.LockLevel);
                else
                    inventory.Unlock();

                RestoreInventory(inventory, saved.Gold, saved.Items);
            }
        }

        private List<NPCSaveData> CollectNpcData()
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

        private void ApplyNpcData(List<NPCSaveData> npcs)
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

        private static string GetHierarchyPath(Transform transform)
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
            List<EquippedItemSaveData> equippedItems)
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

        private void RestoreInventory(Inventory inventory, int gold, List<ItemInstanceSaveData> items)
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

        private ItemComponent CreateItemInstance(ItemInstanceSaveData data, Transform parent)
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
            return item;
        }

        private ItemComponent ResolveItemPrefab(string itemId)
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

                ItemComponent candidate = prefabRoot.GetComponent<ItemComponent>();
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.ItemId, normalizedId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
#endif

            return null;
        }

        private void ClearInventory(Inventory inventory)
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

        private static void UnequipAll(Equipment equipment)
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
            return new ItemInstanceSaveData
            {
                ItemId = item.ItemId,
                OwnerId = item.ItemOwnerId,
                IsStolen = item.IsStolen
            };
        }

        private static HashSet<ItemComponent> BuildEquippedItemSet(Equipment equipment)
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

        private static ContainerSaveData FindMatchingContainer(List<ContainerSaveData> containers, ContainerInteractable interactable)
        {
            if (containers == null || interactable == null)
                return null;

            string containerId = interactable.ContainerId;
            if (!string.IsNullOrWhiteSpace(containerId))
            {
                for (int i = 0; i < containers.Count; i++)
                {
                    ContainerSaveData candidate = containers[i];
                    if (candidate != null
                        && string.Equals(candidate.ContainerId, containerId, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            for (int i = 0; i < containers.Count; i++)
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

        private static GameObject FindPlayerRoot()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                return player;

            PlayerSoul playerSoul = FindFirstObjectByType<PlayerSoul>();
            return playerSoul != null ? playerSoul.gameObject : null;
        }

        private static bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < MaxSlots;
        }

        private static void EnsureSaveDirectory()
        {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);
        }

        private static string GetSlotPath(int slotIndex)
        {
            return Path.Combine(SaveDirectory, $"slot_{slotIndex}.json");
        }

        private static string GetScreenshotPath(int slotIndex)
        {
            return Path.Combine(SaveDirectory, $"slot_{slotIndex}.png");
        }

        private static void TryCaptureScreenshot(int slotIndex)
        {
            try
            {
                EnsureSaveDirectory();
                ScreenCapture.CaptureScreenshot(GetScreenshotPath(slotIndex));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Screenshot capture failed for slot {slotIndex}: {ex.Message}");
            }
        }
    }
}
