"""NeoModLoader mod manager, profile switcher, and save backup helper."""

from __future__ import annotations

import json
import os
import shutil
import zipfile
from datetime import datetime
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
CONFIG_PATH = REPO_ROOT / "local" / "studio_config.json"


def _detect_game_dir() -> str:
    candidates = [
        # macOS Steam
        Path.home() / "Library/Application Support/Steam/steamapps/common/worldbox/worldbox.app/Contents/Resources/Data/StreamingAssets",
        # Windows Steam
        Path("C:/Program Files (x86)/Steam/steamapps/common/worldbox/worldbox_Data/StreamingAssets"),
        # Linux Steam
        Path.home() / ".local/share/Steam/steamapps/common/worldbox/worldbox_Data/StreamingAssets",
    ]
    for c in candidates:
        if c.exists():
            return str(c)
    return ""


def load_config() -> dict[str, Any]:
    defaults: dict[str, Any] = {
        "saves_dir": str(REPO_ROOT / "local" / "saves"),
        "output_dir": str(REPO_ROOT / "local" / "output"),
        "game_streaming_assets": _detect_game_dir(),
        "profiles": {},
    }
    if CONFIG_PATH.exists():
        try:
            saved = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
            defaults.update(saved)
        except Exception:
            pass
    return defaults


def save_config(cfg: dict[str, Any]) -> dict[str, Any]:
    CONFIG_PATH.parent.mkdir(parents=True, exist_ok=True)
    CONFIG_PATH.write_text(json.dumps(cfg, indent=2), encoding="utf-8")
    return cfg


def _mods_dirs(cfg: dict[str, Any]) -> tuple[Path, Path]:
    base_str = cfg.get("game_streaming_assets", "").strip()
    if base_str:
        base = Path(base_str)
        # Accept either WorldBox root, worldbox_Data, StreamingAssets, or Mods itself
        if base.name.lower() == "mods":
            enabled = base
            disabled = base.parent / "Mods_Disabled"
        elif (base / "worldbox_Data" / "StreamingAssets").exists():
            enabled = base / "worldbox_Data" / "StreamingAssets" / "Mods"
            disabled = base / "worldbox_Data" / "StreamingAssets" / "Mods_Disabled"
        elif (base / "StreamingAssets").exists():
            enabled = base / "StreamingAssets" / "Mods"
            disabled = base / "StreamingAssets" / "Mods_Disabled"
        else:
            enabled = base / "Mods"
            disabled = base / "Mods_Disabled"
    else:
        # Local sandbox staging directory when WorldBox is not installed on this machine
        enabled = REPO_ROOT / "local" / "staged_mods" / "Mods"
        disabled = REPO_ROOT / "local" / "staged_mods" / "Mods_Disabled"
    enabled.mkdir(parents=True, exist_ok=True)
    disabled.mkdir(parents=True, exist_ok=True)
    return enabled, disabled


def _read_mod_meta(mod_dir: Path, enabled: bool) -> dict[str, Any]:
    meta_file = mod_dir / "mod.json"
    meta: dict[str, Any] = {
        "folder": mod_dir.name,
        "name": mod_dir.name,
        "author": "Unknown",
        "version": "0.0.0",
        "description": "",
        "enabled": enabled,
        "has_code": (mod_dir / "Code").exists() or any(mod_dir.glob("*.dll")),
        "path": str(mod_dir),
    }
    if meta_file.exists():
        try:
            parsed = json.loads(meta_file.read_text(encoding="utf-8"))
            for k in ("name", "author", "version", "description", "RepoUrl"):
                if k in parsed:
                    meta[k] = parsed[k]
        except Exception:
            pass
    return meta


def list_mods(cfg: dict[str, Any]) -> dict[str, Any]:
    enabled_dir, disabled_dir = _mods_dirs(cfg)
    installed: list[dict[str, Any]] = []
    for folder, is_en in ((enabled_dir, True), (disabled_dir, False)):
        if not folder.exists():
            continue
        for entry in sorted(folder.iterdir()):
            if entry.is_dir() and not entry.name.startswith("."):
                installed.append(_read_mod_meta(entry, is_en))

    suite_mods: list[dict[str, Any]] = []
    for sub_dir, tag in (("mods", "Core"), ("expansions", "Expansion")):
        suite_root = REPO_ROOT / sub_dir
        if suite_root.exists():
            for entry in sorted(suite_root.iterdir()):
                if entry.is_dir() and (entry / "mod.json").exists():
                    info = _read_mod_meta(entry, enabled=False)
                    info["deployed"] = (enabled_dir / entry.name).exists() or (disabled_dir / entry.name).exists()
                    info["tier"] = tag
                    suite_mods.append(info)

    return {
        "using_local_staging": not bool(cfg.get("game_streaming_assets", "").strip()),
        "enabled_dir": str(enabled_dir),
        "disabled_dir": str(disabled_dir),
        "installed": installed,
        "suite_modules": suite_mods,
        "profiles": cfg.get("profiles", {}),
    }


def toggle_mod(cfg: dict[str, Any], folder_name: str, enable: bool) -> dict[str, Any]:
    if "/" in folder_name or "\\" in folder_name or folder_name in (".", ".."):
        raise ValueError("Invalid mod folder name")
    enabled_dir, disabled_dir = _mods_dirs(cfg)
    src = (disabled_dir if enable else enabled_dir) / folder_name
    dst = (enabled_dir if enable else disabled_dir) / folder_name
    if not src.exists():
        if dst.exists():
            return list_mods(cfg)
        raise FileNotFoundError(f"Mod folder not found: {folder_name}")
    if dst.exists():
        shutil.rmtree(dst)
    shutil.move(str(src), str(dst))
    return list_mods(cfg)


def deploy_suite_mod(cfg: dict[str, Any], folder_name: str) -> dict[str, Any]:
    if "/" in folder_name or "\\" in folder_name or folder_name in (".", ".."):
        raise ValueError("Invalid module name")
    src = REPO_ROOT / "mods" / folder_name
    if not src.exists():
        src = REPO_ROOT / "expansions" / folder_name
    if not src.exists():
        raise FileNotFoundError(f"Suite module not found: {folder_name}")
    enabled_dir, disabled_dir = _mods_dirs(cfg)
    dst = enabled_dir / folder_name
    if dst.exists():
        shutil.rmtree(dst)
    if (disabled_dir / folder_name).exists():
        shutil.rmtree(disabled_dir / folder_name)
    shutil.copytree(src, dst)
    return list_mods(cfg)


def save_profile(cfg: dict[str, Any], profile_name: str) -> dict[str, Any]:
    name = profile_name.strip()
    if not name:
        raise ValueError("Profile name cannot be empty")
    state = list_mods(cfg)
    enabled_names = [m["folder"] for m in state["installed"] if m["enabled"]]
    cfg.setdefault("profiles", {})[name] = enabled_names
    save_config(cfg)
    return list_mods(cfg)


def apply_profile(cfg: dict[str, Any], profile_name: str) -> dict[str, Any]:
    profiles = cfg.get("profiles", {})
    if profile_name not in profiles:
        raise KeyError(f"Profile not found: {profile_name}")
    wanted = set(profiles[profile_name])
    state = list_mods(cfg)
    for mod in state["installed"]:
        should_enable = mod["folder"] in wanted
        if mod["enabled"] != should_enable:
            toggle_mod(cfg, mod["folder"], should_enable)
    return list_mods(cfg)


def backup_saves(cfg: dict[str, Any]) -> dict[str, Any]:
    saves_dir = Path(cfg["saves_dir"])
    if not saves_dir.exists():
        raise FileNotFoundError(f"Saves directory not found: {saves_dir}")
    backups_dir = REPO_ROOT / "local" / "backups"
    backups_dir.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    archive_path = backups_dir / f"worldbox_saves_{stamp}.zip"
    count = 0
    with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        for root, _, files in os.walk(saves_dir):
            for fname in files:
                if fname.endswith((".wbox", ".wbax", ".meta", ".png")):
                    full = Path(root) / fname
                    zf.write(full, full.relative_to(saves_dir))
                    count += 1
    return {
        "archive": str(archive_path),
        "files_backed_up": count,
        "size_bytes": archive_path.stat().st_size,
    }
