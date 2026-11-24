# WorldBox Suite Architecture

## Save Format (`saveVersion: 17`)

WorldBox `.wbox` files are raw zlib-compressed UTF-8 JSON payloads.

### Coordinate Systems
* **Chunks:** Map dimensions (`width`, `height`) are measured in chunks of `64 × 64` tiles.
* **Tiles:** Grid size is `(width * 64) × (height * 64)`.
  * `tileMap`: palette of tile strings (`"deep_ocean"`, `"soil_high:grass_high"`, `"mountains"`, etc.).
  * `tileArray[y]` and `tileAmounts[y]`: run-length encoded rows from `y = 0 .. height*64 - 1`.
  * Flat tile indices (`fire`, `frozen_tiles`, `conwayEater`, `conwayCreator`) follow `idx = y * W + x`.
* **Zones:** Each city owns `8 × 8`-tile zones (`cities[].zones[].{x, y}`), so `1 chunk = 8 × 8 zones`.
* **Zero-Omission:** Serialized with default-value omission, meaning `0` coordinates (`x`, `y`, `mainX`, `mainY`) may be absent in JSON and default to `0`.

### Entity ID Namespaces
`mapStats` tracks monotonic counters (`id_unit`, `id_building`, `id_kingdom`, `id_city`, `id_culture`, `id_clan`, `id_alliance`, `id_war`, `id_plot`, `id_book`, `id_subspecies`, `id_family`, `id_army`, `id_language`, `id_religion`, `id_item`, `id_diplomacy`). Non-positive IDs (`<= 0`, typically `-1` or omitted `0`) represent unset references and are never remapped during stitching.

---

## C# Mod Suite Layout (`mods/`)

All in-game modules target **NeoModLoader** on WorldBox's Mono runtime (`.NET Framework 4.7.2` / `netstandard2.1` matching `Assembly-CSharp.dll`).

### 1. `CoreScale`
* **Custom Map Dimensions:** Patches `MapSizeLibrary` and generation bounds to support arbitrary `width × height` chunk grids.
* **Simulation LOD & Time-Slicing:** Throttles distant/off-screen `BatchActors` updates and skips non-essential visual/particle updates outside the camera frustum.
* **Army Consolidation:** Aggregates high-headcount armies into weighted battalion banners during large wars to cut per-tick pathfinding and combat overhead.
* **Strategic Zoom:** Above a configurable camera zoom threshold, hides micro sprites/particles and renders clean political borders, army counters, and battle markers.

### 2. `LinkedContinents`
* Links two or more separate `.wbox` continent saves along configurable ocean edges.
* Fleets or expedition armies crossing a designated border serialize their roster (actors, items, army metadata, kingdom allegiance) into a transfer queue read when switching to or loading the destination continent.

### 3. `GrandWarfare`
* **Naval Combat:** Adds ship-to-ship engagements, coastal bombardments, and amphibious landing staging.
* **Supply & Attrition:** Tracks army distance from friendly cities/ports; oversupply or cut lines trigger gradual attrition.
* **Grand Diplomacy:** Extends `DiplomacyManager` with vassalage, defense pacts, and timed peace treaties.

### 4. `VisualPolish`
* Extended camera zoom bounds and smoothing.
* Optional post-processing overlays (day/night color grading, subtle bloom, atmospheric haze, crisp pixel scaling).

### 5. `EraChronicle`
* Extended technological milestones beyond vanilla tiers.
* Periodic lightweight snapshots of borders, populations, and wars exported as JSON for playback in World Studio.
