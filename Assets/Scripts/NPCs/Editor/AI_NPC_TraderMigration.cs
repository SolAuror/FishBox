#if UNITY_EDITOR
#pragma warning disable CS0618 // legacy NpcTrader is intentionally referenced for migration

using System.Collections.Generic;
using Sol.AI;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    /// <summary>
    /// One-shot migration: copy serialized fields from the legacy NpcTrader component
    /// onto AI_NPC partials (AI_NPC.Conversations.cs / AI_NPC.Shop.cs) for every prefab
    /// in the project, then destroy the NpcTrader component. Idempotent — re-running on
    /// already-migrated prefabs is a no-op (no NpcTrader components remain to migrate).
    ///
    /// Runs once on the first domain reload after this PR via [InitializeOnLoad], gated
    /// by an EditorPrefs flag, and is also exposed as a manual menu item.
    /// </summary>
    [InitializeOnLoad]
    public static class AI_NPC_TraderMigration
    {
        private const string AutoRunPrefKey = "Sol.NPCEditor.TraderMigration.AutoRanV1";
        private const string MenuPath = "Tools/Sol/NPCs/Migrate NpcTrader → AI_NPC";

        // Field-name pairs: (NpcTrader field) -> (AI_NPC field). Both share the same names
        // but we list them explicitly so the migration is auditable.
        private static readonly (string From, string To)[] StringFields =
        {
            ("_prompt", "_prompt"),
            ("_lootPrompt", "_lootPrompt"),
            ("_talkPrompt", "_talkPrompt"),
            ("_greetingLine", "_greetingLine"),
            ("_tradeOptionLabel", "_tradeOptionLabel"),
            ("_goodbyeOptionLabel", "_goodbyeOptionLabel"),
            ("_goldLootItemId", "_goldLootItemId"),
        };

        private static readonly (string From, string To)[] BoolFields =
        {
            ("_useConversationWindow", "_useConversationWindow"),
        };

        private static readonly (string From, string To)[] ObjectFields =
        {
            ("_speakerIcon", "_speakerIcon"),
            ("_goldLootItemTemplate", "_goldLootItemTemplate"),
        };

        private static readonly (string From, string To)[] IntFields =
        {
            ("_maxGoldItemizeAttemptsPerOpen", "_maxGoldItemizeAttemptsPerOpen"),
        };

        static AI_NPC_TraderMigration()
        {
            EditorApplication.delayCall += AutoRunOnce;
        }

        private static void AutoRunOnce()
        {
            if (EditorPrefs.GetBool(AutoRunPrefKey, false))
                return;

            int migrated = MigrateAll(silent: true);
            EditorPrefs.SetBool(AutoRunPrefKey, true);
            if (migrated > 0)
                Debug.Log($"[AI_NPC_TraderMigration] Auto-migrated {migrated} prefab(s) from NpcTrader to AI_NPC.");
        }

        [MenuItem(MenuPath)]
        public static void RunFromMenu()
        {
            int migrated = MigrateAll(silent: false);
            EditorUtility.DisplayDialog(
                "NpcTrader Migration",
                migrated == 0
                    ? "No prefabs needed migration."
                    : $"Migrated {migrated} prefab(s).",
                "OK");
        }

        public static int MigrateAll(bool silent)
        {
            int migratedCount = 0;
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < prefabGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    if (string.IsNullOrEmpty(path))
                        continue;

                    if (!silent)
                    {
                        EditorUtility.DisplayProgressBar(
                            "NpcTrader Migration",
                            path,
                            (float)i / Mathf.Max(1, prefabGuids.Length));
                    }

                    if (MigratePrefab(path))
                        migratedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                if (!silent)
                    EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            return migratedCount;
        }

        private static bool MigratePrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
                return false;

            try
            {
                NpcTrader[] traders = root.GetComponentsInChildren<NpcTrader>(true);
                if (traders == null || traders.Length == 0)
                    return false;

                bool anyMigrated = false;
                List<NpcTrader> toDestroy = new();

                for (int i = 0; i < traders.Length; i++)
                {
                    NpcTrader trader = traders[i];
                    if (trader == null) continue;

                    GameObject host = trader.gameObject;
                    AI_NPC aiNpc = host.GetComponent<AI_NPC>();
                    if (aiNpc == null)
                    {
                        Debug.LogWarning(
                            $"[AI_NPC_TraderMigration] Prefab '{path}' has NpcTrader on '{host.name}' but no AI_NPC. Skipping (manual fix needed).",
                            trader);
                        continue;
                    }

                    CopyFields(trader, aiNpc);
                    EnableTraderFlag(host);
                    toDestroy.Add(trader);
                    anyMigrated = true;
                }

                if (!anyMigrated)
                    return false;

                for (int i = 0; i < toDestroy.Count; i++)
                    Object.DestroyImmediate(toDestroy[i], allowDestroyingAssets: true);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CopyFields(NpcTrader trader, AI_NPC aiNpc)
        {
            SerializedObject from = new(trader);
            SerializedObject to = new(aiNpc);
            from.Update();
            to.Update();

            for (int i = 0; i < StringFields.Length; i++)
            {
                SerializedProperty f = from.FindProperty(StringFields[i].From);
                SerializedProperty t = to.FindProperty(StringFields[i].To);
                if (f != null && t != null && f.propertyType == SerializedPropertyType.String && t.propertyType == SerializedPropertyType.String)
                    t.stringValue = f.stringValue;
            }
            for (int i = 0; i < BoolFields.Length; i++)
            {
                SerializedProperty f = from.FindProperty(BoolFields[i].From);
                SerializedProperty t = to.FindProperty(BoolFields[i].To);
                if (f != null && t != null && f.propertyType == SerializedPropertyType.Boolean && t.propertyType == SerializedPropertyType.Boolean)
                    t.boolValue = f.boolValue;
            }
            for (int i = 0; i < ObjectFields.Length; i++)
            {
                SerializedProperty f = from.FindProperty(ObjectFields[i].From);
                SerializedProperty t = to.FindProperty(ObjectFields[i].To);
                if (f != null && t != null && f.propertyType == SerializedPropertyType.ObjectReference && t.propertyType == SerializedPropertyType.ObjectReference)
                    t.objectReferenceValue = f.objectReferenceValue;
            }
            for (int i = 0; i < IntFields.Length; i++)
            {
                SerializedProperty f = from.FindProperty(IntFields[i].From);
                SerializedProperty t = to.FindProperty(IntFields[i].To);
                if (f != null && t != null && f.propertyType == SerializedPropertyType.Integer && t.propertyType == SerializedPropertyType.Integer)
                    t.intValue = f.intValue;
            }

            to.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnableTraderFlag(GameObject host)
        {
            NPCSoul soul = host != null ? host.GetComponent<NPCSoul>() : null;
            if (soul == null)
                return;

            SerializedObject serializedSoul = new(soul);
            serializedSoul.Update();

            SerializedProperty canTrade = serializedSoul.FindProperty("_canTrade");
            if (canTrade != null)
                canTrade.boolValue = true;

            serializedSoul.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(soul);
        }
    }
}
#endif
