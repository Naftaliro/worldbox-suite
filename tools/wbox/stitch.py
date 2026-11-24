"""Stitch multiple WorldBox saveVersion 17 worlds into a single larger world.

Handles:
- Arbitrary 2D placement (horizontal row, vertical column, or grid)
- Configurable ocean gap & outer ocean border in chunks (1 chunk = 64x64 tiles)
- Full coordinate shifting for tiles, city zones (8x8), buildings, actors, fire, ice
- Collision-free ID remapping across all 19 saveVersion 17 entity namespaces
- Updating mapStats monotonic ID counters so runtime simulation stays clean
"""

from __future__ import annotations

import copy
import math
import random
from dataclasses import dataclass
from typing import Any

from .io import CHUNK_SIZE, ZONES_PER_CHUNK, decode_tiles, encode_tiles

# Entity collections paired with their mapStats monotonic counter key
ENTITY_COUNTERS: dict[str, str] = {
    "actors_data": "id_unit",
    "buildings": "id_building",
    "kingdoms": "id_kingdom",
    "cities": "id_city",
    "cultures": "id_culture",
    "clans": "id_clan",
    "alliances": "id_alliance",
    "wars": "id_war",
    "plots": "id_plot",
    "books": "id_book",
    "subspecies": "id_subspecies",
    "families": "id_family",
    "armies": "id_army",
    "languages": "id_language",
    "religions": "id_religion",
    "items": "id_item",
    "relations": "id_diplomacy",
}

SUMMED_STATS = (
    "housesBuilt",
    "housesDestroyed",
    "population",
    "creaturesBorn",
    "creaturesCreated",
    "subspeciesCreated",
    "subspeciesExtinct",
    "languagesCreated",
    "languagesForgotten",
    "booksWritten",
    "booksRead",
    "booksBurnt",
    "culturesCreated",
    "culturesForgotten",
    "religionsCreated",
    "religionsForgotten",
    "kingdomsCreated",
    "kingdomsDestroyed",
    "citiesCreated",
    "citiesConquered",
    "citiesRebelled",
    "citiesDestroyed",
    "alliancesMade",
    "alliancesDissolved",
    "warsStarted",
    "peacesMade",
    "familiesCreated",
    "familiesDestroyed",
    "armiesCreated",
    "armiesDestroyed",
    "clansCreated",
    "clansDestroyed",
    "plotsStarted",
    "plotsSucceeded",
    "plotsForgotten",
    "deaths",
    "deaths_age",
    "deaths_hunger",
    "deaths_eaten",
    "deaths_acid",
    "deaths_fire",
    "deaths_divine",
    "deaths_weapon",
    "deaths_gravity",
    "deaths_drowning",
    "deaths_water",
    "deaths_explosion",
)


@dataclass
class StitchOptions:
    name: str = "Stitched World"
    layout: str = "row"  # "row", "column", or "grid"
    gap_chunks: int = 2
    border_chunks: int = 0
    fill_tile: str = "deep_ocean"
    mode: str = "full"  # "full" or "terrain_only"
    coast_blend: bool = True
    archipelago: bool = False
    diplomacy_preset: str = "keep"  # "keep", "continent_war", "peace"
    seed: int = 42


def _shift_id(val: Any, offset: int) -> Any:
    if isinstance(val, int) and val > 0:
        return val + offset
    return val


def _shift_id_list(values: list[Any], offset: int) -> list[Any]:
    return [_shift_id(v, offset) for v in values]


def _shift_field(obj: dict[str, Any], key: str, offset: int) -> None:
    if key in obj and isinstance(obj[key], int) and obj[key] > 0:
        obj[key] = obj[key] + offset


def _shift_past_records(records: list[dict[str, Any]], actor_offset: int) -> None:
    for rec in records:
        if isinstance(rec, dict):
            _shift_field(rec, "id", actor_offset)


def _max_id_in_save(save: dict[str, Any], section: str, counter_key: str) -> int:
    max_val = int(save.get("mapStats", {}).get(counter_key, 0) or 0)
    for item in save.get(section, []):
        if isinstance(item, dict):
            val = item.get("id", 0)
            if isinstance(val, int) and val > max_val:
                max_val = val
    return max_val


def _remap_world_entities(
    world: dict[str, Any],
    offsets: dict[str, int],
    dx_chunks: int,
    dy_chunks: int,
) -> dict[str, list[dict[str, Any]]]:
    """Deep-copy and remap all entity IDs and coordinates for one placed world."""
    w = copy.deepcopy(world)
    dx_tiles = dx_chunks * CHUNK_SIZE
    dy_tiles = dy_chunks * CHUNK_SIZE
    dx_zones = dx_chunks * ZONES_PER_CHUNK
    dy_zones = dy_chunks * ZONES_PER_CHUNK

    o_actor = offsets["actors_data"]
    o_bld = offsets["buildings"]
    o_king = offsets["kingdoms"]
    o_city = offsets["cities"]
    o_cult = offsets["cultures"]
    o_clan = offsets["clans"]
    o_alli = offsets["alliances"]
    o_war = offsets["wars"]
    o_plot = offsets["plots"]
    o_book = offsets["books"]
    o_sub = offsets["subspecies"]
    o_fam = offsets["families"]
    o_army = offsets["armies"]
    o_lang = offsets["languages"]
    o_rel = offsets["religions"]
    o_item = offsets["items"]
    o_dip = offsets["relations"]

    # 1. Actors
    for a in w.get("actors_data", []):
        a["x"] = int(a.get("x", 0)) + dx_tiles
        a["y"] = int(a.get("y", 0)) + dy_tiles
        _shift_field(a, "id", o_actor)
        _shift_field(a, "cityID", o_city)
        _shift_field(a, "civ_kingdom_id", o_king)
        _shift_field(a, "homeBuildingID", o_bld)
        _shift_field(a, "army", o_army)
        _shift_field(a, "clan", o_clan)
        _shift_field(a, "culture", o_cult)
        _shift_field(a, "name_culture_id", o_cult)
        _shift_field(a, "language", o_lang)
        _shift_field(a, "religion", o_rel)
        _shift_field(a, "subspecies", o_sub)
        _shift_field(a, "family", o_fam)
        _shift_field(a, "ancestor_family", o_fam)
        _shift_field(a, "parent_id_1", o_actor)
        _shift_field(a, "parent_id_2", o_actor)
        _shift_field(a, "lover", o_actor)
        _shift_field(a, "best_friend_id", o_actor)
        if "saved_items" in a and isinstance(a["saved_items"], list):
            a["saved_items"] = _shift_id_list(a["saved_items"], o_item)

    # 2. Buildings
    for b in w.get("buildings", []):
        b["mainX"] = int(b.get("mainX", 0)) + dx_tiles
        b["mainY"] = int(b.get("mainY", 0)) + dy_tiles
        _shift_field(b, "id", o_bld)
        _shift_field(b, "cityID", o_city)
        books_obj = b.get("books")
        if isinstance(books_obj, dict) and isinstance(books_obj.get("list_books"), list):
            books_obj["list_books"] = _shift_id_list(books_obj["list_books"], o_book)

    # 3. Cities
    for c in w.get("cities", []):
        _shift_field(c, "id", o_city)
        _shift_field(c, "kingdomID", o_king)
        _shift_field(c, "last_kingdom_id", o_king)
        _shift_field(c, "leaderID", o_actor)
        _shift_field(c, "last_leader_id", o_actor)
        _shift_field(c, "founder_id", o_actor)
        _shift_field(c, "id_culture", o_cult)
        _shift_field(c, "name_culture_id", o_cult)
        _shift_field(c, "id_language", o_lang)
        _shift_field(c, "id_religion", o_rel)
        if isinstance(c.get("past_rulers"), list):
            _shift_past_records(c["past_rulers"], o_actor)
        eq = c.get("equipment")
        if isinstance(eq, dict):
            for slot, vals in eq.items():
                if isinstance(vals, list):
                    eq[slot] = _shift_id_list(vals, o_item)
        for z in c.get("zones", []):
            z["x"] = int(z.get("x", 0)) + dx_zones
            z["y"] = int(z.get("y", 0)) + dy_zones

    # 4. Kingdoms
    for k in w.get("kingdoms", []):
        _shift_field(k, "id", o_king)
        _shift_field(k, "capitalID", o_city)
        _shift_field(k, "last_capital_id", o_city)
        _shift_field(k, "kingID", o_actor)
        _shift_field(k, "royal_clan_id", o_clan)
        _shift_field(k, "allianceID", o_alli)
        _shift_field(k, "id_culture", o_cult)
        _shift_field(k, "id_language", o_lang)
        _shift_field(k, "id_religion", o_rel)
        if isinstance(k.get("past_rulers"), list):
            _shift_past_records(k["past_rulers"], o_actor)

    # 5. Clans
    for cl in w.get("clans", []):
        _shift_field(cl, "id", o_clan)
        _shift_field(cl, "chief_id", o_actor)
        _shift_field(cl, "founder_actor_id", o_actor)
        _shift_field(cl, "founder_city_id", o_city)
        _shift_field(cl, "founder_kingdom_id", o_king)
        _shift_field(cl, "creator_subspecies_id", o_sub)
        _shift_field(cl, "name_culture_id", o_cult)
        if isinstance(cl.get("past_chiefs"), list):
            _shift_past_records(cl["past_chiefs"], o_actor)

    # 6. Alliances
    for al in w.get("alliances", []):
        _shift_field(al, "id", o_alli)
        _shift_field(al, "founder_kingdom_id", o_king)
        _shift_field(al, "founder_actor_id", o_actor)
        _shift_field(al, "name_culture_id", o_cult)
        if isinstance(al.get("kingdoms"), list):
            al["kingdoms"] = _shift_id_list(al["kingdoms"], o_king)

    # 7. Wars
    for war in w.get("wars", []):
        _shift_field(war, "id", o_war)
        _shift_field(war, "main_attacker", o_king)
        _shift_field(war, "main_defender", o_king)
        _shift_field(war, "started_by_actor_id", o_actor)
        _shift_field(war, "started_by_kingdom_id", o_king)
        for lk in (
            "list_attackers",
            "list_defenders",
            "past_attackers",
            "past_defenders",
            "died_attackers",
            "died_defenders",
        ):
            if isinstance(war.get(lk), list):
                war[lk] = _shift_id_list(war[lk], o_king)

    # 8. Plots
    for p in w.get("plots", []):
        _shift_field(p, "id", o_plot)
        _shift_field(p, "founder_id", o_actor)
        _shift_field(p, "author_id", o_actor)
        _shift_field(p, "id_initiator_actor", o_actor)
        _shift_field(p, "id_target_actor", o_actor)
        _shift_field(p, "id_target_city", o_city)
        _shift_field(p, "id_target_kingdom", o_king)
        _shift_field(p, "id_target_alliance", o_alli)
        _shift_field(p, "id_target_war", o_war)
        if isinstance(p.get("supporters"), list):
            p["supporters"] = _shift_id_list(p["supporters"], o_actor)

    # 9. Diplomacy Relations
    for rel in w.get("relations", []):
        _shift_field(rel, "id", o_dip)
        _shift_field(rel, "kingdom1_id", o_king)
        _shift_field(rel, "kingdom2_id", o_king)
        if "kingdom1_id" in rel and "kingdom2_id" in rel:
            rel["rel_id"] = f"{rel['kingdom1_id']}_{rel['kingdom2_id']}"

    # 10. Cultures
    for cu in w.get("cultures", []):
        _shift_field(cu, "id", o_cult)
        _shift_field(cu, "creator_id", o_actor)
        _shift_field(cu, "creator_city_id", o_city)
        _shift_field(cu, "creator_clan_id", o_clan)
        _shift_field(cu, "creator_kingdom_id", o_king)
        _shift_field(cu, "creator_subspecies_id", o_sub)

    # 11. Books
    for bk in w.get("books", []):
        _shift_field(bk, "id", o_book)
        _shift_field(bk, "author_id", o_actor)
        _shift_field(bk, "author_clan_id", o_clan)
        _shift_field(bk, "author_kingdom_id", o_king)
        _shift_field(bk, "author_city_id", o_city)
        _shift_field(bk, "language_id", o_lang)
        _shift_field(bk, "culture_id", o_cult)
        _shift_field(bk, "religion_id", o_rel)

    # 12. Subspecies
    for sub in w.get("subspecies", []):
        _shift_field(sub, "id", o_sub)
        _shift_field(sub, "skeleton_form_id", o_sub)

    # 13. Languages
    for lg in w.get("languages", []):
        _shift_field(lg, "id", o_lang)
        _shift_field(lg, "creator_id", o_actor)
        _shift_field(lg, "creator_city_id", o_city)
        _shift_field(lg, "creator_clan_id", o_clan)
        _shift_field(lg, "creator_kingdom_id", o_king)
        _shift_field(lg, "creator_subspecies_id", o_sub)

    # 14. Religions
    for rg in w.get("religions", []):
        _shift_field(rg, "id", o_rel)
        _shift_field(rg, "creator_id", o_actor)
        _shift_field(rg, "creator_city_id", o_city)
        _shift_field(rg, "creator_clan_id", o_clan)
        _shift_field(rg, "creator_kingdom_id", o_king)
        _shift_field(rg, "creator_subspecies_id", o_sub)
        _shift_field(rg, "name_culture_id", o_cult)

    # 15. Families
    for fm in w.get("families", []):
        _shift_field(fm, "id", o_fam)
        _shift_field(fm, "alpha_id", o_actor)
        _shift_field(fm, "main_founder_id_1", o_actor)
        _shift_field(fm, "main_founder_id_2", o_actor)
        _shift_field(fm, "founder_city_id", o_city)
        _shift_field(fm, "founder_kingdom_id", o_king)
        _shift_field(fm, "subspecies_id", o_sub)
        _shift_field(fm, "name_culture_id", o_cult)
        _shift_field(fm, "original_family_1", o_fam)
        _shift_field(fm, "original_family_2", o_fam)

    # 16. Armies
    for ar in w.get("armies", []):
        _shift_field(ar, "id", o_army)
        _shift_field(ar, "id_city", o_city)
        _shift_field(ar, "id_captain", o_actor)
        _shift_field(ar, "id_kingdom", o_king)
        if isinstance(ar.get("past_captains"), list):
            _shift_past_records(ar["past_captains"], o_actor)

    # 17. Items
    for it in w.get("items", []):
        _shift_field(it, "id", o_item)
        _shift_field(it, "creator_id", o_actor)
        _shift_field(it, "creator_kingdom_id", o_king)

    return {sec: w.get(sec, []) for sec in ENTITY_COUNTERS}


def _compute_placements(
    worlds: list[dict[str, Any]], opts: StitchOptions
) -> tuple[int, int, list[tuple[int, int]]]:
    """Compute (total_w_chunks, total_h_chunks, [(dx_chunks, dy_chunks), ...])."""
    n = len(worlds)
    gap = max(0, opts.gap_chunks)
    border = max(0, opts.border_chunks)

    if opts.layout == "row":
        cols, rows = n, 1
    elif opts.layout == "column":
        cols, rows = 1, n
    else:
        cols = math.ceil(math.sqrt(n))
        rows = math.ceil(n / cols)

    col_widths = [0] * cols
    row_heights = [0] * rows
    for idx, w in enumerate(worlds):
        r = idx // cols
        c = idx % cols
        col_widths[c] = max(col_widths[c], int(w["width"]))
        row_heights[r] = max(row_heights[r], int(w["height"]))

    total_w = border * 2 + sum(col_widths) + gap * max(0, cols - 1)
    total_h = border * 2 + sum(row_heights) + gap * max(0, rows - 1)

    placements: list[tuple[int, int]] = []
    for idx, w in enumerate(worlds):
        r = idx // cols
        c = idx % cols
        # Place row 0 at the top visually (higher y in WorldBox coordinates)
        dx = border + sum(col_widths[:c]) + gap * c
        dy_from_top = sum(row_heights[:r]) + gap * r
        # Center smaller worlds vertically/horizontally within their cell
        cell_w = col_widths[c]
        cell_h = row_heights[r]
        dx += (cell_w - int(w["width"])) // 2
        dy = total_h - border - dy_from_top - cell_h + (cell_h - int(w["height"])) // 2
        placements.append((dx, dy))

    return total_w, total_h, placements


def _soften_channel_edges(
    grid: list[list[str]],
    dx_tiles: int,
    dy_tiles: int,
    w_tiles: int,
    h_tiles: int,
    total_w: int,
    total_h: int,
) -> None:
    """Add a 3-tile coastal gradient (shallow -> close -> deep ocean) outside abrupt land edges."""
    ocean_set = {"deep_ocean", "close_ocean", "shallow_waters"}

    def paint_ring(x: int, y: int) -> None:
        if grid[y][x] in ocean_set:
            return
        for dist, ocean_tile in ((1, "shallow_waters"), (2, "close_ocean"), (3, "close_ocean")):
            for nx, ny in ((x - dist, y), (x + dist, y), (x, y - dist), (x, y + dist)):
                if 0 <= nx < total_w and 0 <= ny < total_h:
                    if grid[ny][nx] == "deep_ocean":
                        grid[ny][nx] = ocean_tile

    # Only inspect the 4 outer border lines of each placed world
    for x in range(dx_tiles, dx_tiles + w_tiles):
        paint_ring(x, dy_tiles)
        paint_ring(x, dy_tiles + h_tiles - 1)
    for y in range(dy_tiles, dy_tiles + h_tiles):
        paint_ring(dx_tiles, y)
        paint_ring(dx_tiles + w_tiles - 1, y)


def _scatter_channel_archipelago(
    grid: list[list[str]],
    placements: list[tuple[int, int]],
    worlds: list[dict[str, Any]],
    total_w: int,
    total_h: int,
    seed: int = 42,
) -> None:
    """Procedurally generate natural stepping-stone islands in open deep-ocean channels."""
    rng = random.Random(seed)

    # Track occupied continent rectangles (with 6-tile safety margin so we don't overwrite coastlines)
    occupied: list[tuple[int, int, int, int]] = []
    for world, (dx, dy) in zip(worlds, placements):
        x0 = dx * CHUNK_SIZE - 6
        y0 = dy * CHUNK_SIZE - 6
        x1 = (dx + int(world["width"])) * CHUNK_SIZE + 6
        y1 = (dy + int(world["height"])) * CHUNK_SIZE + 6
        occupied.append((x0, y0, x1, y1))

    def in_continent(x: int, y: int) -> bool:
        for x0, y0, x1, y1 in occupied:
            if x0 <= x < x1 and y0 <= y < y1:
                return True
        return False

    # Collect candidate channel centers
    candidates: list[tuple[int, int]] = []
    step = 18
    for y in range(16, total_h - 16, step):
        for x in range(16, total_w - 16, step):
            if not in_continent(x, y) and grid[y][x] == "deep_ocean":
                candidates.append((x, y))

    if not candidates:
        return

    rng.shuffle(candidates)
    num_islands = max(3, min(14, len(candidates) // 5))
    chosen: list[tuple[int, int, int]] = []
    for cx, cy in candidates:
        if len(chosen) >= num_islands:
            break
        if any((cx - ox) ** 2 + (cy - oy) ** 2 < 26 ** 2 for ox, oy, _ in chosen):
            continue
        radius = rng.randint(7, 15)
        chosen.append((cx, cy, radius))

    for cx, cy, radius in chosen:
        # Irregular organic island shape using a few harmonic lobes
        a1, a2 = rng.uniform(0, 6.28), rng.uniform(0, 6.28)
        f1, f2 = rng.randint(2, 4), rng.randint(3, 6)
        biome = rng.choice(["soil_high:grass_high", "soil_low:jungle_low", "soil_low:savanna_low"])

        for y in range(max(1, cy - radius - 5), min(total_h - 1, cy + radius + 6)):
            for x in range(max(1, cx - radius - 5), min(total_w - 1, cx + radius + 6)):
                if in_continent(x, y):
                    continue
                dx, dy = x - cx, y - cy
                dist = math.hypot(dx, dy)
                ang = math.atan2(dy, dx)
                eff_r = radius * (1.0 + 0.18 * math.sin(f1 * ang + a1) + 0.10 * math.cos(f2 * ang + a2))
                if dist <= eff_r * 0.42:
                    grid[y][x] = "hills" if radius >= 12 and dist <= eff_r * 0.22 else biome
                elif dist <= eff_r * 0.72:
                    grid[y][x] = biome
                elif dist <= eff_r:
                    grid[y][x] = "sand"
                elif dist <= eff_r + 2.2 and grid[y][x] in ("deep_ocean", "close_ocean"):
                    grid[y][x] = "shallow_waters"
                elif dist <= eff_r + 4.4 and grid[y][x] == "deep_ocean":
                    grid[y][x] = "close_ocean"


def _apply_diplomacy_preset(
    merged_entities: dict[str, list[dict[str, Any]]],
    world_kingdom_groups: list[list[int]],
    merged_stats: dict[str, Any],
    preset: str,
) -> None:
    """Apply optional intercontinental war or peace preset after entity remapping."""
    if preset == "peace":
        now = float(merged_stats.get("world_time", 100.0))
        for war in merged_entities["wars"]:
            if not war.get("died_time"):
                war["died_time"] = now
                war["winner"] = 1
        return

    if preset != "continent_war" or len(world_kingdom_groups) < 2:
        return

    # Find highest-population kingdom from continent 1 and continent 2
    pop_by_k: dict[int, int] = {}
    for a in merged_entities["actors_data"]:
        kid = a.get("civ_kingdom_id", -1)
        if isinstance(kid, int) and kid > 0:
            pop_by_k[kid] = pop_by_k.get(kid, 0) + 1

    k_by_id = {k["id"]: k for k in merged_entities["kingdoms"] if isinstance(k, dict) and "id" in k}
    leaders: list[dict[str, Any]] = []
    for grp in world_kingdom_groups[:2]:
        valid = [k_by_id[kid] for kid in grp if kid in k_by_id]
        if valid:
            valid.sort(key=lambda k: -pop_by_k.get(k["id"], 0))
            leaders.append(valid[0])

    if len(leaders) < 2:
        return

    k1, k2 = leaders[0], leaders[1]
    next_war_id = max((int(w.get("id", 0)) for w in merged_entities["wars"]), default=0) + 1
    now = float(merged_stats.get("world_time", 100.0))
    merged_entities["wars"].append(
        {
            "id": next_war_id,
            "name": f"War of {k1.get('name', 'West')} and {k2.get('name', 'East')}",
            "created_time": now,
            "main_attacker": k1["id"],
            "main_defender": k2["id"],
            "list_attackers": [k1["id"]],
            "list_defenders": [k2["id"]],
            "past_attackers": [],
            "past_defenders": [],
            "died_attackers": [],
            "died_defenders": [],
            "started_by_kingdom_id": k1["id"],
            "started_by_kingdom_name": k1.get("name", ""),
            "started_by_actor_id": k1.get("kingID", -1),
            "dead_attackers": 0,
            "dead_defenders": 0,
            "total_deaths": 0,
            "renown": 10,
            "winner": 0,
        }
    )
    merged_stats["id_war"] = max(int(merged_stats.get("id_war", 1)), next_war_id + 1)
    merged_stats["warsStarted"] = int(merged_stats.get("warsStarted", 0)) + 1


def stitch_worlds(worlds: list[dict[str, Any]], options: StitchOptions | None = None) -> dict[str, Any]:
    """Combine 2 or more WorldBox saveVersion 17 saves into a single playable save dict."""
    if len(worlds) < 2:
        raise ValueError("stitch_worlds requires at least 2 worlds")

    opts = options or StitchOptions()
    total_w_chunks, total_h_chunks, placements = _compute_placements(worlds, opts)
    total_w_tiles = total_w_chunks * CHUNK_SIZE
    total_h_tiles = total_h_chunks * CHUNK_SIZE

    # Initialize full ocean canvas
    merged_grid: list[list[str]] = [
        [opts.fill_tile] * total_w_tiles for _ in range(total_h_tiles)
    ]

    merged_fire: list[int] = []
    merged_frozen: list[int] = []
    merged_eater: list[int] = []
    merged_creator: list[int] = []

    merged_entities: dict[str, list[dict[str, Any]]] = {k: [] for k in ENTITY_COUNTERS}
    running_offsets: dict[str, int] = {k: 0 for k in ENTITY_COUNTERS}
    world_kingdom_groups: list[list[int]] = []

    for world, (dx_chunks, dy_chunks) in zip(worlds, placements):
        dx_tiles = dx_chunks * CHUNK_SIZE
        dy_tiles = dy_chunks * CHUNK_SIZE
        w_tiles = int(world["width"]) * CHUNK_SIZE
        h_tiles = int(world["height"]) * CHUNK_SIZE

        # Copy tile terrain
        src_grid = decode_tiles(world)
        for y in range(h_tiles):
            dst_y = dy_tiles + y
            merged_grid[dst_y][dx_tiles : dx_tiles + w_tiles] = src_grid[y]

        if opts.coast_blend and (opts.gap_chunks > 0 or opts.border_chunks > 0):
            _soften_channel_edges(
                merged_grid, dx_tiles, dy_tiles, w_tiles, h_tiles, total_w_tiles, total_h_tiles
            )

        # Shift flat tile index arrays (idx = y * W + x)
        for src_key, dst_list in (
            ("fire", merged_fire),
            ("frozen_tiles", merged_frozen),
            ("conwayEater", merged_eater),
            ("conwayCreator", merged_creator),
        ):
            for idx in world.get(src_key, []):
                sy, sx = divmod(int(idx), w_tiles)
                if 0 <= sx < w_tiles and 0 <= sy < h_tiles:
                    dst_list.append((dy_tiles + sy) * total_w_tiles + (dx_tiles + sx))

        if opts.mode == "full":
            remapped = _remap_world_entities(world, running_offsets, dx_chunks, dy_chunks)
            world_kingdom_groups.append([int(k["id"]) for k in remapped["kingdoms"] if "id" in k])
            for sec, items in remapped.items():
                merged_entities[sec].extend(items)
            for sec, counter_key in ENTITY_COUNTERS.items():
                max_local = _max_id_in_save(world, sec, counter_key)
                running_offsets[sec] += max_local + 100

    if opts.archipelago and opts.gap_chunks > 0:
        _scatter_channel_archipelago(
            merged_grid, placements, worlds, total_w_tiles, total_h_tiles, seed=opts.seed
        )

    # Compress merged terrain back to WorldBox RLE format
    tile_map, tile_array, tile_amounts = encode_tiles(merged_grid)

    # Assemble merged mapStats from primary world + combined stats & safe ID counters
    base = worlds[0]
    merged_stats = copy.deepcopy(base.get("mapStats", {}))
    merged_stats["name"] = opts.name
    merged_stats["world_time"] = max(
        float(w.get("mapStats", {}).get("world_time", 0.0)) for w in worlds
    )
    merged_stats["history_current_year"] = max(
        int(w.get("mapStats", {}).get("history_current_year", 0)) for w in worlds
    )

    if opts.mode == "full":
        for key in SUMMED_STATS:
            total = sum(int(w.get("mapStats", {}).get(key, 0) or 0) for w in worlds)
            if total > 0 or key in merged_stats:
                merged_stats[key] = total
        merged_stats["population"] = len(merged_entities["actors_data"])
        for sec, counter_key in ENTITY_COUNTERS.items():
            max_used = max(
                (int(item.get("id", 0)) for item in merged_entities[sec] if isinstance(item, dict)),
                default=0,
            )
            merged_stats[counter_key] = max(max_used + 1, running_offsets[sec] + 1)
        if opts.diplomacy_preset != "keep":
            _apply_diplomacy_preset(merged_entities, world_kingdom_groups, merged_stats, opts.diplomacy_preset)
    else:
        for key in SUMMED_STATS:
            if key in merged_stats:
                merged_stats[key] = 0
        for counter_key in ENTITY_COUNTERS.values():
            merged_stats[counter_key] = 1

    # Bump transient counters safely
    for extra_counter in ("id_projectile", "id_status"):
        merged_stats[extra_counter] = (
            sum(int(w.get("mapStats", {}).get(extra_counter, 0) or 0) for w in worlds) + 100
        )

    merged: dict[str, Any] = {
        "saveVersion": base.get("saveVersion", 17),
        "width": total_w_chunks,
        "height": total_h_chunks,
        "hotkey_tabs_data": {},
        "camera_pos_x": total_w_tiles / 2.0,
        "camera_pos_y": total_h_tiles / 2.0,
        "camera_zoom": float(max(total_w_tiles, total_h_tiles) * 0.45),
        "mapStats": merged_stats,
        "worldLaws": copy.deepcopy(base.get("worldLaws", {"list": []})),
        "tileMap": tile_map,
        "tileArray": tile_array,
        "tileAmounts": tile_amounts,
        "fire": sorted(set(merged_fire)),
        "conwayEater": sorted(set(merged_eater)),
        "conwayCreator": sorted(set(merged_creator)),
        "frozen_tiles": sorted(set(merged_frozen)),
        "tiles": [],
    }
    for sec in ENTITY_COUNTERS:
        merged[sec] = merged_entities[sec]

    return merged


def verify_save(data: dict[str, Any]) -> list[str]:
    """Validate structural integrity of a saveVersion 17 save. Returns a list of errors (empty = valid)."""
    errors: list[str] = []
    w_chunks = int(data.get("width", 0))
    h_chunks = int(data.get("height", 0))
    w_tiles = w_chunks * CHUNK_SIZE
    h_tiles = h_chunks * CHUNK_SIZE
    w_zones = w_chunks * ZONES_PER_CHUNK
    h_zones = h_chunks * ZONES_PER_CHUNK

    palette = data.get("tileMap", [])
    t_arr = data.get("tileArray", [])
    t_amt = data.get("tileAmounts", [])

    if len(t_arr) != h_tiles or len(t_amt) != h_tiles:
        errors.append(f"Row count mismatch: {len(t_arr)}/{len(t_amt)} vs {h_tiles}")
    else:
        for y, (vals, amts) in enumerate(zip(t_arr, t_amt)):
            if len(vals) != len(amts):
                errors.append(f"Row {y}: tileArray/tileAmounts length mismatch")
                break
            if sum(amts) != w_tiles:
                errors.append(f"Row {y}: RLE sum {sum(amts)} != {w_tiles}")
                break
            if any(v < 0 or v >= len(palette) for v in vals):
                errors.append(f"Row {y}: out-of-range palette index")
                break

    max_flat = w_tiles * h_tiles
    for k in ("fire", "frozen_tiles", "conwayEater", "conwayCreator"):
        for idx in data.get(k, []):
            if not (0 <= idx < max_flat):
                errors.append(f"{k}: flat tile index {idx} out of [0, {max_flat})")
                break

    # Verify uniqueness of entity IDs and counter ceilings
    stats = data.get("mapStats", {})
    for sec, counter_key in ENTITY_COUNTERS.items():
        seen: set[int] = set()
        for item in data.get(sec, []):
            eid = item.get("id")
            if not isinstance(eid, int) or eid <= 0:
                errors.append(f"{sec}: invalid id {eid}")
                break
            if eid in seen:
                errors.append(f"{sec}: duplicate id {eid}")
                break
            seen.add(eid)
        if seen:
            counter = int(stats.get(counter_key, 0) or 0)
            if counter <= max(seen):
                errors.append(f"mapStats.{counter_key} ({counter}) <= max {sec}.id ({max(seen)})")

    # Coordinate bounds checks
    for a in data.get("actors_data", []):
        x, y = int(a.get("x", 0)), int(a.get("y", 0))
        if not (0 <= x < w_tiles and 0 <= y < h_tiles):
            errors.append(f"Actor {a.get('id')} out of bounds: ({x}, {y})")
            break

    for b in data.get("buildings", []):
        x, y = int(b.get("mainX", 0)), int(b.get("mainY", 0))
        if not (0 <= x < w_tiles and 0 <= y < h_tiles):
            errors.append(f"Building {b.get('id')} out of bounds: ({x}, {y})")
            break

    for c in data.get("cities", []):
        for z in c.get("zones", []):
            zx, zy = int(z.get("x", 0)), int(z.get("y", 0))
            if not (0 <= zx < w_zones and 0 <= zy < h_zones):
                errors.append(f"City {c.get('id')} zone out of bounds: ({zx}, {zy})")
                break

    # Verify relation rel_id consistency
    for rel in data.get("relations", []):
        expected = f"{rel.get('kingdom1_id')}_{rel.get('kingdom2_id')}"
        if rel.get("rel_id") != expected:
            errors.append(f"Relation {rel.get('id')} rel_id {rel.get('rel_id')} != {expected}")
            break

    return errors
