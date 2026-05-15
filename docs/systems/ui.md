# User Interface

*Menus, inventories, prompts, previews, shops, loot windows, and dialogue.*

## Purpose

All player-facing UI: crosshair prompts, inventory, equipment, radial menu, trade/loot, context menus, conversation, quest tracker, pause, settings, and save/load. Each feature is its own `MenuSystem`, and shared ownership prevents gameplay input from leaking into blocking menus.

The item/shop upgrade affects UI in two places:

- Item display fields come from `ItemComponent`, which now resolves design data from `ItemRegistry`.
- Merchant trading uses shop sessions, while loot/container/free transfer still uses direct inventory transfer.

## Key Files

- [MenuSystemBase.cs](../../Assets/Scripts/UserInterface/MenuSystemBase.cs): base class for menu open/close/focus/input ownership.
- [MenuUiUtility.cs](../../Assets/Scripts/UserInterface/MenuUiUtility.cs): shared button, focus, and selection helpers.
- [PauseMenuSystem.cs](../../Assets/Scripts/UserInterface/PauseMenuSystem.cs) + [PauseMenuBridge.cs](../../Assets/Scripts/UserInterface/PauseMenuBridge.cs): pause screen.
- [SettingsMenuSystem.cs](../../Assets/Scripts/UserInterface/SettingsMenuSystem.cs): settings menu.
- [SaveLoadMenuSystem.cs](../../Assets/Scripts/UserInterface/SaveLoadMenuSystem.cs): front-end for [SaveManager](save-system.md).
- [CrosshairUI.cs](../../Assets/Scripts/UserInterface/CrosshairUI.cs): raycasts, displays `IInteractable.InteractionPrompt`, and triggers interaction.
- [TooltipUI.cs](../../Assets/Scripts/UserInterface/TooltipUI.cs): item hover tooltip used by inventory, loot, and trade.
- [QuestTrackerHUD.cs](../../Assets/Scripts/UserInterface/QuestTrackerHUD.cs): tracked quest/objective HUD.
- [DialoguePromptSystem.cs](../../Assets/Scripts/UserInterface/DialoguePromptSystem.cs): talk prompt.
- [InventoryUI.cs](../../Assets/Scripts/UserInterface/InventoryUI.cs): inventory panel.
- [InventorySlotUI.cs](../../Assets/Scripts/UserInterface/InventorySlotUI.cs): slot row/icon/count/highlight.
- [InventoryToggle.cs](../../Assets/Scripts/UserInterface/InventoryToggle.cs): inventory open/close.
- [ItemPreviewRenderer.cs](../../Assets/Scripts/UserInterface/ItemPreviewRenderer.cs): 3D item preview renderer for live item instances and registry item ids.
- [ContextMenuUI.cs](../../Assets/Scripts/UserInterface/ContextMenuUI.cs): right-click item actions through `ItemActionSystem`.
- [TradeUI.cs](../../Assets/Scripts/UserInterface/TradeUI.cs): shared paid shop, legacy trade, and loot UI.
- [RadialMenuSystem.cs](../../Assets/Scripts/UserInterface/RadialMenuSystem.cs): hotkey radial.
- [ConversationWindowSystem.cs](../../Assets/Scripts/UserInterface/ConversationWindowSystem.cs): NPC dialogue UI.
- [SleepMenuSystem.cs](../../Assets/Scripts/UserInterface/SleepMenuSystem.cs): radial clock menu opened from [SleepInteractable](../../Assets/Scripts/Interactables/SleepInteractable.cs); previews recovery and confirms a `TimeChangeRequest.AdvanceHours` jump (see [Time of Day](time-of-day.md)).
- [PersistentCoroutineRunner.cs](../../Assets/Scripts/UserInterface/PersistentCoroutineRunner.cs): persistent coroutine host.
- [UIInputModuleFix.cs](../../Assets/Scripts/UserInterface/UIInputModuleFix.cs): Unity Input System/EventSystem workaround.

## Entry Points

- Every `MenuSystem` is scene-placed and opened by input or code.
- `UIStateOwnership` is the shared flag for blocking UI.
- [UIInputManager](../../Assets/Scripts/Management/UIInputManager.cs) routes Input System callbacks to active menus.
- Trader dialogue/interactions open `TradeUI.OpenShop(playerInventory, shopSession)` when a valid shop id exists.
- Containers/loot/direct exchange open the existing inventory-transfer modes.

## Trade UI Modes

`TradeUI` has two important modes:

- **Shop mode:** player inventory plus `ShopRuntimeSession.Inventory`. Cart totals use `ShopRuntimeSession.GetBuyPrice` and `GetSellPrice`; confirm calls `BuyItem` and `SellItem` so shop stock/gold mutates and can be saved.
- **Direct transfer/loot mode:** two inventories transfer through `TradeController`; used for corpse loot, containers, quest delivery, and free/debug exchange.

The prefab/layout is shared. Do not fork the UI for merchant shops; pass the correct mode/session.

## Item Previews

`ItemPreviewRenderer` supports:

- `Show(ItemComponent)`: render an existing live item instance, used by inventory/tooltips and existing item rows.
- `ShowItemId(string itemId)`: resolve the visual/world prefab from `ItemRegistry` and render it, used by shop stock entries that may not have a live selected item instance.

Preview rendering uses prefabs only for visuals. Names, values, icons, stack rules, and stats come from registry definitions through `ItemComponent`.

Missing visual prefabs should produce a clean empty/placeholder preview rather than an exception.

## Input Ownership

Two rules:

1. At most one blocking UI is open.
2. Gameplay systems check `UIStateOwnership.IsBlockingUiOpen()` before consuming input or ticking player-facing loops.

## Adding A New Menu

1. Subclass `MenuSystemBase` in `UserInterface/`.
2. Build the canvas/prefab under [UserInterface/Prefabs/](../../Assets/Scripts/UserInterface/Prefabs/).
3. Register opening/closing with `UIInputManager`.
4. Claim `UIStateOwnership` if it blocks gameplay.
5. Use `MenuUiUtility` for focus and selection.

## Gotchas

- Opening a blocking menu without claiming `UIStateOwnership` leaks gameplay input into UI.
- `TradeUI` is shared across shops, direct trade, and loot. Parameterize mode/session instead of duplicating the prefab.
- Item previews need visual prefabs in `ItemRegistry`; prefab design fields do not affect displayed item facts.
- `PersistentCoroutineRunner` outlives scene loads. Stop coroutines on teardown if they reference scene objects.
