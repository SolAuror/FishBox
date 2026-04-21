using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Quests;

namespace Sol.SaveLoad
{
    [Serializable]
    public class GameSaveData                               // Root class for all save data. Contains metadata and all relevant game state data for saving and loading.
    {
        public const int InitialVersion = 1;                // First version of the save data structure.
        public const int CurrentVersion = 4;                // Increment when making changes to the save data structure.

        public int SaveVersion = CurrentVersion;            // Used to handle loading old save versions and applying necessary conversions.

        public SaveMetadata Metadata = new();               // Populated at time of saving, not used for loading.
        public PlayerSaveData Player = new();               // Player data, including inventory and equipped items.
        public TimeSaveData Time = new();                   // In-game time and date information.
        public List<ContainerSaveData> Containers = new(); // Data for world containers (chests, barrels, etc.) that can hold items.
        public List<NPCSaveData> NPCs = new();              // Data for NPCs, including position, health, inventory, etc.
        public List<CaughtFishData> CaughtFish = new();     // Data for fish caught by the player, used to populate the fish encyclopedia.
        public List<WorldItemSaveData> WorldItems = new(); // Data for items placed in the world (e.g. dropped items), including position and rotation.
        public List<QuestSaveData> Quests = new();          // Quest runtime states (active/ready/completed/failed). SaveVersion 4.
    }

    [Serializable]
    public class SaveMetadata                               // Metadata about the save, used for display in save/load menus. Not used for loading actual game state.
    {
        public int SlotIndex;
        public string SaveName = string.Empty;
        public string Timestamp = string.Empty;
        public long TimestampTicks;                         //for more reliable sorting
        public string InGameDate = string.Empty;
        public float PlaytimeSeconds;
        public string ScreenshotFileName = string.Empty;
    }

    [Serializable]
    public class PlayerSaveData                             // Data for the player character, including position, health, inventory, etc.
    {
        public SerializableVector3 Position;
        public SerializableQuaternion Rotation;
        public float Health;
        public float MaxHealth;
        public int Gold;
        public List<ItemInstanceSaveData> InventoryItems = new();
        public List<EquippedItemSaveData> EquippedItems = new();
    }

    [Serializable]
    public class TimeSaveData                               // In-game time and date information.
    {
        public float CurrentTime;
        public int Day;
        public int Month;
        public int Year;
        public int TotalDaysElapsed;
    }

    [Serializable]
    public class ContainerSaveData                          // Data for world containers (chests, barrels, etc.) that can hold items. Identified by their hierarchy path in the scene to match them on load.
    {
        public string ContainerId = string.Empty;
        public string HierarchyPath = string.Empty;
        public string GameObjectName = string.Empty;
        public SerializableVector3 Position;
        public SerializableQuaternion Rotation;
        public bool IsLocked;
        public int LockLevel;
        public int Gold;
        public List<ItemInstanceSaveData> Items = new();
    }

    [Serializable]
    public class ItemInstanceSaveData                      // Data for an instance of an item, including its ID, owner (for stolen items), and any relevant state (e.g. fish type for caught fish).
    {
        public string ItemId = string.Empty;
        public string OwnerId = string.Empty;
        public bool IsStolen;
        public string FishCode = string.Empty;
    }

    [Serializable]
    public class WorldItemSaveData                      // Data for items placed in the world (e.g. dropped items), including position and rotation to restore them on load.
    {
        public string ItemId = string.Empty;
        public string OwnerId = string.Empty;
        public bool IsStolen;
        public string FishCode = string.Empty;
        public SerializableVector3 Position;
        public SerializableQuaternion Rotation;
    }

    [Serializable]
    public class EquippedItemSaveData                   // Data for an equipped item, including the equipment slot and the item instance data.
    {
        public string SlotType = string.Empty;
        public ItemInstanceSaveData Item = new();
    }

    [Serializable]
    public class NPCSaveData                            // Data for NPCs, including position, health, inventory, etc. Identified by their hierarchy path in the scene to match them on load.
    {
        /// <summary>Full hierarchy path used to match against the scene NPC.</summary>
        public string NpcId = string.Empty;
        public SerializableVector3 Position;
        public SerializableQuaternion Rotation;
        public float Health;
        public float MaxHealth;
        public int Gold;
        public List<ItemInstanceSaveData> InventoryItems = new();
    }

    [Serializable]
    public struct SerializableVector3                   // Wrapper for Vector3 to make it serializable by Unity's JsonUtility.
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(Vector3 value)       // Constructor to convert from Vector3 to SerializableVector3.
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public Vector3 ToVector3()                      // Method to convert back from SerializableVector3 to Vector3.
        {
            return new Vector3(x, y, z);
        }

        public static implicit operator SerializableVector3(Vector3 value) => new(value);
        public static implicit operator Vector3(SerializableVector3 value) => value.ToVector3();
    }

    [Serializable]
    public struct SerializableQuaternion                // Wrapper for Quaternion to make it serializable by Unity's JsonUtility.
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public SerializableQuaternion(Quaternion value) // Constructor to convert from Quaternion to SerializableQuaternion.
        {
            x = value.x;
            y = value.y;
            z = value.z;
            w = value.w;
        }

        public Quaternion ToQuaternion()                // Method to convert back from SerializableQuaternion to Quaternion.
        {
            return new Quaternion(x, y, z, w);
        }

        public static implicit operator SerializableQuaternion(Quaternion value) => new(value);
        public static implicit operator Quaternion(SerializableQuaternion value) => value.ToQuaternion();
    }
}
