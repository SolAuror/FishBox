using System;

namespace Sol.AI
{
    public static class AI_DebugUISettings
    {
        public static bool IsVisible { get; private set; } = true;

        public static event Action<bool> VisibilityChanged;

        public static void SetVisible(bool visible)
        {
            if (IsVisible == visible) return;

            IsVisible = visible;
            VisibilityChanged?.Invoke(visible);
        }

        public static void ToggleVisible()
        {
            SetVisible(!IsVisible);
        }
    }
}
