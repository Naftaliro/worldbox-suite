"""Automated tests for tools.wbox (IO, RLE codec, stitching, and verification)."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from tools.wbox import (
    StitchOptions,
    decode_tiles,
    encode_tiles,
    load_wbox,
    render_map_png_bytes,
    save_wbox,
    stitch_worlds,
    summarize_save,
    verify_save,
)

REPO_ROOT = Path(__file__).resolve().parent.parent
SAVE_1 = REPO_ROOT / "local" / "saves" / "isles_of_death.wbox"
SAVE_2 = REPO_ROOT / "local" / "saves" / "never_box.wbox"


def _make_synthetic_world(name: str, w_chunks: int = 1, h_chunks: int = 1, tile: str = "soil_high:grass_high") -> dict:
    w_tiles = w_chunks * 64
    h_tiles = h_chunks * 64
    grid = [[tile] * w_tiles for _ in range(h_tiles)]
    tmap, tarr, tamt = encode_tiles(grid)
    return {
        "saveVersion": 17,
        "width": w_chunks,
        "height": h_chunks,
        "hotkey_tabs_data": {},
        "camera_pos_x": w_tiles / 2.0,
        "camera_pos_y": h_tiles / 2.0,
        "camera_zoom": 64.0,
        "mapStats": {
            "name": name,
            "world_time": 100.0,
            "history_current_year": 10,
            "id_unit": 5,
            "id_building": 5,
            "id_kingdom": 5,
            "id_city": 5,
            "id_culture": 5,
            "id_clan": 5,
            "id_alliance": 5,
            "id_war": 5,
            "id_plot": 5,
            "id_book": 5,
            "id_subspecies": 5,
            "id_family": 5,
            "id_army": 5,
            "id_language": 5,
            "id_religion": 5,
            "id_item": 5,
            "id_diplomacy": 5,
            "id_projectile": 5,
            "id_status": 5,
        },
        "worldLaws": {"list": []},
        "tileMap": tmap,
        "tileArray": tarr,
        "tileAmounts": tamt,
        "fire": [65],  # (x=1, y=1)
        "conwayEater": [],
        "conwayCreator": [],
        "frozen_tiles": [0],  # (x=0, y=0)
        "tiles": [],
        "cities": [
            {
                "id": 1,
                "name": f"{name} City",
                "kingdomID": 1,
                "leaderID": 1,
                "zones": [{"x": 0, "y": 0}, {"x": 1, "y": 1}],
                "equipment": {"item_storage_weapons": [1]},
            }
        ],
        "actors_data": [
            {
                "id": 1,
                "name": f"{name} Hero",
                "asset_id": "human",
                "x": 10,
                "y": 12,
                "cityID": 1,
                "civ_kingdom_id": 1,
                "homeBuildingID": 1,
                "saved_items": [1],
            }
        ],
        "buildings": [
            {
                "id": 1,
                "asset_id": "house_human_0",
                "mainX": 10,
                "mainY": 12,
                "cityID": 1,
                "books": {"list_books": [1]},
            }
        ],
        "kingdoms": [{"id": 1, "name": f"{name} Kingdom", "capitalID": 1, "kingID": 1, "color_id": 2}],
        "clans": [{"id": 1, "name": "Clan", "chief_id": 1, "founder_city_id": 1, "founder_kingdom_id": 1}],
        "alliances": [],
        "wars": [{"id": 1, "name": "Old War", "main_attacker": 1, "list_attackers": [1], "list_defenders": []}],
        "plots": [],
        "relations": [{"id": 1, "kingdom1_id": 1, "kingdom2_id": 2, "rel_id": "1_2"}],
        "cultures": [{"id": 1, "name": "Cult", "creator_id": 1, "creator_city_id": 1}],
        "books": [{"id": 1, "name": "Book", "author_id": 1, "culture_id": 1}],
        "subspecies": [{"id": 1, "name": "Humans"}],
        "languages": [{"id": 1, "name": "Lang", "creator_id": 1}],
        "religions": [{"id": 1, "name": "Rel", "creator_id": 1}],
        "families": [{"id": 1, "name": "Fam", "alpha_id": 1}],
        "armies": [{"id": 1, "name": "Army", "id_city": 1, "id_captain": 1, "id_kingdom": 1}],
        "items": [{"id": 1, "name": "Sword", "creator_id": 1, "creator_kingdom_id": 1}],
    }


class TestWboxToolkit(unittest.TestCase):
    def test_synthetic_stitch_row_and_grid(self) -> None:
        w1 = _make_synthetic_world("West", 1, 1, "soil_high:grass_high")
        w2 = _make_synthetic_world("East", 1, 1, "soil_high:desert_high")

        merged = stitch_worlds(
            [w1, w2],
            StitchOptions(name="West+East", layout="row", gap_chunks=1, border_chunks=0, mode="full"),
        )
        self.assertEqual(verify_save(merged), [])
        self.assertEqual(merged["width"], 3)
        self.assertEqual(merged["height"], 1)
        self.assertEqual(len(merged["actors_data"]), 2)
        self.assertEqual(len(merged["kingdoms"]), 2)
        self.assertEqual(len(merged["cities"]), 2)

        # Second world shifted by 2 chunks (128 tiles, 16 zones)
        east_actor = [a for a in merged["actors_data"] if a["name"] == "East Hero"][0]
        self.assertEqual((east_actor["x"], east_actor["y"]), (138, 12))
        self.assertGreater(east_actor["id"], 1)
        self.assertEqual(east_actor["cityID"], merged["cities"][1]["id"])
        self.assertEqual(east_actor["civ_kingdom_id"], merged["kingdoms"][1]["id"])
        self.assertEqual(east_actor["saved_items"], [merged["items"][1]["id"]])

        # Check relation rel_id string updated to match shifted kingdom IDs
        east_rel = merged["relations"][1]
        self.assertEqual(east_rel["rel_id"], f"{east_rel['kingdom1_id']}_{east_rel['kingdom2_id']}")

        # Check terrain_only mode drops civilizational data cleanly
        terrain = stitch_worlds(
            [w1, w2],
            StitchOptions(name="Blank", layout="grid", gap_chunks=1, mode="terrain_only"),
        )
        self.assertEqual(verify_save(terrain), [])
        self.assertEqual(len(terrain["actors_data"]), 0)
        self.assertEqual(len(terrain["cities"]), 0)

        # Round-trip save & PNG rendering
        with tempfile.TemporaryDirectory() as tmp:
            out_wbox = Path(tmp) / "test.wbox"
            save_wbox(merged, out_wbox)
            reloaded = load_wbox(out_wbox)
            self.assertEqual(verify_save(reloaded), [])
            png_bytes = render_map_png_bytes(reloaded, scale=1)
            self.assertTrue(png_bytes.startswith(b"\x89PNG\r\n\x1a\n"))

    @unittest.skipUnless(SAVE_1.exists() and SAVE_2.exists(), "Local sample saves not present")
    def test_real_saves_stitch_integrity(self) -> None:
        s1 = load_wbox(SAVE_1)
        s2 = load_wbox(SAVE_2)
        self.assertEqual(verify_save(s1), [])
        self.assertEqual(verify_save(s2), [])

        merged = stitch_worlds(
            [s1, s2],
            StitchOptions(name="Twin Realms", layout="row", gap_chunks=2, mode="full"),
        )
        self.assertEqual(verify_save(merged), [])
        summary = summarize_save(merged)
        self.assertEqual(summary["counts"]["actors"], len(s1["actors_data"]) + len(s2["actors_data"]))
        self.assertEqual(summary["counts"]["kingdoms"], len(s1["kingdoms"]) + len(s2["kingdoms"]))
        self.assertEqual(summary["counts"]["cities"], len(s1["cities"]) + len(s2["cities"]))
        self.assertEqual(summary["counts"]["buildings"], len(s1["buildings"]) + len(s2["buildings"]))

        # Verify tile decode/encode roundtrip preserves exact dimensions
        grid = decode_tiles(merged)
        self.assertEqual(len(grid), 4 * 64)
        self.assertEqual(len(grid[0]), 10 * 64)

        # Verify archipelago + intercontinental war scenario preset
        war_world = stitch_worlds(
            [s1, s2],
            StitchOptions(
                name="Archipelago Clash",
                layout="row",
                gap_chunks=3,
                mode="full",
                archipelago=True,
                diplomacy_preset="continent_war",
            ),
        )
        self.assertEqual(verify_save(war_world), [])
        self.assertEqual(len(war_world["wars"]), len(s1["wars"]) + len(s2["wars"]) + 1)
        self.assertTrue(any(w.get("winner", 0) == 0 and not w.get("died_time") for w in war_world["wars"]))

        # Verify peace preset closes active wars
        peace_world = stitch_worlds(
            [s1, s2],
            StitchOptions(name="Peaceful Realms", gap_chunks=2, diplomacy_preset="peace"),
        )
        self.assertEqual(verify_save(peace_world), [])
        self.assertTrue(all(w.get("died_time", 0) > 0 for w in peace_world["wars"]))

    def test_studio_save_slot_export_and_diagnostics(self) -> None:
        import json
        from tools.studio.diagnostics import analyze_player_log, export_wbox_to_slot, list_save_slots

        w = _make_synthetic_world("Slot Realm", 1, 1, "soil_high:grass_high")
        with tempfile.TemporaryDirectory() as tmp:
            tmp_path = Path(tmp)
            src_wbox = tmp_path / "slot_realm.wbox"
            save_wbox(w, src_wbox)

            slots_root = tmp_path / "saves"
            result = export_wbox_to_slot(src_wbox, 7, str(slots_root))
            self.assertEqual(result["slot"], 7)

            slot_dir = slots_root / "save7"
            self.assertTrue((slot_dir / "map.wbox").exists())
            self.assertTrue((slot_dir / "map.meta").exists())
            self.assertTrue((slot_dir / "preview.png").exists())
            self.assertTrue((slot_dir / "preview_small.png").exists())

            meta = json.loads((slot_dir / "map.meta").read_text(encoding="utf-8"))
            self.assertEqual(meta["saveVersion"], 17)
            self.assertEqual(meta["mapStats"]["name"], "Slot Realm")
            self.assertEqual(meta["kingdoms"], 1)
            self.assertTrue(meta["modded"])

            listing = list_save_slots(str(slots_root), max_slots=10)
            self.assertTrue(listing["exists"])
            slot7 = [s for s in listing["slots"] if s["slot"] == 7][0]
            self.assertTrue(slot7["occupied"])
            self.assertEqual(slot7["name"], "Slot Realm")

            # Verify Player.log diagnostic parsing
            log_file = tmp_path / "Player.log"
            log_file.write_text(
                "\n".join(
                    [
                        "[CoreScale] Loaded v0.2.0",
                        "[ModernAge] Expansion loaded (6 vehicle sprites)",
                        "[ApexFactions] NullReferenceException: Object reference not set",
                        "  at ApexFactions.WardenSystem.Tick () [0x00010]",
                    ]
                ),
                encoding="utf-8",
            )
            diag = analyze_player_log(str(log_file))
            self.assertTrue(diag["found"])
            self.assertEqual(diag["mod_health"]["CoreScale"], "ok")
            self.assertEqual(diag["mod_health"]["ModernAge"], "ok")
            self.assertEqual(diag["mod_health"]["ApexFactions"], "error")
            self.assertEqual(len(diag["errors"]), 1)
            self.assertIn("WardenSystem.Tick", diag["errors"][0])


if __name__ == "__main__":
    unittest.main()
