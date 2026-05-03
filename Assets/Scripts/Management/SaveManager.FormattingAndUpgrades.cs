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
            data.Quests ??= new List<Sol.Quests.QuestSaveData>();
            for (int i = 0; i < data.NPCs.Count; i++)
            {
                if (data.NPCs[i] != null)
                    data.NPCs[i].ConversationFlags ??= new List<string>();
            }

            switch (data.SaveVersion)
            {
                case 1:
                case 2:
                case 3:
                    // Pre-quest-system saves: quests already defaulted to empty above.
                    break;
            }

            data.SaveVersion = GameSaveData.CurrentVersion;
        }
    }
}
