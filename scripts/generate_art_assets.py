"""Generate 32x32 transparent RGBA pixel-art sprites and toolbar icons for WorldBox Suite.

Pure Python standard library only (struct, zlib, math, pathlib).
"""

from __future__ import annotations

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


def render_contact_sheet(items: list[tuple[str, Canvas]], out_path: Path, zoom: int = 4) -> None:
    cols = 6
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

    for idx, (label, img) in enumerate(items):
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

        # 4x zoomed sprite
        zoomed = img.scale_nn(zoom)
        sheet.blit(zoomed, ox, oy)

        # 1x actual-size preview in top-right corner of cell frame? Keep tile clean, put 1x next to label or clean label below
        short_label = label.replace("btn_", "").replace("warden_", "W_")
        draw_label(sheet, ox, oy + tile_h + 6, short_label[:15], (224, 230, 248, 255), scale=2)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(sheet.to_png_bytes())


def main() -> None:
    WARDEN_DIR.mkdir(parents=True, exist_ok=True)
    HUB_RES_DIR.mkdir(parents=True, exist_ok=True)

    wardens: list[tuple[str, Canvas]] = [
        ("warden_citizen", make_warden_citizen()),
        ("warden_warrior", make_warden_warrior()),
        ("warden_leader", make_warden_leader()),
        ("warden_icon", make_warden_icon()),
    ]

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
    ]

    for name, img in wardens:
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

    # Generate 4x review contact sheet
    render_contact_sheet(wardens + buttons, DEFAULT_SHEET_PATH, zoom=4)
    print(f"Wrote review sheet: {DEFAULT_SHEET_PATH}")


if __name__ == "__main__":
    main()
