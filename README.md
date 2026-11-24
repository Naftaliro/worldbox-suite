# worldbox-suite

a pack of 8 NeoModLoader mods and a local web studio for WorldBox (Steam, saveVersion 17). built for giant multi-continent maps, bigger wars that stay playable, and messing with `.wbox` saves without hex editing anything.

catppuccin mocha everywhere.

## install

**windows (one-liner in powershell — no git needed):**
```powershell
irm https://raw.githubusercontent.com/Naftaliro/worldbox-suite/main/scripts/install.ps1 | iex
```

that auto-detects your Steam WorldBox install across drives, downloads `NeoModLoader.dll` if you don't have it yet, and installs all 8 mods into `Mods/`.

if you already cloned the repo:
* **windows:** double-click `install.bat` (or run `powershell -ExecutionPolicy Bypass -File .\scripts\install_mods.ps1`)
* **mac / linux:** run `./scripts/install_mods.sh`

if WorldBox is on a custom path:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install_mods.ps1 -WorldBoxDir "D:\SteamLibrary\steamapps\common\worldbox"
```

open WorldBox, go to settings, turn on **Experimental Mode**, and restart. NeoModLoader compiles the `Code/*.cs` files on startup so you don't need `.NET` installed just to play. once you're in a world, click the **WorldBox Suite** tab on the bottom power bar (or use `F2`–`F12`).

## what's in the pack

### in-game mods (`mods/`)

* **SuiteHub** (`bottom bar` / `F2`–`F5`): adds a native WorldBox Suite tab to the bottom power bar with buttons for every mod in the pack. also includes a floating **War & Diplomacy Monitor** (`F4`) so you can see active wars, casualties, opinion scores and jump straight to fights without leaving normal view, a **Battlefield Conflict Radar** (`F3`) that draws pulsing clash rings over active fights, and an in-game **Multi-Continent World Builder** (`F2`) for spinning up fresh pre-stitched continent maps.
* **CoreScale** (`F8`): unlocks giant map presets (`12×12`, `16×16`, `20×20`, `24×24` chunks) and wider camera zoom. turns on multi-core parallel job settings, skips micro-sprite animations when zoomed out, throttles heavy AI batches progressively past 3k / 8k / 15k pop, and adds army battalion compression to fold huge squads into stronger veteran champions during big wars.
* **VisualPolish** (`F9`, `F6` photo mode): lighting presets (crisp, golden hour, moonlit night, parchment atlas, mocha twilight), optional day/night cycle, soft vignette, closer zoom-in, brighter strategic borders, and `F6` to hide the HUD for screenshots.
* **GrandWarfare** (`F10`): ship-to-ship broadsides and coastal bombardment against enemy shoreline troops, supply-line attrition when armies march far from friendly cities, and defense pacts that pull allies into defensive wars.
* **LinkedContinents** (`F11`): links separate `.wbox` continent saves across ocean edges using a local `portal_queue.json`. fleets sailing past the configured map edge (or sent manually) get queued up and land on the opposite coast when you load the linked world.
* **EraChronicle** (`F7`): tracks kingdoms across 5 era tiers (*Dawn Age* through *High Hegemony*) with milestone buffs and writes periodic JSON snapshots you can open in WorldBox Studio.
* **ApexFactions** (`F12`): adds the **Warden** doctrine (strong, peaceful humans who never start wars, rally hard if attacked, and force a ceasefire once the attacker drops to 15% of its war-start pop so survivors live to rebuild), an instant species awakener (`civ_wolf` spawner + `makeSapient` for all wild animals), and feral pack domains (*Land of the Wolves*, *Land of the Bears*, etc.) with giant Alphas that defend their hunting grounds.
* **Arsenal** (`Alt+A`): balanced blackpowder firearms (flintlock pistol, rifled musket, blunderbuss, hand culverin) paired with real counter-firearm gear (Bastion plate that deflects 60% of bullet damage, Aegis ward amulets that trigger brief shields under fire, fast shock halberds with bonus damage against gunners, and quick-firing composite warbows). era-gated by default or unlockable from year 1.

### worldbox studio & cli (`tools/`)

pure python 3 (no pip installs needed). start the local web UI:

```bash
python3 -m tools.studio.server
```

open `http://127.0.0.1:8765` for:
* **world inspector:** render `.wbox` maps to PNG with toggleable borders/buildings, check kingdoms and war history, or tweak saves (rename worlds, force world peace, kick off a superpower war).
* **world stitcher:** combine 2 or more existing `.wbox` saves (row, column, or grid) with an ocean channel, optional stepping-stone archipelago islands, and instant war/peace presets. shifts all tiles, 8×8 city zones, buildings, units, fire/ice and remaps every entity ID namespace cleanly.
* **chronicle viewer:** population timeline charts and era logs from `EraChronicle` or loaded saves.
* **mod manager:** toggle NeoModLoader mods on/off, switch profiles, deploy suite modules, and zip up save backups.

or stitch straight from the terminal:

```bash
python3 -m tools.wbox stitch world_a.wbox world_b.wbox \
  --gap 3 --archipelago --diplomacy continent_war \
  --name "Twin Continents" \
  --out twin_continents.wbox --preview twin_continents.png
```

## building it

python tests run with stock python 3:

```bash
python3 -m unittest discover -s tests -v
```

to compile-check all 8 C# mods locally, copy your `worldbox_Data/Managed/*.dll`, `NeoModLoader.dll`, and `0Harmony.dll` into `local/libs/` (gitignored, never pushed), install the `.NET` SDK, and run:

```bash
./scripts/build_all.sh
```

`mods/Directory.Build.props` builds offline directly against WorldBox's own Mono BCL DLLs so nothing gets pulled from NuGet.

## license

[MIT](LICENSE)

---

worldbox-suite is an unofficial, fan-made mod pack and toolkit. not affiliated with or endorsed by Maxim Karpenko or WorldBox.
