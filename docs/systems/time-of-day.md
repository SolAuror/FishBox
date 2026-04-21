# Time of Day

*A clock with opinions. Drives the sky, the seasons, and — roughly once in a while — an eclipse.*

## Purpose

A single `TimeOfDay` component runs the full day/night cycle: the sun and moon positions, sun/moon lights, the ambient and fog state, a custom skybox shader's colors and cloud parameters, lunar phase, tertiary planets, and — because why not — genuine solar and lunar eclipses driven by lunar nodal precession. A sibling `Calendar` component tracks day/month/year with configurable month lengths and optional seasonal day-length variation.

Other systems read `DayFactor`, `Hour`, `IsDaytime`, and the calendar events to schedule their own behaviour.

## Key files

- [TimeofDay.cs](../../Assets/Scripts/TimeOfDay/TimeofDay.cs) — master clock. Drives everything.
- [Calendar.cs](../../Assets/Scripts/TimeOfDay/Calendar.cs) — date tracking, month names, seasons (`OnNewDay`, `OnNewMonth`, `OnNewYear` events), `AverageMonthLength` for lunar math.
- [ToD.CelestialBody.cs](../../Assets/Scripts/TimeOfDay/ToD.CelestialBody.cs) — per-body visual: the sun disc, moon disc, tertiary planets. Applies direction, eclipse factor, lit color.
- [ToD.CelestialBodyConfig.cs](../../Assets/Scripts/TimeOfDay/ToD.CelestialBodyConfig.cs) — `ScriptableObject` configs for each body.
- [ToD.TertiaryPlanetEntry.cs](../../Assets/Scripts/TimeOfDay/ToD.TertiaryPlanetEntry.cs) — a spawn entry for additional planets (prefab + config).
- [CelestialBodies/](../../Assets/Scripts/TimeOfDay/CelestialBodies/) — authored configs and prefabs.
- **Local README:** [TimeOfDay/README.md](../../Assets/Scripts/TimeOfDay/README.md). *(Note: its mention of `EnvironmentManager` / `WeatherManager` is stale — those are not in the current codebase; all of that functionality is now in `TimeOfDay` itself.)*

## Entry points

- One `TimeOfDay` component per scene, with a sibling `Calendar` on the same GameObject.
- Assign `sunPrefab` and `moonPrefab` — each must contain a `Light` (and optionally a `CelestialBody`).
- Optionally populate `tertiaryPlanets` for additional planets in the sky.
- Optional `CelestialBodyConfig` assets for sun and moon visual overrides.

## What time drives

```mermaid
flowchart LR
    Calendar[Calendar<br/>day / month / year]
    TOD[TimeOfDay<br/>timeOfDay 0..1]
    TOD --> Sun[Sun light +<br/>CelestialBody]
    TOD --> Moon[Moon light +<br/>phase + illumination]
    TOD --> Planets[Tertiary planets]
    TOD --> Eclipse[Solar / lunar<br/>eclipse factors]
    TOD --> Sky[Skybox material<br/>zenith / horizon / clouds / stars]
    TOD --> Render[RenderSettings<br/>ambient + fog]
    Calendar --> TOD
    Calendar -->|OnNewDay<br/>OnNewMonth<br/>OnNewYear| Listeners[Quests / NPC schedules / etc.]
    TOD -->|Hour, DayFactor,<br/>IsDaytime, LunarPhase| Gameplay[Gameplay systems<br/>FishVolume, AI_NPC, etc.]
```

The clock is authoritative: `Update` advances `timeOfDay` by `(timeScale * dt) / (cycleDurationMinutes * 60)`, rolls past 1.0 into calendar advancement, and everything else (sun angle, moon phase, sky material, eclipses) is derived per-frame from `timeOfDay` + `calendar.TotalDaysElapsed`.

## Eclipses

This is the unusual part and worth understanding. Eclipses aren't scripted — they emerge from the orbital math:

- Lunar phase rolls via `daysFraction / lunarPeriodDays` (period = average month length).
- Lunar tilt oscillates via a separate nodal precession period (`nodalPrecessionDays`, default 168 days).
- Solar eclipse only fires when: near new moon phase AND sun/moon angular distance < threshold AND sun is above the horizon.
- Lunar eclipse fires near full moon under mirrored rules.

That means you can genuinely predict eclipses from the authored parameters, and save/load doesn't break them because they're stateless given `timeOfDay + TotalDaysElapsed`.

## Editor preview

`[ExecuteAlways]` + an edit-mode `UpdateEditModePreview` path updates the skybox, ambient, and fog from `timeOfDay` without spawning prefabs. That means you can scrub `timeOfDay` in the inspector and watch the scene light up — no Play mode required.

## Calendar

Months, month names, and starting date are authored on [Calendar.cs](../../Assets/Scripts/TimeOfDay/Calendar.cs). The default project uses 12 months of 28 days with custom names (`Aurion`, `Solven`, `Thalmer`, …), starting at year 142. Seasons flip at the year boundary and at the year midpoint — `SeasonSign` returns ±1 which `TimeOfDay` uses to bias `dayRatio` when `enableSeasons` is on.

## Public API highlights

```csharp
float  DayFactor        // 0 = night, 1 = zenith
float  Hour             // 0..24
bool   IsDaytime        // sun above horizon
float  LunarPhase       // 0..1
bool   IsEclipse        // solar or lunar

void   SetHour(float)
void   SetNoon()
void   SetMidnight()
void   AdvanceHours(float)
void   SkipToNextSunrise()
void   SkipToNextSunset()
```

Beds, skipping-to-dawn quests, and similar features use `AdvanceHours` / `SkipToNextSunrise` rather than mutating `timeOfDay` directly so calendar advancement happens correctly.

## Gotchas

- **`Calendar` is required.** `TimeOfDay` has `[RequireComponent(typeof(Calendar))]`. Don't split them across GameObjects.
- **Skybox material must be the custom Sol skybox.** All the `_ZenithColor`, `_HorizonColor`, `_CloudCoverage`, etc. properties are project-specific. Swap the skybox and the driver silently no-ops.
- **Edit-mode preview writes to `RenderSettings`.** If you scrub `timeOfDay` in the editor and Unity crashes, the scene can be left with night-time ambient + fog. Not data loss — just open `Window → Rendering → Lighting` and re-bake or toggle `controlAmbient`.
- **Eclipses are continuous, not scripted.** If you want to disable them (e.g. for a cinematic), set `eclipseThresholdDegrees` to zero or force `solarEclipseFactor = 0` downstream — there's no `enableEclipses` bool today.
- **The local [README](../../Assets/Scripts/TimeOfDay/README.md) is out of date** (mentions `EnvironmentManager` / `WeatherManager`). Treat this doc as the source of truth.
