# worldbox-suite

an 8-mod classic pack, an opt-in modern warfare & geopolitics expansion, and a local web studio for WorldBox (Steam, saveVersion 17). built for giant multi-continent maps, bigger wars that stay playable, mossy ruins of fallen empires, and messing with `.wbox` saves without hex editing anything.

catppuccin mocha everywhere.

## install

**windows (one-liner in powershell — no git needed):**

classic melee / fantasy core pack (8 mods, zero guns):
```powershell
irm https://raw.githubusercontent.com/Naftaliro/worldbox-suite/main/scripts/install.ps1 | iex
```

want the opt-in **ModernAge** expansion too (tier 1–3 firearms, tanks, jets, helicopters, radar/SAMs, governments & trade)?
```powershell
$env:WORLDBOX_MODERN=1; irm https://raw.githubusercontent.com/Naftaliro/worldbox-suite/main/scripts/install.ps1 | iex
```

both auto-detect your Steam WorldBox install across drives, download `NeoModLoader.dll` if you don't have it yet, and install the mods cleanly into `Mods/`.

if you already cloned the repo:
* **windows:** double-click `install.bat` (or pass `-IncludeModern` to `scripts\install_mods.ps1`)
* **mac / linux:** `./scripts/install_mods.sh` (or `./scripts/install_mods.sh --modern`)

open WorldBox, go to settings, turn on **Experimental Mode**, and restart. NeoModLoader compiles each mod's `Code/*.cs` on startup so you don't need `.NET` installed just to play. once you're in a world, click the **WorldBox Suite** tab on the bottom power bar.

## what's in the pack

### classic core pack (`mods/` — installed by default, no firearms)

* **SuiteHub** (`bottom bar` / `F2`–`F5`): native WorldBox Suite tab on the bottom power bar with buttons for every installed module. includes the floating **War & Diplomacy Monitor** (`F4`) with opinion scores and 1-click camera jumps, pulsing **Battlefield Conflict Radar** (`F3`), and an in-game **Multi-Continent World Builder** (`F2`).
* **CoreScale** (`F8`): giant map presets (`12×12` up to `24×24` chunks), wider camera zoom, multi-core parallel job tuning, strategic animation culling, 3-tier adaptive AI throttling (3k/8k/15k pop), and army battalion compression.
* **VisualPolish** (`F9`, `F6` photo mode): lighting presets (crisp, golden hour, moonlit night, parchment atlas, mocha twilight), optional day/night cycle, soft vignette, closer zoom-in, brighter strategic borders, and `F6` HUD toggle.
* **GrandWarfare** (`F10`): ship-to-ship broadsides, coastal bombardment of shoreline troops, supply-line attrition outside friendly cities, and defense pacts.
* **LinkedContinents** (`F11`): links separate `.wbox` continent saves across ocean edges via `portal_queue.json` so fleets and expeditions cross between worlds.
* **EraChronicle** (`F7`): 5-tier kingdom era progression (*Dawn Age* through *High Hegemony*) with milestone buffs and JSON history snapshots readable in WorldBox Studio.
* **ApexFactions** (`F12`): the **Warden** "Sleeping Giant" doctrine (strong, peaceful humans who never start wars, rally relentlessly if attacked, and force a ceasefire once the attacker drops to 15% of its war-start pop), instant species awakener (`civ_wolf` + `makeSapient` for all wild animals), and feral pack domains (*Land of the Wolves*, *Land of the Bears*, etc.).
* **WorldRelics** (`Alt+R`): large cities fortify their borders with gated walls, friendly cities across narrow ocean straits build stone sea bridges, and fallen empires leave behind walls and roadworks that slowly weather over 100–300+ years (`wall_order`/`wall_iron` -> `wall_ancient` -> mossy `wall_green` -> vine-covered `wall_wild`) while younger tribes settle inside the ruins.

### opt-in expansion (`expansions/ModernAge/` — `Alt+M`)

kept separate from the classic pack so melee playthroughs stay pure unless you opt in. rock-paper-scissors balance across 3 tech tiers:

* **tier 1 (blackpowder & counters):** flintlock pistol, rifled musket, blunderbuss, hand culverin **vs.** Bastion bulletproof plate (60% bullet deflection), Aegis ward amulets, fast shock halberds (+45% vs gunners), and composite warbows.
* **tier 2 (industrial / early cold war):** light tanks, prop fighters, early gunship helicopters, and paved inter-city highways **vs.** anti-tank rifles (+165% vs armor) and flak AA autocannons (+185% vs aircraft).
* **tier 3 (modern warfare):** main battle tanks (MBTs), attack helicopters, supersonic jets, and automated capital **Radar & SAM defense domes** **vs.** guided anti-armor launchers and radar-guided SAM infantry.
* **governments & international trade:** 6 government types (*Monarchy, Republic, Collectivist, Merchant League, Military Junta, Technocracy*) and trade specialties (*Breadbasket, Armory State lend-lease, Maritime Hub, Research Hub*).

### worldbox studio & cli (`tools/`)

pure python 3 (no pip installs needed):

```bash
python3 -m tools.studio.server
```

open `http://127.0.0.1:8765` for the **World Inspector**, **World Stitcher** (merge 2+ `.wbox` saves with ocean channels, stepping-stone archipelagos, and instant war/peace presets), **Chronicle Viewer**, and **Mod Manager** (deploys both core mods and the `ModernAge` expansion).

## building it

```bash
python3 -m unittest discover -s tests -v
./scripts/build_all.sh
```

to compile-check all 9 C# mods locally, copy `worldbox_Data/Managed/*.dll`, `NeoModLoader.dll`, and `0Harmony.dll` into `local/libs/` (gitignored).

## license

[MIT](LICENSE)

---

worldbox-suite is an unofficial, fan-made mod pack and toolkit. not affiliated with or endorsed by Maxim Karpenko or WorldBox.
