# NPCs

*Townsfolk, guards, quest givers, bandits, and traders. They own pathing, dialogue hooks, identity, and optional shop ids.*

## Purpose

NPC characters use the same locomotion and soul stack as the player, but their intent comes from AI states instead of input. Each NPC has a `NavMeshAgent`, locomotion components, `NPCSoul` identity, and an `AI_NPC` partial class that coordinates state, dialogue, inventory, death/loot, and trading.

The authoring tool seeds NPCs from archetypes such as `Civilian`, `Guard`, `Bandit`, `Quest Giver`, and `Unique`. Archetypes are templates that apply gameplay tags; runtime role truth lives in tags such as `Job.Trader`, `Job.QuestGiver`, `Job.Guard`, `Faction.Bandit`, `Actor.Hostile`, and `Actor.Unique`. Traders now use shop ids for merchant stock. NPC inventory still exists, but it is no longer the source of merchant shop stock.

NPCs can also be assigned an `NpcScheduleDefinition` that drives their day — see [NPC Schedules](npc-schedule.md) for the full system.

## Key Files

- [AI_NPC.cs](../../Assets/Scripts/NPCs/AI_NPC.cs): partial class root and required component declarations.
- [AI_NPC.Core.cs](../../Assets/Scripts/NPCs/AI_NPC.Core.cs): main behavior loop, state switching, and public surface.
- [AI_NPC.Locomotion.cs](../../Assets/Scripts/NPCs/AI_NPC.Locomotion.cs): active-state locomotion intent into `NavMeshAgent`.
- [AI_NPC.Animation.cs](../../Assets/Scripts/NPCs/AI_NPC.Animation.cs): animator parameter mapping.
- [AI_NPC.Inventory.cs](../../Assets/Scripts/NPCs/AI_NPC.Inventory.cs): NPC-side inventory support.
- [AI_NPC.Shop.cs](../../Assets/Scripts/NPCs/AI_NPC.Shop.cs): trader surface, shop id, shop-session opening, and legacy direct trade fallback.
- [AI_NPC.Schedule.cs](../../Assets/Scripts/NPCs/AI_NPC.Schedule.cs): daily-routine driver (location target, desired-state resolution, snap-to-target on load).
- [NpcScheduleDefinition.cs](../../Assets/Scripts/NPCs/NpcScheduleDefinition.cs): `ScriptableObject` schedule asset + `NpcScheduleActivity` enum.
- [NpcScheduleLocation.cs](../../Assets/Scripts/NPCs/NpcScheduleLocation.cs): scene anchor used by schedule entries.
- [AIConfig.cs](../../Assets/Scripts/NPCs/AIConfig.cs): authored per-NPC config.
- [NPCSoul.cs](../../Assets/Scripts/NPCs/NPCSoul.cs): persistent NPC identity and character facts.
- [IntentContext.cs](../../Assets/Scripts/NPCs/IntentContext.cs): glue between AI intent and locomotion.
- [NPCAuthoringDrawerUtility.cs](../../Assets/Scripts/NPCs/Editor/NPCAuthoringDrawerUtility.cs): grouped NPC inspector UI, including shop id dropdown.
- [NPCAuthoringValidator.cs](../../Assets/Scripts/NPCs/Editor/NPCAuthoringValidator.cs): NPC authoring warnings, including missing/invalid shop ids.
- [ShopRuntimeStore.cs](../../Assets/Scripts/RPG/ShopRuntimeStore.cs): resolves a trader's shop id into a mutable runtime session.
- **States:** [States/](../../Assets/Scripts/NPCs/States/) contains idle, patrol, dead, and shared state base classes.
- **Prefabs:** [NPCs/Prefabs/](../../Assets/Scripts/NPCs/Prefabs/).

## Entry Points

- `AI_NPC` is the single behavior component on an NPC root. It requires and resolves navigation, locomotion, inventory, and dialogue/trade pieces.
- Conversations call `BeginConversation()` / `EndConversation()` on `AI_NPC`, which propagates to locomotion conversation state.
- Trader interaction/dialogue asks `AI_NPC` for its trade action.
- If `AI_NPC.ShopId` resolves to a `RpgShopDefinition`, trade opens a `ShopRuntimeSession`.
- If no shop id is assigned, legacy direct inventory trade can still be used where appropriate.
- `AI_NPC.IsTrader` reads `Job.Trader`; `AI_NPC.IsQuestGiver` reads `Job.QuestGiver`. Legacy setters add/remove those tags.

## Trading Model

Trader NPCs should use `SHP#####` shop ids for paid merchant trading:

```mermaid
flowchart TD
    NPC["AI_NPC<br/>ShopId"] --> Store["ShopRuntimeStore.GetOrCreateSession"]
    Store --> Def["RpgShopDefinition<br/>stock item ids"]
    Def --> Session["ShopRuntimeSession<br/>mutable stock + gold"]
    Session --> ItemRegistry["ItemRegistry<br/>item facts + visual prefabs"]
    Session --> TradeUI["TradeUI shop mode"]
```

Authoring rules:

- Add `Job.Trader` to make an NPC a trader. Add `Job.QuestGiver` to surface quest-offer dialogue. A tagged NPC can gain or lose those roles at runtime.
- Assign the shop id in the NPC authoring UI.
- Author merchant stock in the `Shops` tab, not in the NPC inventory.
- NPC inventory remains for non-shop possessions, loot, quest delivery, and direct/free transfer.
- Trader dialogue without a valid shop id warns and does not open a broken paid trade UI.

## AI State Transitions

```mermaid
stateDiagram-v2
    [*] --> Idle
    Idle --> Patrol: patrol points configured
    Patrol --> Idle: patrol loop paused / reached rest
    Idle --> Travel: schedule entry resolves
    Patrol --> Travel: schedule entry resolves
    Travel --> Sleep: arrived (Activity=Sleep)
    Travel --> Work: arrived (Activity=Work)
    Travel --> Eat: arrived (Activity=Eat)
    Travel --> Socialize: arrived (Activity=Socialize)
    Sleep --> Travel: schedule hour rolls
    Work --> Travel: schedule hour rolls
    Eat --> Travel: schedule hour rolls
    Socialize --> Travel: schedule hour rolls
    Idle --> Dead: Health <= 0
    Patrol --> Dead: Health <= 0
    Travel --> Dead: Health <= 0
    Dead --> [*]
```

The `State.Chase` enum value is reserved, but no complete chase state is wired yet. The `Travel` / `Sleep` / `Work` / `Eat` / `Socialize` states are driven by [NPC Schedules](npc-schedule.md); NPCs without a schedule definition stay in the `Idle` / `Patrol` loop.

## Persistence

NPCs persist through `NPCSoul`. Save/load captures each soul's position, rotation, health, role/state tag paths, conversation-ready flags, and non-shop inventory.

Merchant shop stock/gold does not persist through NPC inventory. It persists through `ShopSaveData` and restores through `ShopRuntimeStore`.

Schedule state is **not** serialized — it is re-derived from the clock on load. `SaveManager.ApplyRestore` calls `SnapToCurrentScheduleTarget()` on every NPC that has a schedule, warping it onto the location matching the loaded `ClockHour`.

## Adding A New NPC

1. Use `Window/Sol/Database` -> `NPCs` or duplicate a prefab under [NPCs/Prefabs/](../../Assets/Scripts/NPCs/Prefabs/).
2. Assign/fix a stable owner id through the database tools.
3. Configure archetype, AI config, patrol points, inventory, dialogue, and death/loot.
4. For a merchant, create a `RpgShopDefinition` in the `Shops` tab and assign its shop id to the NPC.
5. Place in-scene on a NavMesh.

## Gotchas

- Start from an NPC prefab. Required components are strict, and Unity auto-add order can hide broken defaults.
- Dead NPCs stop ticking. Loaded dead state has no post-load death grace period.
- Starting a conversation does not automatically stop the `NavMeshAgent`; the active state must provide zero movement if the NPC should stand still.
- `State.Chase` is a placeholder until a concrete chase state lands.
- Merchant stock belongs to `RpgShopDefinition` and `ShopRuntimeSession`, not the NPC inventory seed list.
- Prefer role/status tags over new booleans. `Actor.Hostile`, `Actor.Dead`, `Actor.InCombat`, `Actor.Criminal`, and `Actor.Wanted` can be combined with faction ids/tags for later law and combat behavior.
