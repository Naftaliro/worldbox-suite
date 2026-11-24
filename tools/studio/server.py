"""Local HTTP server for WorldBox Studio & Mod Manager (zero external dependencies)."""

from __future__ import annotations

import argparse
import base64
import json
import re
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any
from urllib.parse import parse_qs, unquote, urlparse

from tools.wbox import (
    StitchOptions,
    load_wbox,
    render_map_png_bytes,
    save_wbox,
    stitch_worlds,
    summarize_save,
    verify_save,
)

from .mod_manager import (
    apply_profile,
    backup_saves,
    deploy_suite_mod,
    list_mods,
    load_config,
    save_config,
    save_profile,
    toggle_mod,
)

STATIC_DIR = Path(__file__).resolve().parent / "static"

# Simple in-memory cache keyed by (path, mtime_ns)
_SAVE_CACHE: dict[tuple[str, int], dict[str, Any]] = {}


def _get_cached_save(path: Path) -> dict[str, Any]:
    key = (str(path.resolve()), path.stat().st_mtime_ns)
    if key not in _SAVE_CACHE:
        # Keep cache bounded
        if len(_SAVE_CACHE) > 24:
            _SAVE_CACHE.clear()
        _SAVE_CACHE[key] = load_wbox(path)
    return _SAVE_CACHE[key]


def _discover_saves(cfg: dict[str, Any]) -> dict[str, Path]:
    found: dict[str, Path] = {}
    for bucket, key in (("saves", "saves_dir"), ("output", "output_dir")):
        root = Path(cfg.get(key, ""))
        if not root.exists():
            continue
        for p in sorted(root.glob("*.wbox")):
            found[f"{bucket}:{p.name}"] = p
        # Also detect standard WorldBox slot folders (save1/map.wbox, etc.)
        for p in sorted(root.glob("*/map.wbox")):
            found[f"{bucket}:{p.parent.name}_map.wbox"] = p
    return found


def _safe_filename(name: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9._-]+", "_", name.strip()).strip("_")
    if not cleaned:
        cleaned = "stitched_world"
    if not cleaned.endswith(".wbox"):
        cleaned += ".wbox"
    return cleaned


class StudioHandler(BaseHTTPRequestHandler):
    def log_message(self, fmt: str, *args: Any) -> None:
        return  # Keep terminal output quiet unless errors occur

    def _send_bytes(self, body: bytes, content_type: str, status: int = 200, extra_headers: dict[str, str] | None = None) -> None:
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        if extra_headers:
            for k, v in extra_headers.items():
                self.send_header(k, v)
        self.end_headers()
        self.wfile.write(body)

    def _send_json(self, payload: Any, status: int = 200) -> None:
        self._send_bytes(json.dumps(payload).encode("utf-8"), "application/json; charset=utf-8", status)

    def _read_json(self) -> dict[str, Any]:
        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length) if length > 0 else b"{}"
        return json.loads(raw.decode("utf-8"))

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        path = parsed.path
        qs = parse_qs(parsed.query)

        try:
            if path in ("/", "/index.html"):
                html = (STATIC_DIR / "index.html").read_bytes()
                self._send_bytes(html, "text/html; charset=utf-8")
                return

            cfg = load_config()

            if path == "/api/config":
                self._send_json(cfg)
                return

            if path == "/api/saves":
                saves_map = _discover_saves(cfg)
                items = []
                for sid, fpath in saves_map.items():
                    try:
                        data = _get_cached_save(fpath)
                        summary = summarize_save(data)
                        items.append(
                            {
                                "id": sid,
                                "filename": fpath.name,
                                "bucket": sid.split(":", 1)[0],
                                "size_bytes": fpath.stat().st_size,
                                "summary": summary,
                            }
                        )
                    except Exception as exc:
                        items.append({"id": sid, "filename": fpath.name, "error": str(exc)})
                self._send_json({"saves": items})
                return

            if path.startswith("/api/saves/"):
                parts = path.split("/")
                if len(parts) >= 5:
                    sid = unquote(parts[3])
                    action = parts[4]
                    saves_map = _discover_saves(cfg)
                    if sid not in saves_map:
                        self._send_json({"error": "Save not found"}, 404)
                        return
                    fpath = saves_map[sid]
                    if action == "summary":
                        data = _get_cached_save(fpath)
                        self._send_json(summarize_save(data))
                        return
                    if action == "preview.png":
                        data = _get_cached_save(fpath)
                        show_k = qs.get("kingdoms", ["1"])[0] != "0"
                        show_b = qs.get("buildings", ["1"])[0] != "0"
                        scale = max(1, min(4, int(qs.get("scale", ["2"])[0])))
                        png = render_map_png_bytes(data, show_kingdoms=show_k, show_buildings=show_b, scale=scale)
                        self._send_bytes(png, "image/png")
                        return
                    if action == "download":
                        self._send_bytes(
                            fpath.read_bytes(),
                            "application/octet-stream",
                            extra_headers={"Content-Disposition": f'attachment; filename="{fpath.name}"'},
                        )
                        return

            if path == "/api/mods":
                self._send_json(list_mods(cfg))
                return

            self._send_json({"error": "Not found"}, HTTPStatus.NOT_FOUND)
        except Exception as exc:
            self._send_json({"error": str(exc)}, HTTPStatus.INTERNAL_SERVER_ERROR)

    def do_POST(self) -> None:
        path = urlparse(self.path).path
        try:
            body = self._read_json()
            cfg = load_config()

            if path == "/api/config":
                for key in ("saves_dir", "output_dir", "game_streaming_assets"):
                    if key in body:
                        cfg[key] = str(body[key]).strip()
                save_config(cfg)
                self._send_json(cfg)
                return

            if path == "/api/saves/upload":
                fname = _safe_filename(body.get("filename", "uploaded.wbox"))
                raw = base64.b64decode(body["content_base64"])
                dest = Path(cfg["saves_dir"]) / fname
                dest.parent.mkdir(parents=True, exist_ok=True)
                dest.write_bytes(raw)
                # Validate immediately
                data = load_wbox(dest)
                self._send_json({"id": f"saves:{fname}", "summary": summarize_save(data)})
                return

            if path == "/api/stitch":
                save_ids: list[str] = body.get("save_ids", [])
                saves_map = _discover_saves(cfg)
                worlds = [_get_cached_save(saves_map[sid]) for sid in save_ids if sid in saves_map]
                if len(worlds) < 2:
                    self._send_json({"error": "Select at least 2 worlds to stitch."}, 400)
                    return

                world_name = str(body.get("name") or "Twin Continents").strip()
                opts = StitchOptions(
                    name=world_name,
                    layout=str(body.get("layout", "row")),
                    gap_chunks=max(0, min(8, int(body.get("gap_chunks", 2)))),
                    border_chunks=max(0, min(4, int(body.get("border_chunks", 0)))),
                    mode=str(body.get("mode", "full")),
                    coast_blend=bool(body.get("coast_blend", True)),
                )
                merged = stitch_worlds(worlds, opts)
                errors = verify_save(merged)
                if errors:
                    self._send_json({"error": "Integrity check failed", "details": errors}, 500)
                    return

                out_fname = _safe_filename(body.get("filename") or world_name.lower().replace(" ", "_"))
                out_path = Path(cfg["output_dir"]) / out_fname
                save_wbox(merged, out_path)
                sid = f"output:{out_fname}"
                self._send_json(
                    {
                        "id": sid,
                        "path": str(out_path),
                        "summary": summarize_save(merged),
                    }
                )
                return

            if path == "/api/mods/toggle":
                res = toggle_mod(cfg, str(body["folder"]), bool(body["enable"]))
                self._send_json(res)
                return

            if path == "/api/mods/deploy":
                res = deploy_suite_mod(cfg, str(body["folder"]))
                self._send_json(res)
                return

            if path == "/api/profiles/save":
                res = save_profile(cfg, str(body["name"]))
                self._send_json(res)
                return

            if path == "/api/profiles/apply":
                res = apply_profile(cfg, str(body["name"]))
                self._send_json(res)
                return

            if path == "/api/backup":
                self._send_json(backup_saves(cfg))
                return

            self._send_json({"error": "Not found"}, HTTPStatus.NOT_FOUND)
        except Exception as exc:
            self._send_json({"error": str(exc)}, HTTPStatus.BAD_REQUEST)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="WorldBox Studio & Mod Manager server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args(argv)

    server = ThreadingHTTPServer((args.host, args.port), StudioHandler)
    print(f"WorldBox Studio running at http://{args.host}:{args.port}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down.")
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
