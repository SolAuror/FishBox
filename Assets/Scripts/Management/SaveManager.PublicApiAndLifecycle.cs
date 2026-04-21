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

                return texture; // ALWAYS a new instance -> caller must destroy
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


        private GameObject FindPlayerRoot() // Finds and returns the root GameObject of the player character. This first checks a cached reference for efficiency, then falls back to searching for a PlayerSoul component in the scene if the cached reference is not set. Returns the player root GameObject, or null if it cannot be found.
        {
            // Use the cached reference
            if (_playerRoot != null)
                return _playerRoot;

            // Fallback to component lookup
            PlayerSoul playerSoul = FindFirstObjectByType<PlayerSoul>();
            return playerSoul != null ? playerSoul.gameObject : null;
        }


        public void CaptureScreenshotForSlot(int slotIndex) //  Captures a screenshot of the current game view and saves it to disk with a filename corresponding to the specified slot index. This is called after saving to capture a visual thumbnail for the save slot. The screenshot is captured at the end of the current frame to ensure that the UI has updated to reflect the saved state.
        {
            if (IsValidSlotIndex(slotIndex))
                TryCaptureScreenshot(slotIndex);
        }
    }
}
