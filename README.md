# WorldBox Suite

Open-source modding suite and standalone toolkit for **WorldBox – God Simulator** (Steam, saveVersion 17 / NeoModLoader).

## What's Inside

### Standalone Tools (`tools/`)
* **`tools/wbox`** — Python library & CLI for `.wbox` save files:
  * Lossless zlib decompress/recompress and inspection
  * Top-down biome & civilization map PNG renderer
  * **World Stitcher** — merge 2 or more saves side-by-side or in a grid with customizable ocean channels, full coordinate shifting (tiles, zones, buildings, actors, fire/ice), and complete entity ID remapping across kingdoms, cities, armies, clans, cultures, languages, religions, subspecies, families, books, items, wars, and diplomacy.
* **`tools/studio`** — Local web UI (**WorldBox Studio & Mod Manager**):
  * Visual map inspector and live world stitcher
  * World history, kingdoms, wars, and stats browser
  * NeoModLoader mod manager (install/toggle/profiles/save backups)

### In-Game Mods (`mods/`) — NeoModLoader (C#)
Planned and scaffolded modules sharing a unified architecture (see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)):
* **`CoreScale`** — Custom map dimensions, off-screen simulation LOD & tick throttling, strategic zoom view, and army battalion consolidation.
* **`LinkedContinents`** — Multi-world ocean borders allowing fleets and armies to cross between separate continent saves.
* **`GrandWarfare`** — Naval combat, amphibious invasions, supply lines/attrition, vassals, and expanded diplomacy.
* **`VisualPolish`** — Extended camera zoom, atmospheric lighting/day-night shaders, and strategic map overlays.
* **`EraChronicle`** — Extended tech era progression and automated war/history timeline snapshots.

## Quick Start

### 1. Run World Studio & Mod Manager
```bash
python3 -m tools.studio.server
```
Then open `http://127.0.0.1:8765` in your browser.

### 2. Stitch Worlds via CLI
```bash
python3 -m tools.wbox stitch \
  local/saves/world_a.wbox local/saves/world_b.wbox \
  --gap 2 \
  --name "Twin Continents" \
  --out local/output/twin_continents.wbox \
  --preview local/output/twin_continents.png
```

## Contributing & Game Assemblies
Proprietary game DLLs (`Assembly-CSharp.dll`, `UnityEngine*.dll`) and personal save files are intentionally ignored in `.gitignore`. Place your local copies under `local/libs/` and `local/saves/` when developing locally.

## License
MIT
