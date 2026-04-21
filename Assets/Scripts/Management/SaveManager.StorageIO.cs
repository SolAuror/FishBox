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
