# WorldBox Suite

Open-source modding suite and standalone toolkit for **WorldBox – God Simulator** (Steam, saveVersion 17 / NeoModLoader), themed in **Catppuccin Mocha**.

## What's Inside

### Standalone Tools (`tools/`)
* **`tools/wbox`** — Zero-dependency Python library & CLI for `.wbox` save files:
  * Lossless zlib decompress/recompress and inspection
  * Top-down biome, kingdom border, building, fire, and ice PNG renderer
  * **World Stitcher** — merge 2+ saves side-by-side, stacked, or in a grid with configurable ocean channels, full coordinate shifting (tiles, 8×8 city zones, buildings, actors, fire/ice), and 19-namespace entity ID remapping.
* **`tools/studio`** — Local web UI (**WorldBox Studio & Mod Manager**, Catppuccin Mocha):
  * **World Inspector:** Live map preview, toggleable borders/buildings, kingdom leaderboard, war chronicle, `.wbox` upload/download
  * **World Stitcher:** Interactive multi-world merger with live preview and one-click `.wbox` export
  * **Chronicle Viewer:** Population timeline SVG chart, kingdom eras, and war/milestone feed
  * **Mod Manager:** Enable/disable NeoModLoader mods, switch mod profiles, one-click suite deployment, and `.zip` save backups

### In-Game NeoModLoader C# Mods (`mods/`)
All seven modules share an offline Mono build configuration (`mods/Directory.Build.props`), include custom 64×64 Catppuccin Mocha pixel icons, and compile cleanly with zero warnings:

| Module | Bottom Bar / Hotkey | Features |
| :--- | :---: | :--- |
| **`SuiteHub`** | **Bottom Tab** / `F2`–`F5` | Native WorldBox bottom power-bar tab integrating the whole suite, floating **War & Diplomacy Monitor** (`F4`) with opinion matrix and jump-to-battle, pulsing on-map **Battlefield Conflict Radar** (`F3`), in-game **Multi-Continent World Builder** (`F2`), and unified Control Center (`F5`). |
| **`CoreScale`** | `F8` | Giant map presets (`Colossal 12×12` through `Leviathan 24×24`), multi-core parallelism unlock, adaptive 3-tier simulation LOD throttling, **Army Battalion Compression**, strategic animation culling, and expanded zoom bounds. |
| **`VisualPolish`** | `F9` / `F6` | Atmospheric lighting presets (`CrispVibrant`, `GoldenHour`, `MoonlitNight`, `ParchmentAtlas`, `MochaTwilight`), dynamic day/night cycle, soft radial vignette, ultra-close zoom, strategic border clarity, and `F6` Photo Mode. |
| **`GrandWarfare`** | `F10` | Ship-to-ship naval volleys and coastal bombardment, army supply-line attrition outside friendly cities, and defense-pact diplomacy that pulls allied kingdoms into defensive wars. |
| **`LinkedContinents`** | `F11` | Ocean-edge fleet transfers connecting separate `.wbox` continent saves via a shared `portal_queue.json`, plus on-demand expedition dispatch and coastal landings. |
| **`EraChronicle`** | `F7` | 5-tier kingdom era progression (*Dawn Age* through *High Hegemony*) with veteran milestones and automated JSON world-history snapshots readable in WorldBox Studio. |
| **`ApexFactions`** | **Bottom Tab** / `F12` | **Warden "Sleeping Giant" Doctrine** (peaceful elite humans who never start wars, rally relentlessly if attacked, and enforce a configurable 15% Mercy Ceasefire so survivors live to tell the tale), **Instant Species Awakener** (`civ_wolf` & `makeSapient`), and **Feral Pack Domains** (*Land of the Wolves*). |

## Quick Start

### 1. Run WorldBox Studio & Mod Manager
```bash
python3 -m tools.studio.server
```
Open `http://127.0.0.1:8765` in your browser.

### 2. Stitch Worlds via CLI
```bash
python3 -m tools.wbox stitch \
  local/saves/world_a.wbox local/saves/world_b.wbox \
  --gap 2 \
  --name "Twin Continents" \
  --out local/output/twin_continents.wbox \
  --preview local/output/twin_continents.png
```

### 3. Install Mods in WorldBox (Desktop)
1. Enable **Experimental Mode** in WorldBox settings.
2. Place `NeoModLoader.dll` into `worldbox_Data/StreamingAssets/mods/` and launch once.
3. Copy any module folder from `mods/` (e.g., `mods/CoreScale`) into your WorldBox `Mods/` folder (or click **Deploy** inside WorldBox Studio's Mod Manager tab). NeoModLoader compiles `Code/*.cs` automatically at launch.

### 4. Build All Mods & Run Tests Locally
Copy WorldBox's `worldbox_Data/Managed/*.dll`, `NeoModLoader.dll`, and `0Harmony.dll` into `local/libs/` (gitignored), then run:
```bash
./scripts/build_all.sh
```

## License
MIT
