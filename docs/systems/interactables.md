# Interactables

*Forges, anvils, fruit trees, wells, beds, chests, quest boards. Anything the player walks up to and presses Use on.*

## Purpose

The interactables layer is the contract between the player's `Interactor` and the world objects it can act on. Every world object that wants a "press E to..." prompt implements [IInteractable](../../Assets/Scripts/Interactables/I_Interactable.cs); the [Interactor](../../Assets/Scripts/Interactables/Interactor.cs) wraps the actor (player or NPC) doing the interacting and exposes its `Inventory`, `PlayerSoul`, and `NPCSoul`.

The bulk of the new "do a thing in the world" surface ports the Solr **InteractionPoint** family: a single component that handles an aligned, animated, time-boxed use with optional actor effects (heal, restore stamina). Specialized subclasses add per-type behaviour like harvesting an item or filling a water container.

## Key files

Contract & dispatch:

- [I_Interactable.cs](../../Assets/Scripts/Interactables/I_Interactable.cs) — the `IInteractable` interface (`InteractionPrompt`, `CanInteract`, `GetInteraction`). Also home to shared utility (`SoulStat`, `EntityCodeUtility`, dropdown attributes).
- [Interactor.cs](../../Assets/Scripts/Interactables/Interactor.cs) — wraps an interacting actor with cached `Inventory`, `NPCSoul`, `PlayerSoul`, and `IsPlayer` flag.
- [ItemActionSystem.cs](../../Assets/Scripts/Interactables/ItemActionSystem.cs) / [ItemActionType.cs](../../Assets/Scripts/Interactables/ItemActionType.cs) — inventory-side action dispatch (`Use`, `Equip`, `Drop`).

Interaction points (Solr port):

- [InteractionPoint.cs](../../Assets/Scripts/Interactables/InteractionPoint.cs) — the base. Owns id (`INP#####`), prompt, display name, type, animation type, alignment transform, use duration, hold-until-cancelled flag, single-occupancy flag, allow-player/allow-NPC flags, and four actor-effect deltas (health/stamina/hunger/thirst). Drives the animator via the `interactionType` (int), `interactionActive` (bool), and `isInteracting` (trigger) parameters.
- [HarvestableInteractionPoint.cs](../../Assets/Scripts/Interactables/HarvestableInteractionPoint.cs) — gives an item from `ItemRegistry` on use; supports deplete-after-use and respawn timer.
- [WaterSourceInteractionPoint.cs](../../Assets/Scripts/Interactables/WaterSourceInteractionPoint.cs) — wells and basins; reserves an item id for a future filled-container flow (currently warns).

Other interactables:

- [ContainerInteractable.cs](../../Assets/Scripts/Interactables/ContainerInteractable.cs) — chests and crates (covered also in [Interactions & Inventory](interactions-inventory.md)).
- [SleepInteractable.cs](../../Assets/Scripts/Interactables/SleepInteractable.cs) — beds and bedrolls; advances time (covered with [Time of Day](time-of-day.md)).
- [QuestBoardInteractable.cs](../../Assets/Scripts/Interactables/QuestBoardInteractable.cs) — board-only quest offer surface (covered with [Quests](quests.md)).
- [NpcTraderInteractable.cs](../../Assets/Scripts/Interactables/NpcTraderInteractable.cs) — opens an NPC trade screen (covered with [NPCs](npcs.md) and [Interactions & Inventory](interactions-inventory.md)).

## Enums

- `InteractionPointType`: `Rest`, `Work`, `Food`, `Water`, `Utility`.
- `InteractionPointAnimationType`: `None`, `Work.Forge`, `Work.Anvil`, `Rest.Sit`, `Rest.Sleep`, `Work.Fishing`, `Gather.Fruit`, `Water.Draw`. The integer value of this enum is what gets pushed into the `interactionType` animator parameter.

## Entry points

- The player `Interactor` raycasts for an `IInteractable`, calls `CanInteract(interactor)`, and on input dispatches the `GameAction` returned by `GetInteraction(interactor)`. For `InteractionPoint` that action is `UseInteractionPointAction`.
- `UseInteractionPointAction` calls `BeginUse(interactor)` to enter the use, runs for `GetUseDuration(interactor)` (or until cancelled if `ShouldHoldUntilCancelled` is true — including the implicit case for `Rest.Sit`), then calls `EndUse(completed)`.
- On a completed use, `InteractionPoint.ApplyActorEffects` applies non-zero `HealthDelta` and `StaminaDelta` to the actor's soul (positive heals/restores, negative drains). `HungerDelta` and `ThirstDelta` are accepted by the inspector but log a one-shot warning at runtime — Fishbox souls currently expose health and stamina only.
- Subclasses customise via the `OnUseStarted` / `OnUseCompleted` / `OnUseCancelled` virtuals (e.g. `HarvestableInteractionPoint.OnUseCompleted` gives the configured item).

## Use lifecycle

```mermaid
stateDiagram-v2
    [*] --> Idle
    Idle --> CanCheck: Interactor raycast hits
    CanCheck --> Idle: CanInteract == false
    CanCheck --> InUse: BeginUse(interactor)
    InUse --> Idle: EndUse(completed=true)<br/>→ apply effects + onUsed
    InUse --> Idle: EndUse(completed=false)<br/>→ OnUseCancelled
    InUse --> Idle: OnDisable<br/>(forces cancel)
```

## Data flow

```mermaid
flowchart LR
    Player["Player input"] --> Raycast["Interactor raycast"]
    Raycast --> Interactable["IInteractable.GetInteraction"]
    Interactable --> Action["GameAction (UseInteractionPointAction)"]
    Action --> Begin["InteractionPoint.BeginUse"]
    Begin --> Animator["Animator params:<br/>interactionType / Active / isInteracting"]
    Begin --> Events["UnityEvent _onUseStarted"]
    Begin --> Hold{"hold until<br/>cancelled?"}
    Hold -- no --> Timer["Wait GetUseDuration"]
    Hold -- yes --> Release["Wait for input release"]
    Timer --> End["EndUse(completed=true)"]
    Release --> End
    End --> Effects["ApplyActorEffects<br/>(HealthDelta / StaminaDelta)"]
    End --> Sub["Subclass OnUseCompleted<br/>(harvest item, fill water, …)"]
    Sub --> Inventory["Inventory / Soul state"]
```

## Authoring an interaction point

1. Add an empty GameObject in the scene where you want the use to align.
2. Add `InteractionPoint` (or a subclass like `HarvestableInteractionPoint`).
3. Set the prompt, display name, type, animation type, and use duration.
4. Optional: assign `_alignPoint` to a child transform that the actor should snap to.
5. Optional: set `_healthDelta` / `_staminaDelta` for vital effects on use.
6. Wire the `_onUseStarted` / `_onUsed` UnityEvents for VFX, SFX, or external state changes.
7. Make sure the actor prefab has an `Animator` with `interactionType` (int), `interactionActive` (bool), and (for `None`-typed points) `isInteracting` (trigger) parameters — the component checks for them and silently no-ops if they're missing.

`_interactionPointId` (`INP#####`) is auto-assigned in the editor by `EntityCodeUtility.EnsureAssignedCode` during `OnValidate`. Don't hand-edit it once assigned.

## Gotchas

- `_singleOccupancy = true` means a second interactor will fail `CanInteract` while the point is in use. `IsInUseBy(interactor)` lets the active actor reuse it.
- `_holdUntilCancelled` is implicitly true for `Rest.Sit` regardless of the flag — sitting is forever until you stand up.
- `HungerDelta` and `ThirstDelta` are inspector-only today. They warn (once per component instance) and are not applied. Don't author content that depends on them.
- `HarvestableInteractionPoint` resolves items through `ItemRegistry.Get()` — make sure the item id is registered. The class warns (and TODO-flags) if the id can't be resolved or the interactor has no `Inventory`.
- `WaterSourceInteractionPoint._filledContainerItemId` is reserved for a future container-fill flow; today it warns instead of acting. Don't ship content depending on the filled-container item being granted.
- `OnDisable` force-calls `EndUse(completed: false)` if the point is mid-use — disabling a station mid-interaction triggers the cancel path and any cleanup it implies.
- Animator parameter writes are guarded by `HasAnimatorParameter` checks — missing parameters do not throw, but they also do not animate. Add the parameters to the actor's animator controller.
