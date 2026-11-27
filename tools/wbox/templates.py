"""Built-in procedural continent templates (saveVersion 17, 4x4 chunks = 256x256 tiles)."""

from __future__ import annotations

import copy
import math
import random
from dataclasses import dataclass
from typing import Any

from .io import CHUNK_SIZE, encode_tiles
from .stitch import ENTITY_COUNTERS

TEMPLATE_CHUNKS = 4
TEMPLATE_TILES = TEMPLATE_CHUNKS * CHUNK_SIZE  # 256x256


@dataclass(frozen=True)
class TemplateSpec:
    id: str
    name: str
    slug: str
    description: str
    seed: int


TEMPLATE_SPECS: tuple[TemplateSpec, ...] = (
    TemplateSpec(
        id="template:continents",
        name="Template: Twin Continents",
        slug="template_continents",
        description="Two rich landmasses split by a winding central ocean channel with varied biomes.",
        seed=1701,
    ),
    TemplateSpec(
        id="template:pangaea",
        name="Template: Central Pangaea",
        slug="template_pangaea",
        description="A vast central supercontinent with mountain spines, northern frost, and southern savannas.",
        seed=2309,
    ),
    TemplateSpec(
        id="template:archipelago",
        name="Template: Island Archipelago",
        slug="template_archipelago",
        description="A chain of lush tropical and temperate islands separated by navigable shallow seas.",
        seed=4117,
    ),
    TemplateSpec(
        id="template:ring_atoll",
        name="Template: Volcanic Ring Atoll",
        slug="template_ring_atoll",
        description="A breached ring landmass enclosing a deep lagoon and a smoldering central caldera peak.",
        seed=5821,
    ),
)

_SPEC_BY_ID: dict[str, TemplateSpec] = {spec.id: spec for spec in TEMPLATE_SPECS}
_TEMPLATE_CACHE: dict[str, dict[str, Any]] = {}


def is_template_id(save_id: str) -> bool:
    return save_id in _SPEC_BY_ID


def list_templates() -> list[dict[str, Any]]:
    """Return metadata for all built-in procedural templates."""
    return [
        {
            "id": spec.id,
            "name": spec.name,
            "slug": spec.slug,
            "filename": f"{spec.slug}.wbox",
            "description": spec.description,
            "width_chunks": TEMPLATE_CHUNKS,
            "height_chunks": TEMPLATE_CHUNKS,
        }
        for spec in TEMPLATE_SPECS
    ]


def _make_value_noise_layer(size: int, cell: int, rng: random.Random) -> list[list[float]]:
    """Generate a smooth 2D bilinear value-noise grid in [0, 1] using pure stdlib."""
    gw = size // cell + 2
    gh = size // cell + 2
    coarse = [[rng.random() for _ in range(gw)] for _ in range(gh)]

    inv_cell = 1.0 / cell
    # Precompute smoothstep weights and cell indices along x
    x_idx = [x // cell for x in range(size)]
    x_wt = []
    for x in range(size):
        fx = (x % cell) * inv_cell
        x_wt.append(fx * fx * (3.0 - 2.0 * fx))

    out: list[list[float]] = []
    for y in range(size):
        gy = y // cell
        fy = (y % cell) * inv_cell
        sy = fy * fy * (3.0 - 2.0 * fy)
        row0 = coarse[gy]
        row1 = coarse[gy + 1]
        out_row = [0.0] * size
        for x in range(size):
            gx = x_idx[x]
            sx = x_wt[x]
            v00 = row0[gx]
            v10 = row0[gx + 1]
            v01 = row1[gx]
            v11 = row1[gx + 1]
            top = v00 + (v10 - v00) * sx
            bot = v01 + (v11 - v01) * sx
            out_row[x] = top + (bot - top) * sy
        out.append(out_row)
    return out


def _fbm_grid(size: int, seed: int) -> list[list[float]]:
    """4-octave fractal noise normalized to roughly [0, 1]."""
    rng = random.Random(seed)
    n1 = _make_value_noise_layer(size, 64, rng)
    n2 = _make_value_noise_layer(size, 32, rng)
    n3 = _make_value_noise_layer(size, 16, rng)
    n4 = _make_value_noise_layer(size, 8, rng)

    grid: list[list[float]] = []
    for y in range(size):
        r1, r2, r3, r4 = n1[y], n2[y], n3[y], n4[y]
        grid.append(
            [
                r1[x] * 0.50 + r2[x] * 0.27 + r3[x] * 0.15 + r4[x] * 0.08
                for x in range(size)
            ]
        )
    return grid


def _biome_tile(elev: float, moist: float, lat: float, force_biome: str | None = None) -> str:
    """Map elevation/moisture/latitude into valid WorldBox saveVersion 17 tiles."""
    if elev < -0.22:
        return "deep_ocean"
    if elev < -0.08:
        return "close_ocean"
    if elev < 0.0:
        return "shallow_waters"
    if elev < 0.045:
        return "sand"
    if elev > 0.62:
        return "summit"
    if elev > 0.50:
        return "mountains"
    if elev > 0.40:
        return "hills"

    high = elev >= 0.20
    soil = "soil_high" if high else "soil_low"
    suffix = "high" if high else "low"

    if force_biome:
        return f"{soil}:{force_biome}_{suffix}"

    # Latitude-driven biome belts (lat in [0, 1], 0 = south, 1 = north)
    if lat > 0.80:
        biome = "permafrost"
    elif lat < 0.22:
        biome = "desert" if moist < 0.44 else "savanna"
    elif moist > 0.64:
        biome = "jungle" if lat < 0.55 else "maple"
    elif moist < 0.36:
        biome = "savanna" if lat < 0.62 else "birch"
    else:
        biome = "grass" if moist > 0.46 else "flower"
    return f"{soil}:{biome}_{suffix}"


def _build_continents_grid(size: int, seed: int) -> list[list[str]]:
    elev_n = _fbm_grid(size, seed)
    moist_n = _fbm_grid(size, seed + 97)
    inv = 1.0 / (size - 1)
    grid: list[list[str]] = []

    for y in range(size):
        v = y * inv
        row_e = elev_n[y]
        row_m = moist_n[y]
        row: list[str] = []
        # Gentle sinusoidal central strait
        strait_x = 0.50 + 0.045 * math.sin(v * math.pi * 2.4 + 0.5)
        for x in range(size):
            u = x * inv
            # Two continental lobes (West & East)
            dw = math.hypot((u - 0.25) / 0.22, (v - 0.50) / 0.38)
            de = math.hypot((u - 0.75) / 0.22, (v - 0.50) / 0.38)
            lobe = max(1.0 - dw, 1.0 - de)

            # Carve central navigable strait and outer ocean margins
            strait_cut = math.exp(-((u - strait_x) / 0.068) ** 2) * 0.48
            edge_d = min(u, 1.0 - u, v, 1.0 - v)
            edge_fade = min(1.0, edge_d / 0.08)

            h = (lobe * 0.72 + (row_e[x] - 0.44) * 0.85 - strait_cut) * edge_fade - (1.0 - edge_fade) * 0.35
            row.append(_biome_tile(h, row_m[x], v))
        grid.append(row)
    return grid


def _build_pangaea_grid(size: int, seed: int) -> list[list[str]]:
    elev_n = _fbm_grid(size, seed)
    moist_n = _fbm_grid(size, seed + 131)
    inv = 1.0 / (size - 1)
    grid: list[list[str]] = []

    for y in range(size):
        v = y * inv
        row_e = elev_n[y]
        row_m = moist_n[y]
        row: list[str] = []
        for x in range(size):
            u = x * inv
            # Central supercontinent with subtle angular lobes
            dx = u - 0.50
            dy = v - 0.50
            ang = math.atan2(dy, dx)
            rad_mod = 0.38 + 0.055 * math.sin(ang * 3.0 + 0.4) + 0.035 * math.cos(ang * 5.0 - 0.7)
            dist = math.hypot(dx, dy) / rad_mod

            # Mountain spine running gently down the interior
            ridge = math.exp(-((u - (0.48 + 0.07 * math.sin(v * math.pi * 2.0))) / 0.065) ** 2) * 0.24
            edge_d = min(u, 1.0 - u, v, 1.0 - v)
            edge_fade = min(1.0, edge_d / 0.09)

            h = ((1.0 - dist) * 0.68 + ridge + (row_e[x] - 0.43) * 0.72) * edge_fade - (1.0 - edge_fade) * 0.38
            row.append(_biome_tile(h, row_m[x], v))
        grid.append(row)
    return grid


def _build_archipelago_grid(size: int, seed: int) -> list[list[str]]:
    elev_n = _fbm_grid(size, seed)
    moist_n = _fbm_grid(size, seed + 193)
    rng = random.Random(seed + 7)
    inv = 1.0 / (size - 1)

    # Curated island centers (u, v, radius, optional biome accent)
    islands: list[tuple[float, float, float, str | None]] = [
        (0.22, 0.24, 0.145, "savanna"),
        (0.49, 0.21, 0.130, "jungle"),
        (0.77, 0.26, 0.145, "grass"),
        (0.19, 0.52, 0.135, "birch"),
        (0.46, 0.49, 0.160, "jungle"),
        (0.74, 0.54, 0.140, "flower"),
        (0.26, 0.79, 0.140, "maple"),
        (0.54, 0.78, 0.135, "grass"),
        (0.80, 0.79, 0.125, "savanna"),
        (0.35, 0.36, 0.085, None),
        (0.62, 0.65, 0.085, None),
    ]
    # Add tiny deterministic jitter so islands feel organic
    islands = [
        (
            u + rng.uniform(-0.025, 0.025),
            v + rng.uniform(-0.025, 0.025),
            r,
            b,
        )
        for (u, v, r, b) in islands
    ]

    grid: list[list[str]] = []
    for y in range(size):
        v = y * inv
        row_e = elev_n[y]
        row_m = moist_n[y]
        row: list[str] = []
        for x in range(size):
            u = x * inv
            best_cone = -0.45
            best_biome: str | None = None
            for iu, iv, ir, ib in islands:
                cone = 1.0 - math.hypot(u - iu, v - iv) / ir
                if cone > best_cone:
                    best_cone = cone
                    best_biome = ib

            edge_d = min(u, 1.0 - u, v, 1.0 - v)
            edge_fade = min(1.0, edge_d / 0.07)
            h = (best_cone * 0.72 + (row_e[x] - 0.48) * 0.62) * edge_fade - (1.0 - edge_fade) * 0.35
            row.append(_biome_tile(h, row_m[x], v, force_biome=best_biome if h >= 0.045 else None))
        grid.append(row)
    return grid


def _build_ring_atoll_grid(size: int, seed: int) -> list[list[str]]:
    elev_n = _fbm_grid(size, seed)
    moist_n = _fbm_grid(size, seed + 251)
    inv = 1.0 / (size - 1)
    grid: list[list[str]] = []

    for y in range(size):
        v = y * inv
        row_e = elev_n[y]
        row_m = moist_n[y]
        row: list[str] = []
        for x in range(size):
            u = x * inv
            dx = u - 0.50
            dy = v - 0.50
            r = math.hypot(dx, dy)
            ang = math.atan2(dy, dx)

            # Outer ring landmass centered around r = 0.31
            ring_center = 0.31 + 0.025 * math.sin(ang * 4.0 + 0.6)
            ring_band = 1.0 - abs(r - ring_center) / 0.115

            # Two natural sea channels breaching the ring (East-Northeast & Southwest)
            ch1 = math.exp(-(((ang - 0.35 + math.pi) % (2.0 * math.pi) - math.pi) / 0.22) ** 2)
            ch2 = math.exp(-(((ang + 2.55 + math.pi) % (2.0 * math.pi) - math.pi) / 0.22) ** 2)
            channel_cut = (ch1 + ch2) * 0.65

            # Central volcanic peak inside the lagoon
            volcano = 1.0 - r / 0.115

            base_h = max(ring_band - channel_cut, volcano)
            edge_d = min(u, 1.0 - u, v, 1.0 - v)
            edge_fade = min(1.0, edge_d / 0.07)
            h = (base_h * 0.72 + (row_e[x] - 0.46) * 0.52) * edge_fade - (1.0 - edge_fade) * 0.35

            if r < 0.032:
                tile = "lava1" if r < 0.018 else "lava0"
            elif r < 0.056 and h > 0.22:
                tile = "mountains"
            elif r < 0.095 and h >= 0.045:
                tile = _biome_tile(h, row_m[x], v, force_biome="infernal")
            else:
                # Lush tropical/temperate outer ring
                ring_biome = "jungle" if row_m[x] > 0.52 else ("savanna" if row_m[x] < 0.34 else "grass")
                tile = _biome_tile(h, row_m[x], v, force_biome=ring_biome if h >= 0.045 else None)
            row.append(tile)
        grid.append(row)
    return grid


def _assemble_save_v17(name: str, grid: list[list[str]]) -> dict[str, Any]:
    """Package a 256x256 tile grid into a complete saveVersion 17 save dict."""
    tile_map, tile_array, tile_amounts = encode_tiles(grid)
    map_stats: dict[str, Any] = {
        "name": name,
        "world_time": 0.0,
        "history_current_year": 1,
        "world_age_id": "age_hope",
        "population": 0,
        "id_projectile": 1,
        "id_status": 1,
    }
    for counter_key in ENTITY_COUNTERS.values():
        map_stats[counter_key] = 1

    save: dict[str, Any] = {
        "saveVersion": 17,
        "width": TEMPLATE_CHUNKS,
        "height": TEMPLATE_CHUNKS,
        "hotkey_tabs_data": {},
        "camera_pos_x": TEMPLATE_TILES / 2.0,
        "camera_pos_y": TEMPLATE_TILES / 2.0,
        "camera_zoom": float(TEMPLATE_TILES * 0.45),
        "mapStats": map_stats,
        "worldLaws": {"list": []},
        "tileMap": tile_map,
        "tileArray": tile_array,
        "tileAmounts": tile_amounts,
        "fire": [],
        "conwayEater": [],
        "conwayCreator": [],
        "frozen_tiles": [],
        "tiles": [],
    }
    for sec in ENTITY_COUNTERS:
        save[sec] = []
    return save


def generate_template_world(template_id: str) -> dict[str, Any]:
    """Generate (or return cached copy of) a built-in procedural 4x4 chunk saveVersion 17 world."""
    spec = _SPEC_BY_ID.get(template_id)
    if spec is None:
        raise KeyError(f"Unknown template ID: {template_id}")

    if template_id not in _TEMPLATE_CACHE:
        if template_id == "template:continents":
            grid = _build_continents_grid(TEMPLATE_TILES, spec.seed)
        elif template_id == "template:pangaea":
            grid = _build_pangaea_grid(TEMPLATE_TILES, spec.seed)
        elif template_id == "template:archipelago":
            grid = _build_archipelago_grid(TEMPLATE_TILES, spec.seed)
        elif template_id == "template:ring_atoll":
            grid = _build_ring_atoll_grid(TEMPLATE_TILES, spec.seed)
        else:
            raise KeyError(f"Unsupported template ID: {template_id}")

        _TEMPLATE_CACHE[template_id] = _assemble_save_v17(spec.name, grid)

    return copy.deepcopy(_TEMPLATE_CACHE[template_id])
