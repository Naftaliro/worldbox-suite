"""Pure-Python PNG renderer for WorldBox saves (terrain, biomes, kingdoms, buildings)."""

from __future__ import annotations

import struct
import zlib
from pathlib import Path
from typing import Any

from .io import CHUNK_SIZE, ZONE_SIZE, decode_tiles

RGB = tuple[int, int, int]

# Biome and terrain colors tuned to resemble WorldBox's in-game minimap
TILE_COLORS: dict[str, RGB] = {
    "deep_ocean": (24, 58, 108),
    "close_ocean": (36, 86, 146),
    "shallow_waters": (66, 134, 186),
    "pit_deep_ocean": (20, 48, 92),
    "pit_close_ocean": (32, 74, 128),
    "pit_shallow_waters": (56, 118, 166),
    "sand": (232, 212, 144),
    "soil_low": (146, 102, 66),
    "soil_high": (122, 84, 52),
    "hills": (118, 122, 128),
    "mountains": (92, 96, 104),
    "summit": (222, 230, 238),
    "lava0": (248, 112, 28),
    "lava1": (252, 144, 38),
    "lava2": (255, 178, 56),
    "lava3": (255, 214, 92),
}

OVERLAY_COLORS: dict[str, RGB] = {
    "grass_low": (116, 178, 72),
    "grass_high": (86, 148, 54),
    "savanna_low": (196, 176, 82),
    "savanna_high": (176, 150, 62),
    "jungle_low": (48, 134, 62),
    "jungle_high": (32, 106, 48),
    "desert_low": (228, 198, 126),
    "desert_high": (212, 176, 104),
    "permafrost_low": (192, 218, 232),
    "permafrost_high": (168, 200, 220),
    "swamp_low": (82, 106, 74),
    "swamp_high": (64, 86, 62),
    "enchanted_low": (120, 208, 152),
    "enchanted_high": (88, 184, 132),
    "corrupted_low": (92, 74, 112),
    "corrupted_high": (72, 54, 92),
    "infernal_low": (148, 52, 42),
    "infernal_high": (118, 36, 30),
    "candy_low": (236, 148, 192),
    "candy_high": (216, 118, 168),
    "crystal_low": (126, 214, 224),
    "crystal_high": (94, 188, 206),
    "maple_low": (208, 116, 52),
    "maple_high": (184, 86, 38),
    "birch_low": (152, 196, 94),
    "birch_high": (126, 172, 74),
    "flower_low": (140, 192, 88),
    "flower_high": (118, 168, 72),
    "clover_low": (86, 168, 78),
    "clover_high": (66, 142, 62),
    "garlic_low": (176, 166, 124),
    "garlic_high": (152, 142, 104),
    "celestial_low": (148, 142, 218),
    "celestial_high": (118, 110, 194),
    "mushroom_low": (146, 124, 152),
    "mushroom_high": (120, 96, 128),
    "wasteland_low": (128, 134, 96),
    "wasteland_high": (104, 112, 78),
    "lemon_low": (182, 206, 86),
    "lemon_high": (158, 182, 66),
    "rocklands_low": (132, 126, 118),
    "rocklands_high": (108, 102, 94),
    "paradox_low": (136, 92, 178),
    "paradox_high": (106, 68, 148),
    "road": (168, 154, 130),
    "field": (204, 178, 86),
}

KINGDOM_PALETTE: list[RGB] = [
    (235, 77, 75),
    (56, 173, 169),
    (246, 185, 59),
    (74, 105, 189),
    (120, 224, 143),
    (229, 142, 38),
    (183, 21, 64),
    (106, 137, 204),
    (248, 194, 145),
    (96, 163, 188),
    (229, 80, 57),
    (155, 89, 182),
    (46, 204, 113),
    (241, 196, 15),
    (230, 126, 34),
    (26, 188, 156),
]


def _tile_color(tile_name: str) -> RGB:
    if tile_name in TILE_COLORS:
        return TILE_COLORS[tile_name]
    if ":" in tile_name:
        base, overlay = tile_name.split(":", 1)
        if overlay in OVERLAY_COLORS:
            return OVERLAY_COLORS[overlay]
        if base in TILE_COLORS:
            return TILE_COLORS[base]
    for key, col in OVERLAY_COLORS.items():
        if key in tile_name:
            return col
    return (110, 110, 110)


def _blend(base: RGB, overlay: RGB, alpha: float) -> RGB:
    inv = 1.0 - alpha
    return (
        int(base[0] * inv + overlay[0] * alpha),
        int(base[1] * inv + overlay[1] * alpha),
        int(base[2] * inv + overlay[2] * alpha),
    )


def _png_chunk(chunk_type: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(chunk_type + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + chunk_type + data + struct.pack(">I", crc)


def encode_png_bytes(rgb_buffer: bytearray, width: int, height: int) -> bytes:
    """Encode raw RGB24 bytes (top-to-bottom rows) into a PNG byte stream."""
    stride = width * 3
    raw_rows = bytearray()
    for y in range(height):
        raw_rows.append(0)  # filter type 0 (None)
        start = y * stride
        raw_rows.extend(rgb_buffer[start : start + stride])

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    compressed = zlib.compress(bytes(raw_rows), level=6)
    return (
        b"\x89PNG\r\n\x1a\n"
        + _png_chunk(b"IHDR", ihdr)
        + _png_chunk(b"IDAT", compressed)
        + _png_chunk(b"IEND", b"")
    )


def render_map_png_bytes(
    data: dict[str, Any],
    show_kingdoms: bool = True,
    show_buildings: bool = True,
    scale: int = 1,
) -> bytes:
    """Render a WorldBox save dict to PNG bytes."""
    grid = decode_tiles(data)
    height = len(grid)
    width = len(grid[0]) if height else 0
    if width == 0 or height == 0:
        raise ValueError("Cannot render empty map")

    # Pre-cache tile colors for fast inner loop
    color_cache = {t: _tile_color(t) for t in data.get("tileMap", [])}

    # Build RGB buffer with y=0 at the bottom (matches WorldBox in-game orientation)
    buf = bytearray(width * height * 3)
    for y in range(height):
        img_y = height - 1 - y
        row_offset = img_y * width * 3
        row = grid[y]
        for x in range(width):
            r, g, b = color_cache.get(row[x], (110, 110, 110))
            pos = row_offset + x * 3
            buf[pos] = r
            buf[pos + 1] = g
            buf[pos + 2] = b

    # Frozen tiles & fire accents
    for idx in data.get("frozen_tiles", []):
        fy, fx = divmod(idx, width)
        if 0 <= fy < height and 0 <= fx < width:
            pos = ((height - 1 - fy) * width + fx) * 3
            r, g, b = _blend((buf[pos], buf[pos + 1], buf[pos + 2]), (220, 240, 255), 0.45)
            buf[pos], buf[pos + 1], buf[pos + 2] = r, g, b

    for idx in data.get("fire", []):
        fy, fx = divmod(idx, width)
        if 0 <= fy < height and 0 <= fx < width:
            pos = ((height - 1 - fy) * width + fx) * 3
            buf[pos], buf[pos + 1], buf[pos + 2] = 255, 110, 20

    # Kingdom territory overlays (city zones: 8x8 tiles each)
    if show_kingdoms:
        kingdom_colors: dict[int, RGB] = {}
        for i, k in enumerate(data.get("kingdoms", [])):
            cid = int(k.get("color_id", i))
            kingdom_colors[k["id"]] = KINGDOM_PALETTE[cid % len(KINGDOM_PALETTE)]

        zone_owner: dict[tuple[int, int], int] = {}
        for city in data.get("cities", []):
            kid = city.get("kingdomID", -1)
            if kid not in kingdom_colors:
                continue
            for z in city.get("zones", []):
                zone_owner[(int(z.get("x", 0)), int(z.get("y", 0)))] = kid

        for (zx, zy), kid in zone_owner.items():
            col = kingdom_colors[kid]
            # Check which edges border a different owner or wilderness
            n_top = zone_owner.get((zx, zy + 1)) != kid
            n_bot = zone_owner.get((zx, zy - 1)) != kid
            n_left = zone_owner.get((zx - 1, zy)) != kid
            n_right = zone_owner.get((zx + 1, zy)) != kid

            x0 = zx * ZONE_SIZE
            y0 = zy * ZONE_SIZE
            for dy in range(ZONE_SIZE):
                ty = y0 + dy
                if not (0 <= ty < height):
                    continue
                img_y = height - 1 - ty
                is_y_border = (dy == 0 and n_bot) or (dy == ZONE_SIZE - 1 and n_top)
                for dx in range(ZONE_SIZE):
                    tx = x0 + dx
                    if not (0 <= tx < width):
                        continue
                    is_x_border = (dx == 0 and n_left) or (dx == ZONE_SIZE - 1 and n_right)
                    alpha = 0.78 if (is_x_border or is_y_border) else 0.22
                    pos = (img_y * width + tx) * 3
                    r, g, b = _blend((buf[pos], buf[pos + 1], buf[pos + 2]), col, alpha)
                    buf[pos], buf[pos + 1], buf[pos + 2] = r, g, b

    # Subtle city buildings highlight
    if show_buildings:
        for bld in data.get("buildings", []):
            asset = bld.get("asset_id", "")
            if asset in ("wheat", "tree", "fruit_bush", "mineral_stone", "mineral_ore"):
                continue
            if "house" in asset or "hall" in asset or "barracks" in asset or "tower" in asset or "dock" in asset or "watch_tower" in asset or "temple" in asset:
                bx = int(bld.get("mainX", 0))
                by = int(bld.get("mainY", 0))
                if 0 <= bx < width and 0 <= by < height:
                    pos = ((height - 1 - by) * width + bx) * 3
                    r, g, b = _blend((buf[pos], buf[pos + 1], buf[pos + 2]), (255, 245, 220), 0.55)
                    buf[pos], buf[pos + 1], buf[pos + 2] = r, g, b

    if scale > 1:
        sw, sh = width * scale, height * scale
        scaled = bytearray(sw * sh * 3)
        for sy in range(sh):
            src_y = sy // scale
            dst_row = sy * sw * 3
            src_row = src_y * width * 3
            for sx in range(sw):
                src_x = sx // scale
                sp = src_row + src_x * 3
                dp = dst_row + sx * 3
                scaled[dp : dp + 3] = buf[sp : sp + 3]
        return encode_png_bytes(scaled, sw, sh)

    return encode_png_bytes(buf, width, height)


def render_map_png(
    data: dict[str, Any],
    out_path: str | Path,
    show_kingdoms: bool = True,
    show_buildings: bool = True,
    scale: int = 1,
) -> Path:
    """Render a WorldBox save dict and write it to a PNG file."""
    out = Path(out_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(
        render_map_png_bytes(
            data,
            show_kingdoms=show_kingdoms,
            show_buildings=show_buildings,
            scale=scale,
        )
    )
    return out
