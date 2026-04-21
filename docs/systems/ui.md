# User Interface

*Menus, inventories, prompts, trades — everything that sits between the world and the player's eyes.*

## Purpose

All player-facing UI: the crosshair with interaction prompt, the inventory panel, equipment, radial menu, trade / loot window, context menu for item actions, conversation window with NPC dialogue, quest tracker HUD, pause menu, settings, and save/load screens. Each feature is its own `MenuSystem`, and a shared ownership layer stops them from stealing input from each other.

## Key files

- [MenuSystemBase.cs](../../Assets/Scripts/UserInterface/MenuSystemBase.cs) — base class for all menu systems. Open/close, focus, input ownership.
- [MenuUiUtility.cs](../../Assets/Scripts/UserInterface/MenuUiUtility.cs) — shared helpers (button styling, selection, focus).
- [PauseMenuSystem.cs](../../Assets/Scripts/UserInterface/PauseMenuSystem.cs) + [PauseMenuBridge.cs](../../Assets/Scripts/UserInterface/PauseMenuBridge.cs) — pause screen, wires `Time.timeScale`.
- [SettingsMenuSystem.cs](../../Assets/Scripts/UserInterface/SettingsMenuSystem.cs) — options: video, audio, input.
- [SaveLoadMenuSystem.cs](../../Assets/Scripts/UserInterface/SaveLoadMenuSystem.cs) — front-end for [SaveManager](save-system.md).
- **World-facing HUD:**
  - [CrosshairUI.cs](../../Assets/Scripts/UserInterface/CrosshairUI.cs) — raycasts, surfaces `IInteractable.InteractionPrompt`, triggers the interact action.
  - [TooltipUI.cs](../../Assets/Scripts/UserInterface/TooltipUI.cs) — item hover tooltip used across inventory / trade / loot.
  - [QuestTrackerHUD.cs](../../Assets/Scripts/UserInterface/QuestTrackerHUD.cs) — current tracked quest + objective.
  - [DialoguePromptSystem.cs](../../Assets/Scripts/UserInterface/DialoguePromptSystem.cs) — shows "talk to X" prompt.
- **Inventory panel:**
  - [InventoryUI.cs](../../Assets/Scripts/UserInterface/InventoryUI.cs) — panel itself: slots, equipment, gold.
  - [InventorySlotUI.cs](../../Assets/Scripts/UserInterface/InventorySlotUI.cs) — one slot (icon, count, highlight).
  - [InventoryToggle.cs](../../Assets/Scripts/UserInterface/InventoryToggle.cs) — opens/closes the panel.
  - [ItemPreviewRenderer.cs](../../Assets/Scripts/UserInterface/ItemPreviewRenderer.cs) — renders a live 3D preview of the selected item.
  - [ContextMenuUI.cs](../../Assets/Scripts/UserInterface/ContextMenuUI.cs) — right-click menu on a slot: Use / Equip / Drop → [ItemActionSystem](../../Assets/Scripts/Interactions/ItemActionSystem.cs).
- **Other:**
  - [TradeUI.cs](../../Assets/Scripts/UserInterface/TradeUI.cs) — side-by-side inventories for NPC trade and container loot.
  - [RadialMenuSystem.cs](../../Assets/Scripts/UserInterface/RadialMenuSystem.cs) — hotkey radial for equip/swap actions.
  - [ConversationWindowSystem.cs](../../Assets/Scripts/UserInterface/ConversationWindowSystem.cs) — NPC dialogue UI; flips `AI_NPC.BeginConversation` / `EndConversation`.
  - [PersistentCoroutineRunner.cs](../../Assets/Scripts/UserInterface/PersistentCoroutineRunner.cs) — singleton coroutine host for UI tweens and for [QuestManager](quests.md) auto-offer.
  - [UIInputModuleFix.cs](../../Assets/Scripts/UserInterface/UIInputModuleFix.cs) — workaround for a known Unity Input System / EventSystem interaction.
- **Prefabs:** [UserInterface/Prefabs/](../../Assets/Scripts/UserInterface/Prefabs/).

## Entry points

- Every `MenuSystem` is a scene-placed singleton, opened/closed by input (pause key, inventory toggle, etc.) or by code (e.g. trade opens on interact).
- `UIStateOwnership` (used by [QuestManager](../../Assets/Scripts/Quests/QuestManager.cs) with `IsBlockingUiOpen()`) is the shared flag other systems check before ticking — if a blocking UI is open, gameplay systems back off.
- [Management/UIInputManager.cs](../../Assets/Scripts/Management/UIInputManager.cs) routes Input System callbacks to the active menu.

## No diagram — straightforward dependency

UI systems are linear: input → menu system → world system (inventory / equipment / save manager / etc.). The noteworthy thing is who *owns input* at any moment, not the data flow.

## Input ownership

Two rules:
1. **At most one "blocking" UI is open** (inventory, trade, conversation, pause). Opening a second one closes the first.
2. **Gameplay systems check `UIStateOwnership.IsBlockingUiOpen()`** before consuming input / ticking timers. See [QuestManager.Update](../../Assets/Scripts/Quests/QuestManager.cs) as the reference pattern.

## Adding a new menu

1. Subclass `MenuSystemBase` in `UserInterface/`.
2. Build the canvas and prefab under [UserInterface/Prefabs/](../../Assets/Scripts/UserInterface/Prefabs/).
3. Register opening/closing with `UIInputManager` and (if it blocks gameplay) with `UIStateOwnership`.
4. Use `MenuUiUtility` for focus/selection — don't hand-roll EventSystem plumbing.

## Gotchas

- **Opening a menu that forgets to claim `UIStateOwnership` will leak gameplay input into the UI.** You'll walk while clicking buttons. Always route through the base class's open/close.
- **`TradeUI` is used both for NPC trade and for container loot.** The two modes share the layout and the `TradeController` transfer path but differ on tabs/permissions. Don't fork the prefab — parametrise.
- **The item preview** in [ItemPreviewRenderer.cs](../../Assets/Scripts/UserInterface/ItemPreviewRenderer.cs) spins up a small render-texture camera. It's cheap, but if you open the inventory during a heavy render frame you'll see the first preview pop a frame late. Not a bug; a frame budget reality.
- **`PersistentCoroutineRunner` outlives scene loads.** Anything it's running keeps running. Stop coroutines on teardown if they reference scene objects.
