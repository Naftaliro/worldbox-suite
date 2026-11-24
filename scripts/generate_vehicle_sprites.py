"""Generate transparent 32x32 pixel-art sprites for ModernAge tanks, helicopters, and aircraft."""

from __future__ import annotations

import struct
import zlib
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = REPO_ROOT / "expansions" / "ModernAge" / "GameResources" / "vehicles"

# RGBA palette (transparent '.' + tactical military & Catppuccin accents)
PAL: dict[str, tuple[int, int, int, int]] = {
    ".": (0, 0, 0, 0),            # Transparent
    "k": (24, 24, 37, 255),       # Dark outline / treads
    "d": (56, 62, 74, 255),       # Dark steel / gunmetal
    "s": (118, 128, 146, 255),    # Light steel / fuselage
    "w": (205, 214, 244, 255),    # Bright highlight / rotor sheen
    "o": (84, 114, 72, 255),      # Olive drab armor
    "g": (118, 152, 96, 255),     # Light olive highlight
    "c": (116, 199, 236, 255),    # Cockpit glass cyan
    "f": (250, 179, 135, 255),    # Afterburner / muzzle glow
    "r": (243, 139, 168, 255),    # Wing stripe / accent
    "y": (249, 226, 175, 255),    # Star / emblem
}

# 16x16 pixel templates upscaled 2x -> 32x32 transparent RGBA sprites
SPRITES: dict[str, list[str]] = {
    "modern_light_tank": [
        "................",
        "................",
        "................",
        "................",
        ".....kkkkk......",
        "....kgggggk.....",
        "....koooookkkkkk",
        "...kkoooookddddd",
        "..kgggggggggk...",
        ".koooooooooook..",
        ".kooddddddoook..",
        ".kkkkkkkkkkkkk..",
        "..kdkdkdkdkdk...",
        "..kkkkkkkkkkk...",
        "................",
        "................",
    ],
    "modern_mbt": [
        "................",
        "................",
        "................",
        "....kkkkkkk.....",
        "...kgggggggk....",
        "...kooyooookkkkk",
        "..kkoooooookdddd",
        ".kggggggggggk...",
        "kkoooooooooookk.",
        "koodddddddddook.",
        "kdddddddddddddk.",
        "kkkkkkkkkkkkkkk.",
        ".kdkdkdkdkdkdk..",
        ".kkkkkkkkkkkkk..",
        "................",
        "................",
    ],
    "modern_prop_fighter": [
        "................",
        "................",
        "......kk........",
        "......ks........",
        "......ksk.......",
        "..kk..kssk......",
        ".kssk.ksssk.....",
        "kssssskccssskkkw",
        "kssrrssssssskkdw",
        ".kssk.ksssk.....",
        "..kk..kssk......",
        "......ksk.......",
        "......ks........",
        "......kk........",
        "................",
        "................",
    ],
    "modern_early_heli": [
        "................",
        "..wwwwwwwwwwww..",
        ".......kk.......",
        "......kkkk......",
        ".....kggggk.....",
        ".k..koocccck....",
        "ksk.koocccck....",
        "kskkkoooooook...",
        "kskkkooyooook...",
        ".k..kkkkkkkk....",
        "......k..k......",
        "....kkkkkkkk....",
        "................",
        "................",
        "................",
        "................",
    ],
    "modern_attack_heli": [
        "................",
        ".wwwwwwwwwwwwww.",
        ".......kk.......",
        ".....kkkkkk.....",
        ".k..kddddddk....",
        "kdkkddccdccdk...",
        "kddddddddddddk..",
        "kddssddrdddddff.",
        ".k..kddddddk....",
        ".....kdddk......",
        "....k.....k.....",
        "................",
        "................",
        "................",
        "................",
        "................",
    ],
    "modern_jet": [
        "................",
        "......kk........",
        ".....kssk.......",
        "....ksssk.......",
        "...kssssk.......",
        "..ksssssk.......",
        "ffksssssscckk...",
        "ffkssrrsssccsskk",
        "ffksssssscckk...",
        "..ksssssk.......",
        "...kssssk.......",
        "....ksssk.......",
        ".....kssk.......",
        "......kk........",
        "................",
        "................",
    ],
}


def _png_chunk(ctype: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(ctype + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + ctype + data + struct.pack(">I", crc)


def encode_rgba_png(rgba: bytearray, w: int, h: int) -> bytes:
    stride = w * 4
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        start = y * stride
        raw.extend(rgba[start : start + stride])
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)  # color_type=6 (RGBA)
    return (
        b"\x89PNG\r\n\x1a\n"
        + _png_chunk(b"IHDR", ihdr)
        + _png_chunk(b"IDAT", zlib.compress(bytes(raw), level=6))
        + _png_chunk(b"IEND", b"")
    )


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    scale = 2
    for name, rows in SPRITES.items():
        h = len(rows)
        w = len(rows[0])
        sw, sh = w * scale, h * scale
        buf = bytearray(sw * sh * 4)
        for y in range(sh):
            src_row = rows[y // scale]
            for x in range(sw):
                r, g, b, a = PAL[src_row[x // scale]]
                pos = (y * sw + x) * 4
                buf[pos] = r
                buf[pos + 1] = g
                buf[pos + 2] = b
                buf[pos + 3] = a
        out = OUT_DIR / f"{name}.png"
        out.write_bytes(encode_rgba_png(buf, sw, sh))
        print(f"Wrote {out.relative_to(REPO_ROOT)}")


if __name__ == "__main__":
    main()
