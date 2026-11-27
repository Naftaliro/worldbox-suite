"""Self-contained GitHub auto-updater for WorldBox Studio and installed WorldBox mods."""

from __future__ import annotations

import json
import shutil
import tempfile
import threading
import urllib.request
import zipfile
from pathlib import Path
from typing import Any

from .mod_manager import CORE_MOD_NAMES, REPO_ROOT, _mods_dirs

COMMITS_API_URL = "https://api.github.com/repos/Naftaliro/worldbox-suite/commits/main"
ARCHIVE_ZIP_URL = "https://github.com/Naftaliro/worldbox-suite/archive/refs/heads/main.zip"
MOD_COPY_ITEMS = ("mod.json", "icon.png", "Code", "Locales", "GameResources", "WebClient")

_UPDATE_LOCK = threading.Lock()


def _copy_mod_items_preserving_userdata(src_mod: Path, dst_mod: Path) -> None:
    """Update code, icons, locales, and assets inside dst_mod while preserving *_config.json & chronicles/."""
    dst_mod.mkdir(parents=True, exist_ok=True)
    for item in MOD_COPY_ITEMS:
        s_item = src_mod / item
        d_item = dst_mod / item
        if s_item.is_dir():
            if d_item.exists():
                shutil.rmtree(d_item)
            shutil.copytree(s_item, d_item)
        elif s_item.is_file():
            shutil.copy2(s_item, d_item)


def read_local_sha(cfg: dict[str, Any]) -> str:
    enabled_dir, disabled_dir = _mods_dirs(cfg)
    candidates = [
        enabled_dir / "SuiteHub" / ".suite_sha",
        disabled_dir / "SuiteHub" / ".suite_sha",
        REPO_ROOT / ".suite_sha",
    ]
    for c in candidates:
        if c.is_file():
            val = c.read_text(encoding="utf-8", errors="replace").strip()
            if val:
                return val

    # Fallback when running inside a git checkout
    git_head = REPO_ROOT / ".git" / "HEAD"
    if git_head.is_file():
        head_txt = git_head.read_text(encoding="utf-8", errors="replace").strip()
        if head_txt.startswith("ref: "):
            ref_path = REPO_ROOT / ".git" / head_txt[5:]
            if ref_path.is_file():
                return ref_path.read_text(encoding="utf-8", errors="replace").strip()
        elif len(head_txt) >= 7:
            return head_txt
    return ""


def fetch_remote_main_info(timeout: float = 8.0) -> dict[str, str] | None:
    req = urllib.request.Request(
        COMMITS_API_URL,
        headers={
            "User-Agent": "WorldBoxSuite-StudioUpdater/1.0",
            "Accept": "application/vnd.github.v3+json",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            payload = json.loads(resp.read().decode("utf-8"))
            sha = str(payload.get("sha", "")).strip()
            msg = str(payload.get("commit", {}).get("message", "")).splitlines()[0] if payload.get("commit") else ""
            date = str(payload.get("commit", {}).get("committer", {}).get("date", ""))
            if sha:
                return {"sha": sha, "message": msg, "date": date}
    except Exception:
        return None
    return None


def check_update_status(cfg: dict[str, Any]) -> dict[str, Any]:
    local_sha = read_local_sha(cfg)
    remote = fetch_remote_main_info()
    if remote is None:
        return {
            "online": False,
            "local_sha": local_sha,
            "remote_sha": "",
            "update_available": False,
            "latest_message": "",
            "auto_update": bool(cfg.get("auto_update", True)),
        }

    remote_sha = remote["sha"]
    update_available = bool(remote_sha) and (local_sha.lower() != remote_sha.lower())
    return {
        "online": True,
        "local_sha": local_sha,
        "remote_sha": remote_sha,
        "update_available": update_available,
        "latest_message": remote["message"],
        "latest_date": remote["date"],
        "auto_update": bool(cfg.get("auto_update", True)),
    }


def apply_suite_update_from_extracted(extracted_root: Path, cfg: dict[str, Any], sha: str, update_repo_root: bool = True) -> dict[str, Any]:
    """Apply extracted worldbox-suite-main files to deployed WorldBox Mods (and standalone Studio root if non-git)."""
    enabled_dir, disabled_dir = _mods_dirs(cfg)
    updated_mods: list[str] = []

    for mod in CORE_MOD_NAMES:
        src = extracted_root / "mods" / mod
        if not src.is_dir():
            continue
        dst = (disabled_dir / mod) if ((disabled_dir / mod).exists() and not (enabled_dir / mod).exists()) else (enabled_dir / mod)
        _copy_mod_items_preserving_userdata(src, dst)
        updated_mods.append(mod)

    src_modern = extracted_root / "expansions" / "ModernAge"
    if src_modern.is_dir():
        for mod_dst in (enabled_dir / "ModernAge", disabled_dir / "ModernAge"):
            if mod_dst.exists():
                _copy_mod_items_preserving_userdata(src_modern, mod_dst)
                updated_mods.append("ModernAge")

    # Update local Studio installation if this is an installed copy (not a developer .git repo)
    is_git_repo = (REPO_ROOT / ".git").exists()
    if update_repo_root and not is_git_repo:
        for dir_name in ("tools", "mods", "expansions", "scripts"):
            s_dir = extracted_root / dir_name
            d_dir = REPO_ROOT / dir_name
            if s_dir.is_dir():
                if d_dir.exists():
                    shutil.rmtree(d_dir)
                shutil.copytree(s_dir, d_dir)
        for fname in ("WorldBox Studio.bat", "install.bat", "README.md", "LICENSE"):
            s_file = extracted_root / fname
            if s_file.is_file():
                shutil.copy2(s_file, REPO_ROOT / fname)

    if sha:
        if not is_git_repo:
            (REPO_ROOT / ".suite_sha").write_text(sha, encoding="utf-8")
        for hub in (enabled_dir / "SuiteHub", disabled_dir / "SuiteHub"):
            if hub.exists():
                (hub / ".suite_sha").write_text(sha, encoding="utf-8")

    return {
        "updated": True,
        "sha": sha,
        "updated_mods": updated_mods,
    }


def download_and_apply_update(cfg: dict[str, Any], force: bool = False) -> dict[str, Any]:
    with _UPDATE_LOCK:
        status = check_update_status(cfg)
        if not status["online"]:
            return {"updated": False, "reason": "GitHub unreachable / offline", **status}

        remote_sha = status["remote_sha"]
        if not force and not status["update_available"]:
            return {"updated": False, "reason": "Already up to date", **status}

        with tempfile.TemporaryDirectory() as tmp:
            zip_path = Path(tmp) / "suite.zip"
            req = urllib.request.Request(ARCHIVE_ZIP_URL, headers={"User-Agent": "WorldBoxSuite-StudioUpdater/1.0"})
            with urllib.request.urlopen(req, timeout=45) as resp:
                zip_path.write_bytes(resp.read())

            with zipfile.ZipFile(zip_path) as zf:
                zf.extractall(tmp)

            extracted = Path(tmp) / "worldbox-suite-main"
            if not extracted.is_dir():
                subdirs = [p for p in Path(tmp).iterdir() if p.is_dir()]
                if not subdirs:
                    raise RuntimeError("Downloaded archive had no root folder")
                extracted = subdirs[0]

            res = apply_suite_update_from_extracted(extracted, cfg, remote_sha)
            return {
                **status,
                **res,
                "local_sha": remote_sha,
                "update_available": False,
            }
