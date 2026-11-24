"""Read, write, decode, encode, and summarize WorldBox .wbox save files."""

from __future__ import annotations

import json
import zlib
from collections import Counter
from pathlib import Path
from typing import Any

CHUNK_SIZE = 64
ZONE_SIZE = 8
ZONES_PER_CHUNK = CHUNK_SIZE // ZONE_SIZE


def load_wbox(path: str | Path) -> dict[str, Any]:
    """Load a .wbox (zlib-compressed JSON) or uncompressed .wbax/.json WorldBox save."""
    raw = Path(path).read_bytes()
    for wbits in (15, -15, 31, None):
        try:
            payload = raw if wbits is None else zlib.decompress(raw, wbits)
            return json.loads(payload.decode("utf-8"))
        except Exception:
            continue
    raise ValueError(f"Could not decode WorldBox save file: {path}")


def save_wbox(data: dict[str, Any], path: str | Path, level: int = 6) -> Path:
    """Serialize and zlib-compress a save dictionary into a .wbox file."""
    out_path = Path(path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(data, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    compressed = zlib.compress(payload, level=level)
    out_path.write_bytes(compressed)
    return out_path


def decode_tiles(data: dict[str, Any]) -> list[list[str]]:
    """Expand run-length encoded tileArray/tileAmounts into a grid[y][x] of tile names."""
    width_tiles = int(data["width"]) * CHUNK_SIZE
    height_tiles = int(data["height"]) * CHUNK_SIZE
    palette: list[str] = data["tileMap"]
    tile_array: list[list[int]] = data["tileArray"]
    tile_amounts: list[list[int]] = data["tileAmounts"]

    if len(tile_array) != height_tiles or len(tile_amounts) != height_tiles:
        raise ValueError(
            f"Tile row count mismatch: expected {height_tiles}, got {len(tile_array)}/{len(tile_amounts)}"
        )

    grid: list[list[str]] = []
    for y, (vals, amts) in enumerate(zip(tile_array, tile_amounts)):
        row: list[str] = []
        for idx, count in zip(vals, amts):
            row.extend([palette[idx]] * count)
        if len(row) != width_tiles:
            raise ValueError(f"Row {y} expanded to {len(row)} tiles, expected {width_tiles}")
        grid.append(row)
    return grid


def encode_tiles(grid: list[list[str]]) -> tuple[list[str], list[list[int]], list[list[int]]]:
    """Compress a grid[y][x] of tile names into (tileMap, tileArray, tileAmounts)."""
    palette_map: dict[str, int] = {}
    palette: list[str] = []
    tile_array: list[list[int]] = []
    tile_amounts: list[list[int]] = []

    for row in grid:
        if not row:
            tile_array.append([])
            tile_amounts.append([])
            continue

        row_vals: list[int] = []
        row_amts: list[int] = []

        first = row[0]
        cur_idx = palette_map.get(first)
        if cur_idx is None:
            cur_idx = len(palette)
            palette_map[first] = cur_idx
            palette.append(first)
        cur_count = 1

        for tile in row[1:]:
            t_idx = palette_map.get(tile)
            if t_idx is None:
                t_idx = len(palette)
                palette_map[tile] = t_idx
                palette.append(tile)
            if t_idx == cur_idx:
                cur_count += 1
            else:
                row_vals.append(cur_idx)
                row_amts.append(cur_count)
                cur_idx = t_idx
                cur_count = 1

        row_vals.append(cur_idx)
        row_amts.append(cur_count)
        tile_array.append(row_vals)
        tile_amounts.append(row_amts)

    return palette, tile_array, tile_amounts


def summarize_save(data: dict[str, Any]) -> dict[str, Any]:
    """Build a structured summary of a WorldBox save for CLI and Studio UI display."""
    stats = data.get("mapStats", {})
    w_chunks = int(data.get("width", 0))
    h_chunks = int(data.get("height", 0))

    cities_by_kingdom: Counter[int] = Counter()
    for city in data.get("cities", []):
        kid = city.get("kingdomID", -1)
        if kid and kid > 0:
            cities_by_kingdom[kid] += 1

    pop_by_kingdom: Counter[int] = Counter()
    species_counts: Counter[str] = Counter()
    for actor in data.get("actors_data", []):
        kid = actor.get("civ_kingdom_id", -1)
        if kid and kid > 0:
            pop_by_kingdom[kid] += 1
        species_counts[actor.get("asset_id", "unknown")] += 1

    kingdoms_summary = []
    for k in data.get("kingdoms", []):
        kid = k.get("id", 0)
        kingdoms_summary.append(
            {
                "id": kid,
                "name": k.get("name", "Unnamed"),
                "motto": k.get("motto", ""),
                "species": k.get("original_actor_asset", "unknown"),
                "color_id": k.get("color_id", 0),
                "cities": cities_by_kingdom.get(kid, 0),
                "population": pop_by_kingdom.get(kid, 0),
                "renown": k.get("renown", 0),
                "kills": k.get("total_kills", 0),
                "deaths": k.get("total_deaths", 0),
            }
        )
    kingdoms_summary.sort(key=lambda x: (-x["population"], -x["cities"], x["name"]))

    wars_summary = []
    for w in data.get("wars", []):
        wars_summary.append(
            {
                "id": w.get("id", 0),
                "name": w.get("name", "Unnamed War"),
                "active": w.get("died_time", 0) == 0 and w.get("winner", 0) == 0,
                "total_deaths": w.get("total_deaths", 0),
                "dead_attackers": w.get("dead_attackers", 0),
                "dead_defenders": w.get("dead_defenders", 0),
                "started_by": w.get("started_by_kingdom_name", ""),
                "created_time": w.get("created_time", 0.0),
                "died_time": w.get("died_time", 0.0),
            }
        )
    wars_summary.sort(key=lambda x: (-int(x["active"]), -x["total_deaths"]))

    # Tile biome breakdown using RLE counts directly (fast, no full decode needed)
    palette = data.get("tileMap", [])
    tile_counts: Counter[str] = Counter()
    for vals, amts in zip(data.get("tileArray", []), data.get("tileAmounts", [])):
        for v, a in zip(vals, amts):
            tile_counts[palette[v]] += a

    total_tiles = w_chunks * CHUNK_SIZE * h_chunks * CHUNK_SIZE
    water_tiles = sum(
        c for t, c in tile_counts.items() if "ocean" in t or "water" in t
    )

    return {
        "name": stats.get("name", "Untitled World"),
        "saveVersion": data.get("saveVersion"),
        "width_chunks": w_chunks,
        "height_chunks": h_chunks,
        "width_tiles": w_chunks * CHUNK_SIZE,
        "height_tiles": h_chunks * CHUNK_SIZE,
        "total_tiles": total_tiles,
        "land_percent": round(100.0 * (total_tiles - water_tiles) / max(total_tiles, 1), 1),
        "year": stats.get("history_current_year", 0),
        "world_time": round(float(stats.get("world_time", 0.0)), 1),
        "world_age": stats.get("world_age_id", ""),
        "counts": {
            "actors": len(data.get("actors_data", [])),
            "cities": len(data.get("cities", [])),
            "kingdoms": len(data.get("kingdoms", [])),
            "buildings": len(data.get("buildings", [])),
            "armies": len(data.get("armies", [])),
            "clans": len(data.get("clans", [])),
            "cultures": len(data.get("cultures", [])),
            "languages": len(data.get("languages", [])),
            "religions": len(data.get("religions", [])),
            "subspecies": len(data.get("subspecies", [])),
            "families": len(data.get("families", [])),
            "wars": len(data.get("wars", [])),
            "books": len(data.get("books", [])),
            "items": len(data.get("items", [])),
        },
        "kingdoms": kingdoms_summary,
        "wars": wars_summary[:25],
        "top_species": species_counts.most_common(12),
        "top_tiles": tile_counts.most_common(12),
    }
