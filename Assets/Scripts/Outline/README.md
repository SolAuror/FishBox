# Sol Outline System

**Full documentation:** [Documentation/OutlineSystem.md](../../Documentation/OutlineSystem.md)

---

## Quick Summary

Screen-space silhouette outlines rendered via a two-pass URP Renderer Feature. Zero draw-call overhead — outlines are drawn in a single post-process pass over flagged objects.
- **`OutlineManager`** — singleton; maintains the list of outlined objects, drives the renderer feature.
- **`OutlineComponent`** — add to any GameObject to make it outlineable; exposes `Enable()` / `Disable()`.

## Scene Requirements

- URP Renderer Data: **SolOutlineRendererFeature** added to the active renderer
- One `OutlineManager` in the scene

## Quick Setup

1. Open the active URP Renderer Data asset → Add Renderer Feature → **SolOutlineRendererFeature**
2. Add **OutlineManager** to a scene manager GameObject
3. Add **OutlineComponent** to any GameObject you want outlined → call `Enable()` to activate

## Key Files

```
Assets/Sol_Ouline/
├── OutlineManager.cs              Singleton — manages outlined object list
├── OutlineComponent.cs            Per-object component — Enable/Disable API
├── SolOutlineRendererFeature.cs   URP Renderer Feature (two-pass silhouette)
└── Shaders/
    └── Sol.Outline.shader
```