using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.SaveLoad
{
    [Serializable]
    public class GameSaveData
    {
        public SaveMetadata Metadata = new();
        public PlayerSaveData Player = new();
        public TimeSaveData Time = new();
        public List<ContainerSaveData> Containers = new();
        public List<NPCSaveData> NPCs = new();
    }

    [Serializable]
    public class SaveMetadata
    {
        public int SlotIndex;
        public string SaveName = string.Empty;
        public string Timestamp = string.Empty;
        public string InGameDate = string.Empty;
        public float PlaytimeSeconds;
        public string ScreenshotFileName = string.Empty;
    }

    [Serializable]
    public class PlayerSaveData
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
    public class TimeSaveData
    {
        public float CurrentTime;
        public int Day;
        public int Month;
        public int Year;
        public int TotalDaysElapsed;
    }

    [Serializable]
    public class ContainerSaveData
    {
        public string ContainerId = string.Empty;
        public string GameObjectName = string.Empty;
        public SerializableVector3 Position;
        public bool IsLocked;
        public int LockLevel;
        public int Gold;
        public List<ItemInstanceSaveData> Items = new();
    }

    [Serializable]
    public class ItemInstanceSaveData
    {
        public string ItemId = string.Empty;
        public string OwnerId = string.Empty;
        public bool IsStolen;
    }

    [Serializable]
    public class EquippedItemSaveData
    {
        public string SlotType = string.Empty;
        public ItemInstanceSaveData Item = new();
    }

    [Serializable]
    public class NPCSaveData
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
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }

        public static implicit operator SerializableVector3(Vector3 value) => new(value);
        public static implicit operator Vector3(SerializableVector3 value) => value.ToVector3();
    }

    [Serializable]
    public struct SerializableQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public SerializableQuaternion(Quaternion value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
            w = value.w;
        }

        public Quaternion ToQuaternion()
        {
            return new Quaternion(x, y, z, w);
        }

        public static implicit operator SerializableQuaternion(Quaternion value) => new(value);
        public static implicit operator Quaternion(SerializableQuaternion value) => value.ToQuaternion();
    }
}
