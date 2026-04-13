# Sol_Interaction

## Setup Instructions

1. **Add Components to GameObjects:**
	- Attach `Inventory`, `Equipment`, and `CrosshairUI` components to your player and/or NPC GameObjects as needed.
	- Add `InteractionPoint` to world objects you want to be interactable (e.g., benches, campfires).
	- Use `NpcTradeInteractable` for NPCs that should support trading.
	- Use `ContainerInteractable` for crates/chests/world containers that should open loot mode.

2. **Configure Inspector Fields:**
	- Set prompts, types, and UnityEvents for `InteractionPoint` objects in the Inspector.
	- Adjust inventory capacity and equipment slots as desired.

3. **Integrate with UI:**
	- Ensure your UI canvas includes the required elements for crosshair and prompts (see `CrosshairUI`).

4. **Connect to Other Sol Systems:**
	- For full functionality, connect with Sol.Grab, Sol.Locomotion, and Sol.AI as referenced in the scripts.

5. **Test in Play Mode:**
	- Enter Play mode and verify that interactions, inventory, equipment, and trading work as expected.

---

The `Sol_Interaction` module provides a flexible, extensible system for world interactions, inventory management, equipment, and trading in the Sol framework. It supports both player and NPC interactions, with a unified pipeline for use, equip, trade, and more.

## Overview

This folder contains scripts for:
- Inventory and equipment systems
- Interaction points (e.g., benches, campfires)
- Item actions (use, equip, drop)
- Trading between inventories
- UI integration for crosshairs and prompts

## Script Summaries

### InventorySlot.cs
- Represents a single slot in an inventory.
- Holds a reference to an item and its stack count.

### Inventory.cs
- Elder Scrolls-style list inventory for both creature inventories and world containers.
- Supports stacking, capacity, gold, and container access rules (locked / owner restricted).

### Interactor.cs
- Context object passed to interactable methods.
- Wraps player/NPC, inventory, and soul references.

### InteractionPoint.cs
- Represents a world interaction point (e.g., bench, workbench).
- Implements `IInteractable` for unified player/NPC use.
- Supports prompts, types, and UnityEvents.

### EquipmentSlotType.cs
- Enum for equipment slots (Head, Chest, MainHand, etc.).

### Equipment.cs
- Minimal equipment system for characters with skeletons.
- Handles equipping items, bone assignment, and IK activation.

### CrosshairUI.cs
- UI script for displaying crosshair and interaction prompts.
- Handles input and raycasting for interactables.

### I_Interactable.cs
- Interface for all interactable objects.
- Requires prompt, `CanInteract`, and `Interact` methods.

### ItemActionType.cs
- Enum for item actions: Use, Equip, Drop.

### ItemActionSystem.cs
- Centralized execution for all item actions.
- Ensures actions are not duplicated across systems.

### Sol.ItemComponent.cs
- Component for world items (pick-up-able).
- Handles item info, stackability, consumable/weapon stats, and interaction.

### NpcTradeInteractable.cs
- Attach to NPCs to make them tradeable.
- Integrates with the player's interaction pipeline and inventory.

### TradeController.cs
- Static class for transferring items between inventories.
- Used by the trade UI.

### ContainerInteractable.cs
- Attach to world containers (chests/crates/barrels) with an Inventory.
- Reuses loot-mode transfer UI and respects inventory lock/ownership access checks.

---

**Usage:**  
Attach these components to your player, NPCs, and world objects to enable interaction, inventory, equipment, and trading features. See each script for detailed usage and extension points.



