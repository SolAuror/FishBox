# Sol Grab System

**Full documentation:** [Documentation/GrabSystem.md](../../Documentation/GrabSystem.md)

---

## Quick Summary

Lets a player pick up, inspect, and throw `GrabbableComponent` objects. Supports free-rotate, axis-locked, and frozen rotation modes.
- **`GrabManager`** — singleton; handles input, ray-cast picking, hold/throw logic.
- **`GrabbableComponent`** — marks an object as grabbable; exposes `OnGrabbed` / `OnReleased` events.

## Scene Requirements

- One `GrabManager` in the scene (on the Player or a manager GameObject)
- `GrabbableComponent` on any Rigidbody object that should be pickable

## Quick Setup

1. Add **GrabManager** to the Player → assign `holdPoint` transform and `throwForce`
2. Add **GrabbableComponent** to any Rigidbody → set `rotationMode` and `freezeOnGrab` as needed
3. Subscribe to `GrabbableComponent.OnGrabbed` / `OnReleased` for custom interactions

## Key Files

```
Assets/Sol_Grabable/
├── GrabManager.cs          Singleton — pick-up, hold, throw logic
└── GrabbableComponent.cs   Per-object — events, rotation mode, freeze flag
```