"""Generate 32x32 transparent RGBA pixel-art sprites and toolbar icons for WorldBox Suite.

Pure Python standard library only (struct, zlib, math, pathlib).
"""

from __future__ import annotations

import argparse
import math
import struct
import tempfile
import zlib
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
WARDEN_DIR = REPO_ROOT / "mods" / "ApexFactions" / "GameResources" / "wardens"
HUB_RES_DIR = REPO_ROOT / "mods" / "SuiteHub" / "GameResources"
HUB_MOD_ICON = REPO_ROOT / "mods" / "SuiteHub" / "icon.png"
APEX_MOD_ICON = REPO_ROOT / "mods" / "ApexFactions" / "icon.png"
DEFAULT_SHEET_PATH = Path(tempfile.gettempdir()) / "worldbox_suite_art_review_sheet.png"
DEFAULT_ANIM_SHEET_PATH = Path(tempfile.gettempdir()) / "worldbox_warden_anim_sheet.png"

Color = tuple[int, int, int, int]

# Shared WorldBox-inspired pixel-art color ramps
TRANSPARENT: Color = (0, 0, 0, 0)
OUTLINE_WARM: Color = (20, 15, 11, 255)      # #140f0b
OUTLINE_DARK: Color = (17, 17, 27, 255)      # #11111b

# Obsidian armor
OBS_0: Color = (22, 23, 36, 255)
OBS_1: Color = (38, 41, 61, 255)
OBS_2: Color = (58, 63, 92, 255)
OBS_3: Color = (88, 96, 134, 255)
OBS_4: Color = (130, 140, 184, 255)

# Gold & Brass
GOLD_0: Color = (94, 52, 10, 255)
GOLD_1: Color = (158, 94, 18, 255)
GOLD_2: Color = (218, 150, 28, 255)
GOLD_3: Color = (248, 206, 74, 255)
GOLD_4: Color = (255, 244, 176, 255)

# Crimson & Ruby
RED_0: Color = (82, 14, 26, 255)
RED_1: Color = (146, 26, 46, 255)
RED_2: Color = (212, 46, 68, 255)
RED_3: Color = (245, 96, 114, 255)
RED_4: Color = (255, 172, 180, 255)

# Orange / Fire
ORG_1: Color = (204, 86, 20, 255)
ORG_2: Color = (242, 132, 34, 255)
ORG_3: Color = (255, 184, 68, 255)

# Emerald & Grass
GRN_0: Color = (16, 62, 38, 255)
GRN_1: Color = (32, 108, 64, 255)
GRN_2: Color = (58, 168, 94, 255)
GRN_3: Color = (114, 218, 132, 255)
GRN_4: Color = (190, 246, 184, 255)

# Sapphire & Ocean & Cyan
BLU_0: Color = (18, 44, 96, 255)
BLU_1: Color = (34, 86, 168, 255)
BLU_2: Color = (56, 144, 228, 255)
BLU_3: Color = (114, 204, 248, 255)
BLU_4: Color = (206, 244, 255, 255)

# Amethyst & Portal Violet
VIO_0: Color = (46, 18, 82, 255)
VIO_1: Color = (94, 40, 154, 255)
VIO_2: Color = (152, 84, 222, 255)
VIO_3: Color = (204, 148, 250, 255)
VIO_4: Color = (240, 222, 255, 255)

# Steel & Silver
STL_0: Color = (44, 50, 64, 255)
STL_1: Color = (84, 94, 116, 255)
STL_2: Color = (138, 152, 178, 255)
STL_3: Color = (194, 206, 228, 255)
STL_4: Color = (240, 246, 255, 255)

# Stone
STN_0: Color = (54, 53, 60, 255)
STN_1: Color = (92, 90, 100, 255)
STN_2: Color = (136, 134, 146, 255)
STN_3: Color = (184, 182, 194, 255)
STN_4: Color = (224, 222, 232, 255)

# Parchment / Ivory
PAR_0: Color = (116, 86, 52, 255)
PAR_1: Color = (176, 138, 90, 255)
PAR_2: Color = (220, 192, 142, 255)
PAR_3: Color = (244, 226, 190, 255)
PAR_4: Color = (255, 248, 230, 255)

# Leather / Chestnut / Wood
BRN_0: Color = (58, 28, 14, 255)
BRN_1: Color = (102, 52, 26, 255)
BRN_2: Color = (152, 82, 42, 255)
BRN_3: Color = (202, 124, 70, 255)
BRN_4: Color = (236, 172, 116, 255)


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
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + _png_chunk(b"IHDR", ihdr)
        + _png_chunk(b"IDAT", zlib.compress(bytes(raw), level=6))
        + _png_chunk(b"IEND", b"")
    )


class Canvas:
    def __init__(self, w: int = 32, h: int = 32) -> None:
        self.w = w
        self.h = h
        self.buf = bytearray(w * h * 4)

    def copy(self) -> "Canvas":
        c = Canvas(self.w, self.h)
        c.buf[:] = self.buf
        return c

    def px(self, x: int, y: int, col: Color) -> None:
        if 0 <= x < self.w and 0 <= y < self.h:
            i = (y * self.w + x) * 4
            if col[3] == 255 or col[3] == 0:
                self.buf[i] = col[0]
                self.buf[i + 1] = col[1]
                self.buf[i + 2] = col[2]
                self.buf[i + 3] = col[3]
            else:
                sa = col[3] / 255.0
                da = self.buf[i + 3] / 255.0
                oa = sa + da * (1.0 - sa)
                if oa > 0:
                    self.buf[i] = int((col[0] * sa + self.buf[i] * da * (1 - sa)) / oa)
                    self.buf[i + 1] = int((col[1] * sa + self.buf[i + 1] * da * (1 - sa)) / oa)
                    self.buf[i + 2] = int((col[2] * sa + self.buf[i + 2] * da * (1 - sa)) / oa)
                    self.buf[i + 3] = int(oa * 255)

    def get(self, x: int, y: int) -> Color:
        if 0 <= x < self.w and 0 <= y < self.h:
            i = (y * self.w + x) * 4
            return (self.buf[i], self.buf[i + 1], self.buf[i + 2], self.buf[i + 3])
        return TRANSPARENT

    def is_solid(self, x: int, y: int) -> bool:
        return self.get(x, y)[3] > 0

    def fill_rect(self, x0: int, y0: int, x1: int, y1: int, col: Color) -> None:
        for y in range(min(y0, y1), max(y0, y1) + 1):
            for x in range(min(x0, x1), max(x0, x1) + 1):
                self.px(x, y, col)

    def hline(self, x0: int, x1: int, y: int, col: Color) -> None:
        for x in range(min(x0, x1), max(x0, x1) + 1):
            self.px(x, y, col)

    def vline(self, x: int, y0: int, y1: int, col: Color) -> None:
        for y in range(min(y0, y1), max(y0, y1) + 1):
            self.px(x, y, col)

    def line(self, x0: int, y0: int, x1: int, y1: int, col: Color) -> None:
        dx = abs(x1 - x0)
        dy = -abs(y1 - y0)
        sx = 1 if x0 < x1 else -1
        sy = 1 if y0 < y1 else -1
        err = dx + dy
        x, y = x0, y0
        while True:
            self.px(x, y, col)
            if x == x1 and y == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x += sx
            if e2 <= dx:
                err += dx
                y += sy

    def ellipse(self, cx: float, cy: float, rx: float, ry: float, col: Color) -> None:
        x0 = max(0, int(math.floor(cx - rx - 1)))
        x1 = min(self.w - 1, int(math.ceil(cx + rx + 1)))
        y0 = max(0, int(math.floor(cy - ry - 1)))
        y1 = min(self.h - 1, int(math.ceil(cy + ry + 1)))
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                nx = (x - cx) / rx
                ny = (y - cy) / ry
                if nx * nx + ny * ny <= 1.0:
                    self.px(x, y, col)

    def circle(self, cx: float, cy: float, r: float, col: Color) -> None:
        self.ellipse(cx, cy, r, r, col)

    def poly(self, pts: list[tuple[float, float]], col: Color) -> None:
        ys = [p[1] for p in pts]
        min_y = max(0, int(math.floor(min(ys))))
        max_y = min(self.h - 1, int(math.ceil(max(ys))))
        n = len(pts)
        for y in range(min_y, max_y + 1):
            py = y + 0.5
            xs: list[float] = []
            for i in range(n):
                x1, y1 = pts[i]
                x2, y2 = pts[(i + 1) % n]
                if (y1 <= py < y2) or (y2 <= py < y1):
                    t = (py - y1) / (y2 - y1)
                    xs.append(x1 + t * (x2 - x1))
            xs.sort()
            for j in range(0, len(xs) - 1, 2):
                x_start = max(0, int(math.ceil(xs[j] - 0.5)))
                x_end = min(self.w - 1, int(math.floor(xs[j + 1] - 0.5)))
                for x in range(x_start, x_end + 1):
                    self.px(x, y, col)

    def stamp(self, rows: list[str], palette: dict[str, Color], ox: int = 0, oy: int = 0) -> None:
        for dy, row in enumerate(rows):
            for dx, ch in enumerate(row):
                if ch in palette:
                    col = palette[ch]
                    if col[3] > 0:
                        self.px(ox + dx, oy + dy, col)

    def add_outline(self, col: Color = OUTLINE_WARM) -> None:
        snap = self.copy()
        for y in range(self.h):
            for x in range(self.w):
                if not snap.is_solid(x, y):
                    if (
                        snap.is_solid(x - 1, y)
                        or snap.is_solid(x + 1, y)
                        or snap.is_solid(x, y - 1)
                        or snap.is_solid(x, y + 1)
                    ):
                        self.px(x, y, col)

    def blit(self, src: "Canvas", ox: int = 0, oy: int = 0) -> None:
        for y in range(src.h):
            for x in range(src.w):
                c = src.get(x, y)
                if c[3] > 0:
                    self.px(ox + x, oy + y, c)

    def scale_nn(self, factor: int) -> "Canvas":
        out = Canvas(self.w * factor, self.h * factor)
        for y in range(out.h):
            sy = y // factor
            for x in range(out.w):
                sx = x // factor
                out.px(x, y, self.get(sx, sy))
        return out

    def to_png_bytes(self) -> bytes:
        return encode_rgba_png(self.buf, self.w, self.h)


# =============================================================================
# 1. WARDEN SPECIES SPRITES (32x32 transparent RGBA)
# =============================================================================

WARDEN_PAL: dict[str, Color] = {
    ".": TRANSPARENT,
    "K": OUTLINE_DARK,
    "o": OBS_0,   # deepest obsidian cloak/shadow
    "d": OBS_1,   # dark obsidian plate
    "s": OBS_2,   # mid obsidian slate
    "l": OBS_3,   # light obsidian bevel
    "h": OBS_4,   # cool highlight
    "b": GOLD_0,  # bronze shadow
    "a": GOLD_1,  # amber gold shadow
    "g": GOLD_2,  # rich gold
    "y": GOLD_3,  # bright gold
    "w": GOLD_4,  # radiant gold highlight
    "r": RED_1,   # dark royal crimson
    "c": RED_2,   # royal crimson
    "p": RED_3,   # bright crimson glow
    "q": RED_4,   # crimson-white hot core
    "m": STL_2,   # steel blade
    "t": STL_3,   # light steel
    "e": STL_4,   # steel glint
}


def make_warden_citizen() -> Canvas:
    """Tall obsidian-and-gold armored Sentinel with flowing cloak & golden visor."""
    c = Canvas(32, 32)
    rows = [
        "................................",
        "................................",
        "..............gg................",
        ".............gyyg...............",
        "............gdlldg..............",
        "...........KdlhhldK.............",
        "...........KdssssdK.............",
        "...........gdKwwKdg.............",
        "...........gdgwwgdg.............",
        "............gdggdg..............",
        ".........gg..agga..gg...........",
        "........gyygdssssdgyyg..........",
        ".......gylsddgwwgddslgg.........",
        "......odlsddsgwwgsddsldo........",
        "......oddddsdsgagsdsdddo........",
        ".....ooddddsdssssdsddddoo.......",
        ".....oosdls.ddggggdd.sldsoo.....",
        ".....oosdls.dgywwygd.sldsoo.....",
        ".....ooodd..dsgagsd..ddooo......",
        ".....oosdg..dsggsd..gdsoo.......",
        ".....ooodd..dsggsd..ddooo.......",
        "....ooosdd..dsl..lsd..ddsooo....",
        "....ooood..dsl..lsd..doooo......",
        "....ooood..dgd..dgd..doooo......",
        "....ooosd..dsl..lsd..dsooo......",
        "....ooogd..dsd..dsd..dgooo......",
        "....ooog...dsd..dsd...gooo......",
        "....ooog..gdgd..dgdg..gooo......",
        ".....oo...gddg..gddg...oo.......",
        "..........bbbb..bbbb............",
        "................................",
        "................................",
    ]
    c.stamp(rows, WARDEN_PAL)
    c.add_outline(OUTLINE_DARK)
    return c


def make_warden_warrior() -> Canvas:
    """Awakened War Sentinel with heavy gold pauldrons, crimson-gold visor, tall crescent halberd."""
    c = Canvas(32, 32)
    rows = [
        ".......................e........",
        "............g...g.....tet.......",
        "...........gy...yg....tmt.gg....",
        "..........gydllldyg...tmtgywg...",
        "..........gdlhhhldg...tmgtetg...",
        "..........gdsssssdg...gmgtemg...",
        "..........gdKpqppdg...gmgtemg...",
        "..........gdgqqqgdg...gmgywg....",
        "...........gdgggdg....gbgg......",
        ".......ggg..aggga..gggag........",
        "......gywwgdsssssdgwwygag.......",
        ".....gylssddgwwwgddsslgag.......",
        "....gdlsssdsgwwwgsdssldag.......",
        "....odddddsdsgagsdsdddda........",
        "...ooodgddsdsssssdsddgdg........",
        "...oooglls.ddggggdd.slgy........",
        "...oooglls.dgywwygd.slga........",
        "...oooglsd..dsgagsd..sga........",
        "...ooogdsd..dsggsd...sga........",
        "...ooogdsd..dsggsd...gga........",
        "...oooodd..dsl..lsd..dga........",
        "...ooood...dsl..lsd...ga........",
        "...ooood...dgd..dgd...ga........",
        "...ooood..ddsl..lsdd..ga........",
        "...ooogd..ddsd..dsdd..ga........",
        "...ooog...ddsd..dsdd..ga........",
        "....oog..gdgdg..gdgdg.ga........",
        "....oo...gdddg..gdddg.gb........",
        ".........bbbbb..bbbbb.gb........",
        "......................gg........",
        "................................",
        "................................",
    ]
    c.stamp(rows, WARDEN_PAL)
    c.add_outline(OUTLINE_DARK)
    return c


def make_warden_leader() -> Canvas:
    """Sovereign Warden King with radiant golden crown, royal crimson cape, ornate halberd-spear."""
    c = Canvas(32, 32)
    rows = [
        ".............w..........e.......",
        "..........w..y..w......tet......",
        "..........gygygyg.....gteyg.....",
        "..........gywywyg....gytetyg....",
        "..........gdlhldg.....gtmtg.....",
        "..........gdsssdg.....gypyg.....",
        "..........gdKwKdg....gggyggg....",
        "..........gdgwgdf......gbg......",
        "...........gdggd........a.......",
        ".......ggg..aga..ggg....a.......",
        "......gywwgdsssdgwwyg...a.......",
        ".....rgyssddgwgddssygr..a.......",
        "....rrcdsssdsgwgsdssdr..a.......",
        "...rrrcddddsdsgsdsddddr.a.......",
        "...rrcddddgsdsssdsgddddga.......",
        "..rrrcgdl.gddgggddg.ldgygr......",
        "..rrrcgdl..dgywygd..ldgarr......",
        "..rrrcgd...dsgagsd...dgarr......",
        "..rrrcgd...dsggsd....dgarr......",
        "..rrrcg....dsggsd....ggarr......",
        "..rrcgg...dsl..lsd...ggcrr......",
        "..rrcg....dsl..lsd...cgar.......",
        "..rrcg....dgd..dgd...cgar.......",
        "..rrcg...ddsl..lsdd..cgar.......",
        "..rrcg...ddsd..dsdd..rgar.......",
        "..rrcg...ddsd..dsdd..rgar.......",
        "..rrgg..gdgdg..gdgdg.rgar.......",
        "...rg...gdddg..gdddg..gbg.......",
        "........bbbbb..bbbbb..gb........",
        "......................gg........",
        "................................",
        "................................",
    ]
    rows = [r.replace("f", "g") for r in rows]
    c.stamp(rows, WARDEN_PAL)
    c.add_outline(OUTLINE_DARK)
    return c


def make_warden_icon() -> Canvas:
    """Crisp close-up Sentinel obsidian-and-gold armored helm icon for Powers toolbar."""
    c = Canvas(32, 32)
    rows = [
        "................................",
        "................................",
        "..............gg................",
        "....g........gyyg........g......",
        "...gyg......gywwyg......gyg.....",
        "...gylg...dddlhhlddd...glyg.....",
        "....gdlg.ddlhhllhhldd.gldg......",
        "....gdlsddlssllhllssldslg.......",
        ".....gdlsdssssssssssdslg........",
        ".....gddsdssllggllssdsdg........",
        "......gddsdlggwwggldsddg........",
        "......gddddgwwwwwwgddddg........",
        "......gddKKKwwwwwwKKKddg........",
        "......gddKwwwKwwKwwwKddg........",
        "......gddKwywKwwKwywKddg........",
        "......gdddKKKgwwgKKKdddg........",
        "......gddlsddgwwgddslgdg........",
        ".......gdlsddgwwgddslgg.........",
        ".......gddlsdgwwgdslgdg.........",
        "........gddsdgwwgdsddg..........",
        "........gddsdgwwgdsddg..........",
        ".........gddsgwwgsddg...........",
        ".........gdddsggdsddg...........",
        "..........gddssssddg............",
        "..........ggdggggdgg............",
        "........ggyydssssdyygg..........",
        ".......gywgdssggssdgwyg.........",
        ".......gbbddggwwggddbbg.........",
        "........bbb.gggggg.bbb..........",
        "................................",
        "................................",
        "................................",
    ]
    c.stamp(rows, WARDEN_PAL)
    c.add_outline(OUTLINE_DARK)
    return c


# =============================================================================
# 2. UPGRADED TOOLBAR ICONS (32x32 transparent RGBA)
# =============================================================================

def make_btn_hub() -> Canvas:
    """SuiteHub: Ornate golden-and-amethyst compass rose with glowing core jewel."""
    c = Canvas(32, 32)
    cx, cy = 15.5, 15.5
    # Crisp 4-way symmetric diagonal secondary points
    for sx in (-1, 1):
        for sy in (-1, 1):
            for step in range(4, 11):
                x = int(round(cx + sx * step * 0.78))
                y = int(round(cy + sy * step * 0.78))
                c.px(x, y, GOLD_3 if sx < 0 or sy < 0 else GOLD_2)
                if step < 9:
                    c.px(x - sx, y, GOLD_2 if sy < 0 else GOLD_1)
                    c.px(x, y - sy, GOLD_3 if sx < 0 else GOLD_1)

    # Outer compass ring
    c.circle(cx, cy, 8.6, GOLD_1)
    c.circle(cx, cy, 7.6, GOLD_3)
    c.circle(cx, cy, 6.2, OBS_1)

    # 4 Main cardinal golden spear points (sharp shaded facets)
    cardinal = [
        (15.5, 1.5, 12.2, 12.2, 18.8, 12.2),   # North
        (15.5, 29.5, 18.8, 18.8, 12.2, 18.8),  # South
        (1.5, 15.5, 12.2, 18.8, 12.2, 12.2),   # West
        (29.5, 15.5, 18.8, 12.2, 18.8, 18.8),  # East
    ]
    for tip_x, tip_y, lx, ly, rx, ry in cardinal:
        c.poly([(tip_x, tip_y), (lx, ly), (cx, cy)], GOLD_3)
        c.poly([(tip_x, tip_y), (cx, cy), (rx, ry)], GOLD_1)
        c.line(int(round(tip_x)), int(round(tip_y)), int(round(cx)), int(round(cy)), GOLD_4)

    # Central obsidian bezel + radiant faceted amethyst gem
    c.circle(cx, cy, 5.4, OBS_0)
    c.circle(cx, cy, 4.5, GOLD_2)
    c.circle(cx, cy, 3.6, VIO_1)
    c.circle(cx - 0.5, cy - 0.5, 2.7, VIO_2)
    c.circle(cx - 1.0, cy - 1.0, 1.6, VIO_3)
    c.px(14, 14, VIO_4)
    c.px(15, 14, VIO_4)
    c.px(14, 15, VIO_4)

    c.add_outline(OUTLINE_DARK)
    return c


def make_btn_scale() -> Canvas:
    """CoreScale: Shaded brass mechanical gear with crackling cyan-gold lightning bolt."""
    c = Canvas(32, 32)
    cx, cy = 15.5, 15.5

    # 8 crisp beveled rectangular mechanical cog teeth
    for i in range(8):
        ang = math.radians(i * 45)
        cos_a, sin_a = math.cos(ang), math.sin(ang)
        perp_x, perp_y = -sin_a, cos_a
        r_in, r_out = 8.4, 13.0
        hw_in, hw_out = 2.4, 2.0
        pts = [
            (cx + cos_a * r_in - perp_x * hw_in, cy + sin_a * r_in - perp_y * hw_in),
            (cx + cos_a * r_out - perp_x * hw_out, cy + sin_a * r_out - perp_y * hw_out),
            (cx + cos_a * r_out + perp_x * hw_out, cy + sin_a * r_out + perp_y * hw_out),
            (cx + cos_a * r_in + perp_x * hw_in, cy + sin_a * r_in + perp_y * hw_in),
        ]
        col = GOLD_3 if (cos_a + sin_a) < -0.2 else (GOLD_1 if (cos_a + sin_a) > 0.5 else GOLD_2)
        c.poly(pts, col)

    # Gear wheel body
    c.circle(cx, cy, 9.8, GOLD_1)
    c.circle(cx - 0.4, cy - 0.4, 8.9, GOLD_2)
    c.circle(cx - 0.8, cy - 0.8, 7.8, GOLD_3)
    c.circle(cx + 0.6, cy + 0.6, 7.8, GOLD_2)
    # Recessed inner dish
    c.circle(cx, cy, 5.6, GOLD_0)
    c.circle(cx, cy, 4.5, OBS_1)
    c.circle(cx, cy, 3.2, OBS_0)
    # Rim glint
    for deg in range(200, 265, 8):
        a = math.radians(deg)
        c.px(int(round(cx + math.cos(a) * 8.2)), int(round(cy + math.sin(a) * 8.2)), GOLD_4)

    # Crisp electric cyan-white lightning bolt
    bolt_layer = Canvas(32, 32)
    bolt_layer.poly([(19.0, 2.5), (23.5, 2.5), (17.5, 12.5), (24.0, 12.5), (11.5, 29.0), (14.5, 17.5), (8.5, 17.5)], BLU_2)
    bolt_layer.poly([(19.5, 3.5), (22.2, 3.5), (16.5, 13.0), (22.0, 13.0), (13.0, 26.5), (15.5, 17.0), (10.2, 17.0)], BLU_3)
    bolt_layer.line(20, 4, 13, 15, BLU_4)
    bolt_layer.line(19, 14, 13, 25, BLU_4)
    bolt_layer.px(19, 5, GOLD_4)
    bolt_layer.px(15, 16, BLU_4)
    bolt_layer.add_outline(OUTLINE_DARK)

    c.add_outline(OUTLINE_WARM)
    c.blit(bolt_layer)
    return c


def make_btn_visual() -> Canvas:
    """VisualPolish: Radiant golden sun (top-left) + sapphire crescent moon (bottom-right) + sparkles."""
    c = Canvas(32, 32)

    # Golden sun in upper-left
    sx, sy = 11.5, 11.5
    for i in range(10):
        ang = math.radians(i * 36)
        r_len = 9.8 if i % 2 == 0 else 8.0
        x1 = int(round(sx + math.cos(ang) * 5.0))
        y1 = int(round(sy + math.sin(ang) * 5.0))
        x2 = int(round(sx + math.cos(ang) * r_len))
        y2 = int(round(sy + math.sin(ang) * r_len))
        c.line(x1, y1, x2, y2, ORG_2 if i % 2 else GOLD_3)
        if i % 2 == 0:
            c.px(x2, y2, GOLD_4)

    c.circle(sx, sy, 6.2, ORG_2)
    c.circle(sx - 0.4, cy_s := sy - 0.4, 5.2, GOLD_3)
    c.circle(sx - 1.2, cy_s - 0.8, 3.0, GOLD_4)

    # Crescent moon in lower-right (built on separate layer for crisp separation)
    moon = Canvas(32, 32)
    mx, my = 20.5, 20.5
    for y in range(32):
        for x in range(32):
            d1 = math.hypot(x - mx, y - my)
            d2 = math.hypot(x - (mx - 3.6), y - (my - 3.2))
            if d1 <= 8.2 and d2 >= 6.4:
                if d1 > 6.8:
                    moon.px(x, y, BLU_1)
                elif d2 < 7.8:
                    moon.px(x, y, BLU_4)
                else:
                    moon.px(x, y, BLU_3)
    moon.add_outline(OUTLINE_DARK)

    # Crisp 4-point twinkling stars
    stars = Canvas(32, 32)
    for stx, sty, rad in [(24, 7, 2), (7, 24, 2)]:
        stars.hline(stx - rad, stx + rad, sty, GOLD_3)
        stars.vline(stx, sty - rad, sty + rad, GOLD_3)
        stars.px(stx, sty, GOLD_4)
    stars.add_outline(OUTLINE_DARK)

    c.add_outline(OUTLINE_WARM)
    c.blit(moon)
    c.blit(stars)
    return c


def make_btn_war_matrix() -> Canvas:
    """War Monitor: Crossed gleaming steel swords over a rolled parchment scroll with wax seal."""
    c = Canvas(32, 32)

    # Parchment scroll behind swords
    c.fill_rect(7, 6, 24, 25, PAR_1)
    c.fill_rect(8, 7, 23, 24, PAR_2)
    c.fill_rect(9, 8, 22, 22, PAR_3)
    # Scroll rollers top & bottom
    c.fill_rect(5, 4, 26, 7, BRN_1)
    c.fill_rect(6, 5, 25, 6, GOLD_2)
    c.hline(6, 24, 5, GOLD_3)
    c.fill_rect(5, 24, 26, 27, BRN_1)
    c.fill_rect(6, 25, 25, 26, GOLD_2)
    c.hline(6, 24, 25, GOLD_3)
    # Subtle war ledger lines
    for ly in (11, 14, 17, 20):
        c.hline(11, 20, ly, PAR_0)
    # Crimson wax seal at bottom center
    c.circle(15.5, 21.5, 2.5, RED_1)
    c.circle(15.2, 21.2, 1.7, RED_2)
    c.px(15, 21, RED_3)
    c.add_outline(OUTLINE_WARM)

    # Crossed steel swords on top layer
    swords = Canvas(32, 32)
    # Sword 1: bottom-left hilt -> top-right tip
    for i in range(19):
        x = 8 + i
        y = 23 - i
        swords.px(x, y, STL_3)
        swords.px(x + 1, y, STL_2)
        swords.px(x, y - 1, STL_4)
    swords.px(27, 4, STL_4)
    # Crossguard 1
    swords.line(6, 19, 12, 25, GOLD_2)
    swords.line(6, 18, 12, 24, GOLD_3)
    # Grip & pommel 1
    swords.line(4, 27, 8, 23, BRN_2)
    swords.circle(4, 27, 1.5, RED_2)
    swords.px(4, 27, RED_3)

    # Sword 2: bottom-right hilt -> top-left tip
    for i in range(19):
        x = 23 - i
        y = 23 - i
        swords.px(x, y, STL_3)
        swords.px(x - 1, y, STL_2)
        swords.px(x, y - 1, STL_4)
    swords.px(4, 4, STL_4)
    # Crossguard 2
    swords.line(25, 19, 19, 25, GOLD_2)
    swords.line(25, 18, 19, 24, GOLD_3)
    # Grip & pommel 2
    swords.line(27, 27, 23, 23, BRN_2)
    swords.circle(27, 27, 1.5, RED_2)
    swords.px(27, 27, RED_3)

    swords.add_outline(OUTLINE_DARK)
    c.blit(swords)
    return c


def make_btn_battle_radar() -> Canvas:
    """Battle Radar: Brass tactical targeting reticle with glowing crimson ping."""
    c = Canvas(32, 32)
    cx, cy = 15.5, 15.5

    # Outer brass bezel
    c.circle(cx, cy, 11.4, GOLD_1)
    c.circle(cx - 0.3, cy - 0.3, 10.6, GOLD_3)
    c.circle(cx + 0.4, cy + 0.4, 10.4, GOLD_2)
    # Dark tactical radar glass
    c.circle(cx, cy, 8.8, OBS_0)
    c.circle(cx, cy, 8.2, OBS_1)

    # Subtle sweep wedge in upper-right quadrant
    for y in range(6, 17):
        for x in range(15, 25):
            if math.hypot(x - cx, y - cy) <= 7.8:
                ang = math.degrees(math.atan2(y - cy, x - cx))
                if -78 <= ang <= -15:
                    c.px(x, y, RED_0)

    # Concentric radar rings
    for y in range(6, 26):
        for x in range(6, 26):
            d = math.hypot(x - cx, y - cy)
            if 4.7 <= d <= 5.4:
                c.px(x, y, RED_1)

    # Crosshair lines inside
    c.hline(8, 13, 15, RED_1)
    c.hline(18, 23, 15, RED_1)
    c.vline(15, 8, 13, RED_1)
    c.vline(15, 18, 23, RED_1)

    # Bold crimson outer crosshair ticks protruding past brass bezel
    c.fill_rect(14, 1, 17, 6, RED_2)
    c.vline(15, 2, 5, RED_3)
    c.fill_rect(14, 25, 17, 30, RED_2)
    c.vline(15, 26, 29, RED_3)
    c.fill_rect(1, 14, 6, 17, RED_2)
    c.hline(2, 5, 15, RED_3)
    c.fill_rect(25, 14, 30, 17, RED_2)
    c.hline(26, 29, 15, RED_3)

    # Center target dot + glowing battle ping
    c.circle(cx, cy, 2.2, RED_2)
    c.circle(cx, cy, 1.3, RED_3)
    c.px(15, 15, GOLD_4)

    # Active skirmish blip in NE quadrant
    c.circle(19.5, 11.5, 2.0, RED_2)
    c.circle(19.5, 11.5, 1.1, ORG_3)
    c.px(19, 11, GOLD_4)

    c.add_outline(OUTLINE_DARK)
    return c


def make_btn_world_builder() -> Canvas:
    """World Builder: Vibrant pixel-art fantasy globe with continents, coasts, snow caps & compass glint."""
    c = Canvas(32, 32)
    cx, cy = 15.5, 15.5
    r = 12.8

    # Ocean sphere with rich 3D top-left lighting
    for y in range(32):
        for x in range(32):
            dx = x - cx
            dy = y - cy
            d = math.hypot(dx, dy)
            if d <= r:
                if d > r - 1.6 and (dx + dy) > 2:
                    c.px(x, y, BLU_0)
                elif math.hypot(dx + 4.0, dy + 4.0) < 7.0:
                    c.px(x, y, BLU_3)
                else:
                    c.px(x, y, BLU_2)
                if dx > 6 or dy > 7:
                    c.px(x, y, BLU_1)

    # Organic fantasy continents wrapping cleanly around the globe
    land_shapes = [
        # Main West/North-West emerald continent
        (9.8, 12.0, 5.2, 5.6),
        (12.2, 17.0, 3.6, 4.2),
        # East desert-and-forest continent
        (21.5, 13.5, 4.8, 4.2),
        (20.0, 20.5, 4.2, 3.8),
        # Small southern island
        (12.0, 24.5, 2.8, 1.8),
    ]
    for lx, ly, rx, ry in land_shapes:
        for y in range(3, 29):
            for x in range(3, 29):
                if math.hypot(x - cx, y - cy) <= r - 0.5:
                    val = ((x - lx) / rx) ** 2 + ((y - ly) / ry) ** 2
                    if val <= 1.0:
                        if val > 0.78:
                            c.px(x, y, PAR_2)  # sandy beach coast
                        elif x + y < 27:
                            c.px(x, y, GRN_3)  # sunlit plains
                        elif x + y > 36:
                            c.px(x, y, GRN_1)  # deep forest
                        else:
                            c.px(x, y, GRN_2)

    # Tiny mountain peaks on continents
    for mx, my in [(9, 12), (21, 13), (20, 20)]:
        c.px(mx, my, BRN_2)
        c.px(mx - 1, my + 1, BRN_1)
        c.px(mx + 1, my + 1, BRN_1)
        c.px(mx, my - 1, STN_4)

    # Crisp snowy north & south polar ice caps
    for y in range(3, 7):
        for x in range(9, 23):
            if math.hypot(x - cx, y - cy) <= r - 0.4 and y <= 5 + (1 if abs(x - cx) < 3.5 else 0):
                c.px(x, y, STL_4 if y <= 4 else STL_3)
    for y in range(26, 29):
        for x in range(11, 21):
            if math.hypot(x - cx, y - cy) <= r - 0.4:
                c.px(x, y, STL_3)

    # Subtle ocean shimmer & bright golden sparkle in top-right sky
    c.px(8, 8, BLU_4)
    c.px(9, 8, BLU_4)
    c.px(8, 9, BLU_4)

    c.add_outline(OUTLINE_DARK)
    return c


def make_btn_portal() -> Canvas:
    """LinkedContinents: Glowing violet-cyan swirling portal vortex in rune-carved stone arch."""
    c = Canvas(32, 32)
    cx, cy = 15.5, 16.5

    # Swirling portal energy core
    for y in range(7, 28):
        for x in range(7, 25):
            nx = (x - cx) / 7.8
            ny = (y - cy) / 10.0
            d = math.hypot(nx, ny)
            if d <= 1.0:
                ang = math.atan2(ny, nx)
                swirl = math.sin(ang * 3.0 + d * 7.5)
                if d < 0.25:
                    c.px(x, y, BLU_4)
                elif d < 0.50:
                    c.px(x, y, BLU_3 if swirl > -0.2 else VIO_3)
                elif d < 0.78:
                    c.px(x, y, VIO_2 if swirl > 0 else VIO_3)
                else:
                    c.px(x, y, VIO_1 if swirl > -0.1 else VIO_0)

    # Carved ancient stone arch frame
    for y in range(3, 29):
        for x in range(3, 29):
            nx = (x - cx) / 11.4
            ny = (y - 16.5) / 12.8
            d = math.hypot(nx, ny)
            in_arch = (0.72 <= d <= 1.05 and y <= 17) or (
                y > 17 and (5 <= x <= 9 or 22 <= x <= 26)
            )
            if in_arch:
                if x < 14 and y < 14:
                    c.px(x, y, STN_3)
                elif x > 22 or y > 24:
                    c.px(x, y, STN_0)
                else:
                    c.px(x, y, STN_2 if (x + y) % 3 else STN_1)

    # Keystone at top center & stone base plinth
    c.fill_rect(13, 2, 18, 7, STN_3)
    c.fill_rect(14, 3, 17, 6, STN_2)
    c.px(15, 4, BLU_3)
    c.px(16, 4, BLU_4)
    c.fill_rect(3, 27, 28, 29, STN_1)
    c.hline(4, 27, 27, STN_3)

    # Glowing cyan runes along the pillars
    for ry in (12, 17, 22):
        c.px(7, ry, BLU_3)
        c.px(7, ry + 1, BLU_4)
        c.px(24, ry, BLU_3)
        c.px(24, ry + 1, BLU_4)

    c.add_outline(OUTLINE_DARK)
    return c


def make_btn_apex() -> Canvas:
    """ApexFactions: Gleaming obsidian-and-gold Warden kite shield with winged helm/crown crest."""
    c = Canvas(32, 32)
    cx = 15.5

    # Shield silhouette: broad curved top, sweeping taper to sharp bottom point
    for y in range(4, 29):
        if y <= 13:
            half_w = 10.5 - max(0, (6 - y) * 0.6)
        else:
            t = (y - 13) / 15.5
            half_w = 10.5 * (1.0 - t * t * 0.96)
        for x in range(3, 29):
            dist = abs(x - cx)
            if dist <= half_w:
                # Outer golden rim (2px)
                if dist >= half_w - 2.1 or y <= 6 or y >= 27:
                    if x < 16 and y < 16:
                        c.px(x, y, GOLD_3)
                    elif x > 18 or y > 22:
                        c.px(x, y, GOLD_1)
                    else:
                        c.px(x, y, GOLD_2)
                else:
                    # Faceted obsidian center field
                    if x < 15:
                        c.px(x, y, OBS_2 if y < 17 else OBS_1)
                    else:
                        c.px(x, y, OBS_1 if y < 17 else OBS_0)

    # Top-left golden specular glint
    c.hline(6, 12, 5, GOLD_4)
    c.vline(6, 6, 12, GOLD_4)

    # Crisp Winged Sentinel Helm Crest embossed in gold on the shield
    crest = [
        ".....g..w..g...",
        ".....gyywygg...",
        ".gg..dlhhld..gg",
        "gyyg.dssssd.gyg",
        "gylg.KwwwwK.glg",
        ".gddg.gwwg.gddg",
        "..gddggwwggddg.",
        "...gdggwwggdg..",
        ".....gwwwwg....",
        "......gyyg.....",
        ".......gg......",
    ]
    c.stamp(crest, WARDEN_PAL, ox=8, oy=9)
    # Ruby stud at top center
    c.fill_rect(15, 5, 16, 6, RED_2)
    c.px(15, 5, RED_4)

    c.add_outline(OUTLINE_DARK)
    return c


def make_btn_chronicle() -> Canvas:
    """EraChronicle: Open leather-bound golden chronicle ledger with white-gold feather quill."""
    c = Canvas(32, 32)

    # Leather cover base
    c.poly([(3, 10), (15, 12), (27, 10), (28, 25), (15, 27), (2, 25)], BRN_1)
    c.poly([(4, 11), (15, 13), (26, 11), (27, 24), (15, 26), (3, 24)], BRN_2)
    # Gold corner clasps
    for gx, gy in [(3, 11), (26, 11), (3, 24), (26, 24)]:
        c.fill_rect(gx, gy, gx + 1, gy + 1, GOLD_3)

    # Left & Right cream parchment pages (curved open book)
    c.poly([(4, 9), (15, 11), (15, 23), (4, 22)], PAR_3)
    c.poly([(16, 11), (26, 9), (26, 22), (16, 23)], PAR_2)
    # Spine shadow crease
    c.vline(15, 11, 24, PAR_0)
    c.vline(14, 11, 23, PAR_1)
    # Top bright page edges
    c.line(5, 9, 14, 11, PAR_4)
    c.line(16, 11, 25, 9, PAR_4)

    # Crisp ink text lines on both pages
    for ly in (13, 16, 19):
        c.hline(6, 12, ly, PAR_0)
        c.hline(18, 23, ly, PAR_0)
    # Illuminated red initial on left page
    c.fill_rect(6, 12, 7, 13, RED_2)

    # Crimson bookmark ribbon hanging below spine
    c.fill_rect(14, 24, 16, 29, RED_2)
    c.vline(14, 24, 28, RED_3)
    c.px(15, 29, TRANSPARENT)

    c.add_outline(OUTLINE_WARM)

    # Sweeping feather quill resting diagonally across top-right
    quill = Canvas(32, 32)
    quill.poly([(19, 17), (23, 8), (29, 3), (28, 10), (23, 16)], PAR_3)
    quill.poly([(20, 15), (24, 8), (28, 4), (25, 11)], PAR_4)
    # Feather notches & golden shaft
    quill.line(17, 20, 28, 4, GOLD_2)
    quill.line(18, 19, 27, 5, GOLD_3)
    quill.px(16, 21, OBS_0)  # ink-dipped nib
    quill.px(17, 20, STL_3)
    quill.add_outline(OUTLINE_WARM)
    c.blit(quill)
    return c


def make_btn_relics() -> Canvas:
    """WorldRelics: Ancient mossy stone temple arch framing a floating glowing cyan-gold rune crystal."""
    c = Canvas(32, 32)

    # Stone base plinth
    c.fill_rect(3, 25, 28, 29, STN_1)
    c.fill_rect(4, 25, 27, 27, STN_2)
    c.hline(4, 27, 25, STN_3)

    # Left & Right weathered pillars
    c.fill_rect(5, 9, 10, 25, STN_1)
    c.fill_rect(6, 9, 9, 24, STN_2)
    c.vline(6, 10, 24, STN_3)

    c.fill_rect(21, 9, 26, 25, STN_0)
    c.fill_rect(21, 9, 25, 24, STN_1)
    c.vline(22, 10, 24, STN_2)

    # Stone arch lintel / pediment
    c.fill_rect(4, 5, 27, 10, STN_2)
    c.fill_rect(6, 3, 25, 6, STN_3)
    c.hline(7, 24, 3, STN_4)
    # Arch cutout chamfered stone corners
    c.fill_rect(10, 10, 12, 11, STN_0)
    c.fill_rect(19, 10, 21, 11, STN_0)
    c.px(10, 12, STN_1)
    c.px(21, 12, STN_0)

    # Lush emerald moss & hanging vines draped across left top & pillar
    moss_pts = [
        (5, 4), (6, 4), (7, 4), (8, 4), (9, 4), (10, 4), (11, 5), (12, 5),
        (5, 5), (6, 5), (7, 5), (8, 5), (9, 6), (10, 6), (5, 6), (6, 6), (7, 6),
        (5, 7), (6, 7), (7, 8), (6, 9), (6, 10), (7, 11), (6, 12), (6, 13),
        (22, 4), (23, 4), (24, 4), (25, 5), (24, 5), (25, 6), (26, 7), (25, 8),
        (4, 25), (5, 25), (6, 26), (7, 26), (24, 25), (25, 25), (26, 26),
    ]
    for mx, my in moss_pts:
        c.px(mx, my, GRN_3 if (mx + my) % 2 == 0 else GRN_2)
    for mx, my in [(6, 5), (8, 5), (23, 4), (5, 25)]:
        c.px(mx, my, GRN_4)

    # Floating glowing cyan-and-gold rune crystal in center of archway
    gem = Canvas(32, 32)
    gem.poly([(15.5, 11.5), (20.0, 17.5), (15.5, 23.5), (11.0, 17.5)], BLU_2)
    gem.poly([(15.5, 11.5), (15.5, 17.5), (11.0, 17.5)], BLU_4)
    gem.poly([(15.5, 11.5), (20.0, 17.5), (15.5, 17.5)], BLU_3)
    gem.poly([(15.5, 17.5), (20.0, 17.5), (15.5, 23.5)], BLU_1)
    # Golden rune core sparkle + tiny magic motes
    gem.px(15, 17, GOLD_4)
    gem.px(16, 17, GOLD_3)
    gem.px(15, 16, GOLD_4)
    gem.add_outline(OUTLINE_DARK)
    gem.px(11, 13, GOLD_4)
    gem.px(20, 22, BLU_4)

    c.add_outline(OUTLINE_WARM)
    c.blit(gem)
    return c


def make_btn_coop() -> Canvas:
    """Co-Op: Twin crisp royal crowns (gold + sapphire-silver) linked by an emerald alliance ribbon."""
    c = Canvas(32, 32)

    def draw_crown(ox: int, oy: int, c0: Color, c1: Color, c2: Color, c3: Color, gem: Color, gem_hi: Color) -> Canvas:
        cr = Canvas(32, 32)
        rows = [
            "......w......",
            ".....wyw.....",
            ".w...gyg...g.",
            ".gy..gyg..yg.",
            ".gyg.gyg.gyg.",
            ".gygggygggyg.",
            ".ggggggggggg.",
            ".yyyyyyyyyyy.",
            ".gggagRgaggg.",
            ".bbbbbbbbbbb.",
        ]
        pal = {
            ".": TRANSPARENT,
            "b": c0,
            "a": c1,
            "g": c1,
            "y": c2,
            "w": c3,
            "R": gem,
        }
        cr.stamp(rows, pal, ox=ox, oy=oy)
        cr.px(ox + 6, oy + 8, gem_hi)
        cr.add_outline(OUTLINE_DARK)
        return cr

    # Sweeping emerald alliance banner ribbon with swallowtail ends
    c.fill_rect(5, 20, 26, 25, GRN_1)
    c.fill_rect(6, 21, 25, 24, GRN_2)
    c.hline(6, 25, 21, GRN_3)
    # Swallowtail ribbon tails left & right
    c.poly([(2, 22), (6, 21), (6, 26), (2, 27), (4, 24.5)], GRN_1)
    c.poly([(29, 22), (25, 21), (25, 26), (29, 27), (27, 24.5)], GRN_1)
    c.hline(3, 5, 23, GRN_2)
    c.hline(26, 28, 23, GRN_2)
    # Center golden alliance medallion clasp
    c.circle(15.5, 23.0, 3.2, GOLD_1)
    c.circle(15.3, 22.8, 2.3, GOLD_3)
    c.px(15, 22, GOLD_4)
    c.add_outline(OUTLINE_DARK)

    # Left Gold Crown (ruby gem) + Right Sapphire-Silver Crown (sapphire gem)
    c_left = draw_crown(2, 6, GOLD_0, GOLD_1, GOLD_3, GOLD_4, RED_2, RED_4)
    c_right = draw_crown(17, 8, BLU_0, STL_2, STL_3, STL_4, BLU_2, BLU_4)
    c.blit(c_left)
    c.blit(c_right)
    return c


def make_btn_modern() -> Canvas:
    """ModernAge: Sleek supersonic steel fighter jet banking diagonally with cyan canopy & afterburner."""
    c = Canvas(32, 32)

    # Afterburner flame plume at bottom-left
    c.poly([(3, 28), (7, 21), (11, 25)], ORG_1)
    c.poly([(5, 26), (8, 22), (10, 24)], GOLD_3)
    c.px(8, 23, GOLD_4)

    # Swept delta wings
    c.poly([(7, 17), (19, 13), (12, 21), (4, 15)], STL_2)
    c.poly([(14, 19), (18, 12), (20, 27), (14, 24)], STL_1)
    # Highlight on upper-left wing leading edge
    c.line(5, 15, 18, 12, STL_3)
    # Tail stabilizers
    c.poly([(6, 21), (10, 19), (8, 24)], STL_1)
    c.poly([(10, 24), (12, 21), (14, 26)], STL_0)

    # Fuselage spine (bottom-left to top-right nose cone)
    c.poly([(8, 23), (27, 4), (28, 5), (10, 24)], STL_3)
    c.poly([(9, 24), (27, 5), (26, 8), (11, 25)], STL_2)
    c.line(10, 21, 27, 4, STL_4)

    # Crimson squadron wing stripes
    c.line(9, 16, 13, 15, RED_2)
    c.line(16, 20, 17, 24, RED_2)

    # Cyan bubble cockpit canopy
    c.poly([(17, 12), (22, 8), (23, 10), (19, 14)], BLU_2)
    c.line(18, 12, 21, 9, BLU_4)
    c.px(20, 11, BLU_3)

    c.add_outline(OUTLINE_DARK)
    return c


# =============================================================================
# 3. NEW PHASE 1 POWER & TOGGLE ICONS (32x32 transparent RGBA)
# =============================================================================

def make_btn_warden_drop() -> Canvas:
    """Drop Wardens brush: Diagonal golden-steel halberd behind crisp obsidian-and-gold Sentinel helm."""
    c = Canvas(32, 32)

    # Diagonal golden halberd shaft (bottom-left to top-right)
    for i in range(25):
        x = 3 + i
        y = 28 - i
        c.px(x, y, GOLD_1)
        c.px(x, y - 1, GOLD_3)
    # Razor-sharp spear tip + curved crescent steel-and-gold axe blade
    c.poly([(22, 6), (29, 2), (27, 9)], STL_3)
    c.line(23, 6, 29, 2, STL_4)
    c.poly([(20, 8), (27, 9), (28, 16), (22, 13)], STL_2)
    c.line(26, 9, 28, 15, STL_4)
    c.line(21, 9, 25, 13, GOLD_3)
    c.add_outline(OUTLINE_DARK)

    # Crisp obsidian-and-gold armored Sentinel helm in center foreground
    helm = Canvas(32, 32)
    rows = [
        ".......gg.......",
        "..g...gyyg...g..",
        ".gyg.dlhhld.gyg.",
        ".gdlglhllhlgldg.",
        "..gdldssssdldg..",
        "..gddssggssddg..",
        "..gddKKwwKKddg..",
        "..gddKwwwwKddg..",
        "..gddKwywwKddg..",
        "...gddKwwKddg...",
        "...gdldwwdldg...",
        "....gddwwddg....",
        "....gddssddg....",
        "...gywggggwyg...",
        "....bbggggbb....",
    ]
    helm.stamp(rows, WARDEN_PAL, ox=6, oy=10)
    helm.add_outline(OUTLINE_DARK)
    c.blit(helm)
    return c


def make_btn_animal_kingdoms() -> Canvas:
    """Animal Kingdoms: Crowned chestnut-and-amber wolf/bear paw emblem."""
    c = Canvas(32, 32)

    # Large central paw pad (contoured heart/trapezoid beast pad)
    c.ellipse(15.5, 22.8, 6.2, 4.6, BRN_1)
    c.ellipse(15.2, 22.2, 5.2, 3.7, BRN_2)
    c.ellipse(14.5, 21.3, 3.4, 2.2, BRN_3)
    c.px(13, 20, BRN_4)
    c.px(14, 20, BRN_4)

    # 4 Toe pads fanning cleanly around top of paw
    toes = [
        (7.5, 17.5, 2.6, 3.2),
        (12.2, 14.3, 2.6, 3.3),
        (18.8, 14.3, 2.6, 3.3),
        (23.5, 17.5, 2.6, 3.2),
    ]
    for tx, ty, rx, ry in toes:
        c.ellipse(tx, ty, rx, ry, BRN_1)
        c.ellipse(tx - 0.3, ty - 0.4, rx - 0.6, ry - 0.7, BRN_2)
        c.ellipse(tx - 0.6, ty - 0.9, rx - 1.3, ry - 1.6, BRN_3)
        # Small sharp ivory claw above each toe
        cx_i = int(round(tx))
        cy_i = int(round(ty - ry - 0.8))
        c.px(cx_i, cy_i, PAR_3)
        c.px(cx_i, cy_i - 1, PAR_4)

    c.add_outline(OUTLINE_WARM)

    # Sparkling 3-point golden royal crown perched cleanly on top
    crown = Canvas(32, 32)
    crown.fill_rect(9, 8, 22, 11, GOLD_1)
    crown.hline(9, 22, 8, GOLD_3)
    crown.hline(9, 22, 11, GOLD_0)
    crown.poly([(9, 8), (8, 3), (13, 8)], GOLD_2)
    crown.poly([(12, 8), (15.5, 1.5), (19, 8)], GOLD_3)
    crown.poly([(18, 8), (23, 3), (22, 8)], GOLD_1)
    crown.px(8, 3, GOLD_4)
    crown.px(15, 2, GOLD_4)
    crown.px(16, 2, GOLD_4)
    crown.px(23, 3, GOLD_3)
    # Ruby jewel center
    crown.fill_rect(15, 9, 16, 10, RED_2)
    crown.px(15, 9, RED_4)
    crown.add_outline(OUTLINE_WARM)

    c.blit(crown)
    return c


def make_btn_snake_mode() -> Canvas:
    """Snake Mode: Coiled emerald serpent with lighter jade belly scales, forked red tongue & golden eye."""
    c = Canvas(32, 32)

    # Coiled body path from tail (bottom-left) -> wide bottom coil -> mid S-bend -> raised neck
    def draw_coil_seg(cx: float, cy: float, r: float) -> None:
        c.circle(cx, cy, r, GRN_1)
        c.circle(cx - 0.5, cy - 0.6, r * 0.78, GRN_2)
        c.circle(cx - 0.9, cy - 1.0, r * 0.42, GRN_3)

    # Lower coil (back to front)
    pts_coil = [
        # Tail tip curving bottom-right
        (25.5, 25.5, 1.4), (24.0, 26.2, 1.8), (21.5, 26.8, 2.3),
        (18.0, 27.0, 2.7), (14.0, 27.0, 2.9), (10.0, 26.5, 2.9),
        (7.2, 24.8, 2.8), (6.5, 22.0, 2.8), (8.8, 20.0, 2.8),
        (13.0, 19.6, 2.8), (17.8, 19.6, 2.8), (21.8, 18.6, 2.7),
        (23.2, 16.0, 2.6), (21.0, 13.8, 2.6), (16.5, 13.4, 2.6),
        (12.2, 12.6, 2.6), (10.5, 10.0, 2.6), (12.2, 7.6, 2.7),
        (15.5, 7.2, 2.8),
    ]
    for px_f, py_f, rad in pts_coil:
        draw_coil_seg(px_f, py_f, rad)

    # Jade-yellow ribbed belly accent along bottom edges of coils
    for bx in range(9, 22, 2):
        c.px(bx, 28, GRN_4)
        c.px(bx + 1, 28, GOLD_3)
    for bx in range(10, 21, 2):
        c.px(bx, 21, GRN_4)
    # Dark dorsal scale markings for crisp texture
    for dx, dy in [(11, 25), (15, 25), (19, 25), (12, 18), (16, 18), (20, 17), (15, 12)]:
        c.px(dx, dy, GRN_0)
        c.px(dx + 1, dy, GRN_0)

    # Crisp 1px dark coil-separation creases so S-coils read cleanly at 1x scale
    c.hline(10, 20, 23, OUTLINE_DARK)
    c.hline(11, 20, 16, OUTLINE_DARK)

    # Striking viper head at top-right (facing right)
    c.ellipse(19.0, 7.2, 4.6, 3.2, GRN_1)
    c.ellipse(18.5, 6.6, 3.8, 2.4, GRN_2)
    c.ellipse(17.8, 5.8, 2.3, 1.3, GRN_3)
    # Fierce gleaming golden slit eye
    c.fill_rect(19, 5, 21, 7, GOLD_3)
    c.px(20, 5, GOLD_4)
    c.px(20, 6, OUTLINE_DARK)
    # Nostril
    c.px(22, 6, GRN_0)
    # Forked crimson tongue darting out right
    c.hline(24, 27, 8, RED_2)
    c.px(28, 7, RED_3)
    c.px(28, 9, RED_3)
    c.px(29, 7, RED_2)
    c.px(29, 9, RED_2)

    c.add_outline(OUTLINE_DARK)
    return c


def make_btn_always_day() -> Canvas:
    """Always Day: Radiant golden sun with alternating bold long & short solar rays."""
    c = Canvas(32, 32)
    cx, cy = 15.5, 15.5

    # 8 bold long triangular rays + 8 short intermediate rays
    for i in range(16):
        ang = math.radians(i * 22.5)
        if i % 2 == 0:
            a1 = math.radians(i * 22.5 - 9)
            a2 = math.radians(i * 22.5 + 9)
            pts = [
                (cx + math.cos(a1) * 7.2, cy + math.sin(a1) * 7.2),
                (cx + math.cos(ang) * 14.0, cy + math.sin(ang) * 14.0),
                (cx + math.cos(a2) * 7.2, cy + math.sin(a2) * 7.2),
            ]
            c.poly(pts, ORG_2)
            # Inner bright golden ray spine
            c.line(
                int(round(cx + math.cos(ang) * 7.5)),
                int(round(cy + math.sin(ang) * 7.5)),
                int(round(cx + math.cos(ang) * 13.0)),
                int(round(cy + math.sin(ang) * 13.0)),
                GOLD_3,
            )
        else:
            x1 = int(round(cx + math.cos(ang) * 8.2))
            y1 = int(round(cy + math.sin(ang) * 8.2))
            x2 = int(round(cx + math.cos(ang) * 11.2))
            y2 = int(round(cy + math.sin(ang) * 11.2))
            c.line(x1, y1, x2, y2, GOLD_3)
            c.px(x2, y2, GOLD_4)

    # Layered glowing sun disk
    c.circle(cx, cy, 8.2, ORG_2)
    c.circle(cx - 0.3, cy - 0.3, 7.2, GOLD_2)
    c.circle(cx - 0.6, cy - 0.6, 6.0, GOLD_3)
    c.circle(cx - 1.5, cy - 1.5, 3.6, GOLD_4)

    c.add_outline(OUTLINE_WARM)
    return c


def make_btn_age_hope() -> Canvas:
    """Lock Age of Hope: Golden sunrise over lush emerald hills + bright golden padlock badge."""
    c = Canvas(32, 32)
    cx, cy = 15.5, 15.5
    r = 12.2

    # Circular medallion sky + radiant golden sunrise + emerald rolling hills
    for y in range(3, 29):
        for x in range(3, 29):
            if math.hypot(x - cx, y - cy) <= r:
                # Sky gradient
                if y < 12:
                    c.px(x, y, BLU_2)
                elif y < 17:
                    c.px(x, y, BLU_3)
                else:
                    c.px(x, y, GOLD_4)

                # Rising golden sun behind hills
                dsun = math.hypot(x - 14.0, y - 18.0)
                if dsun <= 6.8:
                    c.px(x, y, GOLD_3 if dsun > 3.6 else GOLD_4)

                # Rolling emerald hills (back right hill + front left hill)
                hill_back = 18.0 + ((x - 21.0) ** 2) * 0.045
                hill_front = 19.2 + ((x - 10.5) ** 2) * 0.040
                if y >= hill_back:
                    c.px(x, y, GRN_2 if y < hill_back + 2.5 else GRN_1)
                if y >= hill_front:
                    if y < hill_front + 2.0:
                        c.px(x, y, GRN_3)
                    elif y < hill_front + 5.5:
                        c.px(x, y, GRN_2)
                    else:
                        c.px(x, y, GRN_1)

    # Golden halo rim around the medallion
    for y in range(2, 30):
        for x in range(2, 30):
            d = math.hypot(x - cx, y - cy)
            if 11.4 <= d <= 13.0:
                c.px(x, y, GOLD_3 if (x + y) < 31 else GOLD_2)

    c.add_outline(OUTLINE_WARM)

    # Crisp golden padlock badge overlaid cleanly at bottom-right
    lock = Canvas(32, 32)
    # Shackle arc
    lock.fill_rect(20, 15, 26, 20, GOLD_3)
    lock.fill_rect(22, 17, 24, 20, TRANSPARENT)
    lock.px(20, 15, TRANSPARENT)
    lock.px(26, 15, TRANSPARENT)
    # Lock body
    lock.fill_rect(18, 20, 28, 28, GOLD_1)
    lock.fill_rect(19, 21, 27, 27, GOLD_2)
    lock.hline(19, 27, 21, GOLD_4)
    lock.vline(19, 21, 27, GOLD_3)
    # Keyhole
    lock.px(23, 23, OUTLINE_WARM)
    lock.px(23, 24, OUTLINE_WARM)
    lock.px(23, 25, OUTLINE_WARM)
    lock.add_outline(OUTLINE_WARM)

    c.blit(lock)
    return c


def make_btn_war_ticker() -> Canvas:
    """War Ticker: Unfurled parchment ticker scroll with crossed crimson battle pennants."""
    c = Canvas(32, 32)

    # Left & Right crossed golden banner lances
    c.line(4, 28, 24, 3, GOLD_1)
    c.line(5, 28, 25, 3, GOLD_3)
    c.line(27, 28, 7, 3, GOLD_1)
    c.line(26, 28, 6, 3, GOLD_3)

    # Left crimson battle pennant fluttering outward-left from left-top spear
    c.poly([(7, 4), (16, 4), (14, 8), (16, 12), (8, 12)], RED_2)
    c.hline(8, 15, 5, RED_3)
    c.hline(8, 14, 11, GOLD_2)

    # Right crimson battle pennant fluttering outward-right from right-top spear
    c.poly([(24, 4), (15, 4), (17, 8), (15, 12), (23, 12)], RED_1)
    c.hline(17, 23, 5, RED_2)
    c.hline(17, 23, 11, GOLD_2)

    # Spear spearhead finials
    c.fill_rect(5, 2, 7, 4, GOLD_3)
    c.px(6, 2, GOLD_4)
    c.fill_rect(24, 2, 26, 4, GOLD_3)
    c.px(25, 2, GOLD_4)

    c.add_outline(OUTLINE_WARM)

    # Bold unfurled horizontal war-ticker scroll across foreground
    scroll = Canvas(32, 32)
    scroll.fill_rect(4, 13, 27, 24, PAR_1)
    scroll.fill_rect(5, 14, 26, 23, PAR_2)
    scroll.fill_rect(6, 15, 25, 22, PAR_3)
    # Side scroll rollers
    scroll.fill_rect(2, 12, 5, 25, GOLD_1)
    scroll.vline(3, 13, 24, GOLD_3)
    scroll.fill_rect(26, 12, 29, 25, GOLD_1)
    scroll.vline(27, 13, 24, GOLD_3)
    # Live war ticker header bar (crimson vs sapphire factions + ink report lines)
    scroll.fill_rect(8, 16, 14, 17, RED_2)
    scroll.fill_rect(17, 16, 23, 17, BLU_2)
    scroll.px(15, 16, GOLD_3)
    scroll.px(16, 17, GOLD_3)
    scroll.hline(8, 22, 19, PAR_0)
    scroll.hline(8, 23, 21, PAR_0)
    scroll.add_outline(OUTLINE_WARM)

    c.blit(scroll)
    return c


def make_btn_species_panel() -> Canvas:
    """Species Bestiary Panel: Leather-bound bestiary book with brass clasps & golden crowned paw."""
    c = Canvas(32, 32)

    # Cream page block peeking along right & bottom edge (3/4 upright book perspective)
    c.fill_rect(8, 4, 26, 28, PAR_1)
    c.fill_rect(9, 5, 25, 27, PAR_3)
    for py in range(7, 26, 2):
        c.hline(23, 25, py, PAR_1)

    # Rich leather front cover & spine
    c.fill_rect(5, 3, 23, 26, BRN_1)
    c.fill_rect(8, 4, 22, 25, BRN_2)
    # Spine shadow + ribbed binding bands
    c.fill_rect(5, 3, 7, 26, BRN_0)
    for sy in (7, 14, 21):
        c.hline(5, 7, sy, GOLD_2)
        c.px(6, sy, GOLD_3)

    # Brass corner brackets on cover
    for bx, by in [(20, 4), (20, 23), (8, 4), (8, 23)]:
        c.fill_rect(bx, by, bx + 2, by + 2, GOLD_2)
        c.px(bx, by, GOLD_4)

    # Golden Crowned Paw emblem embossed cleanly on cover center
    # Mini royal crown with crimson jewel
    c.hline(12, 18, 10, GOLD_3)
    c.hline(12, 18, 11, GOLD_1)
    c.px(12, 9, GOLD_3)
    c.px(15, 8, GOLD_4)
    c.px(15, 9, GOLD_3)
    c.px(18, 9, GOLD_3)
    c.px(15, 10, RED_2)
    # 4 crisp golden paw toes
    for tx, ty in [(10, 15), (13, 13), (16, 13), (19, 15)]:
        c.fill_rect(tx, ty, tx + 1, ty + 1, GOLD_3)
        c.px(tx, ty, GOLD_4)
    # Golden main paw pad
    c.fill_rect(12, 17, 18, 20, GOLD_2)
    c.fill_rect(13, 17, 17, 19, GOLD_3)
    c.px(14, 17, GOLD_4)
    c.px(12, 17, BRN_2)
    c.px(18, 17, BRN_2)

    c.add_outline(OUTLINE_WARM)
    return c


def make_btn_dense_cities() -> Canvas:
    """Dense Cities: Tightly clustered medieval stone houses, terracotta/slate roofs, glowing windows & central keep."""
    c = Canvas(32, 32)

    # Central tall stone keep & slate spire rising in the center-back (x=11..20, y=2..19)
    # Golden spire finial & tiny crimson pennant
    c.vline(15, 2, 4, GOLD_3)
    c.px(15, 2, GOLD_4)
    c.hline(16, 18, 3, RED_2)
    c.px(16, 3, RED_3)
    # Steep slate-blue keep spire roof
    c.poly([(15.5, 4.0), (20.5, 10.0), (10.5, 10.0)], BLU_1)
    c.poly([(15.5, 4.0), (15.5, 10.0), (10.5, 10.0)], BLU_2)
    c.line(15, 4, 11, 9, BLU_3)
    c.hline(10, 21, 10, OUTLINE_WARM)

    # Crenellated stone battlements & keep tower body
    c.fill_rect(11, 10, 20, 19, STN_2)
    c.fill_rect(11, 11, 15, 19, STN_3)
    c.fill_rect(17, 11, 20, 19, STN_1)
    c.hline(11, 20, 10, STN_0)
    # Merlons (battlement teeth)
    for mx in (11, 14, 17, 20):
        c.px(mx, 9, STN_3 if mx < 16 else STN_2)
    # Keep arched glowing windows
    c.fill_rect(14, 12, 15, 14, GOLD_3)
    c.px(14, 12, GOLD_4)
    c.fill_rect(17, 12, 18, 14, GOLD_2)
    c.px(17, 12, GOLD_3)

    # Back-left multi-story slate-roof townhouse (x=4..11, y=10..24)
    c.poly([(7.5, 9.5), (11.8, 14.2), (3.2, 14.2)], OBS_2)
    c.poly([(7.5, 9.5), (7.5, 14.2), (3.2, 14.2)], OBS_3)
    c.line(7, 10, 4, 14, STL_3)
    c.fill_rect(4, 14, 10, 24, PAR_2)
    c.fill_rect(4, 14, 7, 24, PAR_3)
    c.hline(4, 10, 14, OUTLINE_WARM)
    c.hline(4, 10, 15, PAR_1)
    c.vline(10, 14, 24, STN_0)
    # Glowing stacked townhouse windows
    c.fill_rect(5, 16, 6, 17, GOLD_3)
    c.px(5, 16, GOLD_4)
    c.fill_rect(8, 16, 9, 17, GOLD_2)

    # Back-right multi-story terracotta-roof townhouse (x=20..27, y=9..24)
    # Little chimney & smoke wisp
    c.fill_rect(25, 8, 26, 11, STN_1)
    c.px(25, 8, STN_3)
    c.px(26, 6, STL_3)
    c.px(27, 5, STL_4)
    c.poly([(23.5, 9.5), (27.8, 14.2), (19.2, 14.2)], RED_1)
    c.poly([(23.5, 9.5), (23.5, 14.2), (19.2, 14.2)], RED_2)
    c.line(23, 10, 20, 14, ORG_3)
    c.fill_rect(21, 14, 27, 24, STN_1)
    c.fill_rect(21, 14, 24, 24, STN_2)
    c.hline(21, 27, 14, OUTLINE_WARM)
    c.hline(21, 27, 15, STN_0)
    c.vline(21, 14, 24, STN_0)
    c.fill_rect(22, 16, 23, 17, GOLD_3)
    c.px(22, 16, GOLD_4)
    c.fill_rect(25, 16, 26, 17, GOLD_2)

    # Front-left cozy terracotta gable house (x=2..10, y=17..28)
    c.poly([(6.0, 16.5), (10.5, 21.2), (1.5, 21.2)], ORG_1)
    c.poly([(6.0, 16.5), (6.0, 21.2), (1.5, 21.2)], RED_2)
    c.line(6, 17, 2, 21, ORG_3)
    c.hline(2, 10, 21, OUTLINE_WARM)
    c.fill_rect(3, 21, 10, 28, STN_2)
    c.fill_rect(3, 21, 6, 28, STN_3)
    c.vline(10, 21, 28, OUTLINE_WARM)
    # Warm windows & timber door
    c.fill_rect(4, 22, 5, 23, GOLD_3)
    c.px(4, 22, GOLD_4)
    c.fill_rect(7, 22, 8, 23, GOLD_3)
    c.fill_rect(4, 25, 5, 27, BRN_1)
    c.px(4, 25, BRN_2)
    c.fill_rect(7, 25, 8, 26, GOLD_2)

    # Front-right slate gable house (x=21..29, y=17..28)
    c.poly([(25.0, 16.5), (29.5, 21.2), (20.5, 21.2)], OBS_1)
    c.poly([(25.0, 16.5), (25.0, 21.2), (20.5, 21.2)], OBS_3)
    c.line(25, 17, 21, 21, STL_3)
    c.hline(21, 29, 21, OUTLINE_WARM)
    c.fill_rect(21, 21, 28, 28, STN_1)
    c.fill_rect(21, 21, 25, 28, STN_2)
    c.vline(21, 21, 28, OUTLINE_WARM)
    # Warm glowing multi-story windows
    c.fill_rect(22, 22, 23, 23, GOLD_3)
    c.px(22, 22, GOLD_4)
    c.fill_rect(26, 22, 27, 23, GOLD_2)
    c.fill_rect(22, 25, 23, 26, GOLD_3)
    c.fill_rect(26, 25, 27, 27, BRN_1)

    # Front-center packed terracotta guildhall & arched city gate (x=10..21, y=16..28)
    c.poly([(15.5, 15.8), (21.2, 20.5), (9.8, 20.5)], RED_1)
    c.poly([(15.5, 15.8), (15.5, 20.5), (9.8, 20.5)], ORG_2)
    c.line(15, 16, 11, 20, GOLD_3)
    c.hline(10, 21, 20, OUTLINE_WARM)
    c.fill_rect(11, 20, 20, 28, STN_2)
    c.fill_rect(11, 20, 15, 28, STN_3)
    c.fill_rect(17, 20, 20, 28, STN_1)
    # Glowing upper windows flanking the center
    c.fill_rect(12, 21, 13, 22, GOLD_3)
    c.px(12, 21, GOLD_4)
    c.fill_rect(18, 21, 19, 22, GOLD_3)
    c.px(18, 21, GOLD_4)
    # Arched medieval city gate with warm inner lantern glow
    c.fill_rect(14, 24, 17, 28, OUTLINE_WARM)
    c.fill_rect(15, 23, 16, 28, OBS_0)
    c.fill_rect(15, 25, 16, 27, ORG_2)
    c.px(15, 25, GOLD_4)
    c.px(16, 25, GOLD_3)

    # Compact cobblestone city foundation base
    c.hline(3, 28, 28, STN_0)
    for sx in range(4, 28, 3):
        c.px(sx, 28, STN_2)

    c.add_outline(OUTLINE_WARM)
    return c


def make_btn_pangaea_drift() -> Canvas:
    """Pangaea Drift: Matching green-and-brown island halves pulling apart over opening sea with golden arrows."""
    c = Canvas(32, 32)

    # 1. Opening ocean channel beneath and between the two drifting halves
    # Clean organic sea basin (x=6..25, y=13..29)
    c.ellipse(15.5, 22.0, 10.4, 7.0, BLU_0)
    c.ellipse(15.5, 21.4, 9.6, 6.2, BLU_1)
    c.ellipse(15.5, 20.6, 8.2, 5.2, BLU_2)

    # Clean layered ocean currents & surf crests down the widening central strait
    for y in range(14, 28):
        shift = 1 if (y // 3) % 2 == 0 else 0
        c.hline(13 + shift, 17 + shift, y, BLU_2 if y < 24 else BLU_1)
    # Crisp cyan-white wave lines across the opening channel
    for x0, x1, wy in [(14, 17, 15), (13, 16, 18), (15, 18, 21), (12, 16, 24), (16, 19, 26)]:
        c.hline(x0, x1, wy, BLU_3)
        c.px(x0 + 1, wy, BLU_4)
    c.add_outline(OUTLINE_DARK)

    # 2. Matching puzzle-fit island halves drawn on separate layers so inner coastlines have crisp 1px outlines
    # Left half: jigsaw eastern coast (cape at y=16..18, cove at y=13..15)
    left_land_rows = {
        11: (4, 11),
        12: (3, 12),
        13: (2, 11),   # inward cove matching right point
        14: (2, 10),
        15: (2, 11),
        16: (2, 12),   # protruding cape matching right cove
        17: (2, 13),
        18: (3, 12),
        19: (3, 11),
        20: (4, 11),
        21: (5, 10),
    }
    # Right half: complementary western coast (point at y=13..15, cove at y=16..18)
    right_land_rows = {
        11: (20, 27),
        12: (19, 28),
        13: (19, 29),  # protruding point matching left cove
        14: (18, 29),
        15: (19, 29),
        16: (20, 29),  # inward cove matching left cape
        17: (21, 29),
        18: (20, 28),
        19: (19, 28),
        20: (20, 27),
        21: (21, 26),
    }

    def make_island_half_layer(row_spans: dict[int, tuple[int, int]], is_left: bool) -> Canvas:
        layer = Canvas(32, 32)
        col_bottom: dict[int, int] = {}
        for y, (x0, x1) in row_spans.items():
            for x in range(x0, x1 + 1):
                if x not in col_bottom or y > col_bottom[x]:
                    col_bottom[x] = y

        # 3D rocky brown earth cliff underside (4px thick)
        for x, y_bot in col_bottom.items():
            for dy in range(1, 5):
                cy = y_bot + dy
                if cy > 26:
                    continue
                if dy == 1:
                    col = BRN_3 if (x + cy) % 3 == 0 else BRN_2
                elif dy == 2:
                    col = BRN_2 if is_left else BRN_1
                elif dy == 3:
                    col = BRN_1
                else:
                    col = BRN_0
                layer.px(x, cy, col)

        # Lush emerald grass plateau + warm sandy sheared coast rim
        for y, (x0, x1) in row_spans.items():
            for x in range(x0, x1 + 1):
                is_inner_edge = (x == x1) if is_left else (x == x0)
                if is_inner_edge:
                    layer.px(x, y, PAR_2 if y % 2 == 0 else PAR_1)
                elif y <= 13 and ((x - x0 < 5) if is_left else (x - x0 < 5)):
                    layer.px(x, y, GRN_3)
                elif y >= 19:
                    layer.px(x, y, GRN_1)
                else:
                    layer.px(x, y, GRN_2)

        # Sunlit grassy knolls & tiny dark-green forest tufts
        if is_left:
            layer.fill_rect(4, 13, 7, 15, GRN_3)
            layer.px(5, 13, GRN_4)
            layer.px(8, 16, GRN_1)
            layer.px(9, 17, GRN_0)
            layer.px(9, 16, GRN_1)
            layer.px(6, 18, GRN_1)
        else:
            layer.fill_rect(23, 13, 26, 15, GRN_3)
            layer.px(24, 13, GRN_4)
            layer.px(22, 15, GRN_1)
            layer.px(25, 17, GRN_0)
            layer.px(25, 16, GRN_1)
            layer.px(23, 19, GRN_1)

        layer.add_outline(OUTLINE_WARM)
        return layer

    c.blit(make_island_half_layer(left_land_rows, is_left=True))
    c.blit(make_island_half_layer(right_land_rows, is_left=False))

    # 3. Bold golden outward drift arrows (<-- and -->) on top layer
    arrows = Canvas(32, 32)

    # Left arrow (<--): tip at x=2, head x=2..6, shaft x=6..13, centered at y=6
    arrows.poly([(1.8, 6.0), (6.5, 2.2), (6.5, 9.8)], GOLD_2)
    arrows.poly([(2.8, 6.0), (6.2, 3.2), (6.2, 6.0)], GOLD_3)
    arrows.fill_rect(6, 5, 13, 7, GOLD_2)
    arrows.hline(5, 13, 5, GOLD_3)
    arrows.hline(6, 13, 7, GOLD_1)
    arrows.px(3, 6, GOLD_4)
    arrows.px(4, 5, GOLD_4)
    arrows.px(5, 4, GOLD_4)

    # Right arrow (-->): tip at x=29, head x=25..29, shaft x=18..25, centered at y=6
    arrows.poly([(29.2, 6.0), (24.5, 2.2), (24.5, 9.8)], GOLD_2)
    arrows.poly([(28.2, 6.0), (24.8, 3.2), (24.8, 6.0)], GOLD_3)
    arrows.fill_rect(18, 5, 25, 7, GOLD_2)
    arrows.hline(18, 26, 5, GOLD_3)
    arrows.hline(18, 25, 7, GOLD_1)
    arrows.px(28, 6, GOLD_4)
    arrows.px(27, 5, GOLD_4)
    arrows.px(26, 4, GOLD_4)

    arrows.add_outline(OUTLINE_DARK)
    c.blit(arrows)
    return c


def make_btn_tectonic_rift() -> Canvas:
    """Tectonic Rift: Landmass cracked down the center by a glowing magma fault with blue sea rushing in."""
    c = Canvas(32, 32)

    # Sharp jagged lightning-style earthquake fault spine from top (y=3) to flooded mouth (y=28)
    rift_cx: dict[int, int] = {
        3: 16, 4: 15, 5: 14, 6: 14, 7: 15, 8: 17, 9: 17, 10: 16,
        11: 14, 12: 14, 13: 15, 14: 16, 15: 16, 16: 15, 17: 15,
        18: 16, 19: 16, 20: 15, 21: 15, 22: 15, 23: 15, 24: 15,
        25: 15, 26: 15, 27: 15, 28: 15,
    }
    rift_hw: dict[int, int] = {
        3: 1, 4: 1, 5: 1, 6: 2, 7: 2, 8: 2, 9: 2, 10: 2,
        11: 2, 12: 2, 13: 2, 14: 2, 15: 2, 16: 3, 17: 3,
        18: 3, 19: 3, 20: 3, 21: 4, 22: 4, 23: 5, 24: 5,
        25: 5, 26: 5, 27: 4, 28: 3,
    }

    # 1. Rounded ocean bay base at the bottom so rushing sea silhouette curves organically (no flat box edge)
    c.ellipse(15.5, 24.0, 8.2, 5.2, BLU_0)
    c.ellipse(15.5, 23.4, 7.4, 4.6, BLU_1)
    c.ellipse(15.5, 22.8, 6.2, 3.8, BLU_2)

    # 2. Build left and right fractured island plates on separate layer with crisp 1px dark fault rim
    land = Canvas(32, 32)
    for y in range(3, 27):
        rcx = rift_cx.get(y, 15)
        hw = rift_hw.get(y, 2)
        for x in range(2, 30):
            if abs(x - rcx) <= hw:
                continue

            is_right = x > rcx
            sy = y - (1 if is_right else 0)
            nx = (x - 15.5) / 13.0
            ny = (sy - 13.8) / 10.4
            if nx * nx + ny * ny <= 1.0:
                if sy >= 20:
                    cliff_d = sy - 20
                    col = BRN_3 if cliff_d == 0 else (BRN_2 if cliff_d == 1 else (BRN_1 if cliff_d == 2 else BRN_0))
                    land.px(x, y, col)
                else:
                    if sy <= 9 and x < 14:
                        col = GRN_3
                    elif sy <= 11 and x > 18:
                        col = GRN_3 if (x + sy) % 2 == 0 else GRN_2
                    elif sy >= 17:
                        col = GRN_1
                    else:
                        col = GRN_2
                    land.px(x, y, col)

    # Shaded mountain peaks flanking the earthquake fault
    for mx, my in [(7, 11), (9, 13), (23, 12), (25, 14)]:
        land.poly([(mx, my - 3), (mx - 3, my + 2), (mx + 3, my + 2)], STN_1)
        land.poly([(mx, my - 3), (mx - 3, my + 2), (mx, my + 2)], STN_2)
        land.px(mx, my - 3, STN_4)
        land.px(mx - 1, my - 2, STN_3)

    # Crisp inner basalt/earth fracture edge right along the fault walls
    for y in range(4, 25):
        rcx = rift_cx[y]
        hw = rift_hw[y]
        for side in (-1, 1):
            wx = rcx + side * (hw + 1)
            if 2 <= wx <= 29 and land.is_solid(wx, y):
                land.px(wx, y, BRN_2 if y > 16 else STN_1)

    # Subtle secondary earthquake cracks branching into grass
    for bx, by in [(10, 9), (9, 10), (21, 10), (22, 11), (10, 17), (9, 18), (21, 18), (22, 19)]:
        if land.is_solid(bx, by):
            land.px(bx, by, OUTLINE_WARM)

    land.add_outline(OUTLINE_DARK)

    # 3. Glowing volcanic magma fissure (upper y=4..16) + rushing sapphire-cyan ocean (lower y=17..28)
    rift = Canvas(32, 32)
    for y in range(4, 28):
        rcx = rift_cx[y]
        hw = rift_hw[y]
        for x in range(rcx - hw, rcx + hw + 1):
            dist = abs(x - rcx)
            if y <= 15:
                # Molten magma core
                if dist == 0:
                    col = GOLD_4 if y in (5, 8, 11, 14) else GOLD_3
                elif dist == 1:
                    col = ORG_3 if (x + y) % 2 == 0 else ORG_2
                else:
                    col = RED_2 if dist == hw else RED_1
                rift.px(x, y, col)
            elif y <= 18:
                # Sizzling magma-meets-sea steam & white-cyan surf clash
                if y == 16 and dist <= 1:
                    col = GOLD_4 if dist == 0 else ORG_3
                elif dist <= 1:
                    col = BLU_4
                elif dist == 2:
                    col = BLU_3 if y >= 17 else ORG_2
                else:
                    col = RED_1 if y == 16 else BLU_2
                rift.px(x, y, col)
            else:
                # Surging blue ocean rushing upward into the cracked chasm
                if y >= 26:
                    col = BLU_1 if abs(x - rcx) >= 3 else BLU_2
                elif dist == hw:
                    col = BLU_3
                elif dist <= 1:
                    col = BLU_2 if y % 2 == 0 else BLU_3
                else:
                    col = BLU_2 if y < 24 else BLU_1
                rift.px(x, y, col)

    # Crisp surging wave crests rushing upward into the rift mouth
    for x0, x1, wy in [(14, 17, 18), (13, 16, 21), (15, 18, 23), (12, 16, 25), (15, 18, 27)]:
        rift.hline(x0, x1, wy, BLU_3)
        rift.px(x0 + 1, wy, BLU_4)

    # Glowing volcanic spark embers popping out of the upper fissure
    rift.px(13, 3, ORG_3)
    rift.px(17, 3, GOLD_3)

    c.blit(land)
    c.blit(rift)
    c.add_outline(OUTLINE_DARK)
    return c


def make_btn_tectonic_speed() -> Canvas:
    """Tectonic Speed: Golden sand hourglass sitting over a cracked stone tectonic plate."""
    c = Canvas(32, 32)

    # 1. Prominent cracked stone tectonic plate base (y=17..29, x=2..29)
    # Left stone slab half (slightly elevated 1px)
    c.poly([(2.0, 21.0), (14.8, 17.5), (14.4, 25.0), (3.0, 26.2)], STN_2)
    c.poly([(2.8, 21.2), (14.2, 18.2), (13.4, 22.8), (4.2, 24.2)], STN_3)
    # Left 3D cliff thickness (y=24..28)
    c.poly([(2.0, 21.0), (3.0, 26.2), (14.4, 25.0), (13.8, 28.6), (3.4, 28.8), (2.0, 24.8)], STN_1)
    c.hline(4, 13, 28, STN_0)

    # Right stone slab half (shifted slightly down-right across the tectonic fault)
    c.poly([(17.2, 18.2), (29.5, 21.5), (28.5, 26.5), (16.8, 25.6)], STN_2)
    c.poly([(17.6, 18.8), (28.5, 21.6), (27.2, 24.4), (17.2, 23.6)], STN_3)
    # Right 3D cliff thickness (y=24..28)
    c.poly([(16.8, 25.6), (28.5, 26.5), (29.5, 21.5), (29.5, 25.2), (28.2, 28.8), (17.2, 28.6)], STN_1)
    c.hline(18, 27, 28, STN_0)

    # Crisp stone highlights & weathered bevel ticks
    for sx, sy in [(5, 21), (8, 20), (11, 20), (21, 20), (25, 21)]:
        c.px(sx, sy, STN_4)
    for sx, sy in [(6, 25), (10, 26), (22, 26), (26, 25)]:
        c.px(sx, sy, STN_0)

    # Bold jagged tectonic fault crack zig-zagging down through the center of the stone plate (y=19..28)
    crack_path = [
        (15, 19), (16, 20), (15, 21), (14, 22), (15, 23),
        (16, 24), (16, 25), (15, 26), (14, 27), (15, 28),
    ]
    for cx, cy in crack_path:
        c.px(cx, cy, OUTLINE_DARK)
        c.px(cx + 1, cy, OBS_0)
        # Warm glowing mantle seam inside the deep stone fracture
        if cy in (23, 24, 26):
            c.px(cx, cy, ORG_2)
        if cy == 25:
            c.px(cx, cy, GOLD_3)
        if cy == 27:
            c.px(cx, cy, RED_2)

    # Branching hairline stone cracks on left & right plate shoulders
    for fx, fy in [(10, 23), (9, 24), (8, 24), (21, 23), (22, 24), (23, 24)]:
        c.px(fx, fy, STN_0)
        c.px(fx, fy - 1, STN_3)

    c.add_outline(OUTLINE_DARK)

    # Soft cast shadow where the hourglass base rests atop the cracked stone plate
    c.hline(10, 21, 21, STN_0)
    c.hline(11, 20, 22, OBS_0)

    # 2. Iconic pinched-waist golden sand hourglass (y=2..21) — unmistakable >< silhouette!
    hg = Canvas(32, 32)

    # Symmetrical pinched crystal glass bulbs (y=5..18)
    bulb_half_w: dict[int, int] = {
        5: 5, 6: 5, 7: 5, 8: 4, 9: 3, 10: 2,
        11: 1,  # crisp pinched waist
        12: 2, 13: 3, 14: 4, 15: 5, 16: 5, 17: 5, 18: 5,
    }
    for y, hw in bulb_half_w.items():
        for x in range(15 - hw, 16 + hw + 1):
            if x == 15 - hw or x == 16 + hw:
                col = BLU_2  # glass contour tint
            elif x == 15 - hw + 1:
                col = BLU_4  # curved bright reflection on left glass wall
            elif x >= 16 + hw - 1:
                col = STL_2
            else:
                col = BLU_3 if y < 11 else STL_3
            hg.px(x, y, col)

    # Upper bulb golden sand funnel (y=7..10)
    upper_sand = {
        7: (12, 19),
        8: (13, 18),
        9: (14, 17),
        10: (15, 16),
    }
    for y, (x0, x1) in upper_sand.items():
        for x in range(x0, x1 + 1):
            if y == 7 and x in (13, 14, 15):
                col = GOLD_4
            elif x <= 15:
                col = GOLD_3
            elif x == x1:
                col = GOLD_1
            else:
                col = GOLD_2
            hg.px(x, y, col)

    # Falling golden sand trickle through the pinched waist (y=11..15)
    for y in range(11, 16):
        hg.px(15, y, GOLD_4 if y % 2 == 1 else GOLD_3)
        if y <= 12:
            hg.px(16, y, GOLD_2)

    # Lower bulb golden sand pyramid mound (y=15..18)
    lower_sand = {
        15: (15, 16),
        16: (13, 18),
        17: (12, 19),
        18: (11, 20),
    }
    for y, (x0, x1) in lower_sand.items():
        for x in range(x0, x1 + 1):
            if y == 15 and x == 15:
                col = GOLD_4
            elif x < 15:
                col = GOLD_3 if y < 18 else GOLD_2
            elif x == 15:
                col = GOLD_4 if y == 16 else GOLD_3
            elif x >= x1 - 1:
                col = GOLD_1
            else:
                col = GOLD_2
            hg.px(x, y, col)

    # Curved white-cyan glass specular highlights over left glass wall
    for gx, gy in [(11, 6), (11, 7), (12, 8), (12, 15), (11, 16), (11, 17)]:
        hg.px(gx, gy, BLU_4)

    # Ornate golden collar pinching the narrow waist (y=11)
    hg.hline(14, 17, 11, GOLD_2)
    hg.px(14, 11, GOLD_3)
    hg.px(15, 11, GOLD_4)

    # Ornate wide golden top cap (y=2..4, x=8..23)
    hg.fill_rect(9, 2, 22, 4, GOLD_2)
    hg.hline(8, 23, 3, GOLD_2)
    hg.hline(9, 21, 2, GOLD_3)
    hg.hline(10, 16, 2, GOLD_4)
    hg.hline(9, 22, 4, GOLD_1)
    hg.px(8, 3, GOLD_3)
    hg.px(23, 3, GOLD_0)

    # Ornate wide golden bottom base (y=19..21, x=8..23)
    hg.fill_rect(9, 19, 22, 21, GOLD_2)
    hg.hline(8, 23, 20, GOLD_2)
    hg.hline(9, 21, 19, GOLD_3)
    hg.hline(10, 15, 19, GOLD_4)
    hg.hline(9, 22, 21, GOLD_0)
    hg.px(8, 20, GOLD_3)
    hg.px(23, 20, GOLD_0)

    hg.add_outline(OUTLINE_DARK)
    c.blit(hg)
    return c


def make_btn_espionage() -> Canvas:
    """Espionage toggle: Hooded midnight-purple spy cowl with glowing golden-amber eyes & silver stiletto."""
    c = Canvas(32, 32)

    # Midnight-purple cowl palette
    mid_0: Color = (24, 12, 46, 255)
    mid_1: Color = (48, 22, 86, 255)
    mid_2: Color = (78, 36, 134, 255)
    mid_3: Color = (122, 62, 192, 255)
    mid_4: Color = (172, 110, 236, 255)

    cowl_pal: dict[str, Color] = {
        ".": TRANSPARENT,
        "K": OUTLINE_DARK,
        "v": OBS_0,
        "0": mid_0,
        "1": mid_1,
        "2": mid_2,
        "3": mid_3,
        "4": mid_4,
        "a": ORG_1,
        "o": ORG_2,
        "g": GOLD_2,
        "y": GOLD_3,
        "w": GOLD_4,
    }

    # Pointed midnight-purple spy cowl, deep shadowed hood opening, glowing golden-amber eyes & draped mantle
    rows = [
        "................................",
        "................................",
        "...............32...............",
        ".............43211..............",
        "............4322110.............",
        "...........432221100............",
        "..........43211221100...........",
        ".........4321000001100..........",
        ".........3210KKKKKK0110.........",
        "........3210KvvvvvvK0110........",
        "........321KvKKKKKKvK110........",
        "........321KoKvvvvKoK110........",
        "........210KywgKKgwyK100........",
        "........210KKoaKKaoKK100........",
        ".......3210KKKvvvvKKK0100.......",
        ".......321001222222100100.......",
        "......4321012332232101100.......",
        ".....432210012211221012100......",
        ".....322110001211210012100......",
        "....32210121001221001211000.....",
        "....22101232100110012210100.....",
        "....21001221001221001100100.....",
        ".....100111001111110000110......",
        "......0000000000000000000.......",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
    ]
    c.stamp(rows, cowl_pal)
    c.add_outline(OUTLINE_DARK)

    # Sleek silver stiletto dagger crossed beneath the cowl (crisp 2:1 angle, separate outlined layer)
    dagger = Canvas(32, 32)

    # Faceted silver needle blade sweeping from guard (10, 24) to gleaming tip (29, 15)
    for step in range(20):
        bx = 10 + step
        by = 24 - (step // 2)
        if step < 15:
            dagger.px(bx, by, STL_4 if step % 2 == 0 else STL_3)
            dagger.px(bx, by + 1, STL_2 if step < 11 else STL_3)
        elif step < 18:
            dagger.px(bx, by, STL_4)
            if step == 15:
                dagger.px(bx, by + 1, STL_2)
        else:
            dagger.px(bx, by, STL_4)
    # Extra razor highlight glint along upper spine & tip
    for gx, gy in [(12, 23), (16, 21), (20, 19), (24, 17), (28, 15), (29, 15)]:
        dagger.px(gx, gy, STL_4)

    # Ornate swept golden stiletto crossguard centered at (9, 25)
    guard_pts = [
        (7, 22, GOLD_4),
        (7, 23, GOLD_3),
        (8, 23, GOLD_2),
        (8, 24, GOLD_3),
        (9, 24, GOLD_4),
        (9, 25, GOLD_3),
        (10, 25, GOLD_2),
        (10, 26, GOLD_2),
        (11, 26, GOLD_3),
        (11, 27, GOLD_2),
        (12, 27, GOLD_1),
        (12, 28, GOLD_1),
    ]
    for gx, gy, gcol in guard_pts:
        dagger.px(gx, gy, gcol)

    # Wrapped dark grip & golden pommel on bottom-left
    grip_pts = [
        (8, 26, OBS_1),
        (7, 26, OBS_2),
        (7, 27, OBS_1),
        (6, 27, GOLD_2),
        (5, 27, OBS_2),
        (5, 28, OBS_1),
    ]
    for gx, gy, gcol in grip_pts:
        dagger.px(gx, gy, gcol)

    # Pommel jewel
    dagger.px(4, 28, GOLD_3)
    dagger.px(3, 28, GOLD_4)
    dagger.px(4, 29, GOLD_1)
    dagger.px(3, 29, GOLD_2)

    dagger.add_outline(OUTLINE_DARK)
    c.blit(dagger)
    return c


def make_btn_dispatch_spy() -> Canvas:
    """Dispatch Spy power: Wax-sealed royal parchment letter with tiny golden crown pierced by silver dagger."""
    c = Canvas(32, 32)

    # 1. Folded royal parchment letter/envelope (x=2..23, y=10..26)
    letter = Canvas(32, 32)
    letter.fill_rect(2, 10, 23, 26, PAR_2)
    letter.fill_rect(3, 11, 22, 25, PAR_3)
    # Warm top-left ivory highlight & soft right/bottom parchment shading
    letter.hline(2, 22, 10, PAR_4)
    letter.vline(2, 10, 25, PAR_4)
    letter.vline(23, 11, 26, PAR_1)
    letter.hline(3, 23, 26, PAR_1)
    letter.hline(3, 22, 25, PAR_2)

    # Lower envelope fold creases (subtle recessed diagonals)
    letter.line(3, 25, 10, 19, PAR_1)
    letter.line(22, 25, 15, 19, PAR_1)
    letter.line(3, 24, 9, 19, PAR_2)

    # Upper triangular royal flap fold creases (crisp V meeting at wax seal)
    letter.line(3, 11, 11, 19, PAR_1)
    letter.line(22, 11, 14, 19, PAR_1)
    letter.line(3, 12, 10, 19, PAR_0)
    letter.line(22, 12, 15, 19, PAR_0)
    # Crisp bright bevel just above left flap crease
    letter.line(4, 11, 11, 18, PAR_4)

    # Tiny golden royal crown emblem centered on the upper flap (x=9..15, y=11..14)
    crown_pal: dict[str, Color] = {
        ".": TRANSPARENT,
        "k": OUTLINE_WARM,
        "b": GOLD_0,
        "a": GOLD_1,
        "g": GOLD_2,
        "y": GOLD_3,
        "w": GOLD_4,
        "r": RED_2,
    }
    crown_rows = [
        "g..g..g",
        "gy.w.yg",
        "gygrygb",
        "baaaaab",
    ]
    letter.stamp(crown_rows, crown_pal, ox=9, oy=11)
    # Crisp dark bronze drop-shadow line under crown band
    letter.hline(9, 15, 15, PAR_0)

    # Rich crimson wax seal at flap apex (cx=12.5, cy=19.5)
    letter.circle(12.8, 20.0, 3.3, PAR_0)   # cast shadow on parchment
    letter.circle(12.5, 19.5, 3.2, RED_0)   # dark wax rim
    letter.circle(12.5, 19.5, 2.5, RED_1)   # wax body shadow
    letter.circle(12.1, 19.1, 2.0, RED_2)   # vivid crimson wax
    letter.px(11, 18, RED_4)
    letter.px(12, 18, RED_3)
    letter.px(11, 19, RED_3)
    # Scalloped melted wax edge drips
    letter.px(11, 23, RED_1)
    letter.px(13, 23, RED_0)

    letter.add_outline(OUTLINE_WARM)
    c.blit(letter)

    # 2. Emerging lower silver blade tip poking out of lower-left parchment (x=4..9, y=24..29)
    tip = Canvas(32, 32)
    tip_steps = [
        (9, 24, True),
        (8, 25, True),
        (7, 26, False),
        (6, 27, False),
        (5, 28, False),
        (4, 29, False),
    ]
    for tx, ty, wide in tip_steps:
        tip.px(tx, ty, STL_4)
        if wide:
            tip.px(tx - 1, ty, STL_3)
            tip.px(tx, ty + 1, STL_2)
        elif tx >= 6:
            tip.px(tx, ty + 1, STL_2)
    tip.add_outline(OUTLINE_DARK)
    c.blit(tip)

    # Crisp dark puncture tear shadow where the emerging tip cuts through the lower-left envelope
    c.px(9, 23, OUTLINE_DARK)
    c.px(10, 24, OUTLINE_DARK)
    c.px(8, 23, PAR_0)

    # 3. Upper gleaming silver dagger plunging diagonally down-left into the crimson wax seal
    dagger = Canvas(32, 32)

    # Gleaming faceted blade from crossguard (23, 10) down-left into wax seal (15, 18)
    for i in range(9):
        bx = 23 - i
        by = 10 + i
        dagger.px(bx, by, STL_4)       # bright center ridge
        dagger.px(bx - 1, by, STL_3)   # upper-left bevel
        dagger.px(bx, by + 1, STL_2)   # lower-right bevel
        if i < 6:
            dagger.px(bx + 1, by + 1, STL_1)

    # Swept golden crossguard centered at (24, 9), spanning (21, 6) to (27, 12)
    for d in range(-3, 4):
        gx = 24 + d
        gy = 9 + d
        col = GOLD_4 if d == -3 else (GOLD_3 if d <= 0 else (GOLD_2 if d < 3 else GOLD_1))
        dagger.px(gx, gy, col)
        dagger.px(gx + 1, gy, GOLD_2 if d < 2 else GOLD_1)
    dagger.px(24, 9, GOLD_4)

    # Midnight-purple wrapped spy grip from (25, 8) to (27, 6)
    mid_grip_hi: Color = (122, 62, 192, 255)
    mid_grip_lo: Color = (48, 22, 86, 255)
    for i in range(3):
        hx = 25 + i
        hy = 8 - i
        dagger.px(hx, hy, mid_grip_hi if i % 2 == 0 else GOLD_2)
        dagger.px(hx + 1, hy, mid_grip_lo)
        dagger.px(hx, hy - 1, mid_grip_hi)

    # Ornate golden pommel at (28, 5)-(29, 4)
    dagger.px(28, 4, GOLD_3)
    dagger.px(29, 4, GOLD_4)
    dagger.px(28, 5, GOLD_2)
    dagger.px(29, 5, GOLD_1)

    dagger.add_outline(OUTLINE_DARK)
    c.blit(dagger)

    # Crimson wax lip overlapping the entry puncture so the blade reads genuinely sunk into the wax seal
    c.px(14, 18, RED_2)
    c.px(14, 19, RED_1)
    c.px(15, 19, OUTLINE_DARK)
    c.px(15, 20, RED_0)
    return c


def make_btn_armory_blessing() -> Canvas:
    """Armory Blessing: Bright cyan-blue & gold anvil with a glowing forged sword rising above it."""
    c = Canvas(32, 32)

    # Electric cyan-aqua ramp for the blessed forge anvil & glowing blade
    cya_0: Color = (8, 72, 110, 255)
    cya_1: Color = (14, 128, 184, 255)
    cya_2: Color = (32, 194, 240, 255)
    cya_3: Color = (120, 238, 255, 255)
    cya_4: Color = (232, 254, 255, 255)
    pure_w: Color = (255, 255, 255, 255)

    # 1. Heavy cyan-blue & gold blacksmith anvil (lower half, y=16..29)
    anvil = Canvas(32, 32)

    # Pointed horn jutting left (x=2..10, y=17..21)
    anvil.poly([(2.0, 18.0), (10.5, 16.5), (10.5, 21.5), (5.5, 20.5)], cya_2)
    anvil.line(3, 18, 10, 17, cya_3)
    anvil.px(2, 18, cya_4)
    anvil.line(5, 20, 10, 21, cya_1)
    anvil.line(7, 21, 10, 21, cya_0)

    # Broad anvil face/deck (x=10..28, y=16..20)
    anvil.fill_rect(10, 16, 28, 20, cya_2)
    anvil.hline(10, 27, 16, cya_4)
    anvil.px(28, 16, cya_3)
    anvil.hline(10, 27, 17, cya_3)
    anvil.px(28, 17, cya_2)
    # Rich gilded trim band across anvil face
    anvil.hline(10, 28, 18, GOLD_3)
    anvil.hline(24, 28, 18, GOLD_2)
    anvil.hline(10, 15, 18, GOLD_4)
    anvil.hline(10, 28, 19, GOLD_1)
    anvil.hline(11, 27, 20, cya_0)
    anvil.px(28, 19, GOLD_0)

    # Sculpted waist (x=12..24, y=21..25)
    anvil.fill_rect(12, 21, 24, 25, cya_1)
    anvil.fill_rect(13, 21, 23, 25, cya_2)
    anvil.vline(13, 21, 24, cya_3)
    anvil.vline(22, 21, 24, cya_1)
    anvil.vline(23, 21, 25, cya_0)
    # Gilded forge emblem on anvil waist
    anvil.fill_rect(16, 22, 19, 24, GOLD_2)
    anvil.fill_rect(17, 22, 18, 23, GOLD_4)
    anvil.px(19, 24, GOLD_0)

    # Heavy flared base foot (x=7..28, y=26..29)
    anvil.poly([(7.0, 29.0), (11.0, 25.5), (24.5, 25.5), (28.5, 29.0)], cya_1)
    anvil.hline(10, 25, 26, GOLD_3)
    anvil.hline(20, 25, 26, GOLD_2)
    anvil.hline(9, 26, 27, cya_2)
    anvil.hline(9, 14, 27, cya_3)
    anvil.hline(8, 27, 28, cya_1)
    anvil.hline(7, 28, 29, cya_0)
    # Crisp bottom arch notch under the foot
    anvil.fill_rect(16, 29, 19, 29, TRANSPARENT)

    anvil.add_outline(OUTLINE_DARK)
    c.blit(anvil)

    # 2. Razor-sharp glowing cyan-white forged sword rising vertically above the anvil deck (cx=18, y=2..16)
    sword = Canvas(32, 32)
    cx = 18

    # Crisp tapered broadsword blade (y=2..11) — sharp point, bright white spine, glowing cyan edges
    sword.px(cx, 2, pure_w)
    sword.px(cx, 3, pure_w)
    sword.px(cx - 1, 3, cya_4)
    sword.px(cx + 1, 3, cya_3)
    for y in range(4, 12):
        sword.px(cx - 2, y, cya_3 if y >= 6 else TRANSPARENT)
        sword.px(cx - 1, y, cya_4)
        sword.px(cx, y, pure_w)
        sword.px(cx + 1, y, cya_3)
        sword.px(cx + 2, y, cya_2 if y >= 6 else TRANSPARENT)
    # Tapered beveled shoulder at y=4..5
    sword.px(cx - 1, 4, cya_4)
    sword.px(cx + 1, 4, cya_2)
    sword.px(cx - 1, 5, cya_4)
    sword.px(cx + 1, 5, cya_3)

    # Swept ornate golden crossguard (y=11..13, x=12..24)
    sword.hline(13, 23, 12, GOLD_3)
    sword.hline(14, 22, 13, GOLD_2)
    sword.hline(15, 21, 13, GOLD_1)
    # Upward swept wingtips on guard
    sword.px(12, 11, GOLD_4)
    sword.px(13, 11, GOLD_3)
    sword.px(24, 11, GOLD_3)
    sword.px(23, 11, GOLD_2)
    sword.px(13, 12, GOLD_4)
    # Glowing cyan forge gem set into the crossguard
    sword.px(cx, 12, cya_4)
    sword.px(cx, 13, cya_2)

    # Grip & golden pommel hovering just above the anvil face (y=14..16)
    sword.vline(cx, 14, 15, cya_3)
    sword.px(cx - 1, 14, GOLD_2)
    sword.px(cx + 1, 14, GOLD_1)
    sword.hline(cx - 1, cx + 1, 15, GOLD_3)
    sword.px(cx, 15, GOLD_4)
    sword.px(cx, 16, GOLD_2)

    sword.add_outline(OUTLINE_DARK)
    c.blit(sword)

    # 3. Crisp radiant golden-white blessing sparks & cyan forge motes flanking the rising blade
    sparks = Canvas(32, 32)
    for sx, sy in [(7, 6), (27, 7)]:
        sparks.hline(sx - 2, sx + 2, sy, GOLD_3)
        sparks.vline(sx, sy - 2, sy + 2, GOLD_3)
        sparks.px(sx, sy, pure_w)
    for sx, sy in [(10, 12), (26, 13)]:
        sparks.px(sx, sy, pure_w)
        sparks.px(sx - 1, sy, cya_3)
        sparks.px(sx + 1, sy, cya_2)
        sparks.px(sx, sy - 1, cya_4)
        sparks.px(sx, sy + 1, cya_2)
    sparks.add_outline(OUTLINE_DARK)
    c.blit(sparks)
    return c


def make_btn_anoint_hero() -> Canvas:
    """Anoint War Hero: Radiant golden winged star halo crowning a bright white-silver knight helmet."""
    c = Canvas(32, 32)

    # Gleaming white-silver paladin steel ramp
    slv_0: Color = (92, 106, 132, 255)
    slv_1: Color = (146, 162, 190, 255)
    slv_2: Color = (196, 210, 232, 255)
    slv_3: Color = (234, 242, 254, 255)
    slv_4: Color = (255, 255, 255, 255)

    # 1. Clean sculpted golden valkyrie wings, arched halo & crowning 4-point star (y=1..14)
    halo = Canvas(32, 32)
    wing_pal: dict[str, Color] = {
        ".": TRANSPARENT,
        "a": GOLD_1,
        "g": GOLD_2,
        "y": GOLD_3,
        "w": GOLD_4,
        "x": slv_4,
    }
    wing_rows = [
        "..............ww..............",
        "..............wx..............",
        ".............wyxw.............",
        "..ww.........wyxw.........ww..",
        ".wwwyy....wwyxxxxyww....yyggg.",
        "..gyyyyg.....ywwy.....gyyyyg..",
        "...gyyyygg...gwwg...ggyyyyg...",
        "..wyyyyyyyg..gwwg..gyyyyyyg...",
        "...agyyyyygg.gwwg.ggyyyyga....",
        "....agyyyyyggggggggyyyyga.....",
        ".....aagyyggggggggggyyga......",
        ".......aaaggg....gggaaa.......",
        ".........aa........aa.........",
    ]
    halo.stamp(wing_rows, wing_pal, ox=1, oy=1)

    halo.add_outline(OUTLINE_WARM)
    c.blit(halo)

    # 2. Bright white-silver knight paladin greathelm (foreground centerpiece, x=9..22, y=10..29)
    helm = Canvas(32, 32)
    helm_pal: dict[str, Color] = {
        ".": TRANSPARENT,
        "K": OUTLINE_DARK,
        "0": slv_0,
        "1": slv_1,
        "2": slv_2,
        "3": slv_3,
        "4": slv_4,
        "a": GOLD_1,
        "g": GOLD_2,
        "y": GOLD_3,
        "w": GOLD_4,
    }
    helm_rows = [
        "..............44..............",
        "............344432............",
        "..........3444443210..........",
        ".........344444432210.........",
        "........34443344322110........",
        "........34433344322110........",
        "........ywygggywygagga........",
        "........34433344322110........",
        "........34KKKK44KKKK10........",
        "........34KwKK44KKwK10........",
        "........344333KK222110........",
        "........344333KK222110........",
        "........34K3K3KK2K1K10........",
        ".........34333KK22210.........",
        ".........34K3KKK2K1K0.........",
        "..........3433KK2210..........",
        "..........3433KK2210..........",
        "...........34332210...........",
        "...........ywygggaa...........",
        "............yggaaa............",
    ]
    helm.stamp(helm_rows, helm_pal, ox=1, oy=10)

    helm.add_outline(OUTLINE_DARK)
    c.blit(helm)
    return c


def make_btn_merc_drop() -> Canvas:
    """Mercenary Drop: Vibrant emerald-green & gold heraldic war banner flanked by crossed steel battleaxes."""
    c = Canvas(32, 32)

    # Rich vibrant emerald-green ramp
    emr_0: Color = (10, 66, 36, 255)
    emr_1: Color = (20, 120, 62, 255)
    emr_2: Color = (36, 182, 92, 255)
    emr_3: Color = (86, 232, 130, 255)
    emr_4: Color = (182, 252, 192, 255)

    # 1. Crossed steel battleaxes flanking behind the banner (x=2..29, y=4..28)
    axes = Canvas(32, 32)

    # Diagonal wooden hafts crossing from bottom corners up into the axe sockets (no awkward nubs past heads)
    for i in range(21):
        # Haft 1: bottom-left (4, 27) -> upper-right socket (24, 7)
        x1 = 4 + i
        y1 = 27 - i
        axes.px(x1, y1, BRN_2)
        axes.px(x1 + 1, y1, BRN_1)
        axes.px(x1, y1 - 1, BRN_3)
        # Haft 2: bottom-right (27, 27) -> upper-left socket (7, 7)
        x2 = 27 - i
        y2 = 27 - i
        axes.px(x2, y2, BRN_2)
        axes.px(x2 - 1, y2, BRN_1)
        axes.px(x2, y2 - 1, BRN_3)

    # Brass pommel caps at bottom ends of crossed hafts
    for px0, py0 in [(3, 27), (27, 27)]:
        axes.fill_rect(px0, py0, px0 + 1, py0 + 1, GOLD_3)
        axes.px(px0 + 1, py0 + 1, GOLD_1)

    # Bold bearded steel crescent battleaxe head on LEFT (x=2..10, y=4..17)
    axes.poly([(10.0, 7.0), (5.0, 4.0), (2.0, 6.8), (2.0, 13.5), (5.0, 16.5), (8.4, 13.8), (9.6, 10.6)], STL_2)
    axes.poly([(8.5, 7.2), (5.0, 4.8), (2.4, 7.2), (2.4, 13.0), (5.0, 15.4), (6.8, 12.2)], STL_3)
    for bx, by in [(5, 4), (4, 5), (3, 6), (2, 7), (2, 8), (2, 9), (2, 10), (2, 11), (2, 12), (2, 13), (3, 14), (4, 15), (5, 16)]:
        axes.px(bx, by, STL_4)
    axes.fill_rect(8, 8, 9, 10, GOLD_2)  # brass haft collar

    # Bold bearded steel crescent battleaxe head on RIGHT (x=21..29, y=4..17)
    axes.poly([(21.0, 7.0), (26.0, 4.0), (29.0, 6.8), (29.0, 13.5), (26.0, 16.5), (22.6, 13.8), (21.4, 10.6)], STL_2)
    axes.poly([(22.5, 7.2), (26.0, 4.8), (28.6, 7.2), (28.6, 13.0), (26.0, 15.4), (24.2, 12.2)], STL_3)
    for bx, by in [(26, 4), (27, 5), (28, 6), (29, 7), (29, 8), (29, 9), (29, 10), (29, 11), (29, 12), (29, 13), (28, 14), (27, 15), (26, 16)]:
        axes.px(bx, by, STL_4)
    axes.fill_rect(22, 8, 23, 10, GOLD_2)  # brass haft collar

    axes.add_outline(OUTLINE_DARK)
    c.blit(axes)

    # 2. Vibrant emerald-green & gold heraldic swallowtail war banner (front center, x=9..22, y=2..29)
    banner = Canvas(32, 32)

    # Top spear finial & horizontal gilded crossbar
    banner.fill_rect(15, 2, 16, 4, GOLD_3)
    banner.px(15, 2, GOLD_4)
    banner.fill_rect(7, 5, 24, 6, GOLD_2)
    banner.hline(7, 23, 5, GOLD_4)
    banner.hline(8, 24, 6, GOLD_1)
    # Round gold bar end finials
    banner.fill_rect(6, 4, 7, 7, GOLD_3)
    banner.px(6, 4, GOLD_4)
    banner.px(7, 7, GOLD_1)
    banner.fill_rect(24, 4, 25, 7, GOLD_2)
    banner.px(24, 4, GOLD_3)
    banner.px(25, 7, GOLD_0)

    # Emerald swallowtail cloth body (x=9..22, y=7..29) with dramatic drapery shading
    for y in range(7, 30):
        for x in range(9, 23):
            if y >= 24:
                dist_center = abs(x - 15.5)
                needed = (y - 23) * 0.92
                if dist_center < needed:
                    continue
            if x in (11, 12, 16):
                col = emr_3
            elif x in (9, 22):
                col = emr_0
            elif x in (19, 20, 21):
                col = emr_1
            else:
                col = emr_2
            if y == 7:
                col = emr_0
            elif y == 8 and x in (11, 12, 15, 16):
                col = emr_4
            banner.px(x, y, col)

    # Crisp 1px gold heraldic trim down both banner edges & along swallowtail fringe
    for y in range(7, 30):
        banner.px(10, y, GOLD_3 if y < 24 else GOLD_4)
        banner.px(21, y, GOLD_2 if y < 24 else GOLD_1)
    for step in range(6):
        banner.px(15 - step, 23 + step, GOLD_3)
        banner.px(16 + step, 23 + step, GOLD_2)

    # Crisp golden heraldic war-chevron & radiant diamond star crest on the emerald field
    crest_pal: dict[str, Color] = {
        ".": TRANSPARENT,
        "s": emr_0,
        "a": GOLD_1,
        "g": GOLD_2,
        "y": GOLD_3,
        "w": GOLD_4,
    }
    crest_rows = [
        "...ww...",
        "..wyyg..",
        ".wywwyg.",
        "..syyg..",
        "...ss...",
        "..wyyg..",
        ".wyggyga",
        "wygaagya",
        "sa....as",
    ]
    banner.stamp(crest_rows, crest_pal, ox=12, oy=11)

    banner.add_outline(OUTLINE_DARK)
    c.blit(banner)
    return c


def make_btn_geopolitics() -> Canvas:
    """Geopolitics & Alliance Trade: Royal purple & gold balance scales over a marble senate arch."""
    c = Canvas(32, 32)

    # Deep vibrant royal purple ramp (dominant hue)
    pur_0: Color = (42, 12, 82, 255)
    pur_1: Color = (86, 26, 154, 255)
    pur_2: Color = (142, 52, 222, 255)
    pur_3: Color = (196, 114, 250, 255)
    pur_4: Color = (242, 212, 255, 255)

    # Ivory marble ramp for the classical senate arch
    mrb_0: Color = (128, 126, 140, 255)
    mrb_1: Color = (176, 174, 186, 255)
    mrb_2: Color = (218, 214, 220, 255)
    mrb_3: Color = (244, 240, 236, 255)
    mrb_4: Color = (255, 254, 248, 255)

    # 1. Marble Senate Arch framing a luminous royal-purple vaulted interior (x=4..27, y=3..29)
    arch = Canvas(32, 32)
    cx = 15.5

    # Royal purple vaulted background fill inside the arch
    for y in range(5, 27):
        for x in range(6, 26):
            if y < 14:
                d = math.hypot(x - cx, y - 14.0)
                if d > 9.8:
                    continue
            rd = math.hypot(x - cx, (y - 16.0) * 0.85)
            if rd < 3.8:
                col = pur_3
            elif rd < 7.0:
                col = pur_2
            elif rd < 9.4:
                col = pur_1
            else:
                col = pur_0
            arch.px(x, y, col)

    # Smooth semicircular marble arch ring spanning overhead (y=3..14)
    for y in range(3, 15):
        for x in range(4, 28):
            d = math.hypot(x - cx, y - 14.2)
            if 8.2 <= d <= 11.4:
                if x < 14 and y < 11:
                    col = mrb_4 if d > 10.1 else mrb_3
                elif x > 18:
                    col = mrb_1 if d < 9.3 else mrb_2
                else:
                    col = mrb_3 if d > 9.8 else mrb_2
                arch.px(x, y, col)

    # Royal purple keystone medallion crowning the marble arch
    arch.fill_rect(14, 2, 17, 5, GOLD_2)
    arch.hline(14, 17, 2, GOLD_4)
    arch.fill_rect(15, 3, 16, 4, pur_3)
    arch.px(15, 3, pur_4)

    # Twin fluted marble senate columns on left (x=5..8) and right (x=23..26)
    for col_x in (5, 23):
        arch.fill_rect(col_x - 1, 13, col_x + 4, 14, mrb_3 if col_x < 15 else mrb_2)
        arch.hline(col_x - 1, col_x + 4, 13, mrb_4 if col_x < 15 else mrb_3)
        for y in range(15, 26):
            arch.px(col_x, y, mrb_4 if col_x < 15 else mrb_3)
            arch.px(col_x + 1, y, mrb_2)
            arch.px(col_x + 2, y, mrb_3 if col_x < 15 else mrb_1)
            arch.px(col_x + 3, y, mrb_0)
        arch.fill_rect(col_x - 1, 25, col_x + 4, 26, mrb_2)

    # Stepped marble senate plinth along bottom (y=26..29)
    arch.fill_rect(4, 26, 27, 27, mrb_3)
    arch.hline(4, 27, 26, mrb_4)
    arch.fill_rect(3, 28, 28, 29, mrb_1)
    arch.hline(3, 27, 28, mrb_2)
    arch.hline(3, 28, 29, mrb_0)

    arch.add_outline(OUTLINE_DARK)
    c.blit(arch)

    # 2. Gleaming gold & royal-purple balance scales prominently overlaid in foreground
    scales = Canvas(32, 32)

    # Central ornate golden pillar & wide base
    scales.vline(15, 6, 24, GOLD_3)
    scales.vline(16, 6, 24, GOLD_2)
    scales.px(15, 11, GOLD_4)
    scales.px(15, 16, GOLD_4)
    scales.fill_rect(13, 24, 18, 25, GOLD_2)
    scales.hline(13, 17, 24, GOLD_3)
    scales.fill_rect(11, 26, 20, 27, GOLD_1)
    scales.hline(11, 19, 26, GOLD_3)

    # Wide horizontal golden balance beam (y=8..9, x=5..26) with ornamental curled tips
    scales.hline(5, 26, 8, GOLD_4)
    scales.hline(5, 26, 9, GOLD_2)
    scales.px(4, 9, GOLD_3)
    scales.px(27, 9, GOLD_2)
    # Glowing royal-purple crown orb atop the scale pivot
    scales.fill_rect(14, 5, 17, 8, pur_2)
    scales.fill_rect(15, 6, 16, 7, pur_3)
    scales.px(15, 6, pur_4)
    scales.hline(14, 17, 9, GOLD_3)

    # Left & right V-shaped golden suspension chains & hanging scale pans (protruding past arch!)
    for pan_cx in (6, 25):
        scales.line(pan_cx, 10, pan_cx - 4, 18, GOLD_3)
        scales.line(pan_cx, 10, pan_cx + 4, 18, GOLD_2)
        # Luminous royal-purple trade gems resting inside each golden scale pan
        scales.fill_rect(pan_cx - 2, 16, pan_cx + 2, 18, pur_2)
        scales.fill_rect(pan_cx - 1, 15, pan_cx + 1, 17, pur_3)
        scales.px(pan_cx - 1, 16, pur_4)
        # Curved golden scale pan bowl (y=18..21)
        scales.hline(pan_cx - 4, pan_cx + 4, 18, GOLD_4)
        scales.hline(pan_cx - 4, pan_cx + 4, 19, GOLD_3)
        scales.hline(pan_cx - 3, pan_cx + 3, 20, GOLD_2)
        scales.hline(pan_cx - 2, pan_cx + 2, 21, GOLD_1)

    scales.add_outline(OUTLINE_DARK)
    c.blit(scales)
    return c


def make_btn_highways_citadels() -> Canvas:
    """Highways & Citadels: Warm sandstone twin-tower fortress gatehouse spanning a wide cobblestone imperial highway."""
    c = Canvas(32, 32)

    # Warm desert-sandstone / terracotta-ochre citadel ramp
    snd_0: Color = (104, 54, 24, 255)
    snd_1: Color = (158, 90, 40, 255)
    snd_2: Color = (208, 134, 64, 255)
    snd_3: Color = (240, 180, 102, 255)
    snd_4: Color = (254, 224, 164, 255)

    # 1. Warm sandstone twin-tower fortress gatehouse (x=2..29, y=3..25)
    fort = Canvas(32, 32)

    # Central connecting gatehouse wall & battlements (x=10..21, y=7..21)
    fort.fill_rect(10, 9, 21, 21, snd_2)
    fort.hline(10, 21, 9, snd_3)
    # Central wall merlons
    fort.fill_rect(12, 7, 14, 9, snd_3)
    fort.hline(12, 14, 7, snd_4)
    fort.fill_rect(17, 7, 19, 9, snd_3)
    fort.hline(17, 19, 7, snd_4)
    fort.hline(11, 20, 10, snd_1)

    # Left & right tall battlemented sandstone citadel towers
    for tx0, tx1, is_left in [(2, 10, True), (21, 29, False)]:
        fort.fill_rect(tx0, 6, tx1, 24, snd_2)
        fort.vline(tx0, 6, 24, snd_4 if is_left else snd_3)
        fort.vline(tx0 + 1, 6, 24, snd_3)
        fort.vline(tx1 - 1, 6, 24, snd_1)
        fort.vline(tx1, 6, 24, snd_0)
        # Bold 3-merlon crenellated battlement crown (y=3..6)
        for mx0, mx1 in [(tx0, tx0 + 2), (tx0 + 3, tx0 + 5), (tx0 + 6, tx1)]:
            fort.fill_rect(mx0, 3, mx1, 6, snd_2)
            fort.hline(mx0, mx1, 3, snd_4 if is_left else snd_3)
            fort.vline(mx0, 3, 5, snd_3)
            fort.vline(mx1, 4, 6, snd_1)
        fort.hline(tx0, tx1, 7, snd_0)
        fort.hline(tx0, tx1 - 1, 6, snd_3)
        for by in (11, 15, 19, 23):
            fort.hline(tx0 + 1, tx1 - 1, by, snd_1)
            fort.px(tx0 + 3 + (by % 3), by - 2, snd_1)
        # Dark arrow-slit embrasure window centered on tower
        wx = (tx0 + tx1) // 2
        fort.vline(wx, 10, 14, OUTLINE_WARM)
        fort.px(wx - 1, 12, OUTLINE_WARM)
        fort.px(wx + 1, 12, OUTLINE_WARM)
        fort.px(wx, 15, snd_4)
        # Tower base plinth
        fort.fill_rect(tx0, 23, tx1, 25, snd_1)
        fort.hline(tx0, tx1 - 1, 23, snd_3)

    # Deep grand rounded gatehouse archway opening (x=12..19, y=12..21)
    for y in range(12, 22):
        for x in range(12, 20):
            dx = x - 15.5
            dy = y - 15.8
            if y < 16 and (dx * dx) / 14.5 + (dy * dy) / 13.5 > 1.0:
                continue
            fort.px(x, y, OBS_0 if y < 17 else OBS_1)
    # Bright carved sandstone arch ring (voussoirs) framing the portal
    for deg in range(180, 361, 10):
        a = math.radians(deg)
        ax = int(round(15.5 + math.cos(a) * 4.5))
        ay = int(round(16.0 + math.sin(a) * 4.2))
        fort.px(ax, ay, snd_4 if ax <= 16 else snd_3)
    # Gleaming steel portcullis teeth peeking at top of the open archway
    for px in (13, 15, 16, 18):
        fort.vline(px, 13, 15, STL_2)
        fort.px(px, 15, STL_4)

    fort.add_outline(OUTLINE_WARM)
    c.blit(fort)

    # 2. Wide perspective cobblestone imperial highway flaring cleanly out of the archway (y=18..29)
    road = Canvas(32, 32)
    for y in range(18, 30):
        t = (y - 18) / 11.0
        half_w = 3.2 + t * 8.6
        x_left = int(round(15.5 - half_w))
        x_right = int(round(15.5 + half_w))
        for x in range(x_left, x_right + 1):
            if x == x_left or x == x_left + 1:
                col = snd_4 if y % 2 == 0 else GOLD_3
            elif x == x_right or x == x_right - 1:
                col = snd_3 if y % 2 == 0 else GOLD_2
            else:
                row_band = (y - 18) // 2
                tile_w = 2 if y < 22 else 3
                tile_idx = (x + (row_band % 2) * 2) // tile_w
                is_mortar_y = (y in (20, 23, 26))
                is_mortar_x = ((x + (row_band % 2) * 2) % tile_w == 0) and y >= 21
                if is_mortar_y or is_mortar_x:
                    col = STN_1 if y < 23 else STN_0
                elif abs(x - 15.5) < 1.2 and y >= 20:
                    col = STN_4
                elif (tile_idx + row_band) % 2 == 0:
                    col = STN_3
                else:
                    col = STN_2
            road.px(x, y, col)

    road.add_outline(OUTLINE_WARM)
    c.blit(road)
    return c


def make_btn_distinct_colors() -> Canvas:
    """Auto Kingdom Colors & Borders: High-contrast 4-quadrant royal shield with crisp dark borders."""
    c = Canvas(32, 32)
    cx = 15.5

    # Classic clean royal heater shield geometry (x=4..27, y=3..29)
    def shield_half_width(y: int) -> float:
        if y < 3 or y > 29:
            return -1.0
        if y <= 14:
            return 11.5
        if y == 29:
            return 0.6
        t = (y - 14) / 15.0
        return max(1.5, 11.5 * math.pow(1.0 - t * t, 0.72))

    for y in range(3, 30):
        hw = shield_half_width(y)
        if hw < 0:
            continue
        x0 = int(round(cx - hw))
        x1 = int(round(cx + hw))
        for x in range(x0, x1 + 1):
            if y == 3 and abs(x - cx) > 2.5 and abs(x - cx) < 9.0:
                continue

            edge_x = min(x - x0, x1 - x)
            top_edge = y - (4 if (abs(x - cx) > 2.5 and abs(x - cx) < 9.0) else 3)
            bot_edge = 29 - y
            near_rim = edge_x <= 1 or top_edge <= 1 or bot_edge <= 1
            inner_border = edge_x == 2 or top_edge == 2 or bot_edge == 2

            if near_rim:
                if x <= 14 and y <= 15:
                    col = STL_4 if (edge_x == 0 or top_edge == 0) else STL_3
                elif x > 17 and y > 15:
                    col = STL_1 if edge_x == 0 else STL_2
                else:
                    col = STL_3 if edge_x == 0 else STL_2
                c.px(x, y, col)
            elif inner_border or x in (15, 16) or y in (14, 15):
                c.px(x, y, OUTLINE_DARK)
            else:
                is_left = x < 15
                is_top = y < 14
                if is_top and is_left:
                    # Top-Left: Vivid Crimson Red
                    if (x + y) <= 16:
                        col = RED_3
                    elif x >= 13 or y >= 12:
                        col = RED_1
                    else:
                        col = RED_2
                    if (x, y) in ((8, 7), (9, 7), (8, 8)):
                        col = RED_4
                elif is_top and not is_left:
                    # Top-Right: Vivid Sapphire Blue
                    if (x + y) <= 27:
                        col = BLU_3
                    elif x >= 23 or y >= 12:
                        col = BLU_1
                    else:
                        col = BLU_2
                    if (x, y) in ((19, 7), (20, 7), (19, 8)):
                        col = BLU_4
                elif not is_top and is_left:
                    # Bottom-Left: Vivid Emerald Green
                    if (x + y) <= 29:
                        col = GRN_3
                    elif x >= 13 or y >= 24:
                        col = GRN_1
                    else:
                        col = GRN_2
                    if (x, y) in ((9, 18), (10, 18), (9, 19)):
                        col = GRN_4
                else:
                    # Bottom-Right: Vivid Bright Gold
                    if (x + y) <= 40:
                        col = GOLD_3
                    elif x >= 22 or y >= 24:
                        col = GOLD_1
                    else:
                        col = GOLD_2
                    if (x, y) in ((19, 18), (20, 18), (19, 19)):
                        col = GOLD_4
                c.px(x, y, col)

    # Crisp central silver diamond boss at the cross intersection
    c.fill_rect(14, 13, 17, 16, OUTLINE_DARK)
    c.px(15, 14, STL_4)
    c.px(16, 14, STL_3)
    c.px(15, 15, STL_3)
    c.px(16, 15, STL_2)

    c.add_outline(OUTLINE_DARK)
    return c


def make_btn_warfare_suite() -> Canvas:
    """Consolidated Warfare & Intelligence toggle: Bold crimson shield with crossed broadsword & purple spy dagger."""
    c = Canvas(32, 32)
    cx = 15.5

    # 1. Fierce bold crimson-red war shield with dark-gold riveted iron rim (x=5..26, y=5..28)
    shield = Canvas(32, 32)

    def war_shield_hw(y: int) -> float:
        if y < 5 or y > 28:
            return -1.0
        if y <= 14:
            return 10.2
        if y == 28:
            return 0.6
        t = (y - 14) / 14.0
        return max(1.5, 10.2 * math.pow(1.0 - t * t, 0.76))

    for y in range(5, 29):
        hw = war_shield_hw(y)
        if hw < 0:
            continue
        x0 = int(round(cx - hw))
        x1 = int(round(cx + hw))
        for x in range(x0, x1 + 1):
            edge_x = min(x - x0, x1 - x)
            top_e = y - 5
            bot_e = 28 - y
            if edge_x <= 1 or top_e <= 1 or bot_e <= 1:
                # Gleaming bronze-gold riveted war rim framing the bold crimson face
                if x <= 14 and y <= 15:
                    col = GOLD_3 if (edge_x == 0 or top_e == 0) else GOLD_2
                elif x > 17 and y > 16:
                    col = GOLD_0 if edge_x == 0 else GOLD_1
                else:
                    col = GOLD_2 if edge_x == 0 else GOLD_1
            elif edge_x == 2 or top_e == 2:
                col = RED_0
            else:
                # Bold saturated crimson-red war shield field with subtle specular highlight
                if x < 15:
                    col = RED_3 if (x + y < 23) else RED_2
                elif x in (15, 16):
                    col = RED_3 if y < 14 else RED_1
                else:
                    col = RED_2 if y < 18 else RED_1
            shield.px(x, y, col)

    # Crisp corner rivets along top rim
    for rx, ry in [(7, 6), (24, 6), (15, 5), (16, 5)]:
        shield.px(rx, ry, GOLD_4)

    shield.add_outline(OUTLINE_DARK)
    c.blit(shield)

    # 2. Gleaming steel broadsword slashing from bottom-left (2, 29) up-right to tip (29, 2)
    sword = Canvas(32, 32)

    # Wide faceted steel broadsword blade (step 0..20 from guard (9,22) to tip (29,2))
    for i in range(21):
        bx = 9 + i
        by = 22 - i
        sword.px(bx, by, STL_4)
        if i < 19:
            sword.px(bx - 1, by, STL_4 if i % 3 == 0 else STL_3)
            sword.px(bx, by + 1, STL_2)
        if i < 17:
            sword.px(bx - 1, by - 1, STL_3)
            sword.px(bx + 1, by + 1, STL_1)

    # Bold golden broadsword crossguard spanning (6,19) to (12,25)
    for d in range(-3, 4):
        gx = 9 + d
        gy = 22 + d
        sword.px(gx, gy, GOLD_4 if d <= -1 else (GOLD_3 if d <= 1 else GOLD_2))
        sword.px(gx - 1, gy, GOLD_3 if d <= 0 else GOLD_1)
    # Leather grip & heavy round golden pommel at bottom-left
    for i in range(1, 5):
        sword.px(9 - i, 22 + i, BRN_2 if i % 2 else BRN_1)
        sword.px(8 - i, 22 + i, BRN_3)
    sword.fill_rect(3, 27, 5, 29, GOLD_2)
    sword.px(3, 27, GOLD_4)
    sword.px(4, 27, GOLD_3)
    sword.px(3, 28, GOLD_3)
    sword.px(5, 29, GOLD_0)

    sword.add_outline(OUTLINE_DARK)
    c.blit(sword)

    # 3. Wicked sleek purple spy dagger slashing from bottom-right (29, 29) up-left to razor tip (2, 2)
    dagger = Canvas(32, 32)

    # Sleek razor-thin amethyst-purple spy stiletto blade (distinctly narrower than the broadsword!)
    for i in range(21):
        bx = 22 - i
        by = 22 - i
        dagger.px(bx, by, VIO_4)
        if i < 19:
            dagger.px(bx + 1, by, VIO_3)
            dagger.px(bx, by + 1, VIO_2)
        if i < 10:
            dagger.px(bx - 1, by + 1, VIO_1)

    # Curved assassin S-guard in royal purple & bright violet-white glint
    for d in range(-3, 4):
        gx = 22 - d
        gy = 22 + d
        dagger.px(gx, gy, VIO_4 if d == -3 else (VIO_3 if d <= 0 else VIO_2))
        dagger.px(gx + 1, gy, VIO_1)
    # Hooked spy-guard tips
    dagger.px(25, 18, VIO_4)
    dagger.px(18, 25, VIO_2)
    dagger.px(22, 22, GOLD_4)

    # Midnight-purple wrapped spy hilt & bright amethyst crystal pommel at bottom-right
    for i in range(1, 5):
        dagger.px(22 + i, 22 + i, VIO_2 if i % 2 else OBS_1)
        dagger.px(23 + i, 22 + i, VIO_1)
    dagger.fill_rect(26, 27, 28, 29, VIO_2)
    dagger.px(26, 27, VIO_4)
    dagger.px(27, 27, VIO_3)
    dagger.px(26, 28, VIO_3)
    dagger.px(28, 29, VIO_0)

    dagger.add_outline(OUTLINE_DARK)
    c.blit(dagger)
    return c


# =============================================================================
# 4. MULTI-FRAME WARDEN SENTINEL ANIMATION SUITE (34 frames, 32x32 RGBA)
# =============================================================================

WARDEN_ANIM_PAL: dict[str, Color] = {
    **WARDEN_PAL,
    "R": RED_0,                  # deepest royal crimson cloak shadow
    "x": (255, 255, 248, 255),   # luminous white-gold flash / glint core
    "W": BLU_0,                  # deep water ripple trough
    "u": BLU_1,                  # mid ocean ripple
    "v": BLU_2,                  # bright cyan wave
    "z": BLU_3,                  # light foam cyan
    "f": BLU_4,                  # crisp white-cyan crest
}


def _warden_cloak_colors(role: str) -> tuple[Color, Color, Color, Color]:
    if role == "leader":
        return (RED_0, RED_1, RED_2, GOLD_3)
    return (OBS_0, OBS_1, OBS_2, GOLD_2)


def _draw_warden_cloak(
    c: Canvas,
    cx: int,
    torso_dy: int,
    role: str,
    pose: str,
) -> None:
    """Flowing obsidian (citizen/warrior) or royal crimson (leader) cloak behind the body."""
    c0, c1, c2, trim = _warden_cloak_colors(role)
    y0 = 12 + torso_dy

    if pose in ("stand", "stand_sway"):
        sway = -1 if pose == "stand_sway" else 0
        # Symmetrical stately cloak framing both sides of the armored silhouette
        for dy in range(15):
            y = y0 + dy
            if y > 27:
                break
            w_left = 6 + (dy // 4) + (-sway if dy > 6 else 0)
            w_right = 5 + (dy // 5)
            # Left flowing wing
            for x in range(cx - w_left, cx - 2):
                col = c2 if x == cx - w_left + 1 else (c1 if x < cx - 4 else c0)
                if dy >= 13 and x <= cx - w_left + 2:
                    col = trim
                c.px(x, y, col)
            # Right wing (slightly tucked behind weapon on warrior/leader)
            r_lim = cx + (w_right if role == "citizen" else w_right - 1)
            for x in range(cx + 3, r_lim + 1):
                col = c2 if x == r_lim - 1 else c1
                if dy >= 13 and x >= r_lim - 1:
                    col = trim
                c.px(x, y, col)
            if dy == 14:
                c.px(cx - w_left + 1, y, trim)
    elif pose in ("walk_0", "walk_1", "walk_2", "walk_3"):
        # Billowing cloak streaming dynamically behind-left with smooth continuous wave ripple
        wave_map = {
            "walk_0": (9, 12, 0, -1.0),
            "walk_1": (7, 13, 1, 0.0),
            "walk_2": (10, 12, -1, 1.0),
            "walk_3": (8, 13, 0, 0.5),
        }
        reach, height, fold_phase, flutter = wave_map[pose]
        for dy in range(height):
            y = y0 + dy
            if y > 26:
                continue
            t = dy / max(1.0, float(height - 1))
            wave_curve = math.sin(t * math.pi) * flutter
            left_x = int(round(cx - 4 - reach * (0.25 + 0.75 * t) + wave_curve))
            right_x = cx - 1 + (2 if dy < 4 else 0)
            for x in range(max(1, left_x), right_x + 1):
                ridge = (x + dy + fold_phase) % 3 == 0
                if x <= left_x + 1 and dy >= height - 3:
                    col = trim
                elif ridge and x < cx - 4:
                    col = c2
                elif x < cx - 3:
                    col = c1
                else:
                    col = c0
                c.px(x, y, col)
        # Fluttering tail edge accent
        tx = max(1, cx - 4 - reach)
        ty = min(25, y0 + height - 1)
        c.px(tx, ty, trim)
        c.px(tx + 1, ty, c2)
    elif pose == "attack_windup":
        # Cloak swung slightly down & behind braced legs
        for dy in range(14):
            y = y0 + dy
            if y > 26:
                break
            lx = max(2, cx - 6 - (dy // 3))
            for x in range(lx, cx + 1):
                col = trim if (dy >= 12 and x <= lx + 1) else (c2 if x == lx + 1 else c1)
                c.px(x, y, col)
    elif pose == "attack_lunge":
        # Cloak whipped fiercely straight back-left by forward lunge!
        for dy in range(11):
            y = y0 + dy - (1 if dy > 6 else 0)
            lx = max(1, cx - 5 - int(dy * 0.65))
            rx = cx - 2
            for x in range(lx, rx + 1):
                if dy >= 8 and x <= lx + 1:
                    col = trim
                elif (x + dy) % 2 == 0 and x <= lx + 3:
                    col = c2
                else:
                    col = c1 if x < cx - 3 else c0
                c.px(x, y, col)
    elif pose in ("happy_0", "happy_1"):
        # Cheerfully flared cloak (extra lifted during hop on happy_1)
        flare = 2 if pose == "happy_1" else 1
        for dy in range(13):
            y = y0 + dy
            if y > 26:
                break
            wl = 6 + (dy // 3) + (flare if dy > 5 else 0)
            wr = 5 + (dy // 4) + (1 if pose == "happy_1" and dy > 5 else 0)
            for x in range(max(1, cx - wl), cx - 2):
                col = trim if (dy >= 11 and x <= cx - wl + 1) else (c2 if x == cx - wl + 1 else c1)
                c.px(x, y, col)
            for x in range(cx + 3, min(30, cx + wr + 1)):
                col = trim if (dy >= 11 and x >= cx + wr - 1) else (c2 if x == cx + wr - 1 else c1)
                c.px(x, y, col)
    elif pose in ("swim_0", "swim_1"):
        # Upper mantle shoulders floating above the waterline
        for dy in range(8):
            y = y0 + dy
            if y >= 21:
                break
            wl = 6 + (dy // 3)
            wr = 5 + (dy // 4)
            for x in range(max(2, cx - wl), cx - 2):
                c.px(x, y, c2 if x == cx - wl + 1 else c1)
            for x in range(cx + 3, min(29, cx + wr)):
                c.px(x, y, c2 if x == cx + wr - 2 else c1)


def _draw_armored_leg(
    c: Canvas,
    hip_x: int,
    hip_y: int,
    knee_x: int,
    knee_y: int,
    foot_x: int,
    foot_y: int,
    bright: bool = True,
    boot_dir: int = 1,
    lifted_heel: bool = False,
) -> None:
    """Draw a crisp 3px-wide obsidian-and-gold armored leg with boot anchored exactly at foot_y."""
    d_col = OBS_1 if bright else OBS_0
    m_col = OBS_2 if bright else OBS_1
    hi_col = OBS_3 if bright else OBS_2
    g_col = GOLD_2 if bright else GOLD_1
    y_col = GOLD_3 if bright else GOLD_2
    sole_col = GOLD_0

    # Thigh (hip -> knee)
    steps_u = max(1, abs(knee_y - hip_y))
    for i in range(steps_u + 1):
        t = i / steps_u
        x = int(round(hip_x + (knee_x - hip_x) * t))
        y = int(round(hip_y + (knee_y - hip_y) * t))
        c.px(x - 1, y, d_col)
        c.px(x, y, m_col)
        c.px(x + 1, y, hi_col if boot_dir >= 0 else d_col)

    # Shin (knee -> ankle above foot_y)
    ankle_y = max(knee_y + 1, foot_y - 2)
    steps_l = max(1, abs(ankle_y - knee_y))
    for i in range(steps_l + 1):
        t = i / steps_l
        x = int(round(knee_x + (foot_x - knee_x) * t))
        y = int(round(knee_y + (ankle_y - knee_y) * t))
        c.px(x - 1, y, d_col)
        c.px(x, y, m_col)
        c.px(x + 1, y, hi_col if boot_dir >= 0 else d_col)

    # Gleaming golden knee guard
    c.px(knee_x - 1, knee_y, d_col)
    c.px(knee_x, knee_y, y_col)
    c.px(knee_x + 1, knee_y, g_col)

    # Armored boot cuff & toe (sole strictly anchored at foot_y!)
    if boot_dir > 0:
        # Right-facing stride boot
        c.px(foot_x - 1, foot_y - 2, g_col)
        c.px(foot_x, foot_y - 2, m_col)
        c.px(foot_x + 1, foot_y - 2, g_col)
        c.px(foot_x - 1, foot_y - 1, g_col)
        c.px(foot_x, foot_y - 1, d_col)
        c.px(foot_x + 1, foot_y - 1, m_col)
        c.px(foot_x + 2, foot_y - 1, g_col)
        if lifted_heel:
            c.px(foot_x - 1, foot_y - 1, sole_col)
            c.hline(foot_x, foot_x + 2, foot_y, sole_col)
        else:
            c.hline(foot_x - 1, foot_x + 2, foot_y, sole_col)
    elif boot_dir < 0:
        # Left-angled braced boot
        c.px(foot_x - 1, foot_y - 2, g_col)
        c.px(foot_x, foot_y - 2, m_col)
        c.px(foot_x + 1, foot_y - 2, g_col)
        c.px(foot_x - 2, foot_y - 1, g_col)
        c.px(foot_x - 1, foot_y - 1, m_col)
        c.px(foot_x, foot_y - 1, d_col)
        c.px(foot_x + 1, foot_y - 1, g_col)
        c.hline(foot_x - 2, foot_x + 1, foot_y, sole_col)
    else:
        # Symmetrical grounded standing boot
        c.px(foot_x - 1, foot_y - 2, g_col)
        c.px(foot_x, foot_y - 2, d_col)
        c.px(foot_x + 1, foot_y - 2, g_col)
        c.px(foot_x - 1, foot_y - 1, g_col)
        c.px(foot_x, foot_y - 1, m_col)
        c.px(foot_x + 1, foot_y - 1, g_col)
        c.hline(foot_x - 1, foot_x + 1, foot_y, sole_col)


def _draw_warden_legs(c: Canvas, cx: int, pose: str) -> None:
    """Draw Warden legs with exact ground pivot at y=28 (outline at y=29) for zero jitter."""
    gy = 28
    if pose == "stand":
        # Crisp symmetrical grounded Sentinel stance
        _draw_armored_leg(c, cx - 2, 20, cx - 2, 23, cx - 2, gy, bright=True, boot_dir=0)
        _draw_armored_leg(c, cx + 2, 20, cx + 2, 23, cx + 2, gy, bright=True, boot_dir=0)
    elif pose == "hop":
        # Joyful 2px hop off ground (soles at y=26, slightly kicked out)
        _draw_armored_leg(c, cx - 2, 18, cx - 3, 21, cx - 3, 26, bright=True, boot_dir=-1)
        _draw_armored_leg(c, cx + 2, 18, cx + 3, 21, cx + 3, 26, bright=True, boot_dir=1)
    elif pose == "walk_0":
        # Wide Stride A: Left/far leg pushing back-left, Right/near leg striding boldly forward-right
        _draw_armored_leg(c, cx - 1, 20, cx - 3, 23, cx - 5, gy, bright=False, boot_dir=1, lifted_heel=True)
        _draw_armored_leg(c, cx + 1, 20, cx + 3, 23, cx + 4, gy, bright=True, boot_dir=1)
    elif pose == "walk_1":
        # Passing A: Near leg planted straight down center, Far leg tucked/lifted behind
        _draw_armored_leg(c, cx - 2, 20, cx - 3, 23, cx - 4, 26, bright=False, boot_dir=1, lifted_heel=True)
        _draw_armored_leg(c, cx, 20, cx, 23, cx, gy, bright=True, boot_dir=1)
    elif pose == "walk_2":
        # Wide Stride B (opposite!): Far leg striding forward-right, Near leg kicking back-left
        _draw_armored_leg(c, cx + 1, 20, cx + 3, 23, cx + 4, gy, bright=False, boot_dir=1)
        _draw_armored_leg(c, cx - 1, 20, cx - 3, 23, cx - 5, gy, bright=True, boot_dir=1, lifted_heel=True)
    elif pose == "walk_3":
        # Passing B (opposite!): Far leg planted straight down center, Near leg lifted forward-right
        _draw_armored_leg(c, cx, 20, cx, 23, cx, gy, bright=False, boot_dir=1)
        _draw_armored_leg(c, cx + 1, 20, cx + 3, 23, cx + 3, 26, bright=True, boot_dir=1)
    elif pose == "attack_windup":
        # Braced combat stance shifted slightly back-left
        _draw_armored_leg(c, cx - 2, 20, cx - 3, 23, cx - 3, gy, bright=False, boot_dir=-1)
        _draw_armored_leg(c, cx + 1, 20, cx + 2, 23, cx + 2, gy, bright=True, boot_dir=1)
    elif pose == "attack_lunge":
        # Dynamic deep forward lunge!
        _draw_armored_leg(c, cx - 2, 20, cx - 4, 23, cx - 5, gy, bright=False, boot_dir=1, lifted_heel=True)
        _draw_armored_leg(c, cx + 2, 20, cx + 4, 23, cx + 4, gy, bright=True, boot_dir=1)


def _draw_warden_torso(
    c: Canvas,
    cx: int,
    torso_dy: int,
    role: str,
    lean_right: bool = False,
) -> None:
    """Obsidian plate cuirass, glowing golden chest sigil, ornate waist belt & role pauldrons."""
    ox = cx - 6 + (1 if lean_right else 0)
    oy = 10 + torso_dy

    if role == "citizen":
        rows = [
            ".gg..agga..gg",
            "gyygdssssdgyg",
            "ylsddgwwgddsl",
            "dlsdsgwwgsdsl",
            "dddsdsgagsddd",
            "..dsdssssds..",
            "..ddggggdd...",
            "..dgywwygd...",
            "...dsgagsd...",
            "...dsggsd....",
        ]
    elif role == "warrior":
        rows = [
            "ggg..agga..gg",
            "ywwgdssssdgwy",
            "lssddgwwwgdss",
            "lssdsgwwwgsds",
            "dddsdsgagsddd",
            "..dsdssssds..",
            "..ddggggdd...",
            "..dgywwygd...",
            "...dsgagsd...",
            "...dsggsd....",
        ]
    else:  # leader
        rows = [
            "ggg..agga..gg",
            "ywwgdssssdgwy",
            "ryssddgwgddsr",
            "rcdsdsgwgsdsr",
            "dddsdsgagsddd",
            "..gsdssssdg..",
            "..ddggggdd...",
            "..dgywwygd...",
            "...dsgagsd...",
            "...dsggsd....",
        ]
    c.stamp(rows, WARDEN_ANIM_PAL, ox=ox, oy=oy)


def _draw_warden_helm(
    c: Canvas,
    cx: int,
    head_dy: int,
    role: str,
    bright_visor: bool = False,
    head_dx: int = 0,
    happy_tilt: bool = False,
) -> None:
    """Crisp Sentinel helm (citizen crest, warrior twin horns, leader 3-point golden crown)."""
    ox = cx - 4 + head_dx

    if role == "citizen":
        oy = max(1, 2 + head_dy)
        v_hi = "x" if bright_visor else "w"
        v_mid = "w" if bright_visor else "y"
        if happy_tilt:
            rows = [
                "...gg....",
                "..gyyg...",
                ".gdlldg..",
                "KdlhhldK.",
                f"gdK{v_hi}{v_hi}Kdg.",
                f"gdg{v_mid}{v_mid}gdg.",
                ".gdggdg..",
                "..agga...",
            ]
        else:
            rows = [
                "...gg....",
                "..gyyg...",
                ".gdlldg..",
                "KdlhhldK.",
                "KdssssdK.",
                f"gdK{v_hi}{v_hi}Kdg.",
                f"gdg{v_mid}{v_mid}gdg.",
                ".gdggdg..",
            ]
        c.stamp(rows, WARDEN_ANIM_PAL, ox=ox, oy=oy)
    elif role == "warrior":
        oy = max(1, 2 + head_dy)
        v_core = "x" if bright_visor else "q"
        v_glow = "q" if bright_visor else "p"
        if happy_tilt:
            rows = [
                ".g...g...",
                "gy...yg..",
                "gdlhhdg..",
                f"dK{v_glow}{v_core}{v_glow}Kd..",
                f"dg{v_core}{v_glow}{v_core}gd..",
                ".gdggdg..",
                "..agga...",
            ]
        else:
            rows = [
                ".g...g...",
                "gy...yg..",
                "gdllldg..",
                "dlhhhld..",
                "dsssssd..",
                f"dK{v_glow}{v_core}{v_glow}Kd..",
                f"dg{v_core}{v_glow}{v_core}gd..",
                ".gdggdg..",
            ]
        c.stamp(rows, WARDEN_ANIM_PAL, ox=ox, oy=oy)
    else:  # leader
        oy = max(1, 2 + head_dy)
        v_hi = "x" if bright_visor else "w"
        cr_tip = "x" if bright_visor else "w"
        if happy_tilt:
            rows = [
                f"{cr_tip}..{cr_tip}..{cr_tip}..",
                "gygpgyg..",
                "gywywyg..",
                "gdlhldg..",
                f"gdK{v_hi}Kdg..",
                "gdgwgdf..",
                ".gdggd...",
            ]
        else:
            rows = [
                f"{cr_tip}..{cr_tip}..{cr_tip}..",
                "gygpgyg..",
                "gywywyg..",
                "gdlhldg..",
                "gdsssdg..",
                f"gdK{v_hi}Kdg..",
                "gdgwgdf..",
                ".gdggd...",
            ]
        rows = [r.replace("f", "g") for r in rows]
        c.stamp(rows, WARDEN_ANIM_PAL, ox=ox, oy=oy)


def _draw_gauntlet_arm(
    c: Canvas,
    x0: int,
    y0: int,
    x1: int,
    y1: int,
    bright: bool = True,
) -> None:
    """Draw a 2px-thick armored obsidian-and-gold arm terminating in a golden gauntlet fist."""
    d_col = OBS_1 if bright else OBS_0
    m_col = OBS_2 if bright else OBS_1
    hi_col = OBS_3 if bright else OBS_2
    g_col = GOLD_2 if bright else GOLD_1
    y_col = GOLD_3 if bright else GOLD_2

    steps = max(1, max(abs(x1 - x0), abs(y1 - y0)))
    for i in range(steps + 1):
        t = i / steps
        x = int(round(x0 + (x1 - x0) * t))
        y = int(round(y0 + (y1 - y0) * t))
        c.px(x, y, m_col if i < steps - 1 else g_col)
        c.px(x + 1, y, hi_col if bright else d_col)
    # Golden gauntlet fist cuff
    c.px(x1, y1, y_col)
    c.px(x1 + 1, y1, g_col)
    c.px(x1, y1 + 1, g_col)
    c.px(x1 + 1, y1 + 1, d_col)


def _draw_warrior_halberd(
    c: Canvas,
    pose: str,
    dy: int = 0,
    glint: bool = False,
) -> None:
    """Tall gold-edged steel crescent halberd across idle, walk, swim, attack, and happy poses."""
    tip_col = WARDEN_ANIM_PAL["x"] if glint else STL_4

    if pose == "idle":
        sx = 22
        ty = max(1, 2 + dy)
        # Vertical golden shaft
        c.vline(sx, ty + 6, 28, GOLD_2)
        c.vline(sx + 1, ty + 6, 28, GOLD_1)
        c.px(sx, 28, GOLD_0)
        # Tall spear point
        c.px(sx, ty, tip_col)
        c.fill_rect(sx - 1, ty + 1, sx + 1, ty + 2, STL_3)
        c.px(sx, ty + 1, STL_4)
        c.fill_rect(sx - 1, ty + 3, sx, ty + 6, STL_2)
        c.vline(sx - 1, ty + 3, ty + 6, STL_3)
        # Crescent axe blade curving cleanly right
        blade = [
            "gg...",
            "gywg.",
            "gtetg",
            "gtemg",
            "gywg.",
            "gbg..",
        ]
        c.stamp(blade, WARDEN_ANIM_PAL, ox=sx + 1, oy=ty + 2)
    elif pose in ("walk_0", "walk_1", "walk_2", "walk_3"):
        # Angled charging carry! Forward tilt slightly stronger on contact frames 0 & 2
        tilt = 1 if pose in ("walk_0", "walk_2") else 0
        bob = 0 if pose in ("walk_0", "walk_2") else -1
        x_bot, y_bot = 17, 25 + bob
        x_top, y_top = 24 + tilt, 5 + bob
        c.line(x_bot, y_bot, x_top, y_top, GOLD_1)
        c.line(x_bot + 1, y_bot, x_top + 1, y_top, GOLD_3)
        # Spear tip (stays cleanly >= y=1)
        c.px(x_top + 1, max(1, y_top - 3), tip_col)
        c.px(x_top, max(2, y_top - 2), STL_3)
        c.px(x_top + 1, max(2, y_top - 2), STL_4)
        c.px(x_top, y_top - 1, STL_2)
        c.px(x_top + 1, y_top - 1, STL_3)
        # Gold-edged crescent axe blade facing forward-right
        blade = [
            ".gyg.",
            "gyetg",
            "gtemg",
            "gyetg",
            ".gbg.",
        ]
        c.stamp(blade, WARDEN_ANIM_PAL, ox=x_top + 1, oy=y_top - 1)
    elif pose == "swim":
        # Held angled cleanly up above the waves on the right
        x_bot, y_bot = 20, 20 + dy
        x_top, y_top = 24, 5 + dy
        c.line(x_bot, y_bot, x_top, y_top, GOLD_1)
        c.line(x_bot + 1, y_bot, x_top + 1, y_top, GOLD_3)
        c.px(x_top + 1, y_top - 2, tip_col)
        c.px(x_top, y_top - 1, STL_3)
        c.px(x_top + 1, y_top - 1, STL_4)
        blade = [
            "gyg.",
            "getg",
            "gemg",
            "gyg.",
        ]
        c.stamp(blade, WARDEN_ANIM_PAL, ox=x_top + 1, oy=y_top)
    elif pose == "attack_0":
        # Dramatic wind-up cleanly poised high behind-left, keeping visor 100% unobstructed!
        c.line(18, 21, 5, 6, GOLD_1)
        c.line(19, 21, 6, 6, GOLD_3)
        # Gleaming upward-poised spear tip & overhead crescent axe blade in open left space
        c.px(3, 3, tip_col)
        c.px(4, 4, STL_4)
        c.px(5, 5, STL_3)
        c.poly([(5, 5), (9, 2), (10, 6), (6, 10), (2, 8)], STL_2)
        c.line(4, 4, 9, 2, STL_4)
        c.line(2, 8, 5, 5, STL_3)
        c.line(5, 6, 8, 7, GOLD_3)
        c.px(9, 2, tip_col)
    elif pose == "attack_1":
        # Dramatic forward cleave! Shaft thrusts forward-right, gleaming blade clearly readable inside arc
        c.line(11, 14, 23, 18, GOLD_1)
        c.line(11, 13, 23, 17, GOLD_3)
        # Forward spear tip + broad gleaming crescent axe head
        c.px(26, 19, tip_col)
        c.px(25, 18, STL_4)
        c.px(24, 18, STL_3)
        c.poly([(19, 16), (24, 11), (26, 16), (22, 20)], STL_2)
        c.line(24, 11, 26, 16, STL_4)
        c.line(20, 15, 23, 13, GOLD_3)
        c.px(24, 12, WARDEN_ANIM_PAL["x"])
    elif pose in ("happy_0", "happy_1"):
        # Victorious high salute (tip cleanly at y >= 1 so top dark outline is preserved!)
        if pose == "happy_0":
            sx = 22
            c.vline(sx, 7, 26, GOLD_2)
            c.vline(sx + 1, 7, 26, GOLD_3)
            by = 2
            c.px(sx, by, tip_col)
            c.fill_rect(sx - 1, by + 1, sx + 1, by + 2, STL_3)
            c.px(sx, by + 1, STL_4)
            blade = [
                "gywg.",
                "gtetg",
                "gtemg",
                "gywg.",
                "gbg..",
            ]
            c.stamp(blade, WARDEN_ANIM_PAL, ox=sx + 1, oy=by + 1)
        else:
            # Tilted celebratory victory raise!
            c.line(20, 22, 23, 5, GOLD_2)
            c.line(21, 22, 24, 5, GOLD_3)
            c.px(24, 1, tip_col)
            c.fill_rect(23, 2, 25, 3, STL_3)
            c.px(24, 2, STL_4)
            blade = [
                "gywg.",
                "gtetg",
                "gtemg",
                "gywg.",
                "gbg..",
            ]
            c.stamp(blade, WARDEN_ANIM_PAL, ox=25, oy=2)


def _draw_leader_spear(
    c: Canvas,
    pose: str,
    dy: int = 0,
    glint: bool = False,
) -> None:
    """Ornate sovereign golden halberd-spear with glowing ruby core."""
    tip_col = WARDEN_ANIM_PAL["x"] if glint else STL_4

    if pose == "idle":
        sx = 23
        ty = max(1, 2 + dy)
        c.vline(sx, ty + 7, 28, GOLD_2)
        c.vline(sx + 1, ty + 7, 28, GOLD_1)
        c.px(sx, 28, GOLD_0)
        head = [
            "...e...",
            "..tet..",
            ".gteyg.",
            "gytetyg",
            ".gtmtg.",
            ".gypyg.",
            "gggyggg",
            "..gbg..",
        ]
        c.stamp(head, WARDEN_ANIM_PAL, ox=sx - 3, oy=ty)
        if glint:
            c.px(sx, ty, tip_col)
            c.px(sx, ty + 5, RED_4)
    elif pose in ("walk_0", "walk_1", "walk_2", "walk_3"):
        tilt = 1 if pose in ("walk_0", "walk_2") else 0
        bob = 0 if pose in ("walk_0", "walk_2") else -1
        x_bot, y_bot = 18, 25 + bob
        x_top, y_top = 24 + tilt, 6 + bob
        c.line(x_bot, y_bot, x_top, y_top, GOLD_1)
        c.line(x_bot + 1, y_bot, x_top + 1, y_top, GOLD_3)
        hy = max(1, y_top - 4)
        head = [
            "..e..",
            ".tet.",
            "gtytg",
            "gypyg",
            ".ggg.",
        ]
        c.stamp(head, WARDEN_ANIM_PAL, ox=x_top - 1, oy=hy)
        if glint:
            c.px(x_top + 1, hy, tip_col)
    elif pose == "swim":
        x_bot, y_bot = 20, 20 + dy
        x_top, y_top = 24, 6 + dy
        c.line(x_bot, y_bot, x_top, y_top, GOLD_1)
        c.line(x_bot + 1, y_bot, x_top + 1, y_top, GOLD_3)
        head = [
            "..e..",
            ".tet.",
            "gtytg",
            "gypyg",
            ".ggg.",
        ]
        c.stamp(head, WARDEN_ANIM_PAL, ox=x_top - 1, oy=y_top - 4)
    elif pose == "attack_0":
        # Drawn back cleanly high-left behind shoulder, crown & visor 100% unobstructed!
        c.line(18, 21, 6, 7, GOLD_1)
        c.line(19, 21, 7, 7, GOLD_3)
        head = [
            "e....",
            "te...",
            "gty..",
            ".gypg",
            "..ggg",
        ]
        c.stamp(head, WARDEN_ANIM_PAL, ox=2, oy=3)
        c.px(2, 3, tip_col)
        c.px(4, 6, RED_4)
    elif pose == "attack_1":
        # Commanding forward golden spear thrust/cleave
        c.line(11, 15, 22, 16, GOLD_1)
        c.line(11, 14, 22, 15, GOLD_3)
        # Winged golden spearhead thrust right (clearly readable inside the radiant arc!)
        c.poly([(20, 12), (27, 15), (20, 19), (22, 15)], GOLD_3)
        c.line(21, 15, 27, 15, tip_col)
        c.px(23, 15, RED_3)
        c.px(22, 14, GOLD_4)
        c.px(21, 12, GOLD_4)
        c.px(21, 18, GOLD_2)
    elif pose in ("happy_0", "happy_1"):
        if pose == "happy_0":
            sx = 23
            c.vline(sx, 7, 26, GOLD_2)
            c.vline(sx + 1, 7, 26, GOLD_3)
            head = [
                "..x..",
                ".tet.",
                "gywyg",
                "gypyg",
                "ggggg",
            ]
            c.stamp(head, WARDEN_ANIM_PAL, ox=sx - 2, oy=2)
        else:
            # Tilted celebratory royal staff salute with full top margin!
            c.line(20, 22, 23, 6, GOLD_2)
            c.line(21, 22, 24, 6, GOLD_3)
            head = [
                "..x..",
                ".tet.",
                "gywyg",
                "gypyg",
                "ggggg",
            ]
            c.stamp(head, WARDEN_ANIM_PAL, ox=22, oy=1)


def _draw_slash_arc(c: Canvas, golden_sovereign: bool = False) -> None:
    """Luminous continuous tapered crescent slash arc sweeping cleanly around attack_1."""
    hot = WARDEN_ANIM_PAL["x"]
    trail_col = RED_3 if golden_sovereign else GOLD_2
    edge_col = GOLD_3 if not golden_sovereign else GOLD_4

    # Smooth continuous elliptical crescent (cx=17.5, cy=14.5, rx=12.5, ry=10.8) across right arc
    cx_a, cy_a = 17.5, 14.5
    for y in range(3, 27):
        for x in range(21, 31):
            nx = (x - cx_a) / 12.5
            ny = (y - cy_a) / 10.8
            d = math.hypot(nx, ny)
            ang = math.degrees(math.atan2(y - cy_a, x - cx_a))
            if -64.0 <= ang <= 64.0:
                # Taper width smoothly toward razor tips at +/-64 deg
                taper = math.cos(math.radians(ang * (90.0 / 68.0)))
                inner_r = 1.0 - (0.24 * taper + 0.04)
                if inner_r <= d <= 1.02:
                    if d >= 0.92:
                        c.px(x, y, hot if abs(ang) < 42.0 else GOLD_4)
                    elif d >= 0.84:
                        c.px(x, y, edge_col)
                    else:
                        c.px(x, y, trail_col)

    # Crisp impact glint star on the cleave apex
    c.px(29, 15, hot)
    c.px(30, 15, hot)
    c.px(28, 15, hot)
    c.px(29, 14, hot)
    c.px(29, 16, hot)


def _draw_water_ripples(c: Canvas, frame: int) -> None:
    """Crisp animated cyan-and-white water ripples across y=20..25 for swim frames."""
    if frame == 0:
        rows = [
            ".......f...........ff...........",
            "....ffzzzz..ffff..zzzzff........",
            "..fzvvuuvvzzffffzzvvuuuvzf......",
            "...WuuuvvvzzzzzzzzvvvuuuW.......",
            ".....WWuuvvzzzzvvuuWW...........",
            "........WWWWWWWWWWWW............",
        ]
    else:
        rows = [
            "..........ff...........f........",
            "...f..zzzzffff..zzzzff..........",
            ".ffzzvuuuvvzzzzzzvuuuvvzzf......",
            "..WuuuvvvzzffffffzzvvvuuuW......",
            "....WWuuuvvvzzzzvvvuuuWW........",
            ".......WWWWWWWWWWWWWW...........",
        ]
    c.stamp(rows, WARDEN_ANIM_PAL, ox=0, oy=20)
    if frame == 0:
        c.px(6, 18, BLU_4)
        c.px(25, 19, BLU_3)
    else:
        c.px(5, 19, BLU_3)
        c.px(26, 18, BLU_4)


def _draw_sparkle(c: Canvas, x: int, y: int, big: bool = False) -> None:
    """Crisp warm golden cheer sparkle star."""
    hot = WARDEN_ANIM_PAL["x"]
    if big:
        c.px(x, y, hot)
        c.px(x - 1, y, GOLD_3)
        c.px(x + 1, y, GOLD_3)
        c.px(x, y - 1, GOLD_3)
        c.px(x, y + 1, GOLD_3)
        c.px(x, y - 2, GOLD_4)
    else:
        c.px(x, y, hot)
        c.px(x - 1, y, GOLD_2)
        c.px(x + 1, y, GOLD_2)
        c.px(x, y - 1, GOLD_3)
        c.px(x, y + 1, GOLD_3)


def make_warden_anim_frame(role: str, anim: str, frame: int) -> Canvas:
    """Render one 32x32 RGBA animation frame for a Warden Sentinel role."""
    c = Canvas(32, 32)
    cx = 15 if role == "citizen" else 14

    if anim == "idle":
        dy = -1 if frame == 1 else 0
        _draw_warden_cloak(c, cx, dy, role, "stand_sway" if frame == 1 else "stand")
        _draw_warden_legs(c, cx, "stand")
        _draw_warden_torso(c, cx, dy, role, lean_right=False)
        _draw_gauntlet_arm(c, cx - 5, 13 + dy, cx - 6, 19 + dy, bright=False)
        if role == "citizen":
            _draw_gauntlet_arm(c, cx + 5, 13 + dy, cx + 6, 19 + dy, bright=True)
        else:
            _draw_gauntlet_arm(c, cx + 5, 13 + dy, cx + 7, 17 + dy, bright=True)
        _draw_warden_helm(c, cx, dy, role, bright_visor=(frame == 1))
        if role == "warrior":
            _draw_warrior_halberd(c, "idle", dy=dy, glint=(frame == 1))
        elif role == "leader":
            _draw_leader_spear(c, "idle", dy=dy, glint=(frame == 1))
        c.add_outline(OUTLINE_DARK)
        return c

    if anim == "walk":
        pose = f"walk_{frame}"
        torso_dy = 0 if frame in (0, 2) else -1
        head_dx = 1 if frame in (0, 2) else 0
        _draw_warden_cloak(c, cx, torso_dy, role, pose)
        _draw_warden_legs(c, cx, pose)
        _draw_warden_torso(c, cx, torso_dy, role, lean_right=True)

        # Swinging gauntlets opposite to legs!
        if role == "citizen":
            if frame == 0:
                # Left arm back, Right arm forward-right
                _draw_gauntlet_arm(c, cx - 4, 13 + torso_dy, cx - 7, 18 + torso_dy, bright=False)
                _draw_gauntlet_arm(c, cx + 5, 13 + torso_dy, cx + 7, 17 + torso_dy, bright=True)
            elif frame == 1:
                _draw_gauntlet_arm(c, cx - 4, 13 + torso_dy, cx - 5, 19 + torso_dy, bright=False)
                _draw_gauntlet_arm(c, cx + 4, 13 + torso_dy, cx + 5, 19 + torso_dy, bright=True)
            elif frame == 2:
                # Opposite swing! Left/far arm forward-right, Right/near arm swinging back-left!
                _draw_gauntlet_arm(c, cx + 5, 13 + torso_dy, cx + 7, 16 + torso_dy, bright=False)
                _draw_gauntlet_arm(c, cx - 4, 13 + torso_dy, cx - 6, 19 + torso_dy, bright=True)
            else:
                _draw_gauntlet_arm(c, cx - 4, 13 + torso_dy, cx - 5, 19 + torso_dy, bright=False)
                _draw_gauntlet_arm(c, cx + 4, 13 + torso_dy, cx + 6, 18 + torso_dy, bright=True)
        else:
            l_end_x = cx - 7 if frame == 0 else (cx - 4 if frame == 2 else cx - 6)
            l_end_y = 18 + torso_dy if frame in (0, 2) else 19 + torso_dy
            _draw_gauntlet_arm(c, cx - 4, 13 + torso_dy, l_end_x, l_end_y, bright=False)
            _draw_gauntlet_arm(c, cx + 5, 13 + torso_dy, cx + 7, 16 + torso_dy, bright=True)

        _draw_warden_helm(c, cx, torso_dy, role, bright_visor=(frame == 2), head_dx=head_dx)
        if role == "warrior":
            _draw_warrior_halberd(c, pose, glint=(frame == 0))
        elif role == "leader":
            _draw_leader_spear(c, pose, glint=(frame == 0))
        c.add_outline(OUTLINE_DARK)
        return c

    if anim == "swim":
        bob_dy = 3 if frame == 0 else 2
        _draw_warden_cloak(c, cx, bob_dy, role, f"swim_{frame}")
        _draw_warden_torso(c, cx, bob_dy, role, lean_right=(frame == 1))
        if role == "citizen":
            if frame == 0:
                _draw_gauntlet_arm(c, cx - 5, 13 + bob_dy, cx - 7, 19 + bob_dy, bright=False)
                _draw_gauntlet_arm(c, cx + 5, 13 + bob_dy, cx + 8, 17 + bob_dy, bright=True)
            else:
                _draw_gauntlet_arm(c, cx - 5, 13 + bob_dy, cx - 8, 17 + bob_dy, bright=False)
                _draw_gauntlet_arm(c, cx + 5, 13 + bob_dy, cx + 7, 19 + bob_dy, bright=True)
        else:
            _draw_gauntlet_arm(c, cx - 5, 13 + bob_dy, cx - 7, 18 + bob_dy, bright=False)
            _draw_gauntlet_arm(c, cx + 5, 13 + bob_dy, cx + 7, 16 + bob_dy, bright=True)

        _draw_warden_helm(c, cx, bob_dy, role, bright_visor=(frame == 1))
        if role == "warrior":
            _draw_warrior_halberd(c, "swim", dy=(1 if frame == 0 else 0), glint=(frame == 1))
        elif role == "leader":
            _draw_leader_spear(c, "swim", dy=(1 if frame == 0 else 0), glint=(frame == 1))

        # Cleanly submerge lower body below waterline y=21
        for y in range(21, 32):
            for x in range(32):
                c.px(x, y, TRANSPARENT)

        c.add_outline(OUTLINE_DARK)
        _draw_water_ripples(c, frame)
        return c

    if anim == "attack":
        if frame == 0:
            # Coiled wind-up: draw weapon cleanly off left shoulder behind torso/helm so visor shines!
            acx = cx + 2
            _draw_warden_cloak(c, acx, 0, role, "attack_windup")
            if role == "warrior":
                _draw_warrior_halberd(c, "attack_0", glint=True)
            else:
                _draw_leader_spear(c, "attack_0", glint=True)
            _draw_warden_legs(c, acx, "attack_windup")
            _draw_warden_torso(c, acx, 0, role, lean_right=False)
            _draw_gauntlet_arm(c, acx - 5, 13, acx - 7, 11, bright=False)
            _draw_gauntlet_arm(c, acx + 4, 13, acx + 1, 16, bright=True)
            _draw_warden_helm(c, acx, 0, role, bright_visor=True, head_dx=-1)
            c.add_outline(OUTLINE_DARK)
            return c
        else:
            # Explosive forward cleave / thrust + radiant golden-white slash arc!
            _draw_warden_cloak(c, cx, 0, role, "attack_lunge")
            _draw_warden_legs(c, cx, "attack_lunge")
            _draw_warden_torso(c, cx, 0, role, lean_right=True)
            _draw_gauntlet_arm(c, cx - 4, 13, cx - 2, 16, bright=False)
            _draw_gauntlet_arm(c, cx + 5, 13, cx + 7, 16, bright=True)
            _draw_warden_helm(c, cx, 0, role, bright_visor=True, head_dx=1)
            if role == "warrior":
                _draw_warrior_halberd(c, "attack_1", glint=True)
            else:
                _draw_leader_spear(c, "attack_1", glint=True)
            c.add_outline(OUTLINE_DARK)
            _draw_slash_arc(c, golden_sovereign=(role == "leader"))
            return c

    if anim == "happy":
        hop_dy = -2 if frame == 1 else 0
        _draw_warden_cloak(c, cx, hop_dy, role, "happy_1" if frame == 1 else "happy_0")
        _draw_warden_legs(c, cx, "hop" if frame == 1 else "stand")
        _draw_warden_torso(c, cx, hop_dy, role, lean_right=False)

        if role == "citizen":
            if frame == 0:
                _draw_gauntlet_arm(c, cx - 5, 13, cx - 6, 18, bright=False)
                _draw_gauntlet_arm(c, cx + 5, 13, cx + 8, 10, bright=True)
            else:
                _draw_gauntlet_arm(c, cx - 5, 11, cx - 8, 9, bright=False)
                _draw_gauntlet_arm(c, cx + 5, 11, cx + 8, 6, bright=True)
        else:
            if frame == 0:
                _draw_gauntlet_arm(c, cx - 5, 13, cx - 7, 16, bright=False)
                _draw_gauntlet_arm(c, cx + 5, 13, cx + 7, 11, bright=True)
            else:
                # Joyful raised victory fist + lifted halberd/staff!
                _draw_gauntlet_arm(c, cx - 5, 11, cx - 8, 7, bright=False)
                _draw_gauntlet_arm(c, cx + 5, 11, cx + 7, 9, bright=True)

        _draw_warden_helm(
            c,
            cx,
            hop_dy,
            role,
            bright_visor=True,
            happy_tilt=(frame == 1),
        )
        if role == "warrior":
            _draw_warrior_halberd(c, f"happy_{frame}", glint=(frame == 1))
        elif role == "leader":
            _draw_leader_spear(c, f"happy_{frame}", glint=(frame == 1))

        c.add_outline(OUTLINE_DARK)

        # Warm golden cheer sparkles cleanly in open air beside helm / crown / weapon
        if frame == 0:
            _draw_sparkle(c, cx - 7, 6, big=False)
        else:
            _draw_sparkle(c, cx - 9, 4, big=True)
            if role == "citizen":
                _draw_sparkle(c, cx + 10, 4, big=False)
            else:
                _draw_sparkle(c, 28, 7, big=False)
        return c

    raise ValueError(f"Unsupported animation state: {anim}")


def make_warden_anim_frames() -> list[tuple[str, Canvas]]:
    """Generate all 34 multi-frame Warden Sentinel animation sprites."""
    frames: list[tuple[str, Canvas]] = []
    specs: list[tuple[str, list[tuple[str, int]]]] = [
        ("citizen", [("idle", 2), ("walk", 4), ("swim", 2), ("happy", 2)]),
        ("warrior", [("idle", 2), ("walk", 4), ("swim", 2), ("attack", 2), ("happy", 2)]),
        ("leader", [("idle", 2), ("walk", 4), ("swim", 2), ("attack", 2), ("happy", 2)]),
    ]
    for role, anims in specs:
        for anim, count in anims:
            for idx in range(count):
                frames.append((f"{role}_{anim}_{idx}", make_warden_anim_frame(role, anim, idx)))
    return frames


# =============================================================================
# CONTACT SHEET RENDERER (for visual inspection)
# =============================================================================

FONT_3X5: dict[str, list[str]] = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"],
    "B": ["##.", "#.#", "##.", "#.#", "##."],
    "C": [".##", "#..", "#..", "#..", ".##"],
    "D": ["##.", "#.#", "#.#", "#.#", "##."],
    "E": ["###", "#..", "##.", "#..", "###"],
    "F": ["###", "#..", "##.", "#..", "#.."],
    "G": [".##", "#..", "#.#", "#.#", ".##"],
    "H": ["#.#", "#.#", "###", "#.#", "#.#"],
    "I": ["###", ".#.", ".#.", ".#.", "###"],
    "J": ["..#", "..#", "..#", "#.#", ".#."],
    "K": ["#.#", "#.#", "##.", "#.#", "#.#"],
    "L": ["#..", "#..", "#..", "#..", "###"],
    "M": ["#.#", "###", "###", "#.#", "#.#"],
    "N": ["###", "#.#", "#.#", "#.#", "#.#"],
    "O": [".#.", "#.#", "#.#", "#.#", ".#."],
    "P": ["##.", "#.#", "##.", "#..", "#.."],
    "Q": [".#.", "#.#", "#.#", "##.", "..#"],
    "R": ["##.", "#.#", "##.", "#.#", "#.#"],
    "S": [".##", "#..", ".#.", "..#", "##."],
    "T": ["###", ".#.", ".#.", ".#.", ".#."],
    "U": ["#.#", "#.#", "#.#", "#.#", "###"],
    "V": ["#.#", "#.#", "#.#", "#.#", ".#."],
    "W": ["#.#", "#.#", "###", "###", "#.#"],
    "X": ["#.#", "#.#", ".#.", "#.#", "#.#"],
    "Y": ["#.#", "#.#", ".#.", ".#.", ".#."],
    "Z": ["###", "..#", ".#.", "#..", "###"],
    "0": ["###", "#.#", "#.#", "#.#", "###"],
    "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["##.", "..#", ".#.", "#..", "###"],
    "3": ["##.", "..#", ".#.", "..#", "##."],
    "4": ["#.#", "#.#", "###", "..#", "..#"],
    "5": ["###", "#..", "##.", "..#", "##."],
    "6": [".##", "#..", "###", "#.#", "###"],
    "7": ["###", "..#", ".#.", ".#.", ".#."],
    "8": ["###", "#.#", "###", "#.#", "###"],
    "9": ["###", "#.#", "###", "..#", "##."],
    "_": ["...", "...", "...", "...", "###"],
    "-": ["...", "...", "###", "...", "..."],
    " ": ["...", "...", "...", "...", "..."],
}


def draw_label(canvas: Canvas, x: int, y: int, text: str, col: Color, scale: int = 2) -> None:
    cx = x
    for ch in text.upper():
        glyph = FONT_3X5.get(ch, FONT_3X5[" "])
        for gy, row in enumerate(glyph):
            for gx, bit in enumerate(row):
                if bit == "#":
                    canvas.fill_rect(
                        cx + gx * scale,
                        y + gy * scale,
                        cx + gx * scale + scale - 1,
                        y + gy * scale + scale - 1,
                        col,
                    )
        cx += 4 * scale


def render_contact_sheet(
    items: list[tuple[str, Canvas] | None],
    out_path: Path,
    zoom: int = 4,
    cols: int = 6,
    show_ground_ticks: bool = False,
) -> None:
    rows = math.ceil(len(items) / cols)
    tile_w = 32 * zoom
    tile_h = 32 * zoom
    pad_x = 18
    pad_y = 28
    margin = 16
    cell_w = tile_w + pad_x
    cell_h = tile_h + pad_y

    sheet_w = margin * 2 + cols * cell_w - pad_x
    sheet_h = margin * 2 + rows * cell_h - pad_y + 12
    sheet = Canvas(sheet_w, sheet_h)
    sheet.fill_rect(0, 0, sheet_w - 1, sheet_h - 1, (18, 20, 30, 255))

    for idx, entry in enumerate(items):
        if entry is None:
            continue
        label, img = entry
        col = idx % cols
        row = idx // cols
        ox = margin + col * cell_w
        oy = margin + row * cell_h

        # Subtle dark checkerboard inside each tile so transparency & dark outlines both read clearly
        for cy in range(0, tile_h, 8):
            for cx in range(0, tile_w, 8):
                chk = (38, 41, 60, 255) if ((cx // 8 + cy // 8) % 2 == 0) else (46, 50, 72, 255)
                sheet.fill_rect(ox + cx, oy + cy, ox + cx + 7, oy + cy + 7, chk)

        # Tile border
        border = (78, 84, 118, 255)
        sheet.hline(ox - 1, ox + tile_w, oy - 1, border)
        sheet.hline(ox - 1, ox + tile_w, oy + tile_h, border)
        sheet.vline(ox - 1, oy - 1, oy + tile_h, border)
        sheet.vline(ox + tile_w, oy - 1, oy + tile_h, border)

        # Optional ground-line reference tick at y=29 (bottom edge of feet outline)
        if show_ground_ticks:
            gy = oy + 29 * zoom + (zoom - 1)
            sheet.hline(ox - 5, ox - 2, gy, (248, 206, 74, 255))
            sheet.hline(ox + tile_w + 2, ox + tile_w + 5, gy, (248, 206, 74, 255))

        # 4x zoomed sprite
        zoomed = img.scale_nn(zoom)
        sheet.blit(zoomed, ox, oy)

        short_label = label.replace("btn_", "").replace("warden_", "W_")
        draw_label(sheet, ox, oy + tile_h + 6, short_label[:16], (224, 230, 248, 255), scale=2)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(sheet.to_png_bytes())


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate WorldBox Suite pixel-art sprites and icons.")
    parser.add_argument("--sheet", type=Path, default=DEFAULT_SHEET_PATH, help="Output path for icon contact sheet.")
    parser.add_argument(
        "--anim-sheet",
        type=Path,
        default=DEFAULT_ANIM_SHEET_PATH,
        help="Output path for 4x Warden animation + Dense Cities contact sheet.",
    )
    args = parser.parse_args()

    WARDEN_DIR.mkdir(parents=True, exist_ok=True)
    HUB_RES_DIR.mkdir(parents=True, exist_ok=True)

    wardens: list[tuple[str, Canvas]] = [
        ("warden_citizen", make_warden_citizen()),
        ("warden_warrior", make_warden_warrior()),
        ("warden_leader", make_warden_leader()),
        ("warden_icon", make_warden_icon()),
    ]
    warden_anims: list[tuple[str, Canvas]] = make_warden_anim_frames()

    buttons: list[tuple[str, Canvas]] = [
        ("btn_hub", make_btn_hub()),
        ("btn_scale", make_btn_scale()),
        ("btn_visual", make_btn_visual()),
        ("btn_war_matrix", make_btn_war_matrix()),
        ("btn_battle_radar", make_btn_battle_radar()),
        ("btn_world_builder", make_btn_world_builder()),
        ("btn_portal", make_btn_portal()),
        ("btn_apex", make_btn_apex()),
        ("btn_chronicle", make_btn_chronicle()),
        ("btn_relics", make_btn_relics()),
        ("btn_coop", make_btn_coop()),
        ("btn_modern", make_btn_modern()),
        ("btn_warden_drop", make_btn_warden_drop()),
        ("btn_animal_kingdoms", make_btn_animal_kingdoms()),
        ("btn_snake_mode", make_btn_snake_mode()),
        ("btn_always_day", make_btn_always_day()),
        ("btn_age_hope", make_btn_age_hope()),
        ("btn_war_ticker", make_btn_war_ticker()),
        ("btn_species_panel", make_btn_species_panel()),
        ("btn_dense_cities", make_btn_dense_cities()),
        ("btn_pangaea_drift", make_btn_pangaea_drift()),
        ("btn_tectonic_rift", make_btn_tectonic_rift()),
        ("btn_tectonic_speed", make_btn_tectonic_speed()),
        ("btn_espionage", make_btn_espionage()),
        ("btn_dispatch_spy", make_btn_dispatch_spy()),
        ("btn_armory_blessing", make_btn_armory_blessing()),
        ("btn_anoint_hero", make_btn_anoint_hero()),
        ("btn_merc_drop", make_btn_merc_drop()),
        ("btn_geopolitics", make_btn_geopolitics()),
        ("btn_highways_citadels", make_btn_highways_citadels()),
        ("btn_distinct_colors", make_btn_distinct_colors()),
        ("btn_warfare_suite", make_btn_warfare_suite()),
    ]

    for name, img in wardens + warden_anims:
        out = WARDEN_DIR / f"{name}.png"
        out.write_bytes(img.to_png_bytes())
        print(f"Wrote {out.relative_to(REPO_ROOT)} (32x32 RGBA)")

    for name, img in buttons:
        out = HUB_RES_DIR / f"{name}.png"
        out.write_bytes(img.to_png_bytes())
        print(f"Wrote {out.relative_to(REPO_ROOT)} (32x32 RGBA)")

    # Upgrade 64x64 mod icons for SuiteHub and ApexFactions to match
    hub_64 = dict(buttons)["btn_hub"].scale_nn(2)
    HUB_MOD_ICON.write_bytes(hub_64.to_png_bytes())
    print(f"Wrote {HUB_MOD_ICON.relative_to(REPO_ROOT)} (64x64 RGBA)")

    apex_64 = dict(buttons)["btn_apex"].scale_nn(2)
    APEX_MOD_ICON.write_bytes(apex_64.to_png_bytes())
    print(f"Wrote {APEX_MOD_ICON.relative_to(REPO_ROOT)} (64x64 RGBA)")

    # Generate 4x review contact sheets
    render_contact_sheet(wardens + buttons, args.sheet, zoom=4, cols=6)
    print(f"Wrote review sheet: {args.sheet}")

    # Structured 6x6 animation contact sheet (all 34 Warden frames + btn_dense_cities)
    anim_map = dict(warden_anims)
    btn_dense = dict(buttons)["btn_dense_cities"]
    anim_grid: list[tuple[str, Canvas] | None] = [
        ("citizen_idle_0", anim_map["citizen_idle_0"]),
        ("citizen_idle_1", anim_map["citizen_idle_1"]),
        ("citizen_walk_0", anim_map["citizen_walk_0"]),
        ("citizen_walk_1", anim_map["citizen_walk_1"]),
        ("citizen_walk_2", anim_map["citizen_walk_2"]),
        ("citizen_walk_3", anim_map["citizen_walk_3"]),
        ("citizen_swim_0", anim_map["citizen_swim_0"]),
        ("citizen_swim_1", anim_map["citizen_swim_1"]),
        ("citizen_happy_0", anim_map["citizen_happy_0"]),
        ("citizen_happy_1", anim_map["citizen_happy_1"]),
        None,
        ("btn_dense_cities", btn_dense),
        ("warrior_idle_0", anim_map["warrior_idle_0"]),
        ("warrior_idle_1", anim_map["warrior_idle_1"]),
        ("warrior_walk_0", anim_map["warrior_walk_0"]),
        ("warrior_walk_1", anim_map["warrior_walk_1"]),
        ("warrior_walk_2", anim_map["warrior_walk_2"]),
        ("warrior_walk_3", anim_map["warrior_walk_3"]),
        ("warrior_swim_0", anim_map["warrior_swim_0"]),
        ("warrior_swim_1", anim_map["warrior_swim_1"]),
        ("warrior_attack_0", anim_map["warrior_attack_0"]),
        ("warrior_attack_1", anim_map["warrior_attack_1"]),
        ("warrior_happy_0", anim_map["warrior_happy_0"]),
        ("warrior_happy_1", anim_map["warrior_happy_1"]),
        ("leader_idle_0", anim_map["leader_idle_0"]),
        ("leader_idle_1", anim_map["leader_idle_1"]),
        ("leader_walk_0", anim_map["leader_walk_0"]),
        ("leader_walk_1", anim_map["leader_walk_1"]),
        ("leader_walk_2", anim_map["leader_walk_2"]),
        ("leader_walk_3", anim_map["leader_walk_3"]),
        ("leader_swim_0", anim_map["leader_swim_0"]),
        ("leader_swim_1", anim_map["leader_swim_1"]),
        ("leader_attack_0", anim_map["leader_attack_0"]),
        ("leader_attack_1", anim_map["leader_attack_1"]),
        ("leader_happy_0", anim_map["leader_happy_0"]),
        ("leader_happy_1", anim_map["leader_happy_1"]),
    ]
    render_contact_sheet(anim_grid, args.anim_sheet, zoom=4, cols=6, show_ground_ticks=True)
    print(f"Wrote animation review sheet: {args.anim_sheet}")


if __name__ == "__main__":
    main()
