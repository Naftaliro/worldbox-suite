"""Direct WorldBox save-slot exporter (map.wbox + map.meta + previews) and Player.log diagnostics."""

from __future__ import annotations

import json
import os
import platform
import re
import time
from pathlib import Path
from typing import Any

from tools.wbox import load_wbox, render_map_png, save_wbox

SUITE_MOD_TAGS = [
    "SuiteHub",
    "CoreScale",
    "VisualPolish",
    "GrandWarfare",
    "LinkedContinents",
    "EraChronicle",
    "ApexFactions",
    "WorldRelics",
    "CoopHost",
    "ModernAge",
]


def detect_worldbox_saves_dir() -> str:
    sys_name = platform.system()
    candidates: list[Path] = []
    if sys_name == "Windows":
        userprofile = Path(os.environ.get("USERPROFILE", Path.home()))
        candidates.append(userprofile / "AppData/LocalLow/mkarpenko/WorldBox/saves")
    elif sys_name == "Darwin":
        candidates.append(Path.home() / "Library/Application Support/mkarpenko/WorldBox/saves")
    else:
        candidates.append(Path.home() / ".config/unity3d/mkarpenko/WorldBox/saves")

    for c in candidates:
        if c.exists():
            return str(c)
    return str(candidates[0]) if candidates else ""


def detect_player_log_path() -> str:
    sys_name = platform.system()
    candidates: list[Path] = []
    if sys_name == "Windows":
        userprofile = Path(os.environ.get("USERPROFILE", Path.home()))
        candidates.append(userprofile / "AppData/LocalLow/mkarpenko/WorldBox/Player.log")
    elif sys_name == "Darwin":
        candidates.append(Path.home() / "Library/Logs/mkarpenko/WorldBox/Player.log")
    else:
        candidates.append(Path.home() / ".config/unity3d/mkarpenko/WorldBox/Player.log")

    for c in candidates:
        if c.exists():
            return str(c)
    return str(candidates[0]) if candidates else ""


def list_save_slots(wb_saves_dir: str, max_slots: int = 30) -> dict[str, Any]:
    root = Path(wb_saves_dir) if wb_saves_dir else None
    exists = bool(root and root.exists())
    slots: list[dict[str, Any]] = []

    for i in range(1, max_slots + 1):
        slot_name = f"save{i}"
        entry: dict[str, Any] = {"slot": i, "folder": slot_name, "occupied": False, "name": "", "size": ""}
        if exists and root is not None:
            sdir = root / slot_name
            wbox = sdir / "map.wbox"
            meta = sdir / "map.meta"
            if wbox.exists():
                entry["occupied"] = True
                if meta.exists():
                    try:
                        m = json.loads(meta.read_text(encoding="utf-8"))
                        entry["name"] = m.get("mapStats", {}).get("name", slot_name)
                        entry["size"] = f"{m.get('width', '?')}x{m.get('height', '?')}"
                        entry["population"] = m.get("population", 0)
                    except Exception:
                        entry["name"] = slot_name
                else:
                    entry["name"] = slot_name
        slots.append(entry)

    return {
        "saves_dir": str(root) if root else "",
        "exists": exists,
        "slots": slots,
    }


def build_map_meta(data: dict[str, Any]) -> dict[str, Any]:
    """Generate a saveVersion 17 MapMetaData JSON matching WorldBox's MapMetaData fields."""
    stats = data.get("mapStats", {})
    now_ts = int(time.time())
    return {
        "saveVersion": int(data.get("saveVersion", 17)),
        "width": int(data.get("width", 4)),
        "height": int(data.get("height", 4)),
        "mapStats": stats,
        "cities": len(data.get("cities", [])),
        "units": len(data.get("actors_data", [])),
        "population": len(data.get("actors_data", [])),
        "structures": len(data.get("buildings", [])),
        "kingdoms": len(data.get("kingdoms", [])),
        "buildings": len(data.get("buildings", [])),
        "equipment": len(data.get("items", [])),
        "books": len(data.get("books", [])),
        "wars": len(data.get("wars", [])),
        "alliances": len(data.get("alliances", [])),
        "families": len(data.get("families", [])),
        "clans": len(data.get("clans", [])),
        "cultures": len(data.get("cultures", [])),
        "religions": len(data.get("religions", [])),
        "languages": len(data.get("languages", [])),
        "subspecies": len(data.get("subspecies", [])),
        "modded": True,
        "timestamp": now_ts,
    }


def export_wbox_to_slot(wbox_path: Path, slot_num: int, wb_saves_dir: str) -> dict[str, Any]:
    """Write map.wbox, map.meta, preview.png, and preview_small.png directly into a WorldBox save slot."""
    if not (1 <= slot_num <= 60):
        raise ValueError("Slot number must be between 1 and 60")
    if not wb_saves_dir:
        raise ValueError("WorldBox Steam saves directory is not configured")

    data = load_wbox(wbox_path)
    slot_dir = Path(wb_saves_dir) / f"save{slot_num}"
    slot_dir.mkdir(parents=True, exist_ok=True)

    out_wbox = save_wbox(data, slot_dir / "map.wbox")
    meta = build_map_meta(data)
    (slot_dir / "map.meta").write_text(json.dumps(meta, separators=(",", ":")), encoding="utf-8")

    render_map_png(data, slot_dir / "preview.png", show_kingdoms=True, show_buildings=True, scale=1)
    render_map_png(data, slot_dir / "preview_small.png", show_kingdoms=True, show_buildings=False, scale=1)

    return {
        "slot": slot_num,
        "slot_dir": str(slot_dir),
        "map_wbox": str(out_wbox),
        "world_name": data.get("mapStats", {}).get("name", f"save{slot_num}"),
    }


def analyze_player_log(log_path_str: str) -> dict[str, Any]:
    """Parse WorldBox's Player.log for NeoModLoader status, Suite mod health, errors, and warnings."""
    log_path = Path(log_path_str) if log_path_str else Path(detect_player_log_path())
    if not log_path.exists():
        return {
            "log_path": str(log_path),
            "found": False,
            "mod_health": {m: "unknown" for m in SUITE_MOD_TAGS},
            "errors": [],
            "warnings": [],
            "suite_lines": [],
            "tail": [],
        }

    text = log_path.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()

    mod_health: dict[str, str] = {m: "not_seen" for m in SUITE_MOD_TAGS}
    errors: list[str] = []
    warnings: list[str] = []
    suite_lines: list[str] = []

    err_re = re.compile(r"(Exception|CS\d{4}|error CS|NullReferenceException|Failed to|HarmonyException|TypeLoadException)", re.IGNORECASE)
    warn_re = re.compile(r"(Warning|skipped|deferred)", re.IGNORECASE)

    for idx, line in enumerate(lines):
        stripped = line.strip()
        if not stripped:
            continue

        matched_mod = None
        for mod in SUITE_MOD_TAGS:
            if f"[{mod}]" in stripped or f"{mod}.cs" in stripped or f"/{mod}/" in stripped or f"\\{mod}\\" in stripped:
                matched_mod = mod
                suite_lines.append(stripped)
                if "Loaded v" in stripped or "Expansion loaded" in stripped or "Registered" in stripped:
                    if mod_health[mod] != "error":
                        mod_health[mod] = "ok"
                break

        if err_re.search(stripped):
            ctx = stripped
            if idx + 1 < len(lines) and lines[idx + 1].strip().startswith("at "):
                ctx += "\n  " + lines[idx + 1].strip()
            errors.append(ctx)
            if matched_mod:
                mod_health[matched_mod] = "error"
        elif matched_mod and warn_re.search(stripped):
            warnings.append(stripped)
            if mod_health[matched_mod] == "not_seen":
                mod_health[matched_mod] = "warning"

    return {
        "log_path": str(log_path),
        "found": True,
        "size_bytes": log_path.stat().st_size,
        "total_lines": len(lines),
        "mod_health": mod_health,
        "errors": errors[-60:],
        "warnings": warnings[-40:],
        "suite_lines": suite_lines[-80:],
        "tail": lines[-60:],
    }
