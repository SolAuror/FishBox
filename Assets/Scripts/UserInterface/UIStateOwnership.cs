using System;
using System.Collections.Generic;
using Sol.Locomotion;
using UnityEngine;

namespace Sol.HUD
{
    /// <summary>
    /// Centralized UI modal ownership helpers to keep cursor/input state
    /// consistent and prevent overlapping modal panels.
    /// </summary>
    internal static class UIStateOwnership
    {
        private static readonly Dictionary<Type, MonoBehaviour> _registry = new();

        public static void Register<T>(T instance) where T : MonoBehaviour
        {
            if (instance != null)
                _registry[typeof(T)] = instance;
        }

        public static void Unregister<T>() where T : MonoBehaviour
        {
            _registry.Remove(typeof(T));
        }

        public static T Resolve<T>(bool activateIfInactive) where T : MonoBehaviour
        {
            T instance = GetKnownInstance<T>();
            if (instance != null)
                return instance;

            T[] found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found == null || found.Length == 0)
                return null;

            T resolved = found[0];
            if (resolved != null)
            {
                if (activateIfInactive && !resolved.gameObject.activeSelf)
                    resolved.gameObject.SetActive(true);

                if (!resolved.enabled)
                    resolved.enabled = true;
            }

            return GetKnownInstance<T>() ?? resolved;
        }

        public static T GetKnownInstance<T>() where T : MonoBehaviour
        {
            if (_registry.TryGetValue(typeof(T), out MonoBehaviour instance))
                return instance as T;
            return null;
        }

        public static void CloseConflictingUi(string owner)
        {
            bool openingPauseSubmenu = owner == nameof(SettingsMenuSystem)
                || owner == nameof(SaveLoadMenuSystem);

            if (owner != nameof(InventoryToggle) && InventoryToggle.Instance != null && InventoryToggle.Instance.IsOpen)
                InventoryToggle.Instance.Close();

            if (owner != nameof(TradeUI) && TradeUI.Instance != null && TradeUI.Instance.IsOpen)
                TradeUI.Instance.Close();

            if (owner != nameof(ConversationWindowSystem)
                && ConversationWindowSystem.Instance != null
                && ConversationWindowSystem.Instance.IsVisible)
            {
                ConversationWindowSystem.Instance.Hide();
            }

            if (owner != nameof(ConfirmationPromptSystem) && ConfirmationPromptSystem.Instance != null && ConfirmationPromptSystem.Instance.IsOpen)
                ConfirmationPromptSystem.Instance.Close();

            if (owner != nameof(SaveLoadMenuSystem) && SaveLoadMenuSystem.Instance != null && SaveLoadMenuSystem.Instance.IsOpen)
                SaveLoadMenuSystem.Instance.Close(false);

            if (owner != nameof(SettingsMenuSystem) && SettingsMenuSystem.Instance != null && SettingsMenuSystem.Instance.IsOpen)
                SettingsMenuSystem.Instance.Close(false);

            if (owner != nameof(SleepMenuSystem) && SleepMenuSystem.Instance != null && SleepMenuSystem.Instance.IsOpen)
                SleepMenuSystem.Instance.Close();

            if (owner != nameof(PauseMenuSystem) && PauseMenuSystem.Instance != null && PauseMenuSystem.Instance.IsOpen)
            {
                if (openingPauseSubmenu)
                    PauseMenuSystem.Instance.HideForSubmenu();
                else
                    PauseMenuSystem.Instance.Close();
            }
        }

        public static void SetUiCapture(bool enabled)
        {
            if (enabled)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (LocomotionInputManager.Instance != null)
                    LocomotionInputManager.Instance.UIInputBlocked = true;
                return;
            }

            if (IsBlockingUiOpen())
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (LocomotionInputManager.Instance != null)
                LocomotionInputManager.Instance.UIInputBlocked = false;
        }

        public static bool IsBlockingUiOpen()
        {
            return (InventoryToggle.Instance != null && InventoryToggle.Instance.IsOpen)
                || (TradeUI.Instance != null && TradeUI.Instance.IsOpen)
                || (ConversationWindowSystem.Instance != null && ConversationWindowSystem.Instance.IsVisible)
                || (PauseMenuSystem.Instance != null && PauseMenuSystem.Instance.IsOpen)
                || (SaveLoadMenuSystem.Instance != null && SaveLoadMenuSystem.Instance.IsOpen)
                || (SettingsMenuSystem.Instance != null && SettingsMenuSystem.Instance.IsOpen)
                || (ConfirmationPromptSystem.Instance != null && ConfirmationPromptSystem.Instance.IsOpen)
                || (SleepMenuSystem.Instance != null && SleepMenuSystem.Instance.IsOpen);
        }
    }
}
