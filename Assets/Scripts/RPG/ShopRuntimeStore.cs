using System.Collections.Generic;
using UnityEngine;

namespace Sol.Rpg
{
    public static class ShopRuntimeStore
    {
        private static readonly Dictionary<string, ShopRuntimeSession> Sessions = new(System.StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyDictionary<string, ShopRuntimeSession> ActiveSessions => Sessions;

        public static ShopRuntimeSession GetOrCreateSession(string shopId)
        {
            if (string.IsNullOrWhiteSpace(shopId))
                return null;

            string key = shopId.Trim();
            if (Sessions.TryGetValue(key, out ShopRuntimeSession session) && session != null)
            {
                session.TryRestock();
                return session;
            }

            RpgShopDefinition definition = RpgDefinitionRegistry.Get()?.GetShop(key);
            if (definition == null)
            {
                Debug.LogWarning($"[ShopRuntimeStore] Shop '{shopId}' is not in the RPG registry.");
                return null;
            }

            session = new ShopRuntimeSession(definition);
            Sessions[key] = session;
            return session;
        }

        public static void RestoreSession(string shopId, int gold, IReadOnlyList<ShopStockSaveEntry> stock, double lastRestockRealtime, int lastRestockInGameDay)
        {
            ShopRuntimeSession session = GetOrCreateSession(shopId);
            session?.Restore(gold, stock, lastRestockRealtime, lastRestockInGameDay);
        }

        public static void Clear()
        {
            foreach (KeyValuePair<string, ShopRuntimeSession> pair in Sessions)
                pair.Value?.Dispose();
            Sessions.Clear();
        }
    }
}
