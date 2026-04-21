# Locomotion

*How a character gets from where it is to where it wants to be, and sounds right doing it.*

## Purpose

Character movement, animation hand-off, inverse kinematics, and surface-aware footstep audio for both the player and NPCs. The player and the NPC share the same `LocomotionController` + `LocomotionState` + `LocomotionAnimation` stack — only the *intent provider* differs (player input vs. AI state).

## Key files

- [LocomotionController.cs](../../Assets/Scripts/Locomotion/LocomotionController.cs) — the movement engine. Drives the `CharacterController`, resolves speed/grounding/swimming, and exposes `IsSwimming`, `IsConversing`, movement locks.
- [LocomotionState.cs](../../Assets/Scripts/Locomotion/LocomotionState.cs) — the small state machine (Idle / Walk / Run / Swim / …) the controller and animation read from.
- [LocomotionAnimation.cs](../../Assets/Scripts/Locomotion/LocomotionAnimation.cs) — maps state + velocity to animator parameters.
- [LocomotionInput.cs](../../Assets/Scripts/Locomotion/LocomotionInput.cs) — the player input abstraction (move vector, attack, reel, interact).
- [LocomotionIK.cs](../../Assets/Scripts/Locomotion/LocomotionIK.cs) — procedural IK: foot planting, main-hand weapon hand placement. Activated by [Equipment](../../Assets/Scripts/Interactions/Equipment.cs) when a main-hand weapon is equipped.
- [LocomotionUtil.cs](../../Assets/Scripts/Locomotion/LocomotionUtil.cs) — shared math helpers.
- [CameraMode.cs](../../Assets/Scripts/Locomotion/CameraMode.cs) — first-person vs. third-person camera modes.
- [Management/LocomotionInputManager.cs](../../Assets/Scripts/Management/LocomotionInputManager.cs) — scene-level singleton that binds Unity Input System to `LocomotionInput` and tracks the active gameplay camera.
- **Footsteps:**
  - [Footsteps/FootstepPlayer.cs](../../Assets/Scripts/Locomotion/Footsteps/FootstepPlayer.cs) — triggered by animation events; picks a clip from the current surface.
  - [Footsteps/FootstepSurface.cs](../../Assets/Scripts/Locomotion/Footsteps/FootstepSurface.cs) — scriptable-object surface definitions (grass, stone, wood, water, …).
  - [Footsteps/FootstepLibrary.cs](../../Assets/Scripts/Locomotion/Footsteps/FootstepLibrary.cs) — maps physical material / terrain layer → `FootstepSurface`.
- **IK internals:**
  - [IK/ExtractTransformConstraint.cs](../../Assets/Scripts/Locomotion/IK/ExtractTransformConstraint.cs) and friends — custom Animation Rigging constraint used by `LocomotionIK` to pull transforms out of the rig graph.
- **NPC variant:** [Locomotion/NPC/](../../Assets/Scripts/Locomotion/NPC/) wraps the shared stack with NavMeshAgent-driven intent for AI characters.

## Entry points

- On the player: `LocomotionInput` + `LocomotionController` + `LocomotionState` + `LocomotionAnimation` + `LocomotionIK` + a `CharacterController`.
- On the NPC: same stack, plus `NavMeshAgent` and `AI_NPC` providing intent via `ILocomotionIntentProvider` — see the `[RequireComponent]` attributes on [AI_NPC.cs](../../Assets/Scripts/NPCs/AI_NPC.cs).
- `LocomotionInputManager` is a scene singleton that must be present for player input and camera context to resolve.

## What drives what

```
LocomotionInput (player)  ─┐
                           ├─► LocomotionController ─► CharacterController (moves)
AI intent (NPC)           ─┘                         └─► LocomotionState ─► LocomotionAnimation ─► Animator
                                                                                  └─► FootstepPlayer (anim events)
Equipment.Equip(mainHand) ───────────────────────────────► LocomotionIK (enables hand placement)
WaterVolume.OnTriggerEnter ──────────────────────────────► LocomotionController.IsSwimming = true
ConversationWindow.Begin() ──────────────────────────────► LocomotionController.IsConversing = true (rotation locked)
```

No big diagram needed — the dependency graph is linear.

## Movement locks

`LocomotionController.SetMovementLock(seconds)` freezes player-driven translation for a short window. Used, for example, by [FishingState](../../Assets/Scripts/Fishing/FishingState.cs) during the cast animation so you can't walk out from under your own rod. `ClearMovementLock()` resets it.

## Footsteps

Footsteps fire from animation events — when the foot contacts the ground, the animator calls `FootstepPlayer.OnFootstep()`. The player samples the surface beneath and picks a clip from the matching `FootstepSurface`. If nothing matches, silence (not a fallback clip) — that's intentional, so a missing surface mapping is audible during authoring.

## Gotchas

- **IK activates on main-hand equip only.** Off-hand items don't turn on hand IK. If you add a two-handed weapon type, you need to extend [Equipment.cs](../../Assets/Scripts/Interactions/Equipment.cs) + [LocomotionIK.cs](../../Assets/Scripts/Locomotion/LocomotionIK.cs) — there's no "both hands" slot today.
- **Swimming is driven by trigger volumes.** Entering a [WaterVolume](../../Assets/Scripts/Water/WaterVolume.cs) sets `LocomotionController.IsSwimming = true` automatically. If you swim somewhere without a `WaterVolume`, the controller thinks you're walking.
- **`IsConversing` suppresses rotation but not movement.** If you want a full freeze during dialogue, lock movement too (see [Conversation flow](../../Assets/Scripts/UserInterface/ConversationWindowSystem.cs)).
- **`LocomotionInputManager` is a hard scene dependency.** Code in [FishingState](../../Assets/Scripts/Fishing/FishingState.cs) and elsewhere assumes `LocomotionInputManager.Instance != null` for camera context. Don't load gameplay scenes without it.
