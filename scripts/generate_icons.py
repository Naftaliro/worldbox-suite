"""Generate 64x64 Catppuccin Mocha pixel-art icons for all WorldBox Suite mods and toolbar buttons."""

from __future__ import annotations

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO_ROOT))

from tools.wbox.render import encode_png_bytes

# Catppuccin Mocha RGB palette
PAL = {
    ".": (17, 17, 27),      # Crust
    "b": (30, 30, 46),      # Base
    "s": (49, 50, 68),      # Surface0
    "l": (69, 71, 90),      # Surface1
    "t": (205, 214, 244),   # Text
    "m": (203, 166, 247),   # Mauve
    "v": (180, 190, 254),   # Lavender
    "u": (137, 180, 250),   # Blue
    "c": (116, 199, 236),   # Sapphire
    "e": (148, 226, 213),   # Teal
    "g": (166, 227, 161),   # Green
    "y": (249, 226, 175),   # Yellow
    "p": (250, 179, 135),   # Peach
    "r": (243, 139, 168),   # Red
}

# 16x16 pixel templates upscaled cleanly to 64x64 (4x nearest-neighbor)
TEMPLATES: dict[str, list[str]] = {
    "SuiteHub": [
        "................",
        ".bbbbbbbbbbbbbb.",
        ".bssssssssssssb.",
        ".bs....mm....sb.",
        ".bs...mmmm...sb.",
        ".bs.v..mm..v.sb.",
        ".bs.vvvmmvvv.sb.",
        ".bs..vvvvvv..sb.",
        ".bs..vyyyyv..sb.",
        ".bs..vvvvvv..sb.",
        ".bs.vvvmmvvv.sb.",
        ".bs.v..mm..v.sb.",
        ".bs...mmmm...sb.",
        ".bssssssssssssb.",
        ".bbbbbbbbbbbbbb.",
        "................",
    ],
    "CoreScale": [
        "................",
        ".bbbbbbbbbbbbbb.",
        ".bssssssssssssb.",
        ".bsggg....gggsb.",
        ".bsg........gsb.",
        ".bsg..eeee..gsb.",
        ".bs...e..e...sb.",
        ".bs..ye..ey..sb.",
        ".bs..yyyyyy..sb.",
        ".bs...e..e...sb.",
        ".bsg..eeee..gsb.",
        ".bsg........gsb.",
        ".bsggg....gggsb.",
        ".bssssssssssssb.",
        ".bbbbbbbbbbbbbb.",
        "................",
    ],
    "VisualPolish": [
        "................",
        ".bbbbbbbbbbbbbb.",
        ".bssssssssssssb.",
        ".bs...yy.....sb.",
        ".bs..yyyy.v..sb.",
        ".bs.yyppyyvv.sb.",
        ".bs.yppppyvv.sb.",
        ".bs..yyyyvvv.sb.",
        ".bs...yyvvvv.sb.",
        ".bs..cccmmmm.sb.",
        ".bs.ccccmmmmm.b.",
        ".bs..ccccmmm.sb.",
        ".bs...ccccc..sb.",
        ".bssssssssssssb.",
        ".bbbbbbbbbbbbbb.",
        "................",
    ],
    "GrandWarfare": [
        "................",
        ".bbbbbbbbbbbbbb.",
        ".bssssssssssssb.",
        ".bsrr......rrsb.",
        ".bs.rr....rr.sb.",
        ".bs..rr..rr..sb.",
        ".bs...rrrr...sb.",
        ".bs....pp....sb.",
        ".bs...rrrr...sb.",
        ".bs..rr..rr..sb.",
        ".bs.yrr..rry.sb.",
        ".bsyy......yysb.",
        ".bs.y......y.sb.",
        ".bssssssssssssb.",
        ".bbbbbbbbbbbbbb.",
        "................",
    ],
    "LinkedContinents": [
        "................",
        ".bbbbbbbbbbbbbb.",
        ".bssssssssssssb.",
        ".bsgggguuugggsb.",
        ".bsggguuuugggsb.",
        ".bsggguuuugggsb.",
        ".bsgguucuuuggsb.",
        ".bsgguccccuggsb.",
        ".bsgguucuuuggsb.",
        ".bsggguuuugggsb.",
        ".bsggguuuugggsb.",
        ".bsggguuuugggsb.",
        ".bsgggguuugggsb.",
        ".bssssssssssssb.",
        ".bbbbbbbbbbbbbb.",
        "................",
    ],
    "EraChronicle": [
        "................",
        ".bbbbbbbbbbbbbb.",
        ".bssssssssssssb.",
        ".bs..yyyyyy..sb.",
        ".bs.yymmmmyy.sb.",
        ".bs.ymttttmy.sb.",
        ".bs.ymtmmtmy.sb.",
        ".bs.ymttttmy.sb.",
        ".bs.ymtmmtmy.sb.",
        ".bs.ymttttmy.sb.",
        ".bs.yymmmmyy.sb.",
        ".bs..yyyyyy..sb.",
        ".bs....pp....sb.",
        ".bssssssssssssb.",
        ".bbbbbbbbbbbbbb.",
        "................",
    ],
    "ApexFactions": [
        "................",
        ".bbbbbbbbbbbbbb.",
        ".bssssssssssssb.",
        ".bsg........gsb.",
        ".bsgg......ggsb.",
        ".bsggg....gggsb.",
        ".bsggee..eeggsb.",
        ".bsgeeeeeeeegsb.",
        ".bsgeymttmyegsb.",
        ".bsgeeeeeeeegsb.",
        ".bs.geeeeeeg.sb.",
        ".bs..geeeeg..sb.",
        ".bs....ee....sb.",
        ".bssssssssssssb.",
        ".bbbbbbbbbbbbbb.",
        "................",
    ],
    "WorldRelics": [
        "................",
        ".bbbbbbbbbbbbbb.",
        ".bssssssssssssb.",
        ".bsll..ll..llsb.",
        ".bsllllggllllsb.",
        ".bslltgggtlllsb.",
        ".bslttgtttttlsb.",
        ".bsltgtt..ttlsb.",
        ".bslggtt..ttlsb.",
        ".bsltgtt..ttlsb.",
        ".bsltttt..ttlsb.",
        ".bsggttttttggsb.",
        ".bsggggggggggsb.",
        ".bssssssssssssb.",
        ".bbbbbbbbbbbbbb.",
        "................",
    ],
    "ModernAge": [
        "................",
        ".bbbbbbbbbbbbbb.",
        ".bssssssssssssb.",
        ".bs....cc....sb.",
        ".bs...cccc...sb.",
        ".bs..ccuucc..sb.",
        ".bs.ccuuuucc.sb.",
        ".bsccccppccccsb.",
        ".bs..cuuuuc..sb.",
        ".bs...cuuc...sb.",
        ".bs..cc..cc..sb.",
        ".bs.cc....cc.sb.",
        ".bs..........sb.",
        ".bssssssssssssb.",
        ".bbbbbbbbbbbbbb.",
        "................",
    ],
    "btn_battle_radar": [
        "................",
        ".bbbbbbbbbbbbbb.",
        ".bssssssssssssb.",
        ".bs....rr....sb.",
        ".bs..rr..rr..sb.",
        ".bs.r..rr..r.sb.",
        ".bs.r.rpp..r.sb.",
        ".bsr..pppp..rsb.",
        ".bsr..pppp..rsb.",
        ".bs.r..ppr.r.sb.",
        ".bs.r..rr..r.sb.",
        ".bs..rr..rr..sb.",
        ".bs....rr....sb.",
        ".bssssssssssssb.",
        ".bbbbbbbbbbbbbb.",
        "................",
    ],
    "btn_world_builder": [
        "................",
        ".bbbbbbbbbbbbbb.",
        ".bssssssssssssb.",
        ".bsgggguuppppsb.",
        ".bsgggguuppppsb.",
        ".bsggguuuupppsb.",
        ".bsggguuuypppsb.",
        ".bsuuuuuyyuuusb.",
        ".bsuuuuuyyuuusb.",
        ".bsmmmuuyucccsb.",
        ".bsmmmuuuucccsb.",
        ".bsmmmmuuccccsb.",
        ".bsmmmmuuccccsb.",
        ".bssssssssssssb.",
        ".bbbbbbbbbbbbbb.",
        "................",
    ],
}


def render_template(rows: list[str], scale: int = 4) -> bytes:
    h = len(rows)
    w = len(rows[0])
    sw, sh = w * scale, h * scale
    buf = bytearray(sw * sh * 3)
    for y in range(sh):
        src_row = rows[y // scale]
        for x in range(sw):
            r, g, b = PAL[src_row[x // scale]]
            pos = (y * sw + x) * 3
            buf[pos] = r
            buf[pos + 1] = g
            buf[pos + 2] = b
    return encode_png_bytes(buf, sw, sh)


def main() -> None:
    for mod in ("SuiteHub", "CoreScale", "VisualPolish", "GrandWarfare", "LinkedContinents", "EraChronicle", "ApexFactions", "WorldRelics"):
        out = REPO_ROOT / "mods" / mod / "icon.png"
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_bytes(render_template(TEMPLATES[mod], scale=4))
        print(f"Wrote {out.relative_to(REPO_ROOT)}")

    exp_icon = REPO_ROOT / "expansions" / "ModernAge" / "icon.png"
    exp_icon.parent.mkdir(parents=True, exist_ok=True)
    exp_icon.write_bytes(render_template(TEMPLATES["ModernAge"], scale=4))
    print(f"Wrote {exp_icon.relative_to(REPO_ROOT)}")

    res_dir = REPO_ROOT / "mods" / "SuiteHub" / "GameResources"
    res_dir.mkdir(parents=True, exist_ok=True)
    btn_map = {
        "btn_hub.png": "SuiteHub",
        "btn_war_matrix.png": "GrandWarfare",
        "btn_battle_radar.png": "btn_battle_radar",
        "btn_world_builder.png": "btn_world_builder",
        "btn_visual.png": "VisualPolish",
        "btn_scale.png": "CoreScale",
        "btn_chronicle.png": "EraChronicle",
        "btn_portal.png": "LinkedContinents",
        "btn_apex.png": "ApexFactions",
        "btn_relics.png": "WorldRelics",
        "btn_modern.png": "ModernAge",
    }
    for fname, tpl in btn_map.items():
        p = res_dir / fname
        p.write_bytes(render_template(TEMPLATES[tpl], scale=2))
        print(f"Wrote {p.relative_to(REPO_ROOT)}")


if __name__ == "__main__":
    main()
