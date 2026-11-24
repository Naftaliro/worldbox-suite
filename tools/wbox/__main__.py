"""Command-line interface for WorldBox save inspection, rendering, and stitching."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from .io import load_wbox, save_wbox, summarize_save
from .render import render_map_png
from .stitch import StitchOptions, stitch_worlds, verify_save


def cmd_inspect(args: argparse.Namespace) -> int:
    data = load_wbox(args.file)
    summary = summarize_save(data)
    if args.json:
        print(json.dumps(summary, indent=2))
        return 0

    print(f"World:      {summary['name']} (saveVersion {summary['saveVersion']})")
    print(
        f"Size:       {summary['width_chunks']}x{summary['height_chunks']} chunks "
        f"({summary['width_tiles']}x{summary['height_tiles']} tiles, {summary['land_percent']}% land)"
    )
    print(f"Year / Age: Year {summary['year']} ({summary['world_age']})")
    c = summary["counts"]
    print(
        f"Entities:   {c['actors']} units | {c['kingdoms']} kingdoms | {c['cities']} cities | "
        f"{c['armies']} armies | {c['buildings']} buildings | {c['wars']} wars | {c['items']} items"
    )
    if summary["kingdoms"]:
        print("\nKingdoms:")
        for k in summary["kingdoms"]:
            print(
                f"  - {k['name']} ({k['species']}): {k['population']} pop, "
                f"{k['cities']} cities, {k['kills']} kills"
            )
    return 0


def cmd_render(args: argparse.Namespace) -> int:
    data = load_wbox(args.file)
    out = render_map_png(
        data,
        args.out,
        show_kingdoms=not args.no_kingdoms,
        show_buildings=not args.no_buildings,
        scale=args.scale,
    )
    print(f"Rendered preview: {out}")
    return 0


def cmd_stitch(args: argparse.Namespace) -> int:
    worlds = [load_wbox(p) for p in args.files]
    opts = StitchOptions(
        name=args.name,
        layout=args.layout,
        gap_chunks=args.gap,
        border_chunks=args.border,
        mode=args.mode,
        coast_blend=not args.no_blend,
    )
    merged = stitch_worlds(worlds, opts)
    errors = verify_save(merged)
    if errors:
        print("Verification failed:", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    out = save_wbox(merged, args.out)
    summary = summarize_save(merged)
    print(
        f"Stitched {len(worlds)} worlds -> {out}\n"
        f"  Name:     {summary['name']}\n"
        f"  Size:     {summary['width_chunks']}x{summary['height_chunks']} chunks "
        f"({summary['width_tiles']}x{summary['height_tiles']} tiles)\n"
        f"  Totals:   {summary['counts']['actors']} units, {summary['counts']['kingdoms']} kingdoms, "
        f"{summary['counts']['cities']} cities, {summary['counts']['buildings']} buildings"
    )
    if args.preview:
        render_map_png(merged, args.preview, scale=args.scale)
        print(f"  Preview:  {args.preview}")
    return 0


def cmd_verify(args: argparse.Namespace) -> int:
    data = load_wbox(args.file)
    errors = verify_save(data)
    if errors:
        print("FAILED:")
        for err in errors:
            print(f"  - {err}")
        return 1
    print(f"OK: {Path(args.file).name} passed all integrity checks.")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="wbox", description="WorldBox .wbox save toolkit")
    sub = parser.add_subparsers(dest="command", required=True)

    p_ins = sub.add_parser("inspect", help="Inspect a .wbox save file")
    p_ins.add_argument("file", help="Path to .wbox file")
    p_ins.add_argument("--json", action="store_true", help="Output raw JSON summary")
    p_ins.set_defaults(func=cmd_inspect)

    p_ren = sub.add_parser("render", help="Render a .wbox save to PNG")
    p_ren.add_argument("file", help="Path to .wbox file")
    p_ren.add_argument("--out", "-o", required=True, help="Output PNG path")
    p_ren.add_argument("--scale", type=int, default=2, help="Integer upscale factor")
    p_ren.add_argument("--no-kingdoms", action="store_true", help="Hide kingdom borders")
    p_ren.add_argument("--no-buildings", action="store_true", help="Hide buildings")
    p_ren.set_defaults(func=cmd_render)

    p_st = sub.add_parser("stitch", help="Stitch 2+ .wbox worlds into one larger map")
    p_st.add_argument("files", nargs="+", help="Two or more .wbox files")
    p_st.add_argument("--out", "-o", required=True, help="Output .wbox path")
    p_st.add_argument("--name", default="Twin Continents", help="Merged world name")
    p_st.add_argument("--layout", choices=["row", "column", "grid"], default="row")
    p_st.add_argument("--gap", type=int, default=2, help="Ocean gap between worlds in chunks")
    p_st.add_argument("--border", type=int, default=0, help="Outer ocean border in chunks")
    p_st.add_argument("--mode", choices=["full", "terrain_only"], default="full")
    p_st.add_argument("--no-blend", action="store_true", help="Disable coastal edge blending")
    p_st.add_argument("--preview", help="Optional output PNG preview path")
    p_st.add_argument("--scale", type=int, default=2, help="Preview PNG scale factor")
    p_st.set_defaults(func=cmd_stitch)

    p_ver = sub.add_parser("verify", help="Run integrity checks on a .wbox save")
    p_ver.add_argument("file", help="Path to .wbox file")
    p_ver.set_defaults(func=cmd_verify)

    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
