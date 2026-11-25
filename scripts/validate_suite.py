#!/usr/bin/env python3
"""Repository integrity validator for worldbox-suite (Python 3 stdlib only)."""

from __future__ import annotations

import ast
import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
PNG_MAGIC = b"\x89PNG\r\n\x1a\n"
SKIP_DIRS = {"bin", "obj", ".git", "__pycache__"}

REQUIRED_MOD_KEYS = ("name", "author", "version", "GUID", "description", "iconPath")


def _rel(path: Path) -> str:
    try:
        return path.relative_to(REPO_ROOT).as_posix()
    except ValueError:
        return str(path)


def _is_valid_png(path: Path) -> bool:
    if not path.is_file():
        return False
    try:
        with path.open("rb") as fh:
            return fh.read(len(PNG_MAGIC)) == PNG_MAGIC
    except OSError:
        return False


def _strip_cs_comments(text: str, strip_strings: bool = False) -> str:
    """Strip C# // and /* */ comments while preserving exact line numbers.

    When strip_strings=True, string and char literal contents are also replaced
    with spaces so code pattern matching ignores literal text.
    """
    out: list[str] = []
    i = 0
    n = len(text)

    while i < n:
        ch = text[i]
        nxt = text[i + 1] if i + 1 < n else ""

        # Line comment
        if ch == "/" and nxt == "/":
            out.append("  ")
            i += 2
            while i < n and text[i] != "\n":
                out.append(" ")
                i += 1
            continue

        # Block comment
        if ch == "/" and nxt == "*":
            out.append("  ")
            i += 2
            while i < n:
                if text[i] == "*" and i + 1 < n and text[i + 1] == "/":
                    out.append("  ")
                    i += 2
                    break
                out.append("\n" if text[i] == "\n" else " ")
                i += 1
            continue

        # Verbatim string @"..." or $@"..." or @$"..."
        if (ch == "@" and nxt == '"') or (
            ch in ("$", "@") and nxt in ("$", "@") and ch != nxt and i + 2 < n and text[i + 2] == '"'
        ):
            prefix_len = 2 if ch == "@" and nxt == '"' else 3
            if strip_strings:
                out.append('"' + " " * (prefix_len - 1))
            else:
                out.append(text[i : i + prefix_len])
            i += prefix_len
            while i < n:
                c = text[i]
                if c == '"':
                    if i + 1 < n and text[i + 1] == '"':
                        out.append("  " if strip_strings else '""')
                        i += 2
                        continue
                    out.append('"')
                    i += 1
                    break
                out.append(("\n" if c == "\n" else " ") if strip_strings else c)
                i += 1
            continue

        # Regular or interpolated string "..." / $"..."
        if ch == '"' or (ch == "$" and nxt == '"'):
            prefix_len = 1 if ch == '"' else 2
            out.append('"' if (strip_strings and prefix_len == 1) else (' "' if strip_strings else text[i : i + prefix_len]))
            i += prefix_len
            while i < n:
                c = text[i]
                if c == "\\" and i + 1 < n:
                    out.append("  " if strip_strings else text[i : i + 2])
                    i += 2
                    continue
                if c == '"':
                    out.append('"')
                    i += 1
                    break
                out.append(("\n" if c == "\n" else " ") if strip_strings else c)
                i += 1
            continue

        # Char literal '...'
        if ch == "'":
            out.append("'")
            i += 1
            while i < n:
                c = text[i]
                if c == "\\" and i + 1 < n:
                    out.append("  " if strip_strings else text[i : i + 2])
                    i += 2
                    continue
                if c == "'":
                    out.append("'")
                    i += 1
                    break
                out.append(("\n" if c == "\n" else " ") if strip_strings else c)
                i += 1
            continue

        out.append(ch)
        i += 1

    return "".join(out)


def _discover_mod_dirs(repo_root: Path) -> list[Path]:
    mod_dirs: list[Path] = []
    for parent_name in ("mods", "expansions"):
        parent = repo_root / parent_name
        if not parent.is_dir():
            continue
        for entry in sorted(parent.iterdir()):
            if entry.is_dir() and not entry.name.startswith(".") and entry.name not in SKIP_DIRS:
                mod_dirs.append(entry)
    return mod_dirs


def validate_mod_manifests(repo_root: Path) -> tuple[str, list[str]]:
    errors: list[str] = []
    mod_dirs = _discover_mod_dirs(repo_root)
    seen_guids: dict[str, Path] = {}

    if not mod_dirs:
        errors.append("No mod directories found under mods/ or expansions/.")
        return "0 manifests", errors

    for mod_dir in mod_dirs:
        manifest = mod_dir / "mod.json"
        if not manifest.is_file():
            errors.append(f"{_rel(mod_dir)}: missing mod.json")
            continue

        try:
            data = json.loads(manifest.read_text(encoding="utf-8"))
        except Exception as exc:
            errors.append(f"{_rel(manifest)}: invalid JSON ({exc})")
            continue

        if not isinstance(data, dict):
            errors.append(f"{_rel(manifest)}: top-level JSON value must be an object")
            continue

        for key in REQUIRED_MOD_KEYS:
            val = data.get(key)
            if not isinstance(val, str) or not val.strip():
                errors.append(f"{_rel(manifest)}: missing or empty required key '{key}'")

        guid = data.get("GUID")
        if isinstance(guid, str) and guid.strip():
            norm_guid = guid.strip().lower()
            if norm_guid in seen_guids:
                errors.append(
                    f"{_rel(manifest)}: duplicate GUID '{guid}' (already used in {_rel(seen_guids[norm_guid])})"
                )
            else:
                seen_guids[norm_guid] = manifest

        if "targetGameBuild" in data:
            tgb = data["targetGameBuild"]
            valid_int = isinstance(tgb, int) and not isinstance(tgb, bool) and tgb > 0
            valid_str = isinstance(tgb, str) and tgb.strip().isdigit()
            if not (valid_int or valid_str):
                errors.append(
                    f"{_rel(manifest)}: 'targetGameBuild' must be a positive integer or numeric string"
                )

        icon_rel = data.get("iconPath")
        if isinstance(icon_rel, str) and icon_rel.strip():
            icon_path = mod_dir / icon_rel
            if not icon_path.is_file():
                errors.append(f"{_rel(manifest)}: iconPath '{icon_rel}' does not exist")
            elif icon_path.suffix.lower() == ".png" and not _is_valid_png(icon_path):
                errors.append(f"{_rel(icon_path)}: invalid PNG header")

    return f"{len(mod_dirs)} manifests", errors


def validate_locales(repo_root: Path) -> tuple[str, list[str]]:
    errors: list[str] = []
    locale_files: list[Path] = []
    parsed_locales: dict[Path, dict[str, str]] = {}

    for mod_dir in _discover_mod_dirs(repo_root):
        locales_dir = mod_dir / "Locales"
        if not locales_dir.is_dir():
            continue
        for lf in sorted(locales_dir.glob("*.json")):
            locale_files.append(lf)
            try:
                data = json.loads(lf.read_text(encoding="utf-8"))
            except Exception as exc:
                errors.append(f"{_rel(lf)}: invalid JSON ({exc})")
                continue

            if not isinstance(data, dict) or not data:
                errors.append(f"{_rel(lf)}: must be a non-empty JSON object")
                continue

            clean_map: dict[str, str] = {}
            for k, v in data.items():
                if not isinstance(k, str) or not k.strip():
                    errors.append(f"{_rel(lf)}: contains empty locale key")
                    continue
                if not isinstance(v, str) or not v.strip():
                    errors.append(f"{_rel(lf)}: key '{k}' has empty or non-string value")
                    continue
                clean_map[k] = v
            parsed_locales[lf] = clean_map

    # Cross-check SuiteHub BottomBarIntegration.cs tooltip/tab IDs against SuiteHub/Locales/en.json
    bottom_bar = repo_root / "mods" / "SuiteHub" / "Code" / "BottomBarIntegration.cs"
    suite_en = repo_root / "mods" / "SuiteHub" / "Locales" / "en.json"
    tooltip_ids_checked = 0

    if bottom_bar.is_file():
        if suite_en not in parsed_locales:
            errors.append(f"{_rel(suite_en)}: missing or invalid primary locale for BottomBarIntegration.cs")
        else:
            en_keys = set(parsed_locales[suite_en].keys())
            code = _strip_cs_comments(bottom_bar.read_text(encoding="utf-8"), strip_strings=False)

            # 1. Tab titles & descriptions: TabManager.CreateTab("id", "titleKey", "descKey", ...)
            for m in re.finditer(
                r'TabManager\s*\.\s*CreateTab\s*\(\s*"[^"]*"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"',
                code,
            ):
                for key in (m.group(1), m.group(2)):
                    tooltip_ids_checked += 1
                    if key not in en_keys:
                        errors.append(
                            f"{_rel(suite_en)}: missing tab locale key '{key}' referenced in {_rel(bottom_bar)}"
                        )

            # 2. Detect suffixes wired by WireNativeTooltip (e.g. _description, _tip)
            wired_suffixes: list[str] = []
            wire_match = re.search(
                r"WireNativeTooltip\s*\([^)]*\)\s*\{(?P<body>.*?)\n\s{8}\}",
                code,
                re.DOTALL,
            )
            wire_body = wire_match.group("body") if wire_match else code
            for suffix in ("_description", "_tip"):
                if re.search(rf'\+\s*"{re.escape(suffix)}"', wire_body):
                    wired_suffixes.append(suffix)

            # 3. Button registrations: AddDropPower / AddAction / AddToggle(tab, "group", "id", ...)
            for m in re.finditer(
                r'\b(?:AddDropPower|AddAction|AddToggle)\s*\(\s*[^,]+,\s*"[^"]+"\s*,\s*"([^"]+)"',
                code,
            ):
                btn_id = m.group(1)
                tooltip_ids_checked += 1
                if btn_id not in en_keys:
                    errors.append(
                        f"{_rel(suite_en)}: missing tooltip title key '{btn_id}' for button in {_rel(bottom_bar)}"
                    )
                for suffix in wired_suffixes:
                    full_key = f"{btn_id}{suffix}"
                    if full_key not in en_keys:
                        errors.append(
                            f"{_rel(suite_en)}: missing tooltip key '{full_key}' for button '{btn_id}' in {_rel(bottom_bar)}"
                        )

            # 4. Direct LocalizedTextManager lookups
            for m in re.finditer(
                r'LocalizedTextManager\s*\.\s*(?:stringExists|getText)\s*\(\s*"([^"]+)"\s*\)',
                code,
            ):
                key = m.group(1)
                tooltip_ids_checked += 1
                if key not in en_keys:
                    errors.append(
                        f"{_rel(suite_en)}: missing locale key '{key}' queried in {_rel(bottom_bar)}"
                    )

    return f"{len(locale_files)} locale files, {tooltip_ids_checked} UI IDs", errors


def _collect_cs_string_constants(code_dir: Path) -> dict[str, str]:
    """Collect `const string Name = "value";` constants (qualified and unqualified)."""
    consts: dict[str, str] = {}
    if not code_dir.is_dir():
        return consts

    for cs_file in sorted(code_dir.rglob("*.cs")):
        if any(part in SKIP_DIRS for part in cs_file.parts):
            continue
        code = _strip_cs_comments(cs_file.read_text(encoding="utf-8"), strip_strings=False)
        current_class: str | None = None
        for line in code.splitlines():
            cls_m = re.search(r"\b(?:class|struct)\s+([A-Za-z_]\w*)", line)
            if cls_m:
                current_class = cls_m.group(1)
            const_m = re.search(
                r'\bconst\s+string\s+([A-Za-z_]\w*)\s*=\s*"([^"]+)"\s*;',
                line,
            )
            if const_m:
                name, val = const_m.group(1), const_m.group(2)
                consts[name] = val
                if current_class:
                    consts[f"{current_class}.{name}"] = val
    return consts


def _validate_sprite_loader_cs(cs_path: Path, mod_dir: Path, errors: list[str]) -> int:
    """Validate GameResources PNG references inside a *Sprites.cs file."""
    checked = 0
    code = _strip_cs_comments(cs_path.read_text(encoding="utf-8"), strip_strings=False)
    consts = _collect_cs_string_constants(mod_dir / "Code")

    # Find GameResources subfolder, e.g. Path.Combine(modFolder, "GameResources", "vehicles")
    sub_parts: list[str] = ["GameResources"]
    dir_m = re.search(r'Path\s*\.\s*Combine\s*\([^;]*?"GameResources"([^)]*)\)', code)
    if dir_m:
        for extra in re.findall(r'"([^"]+)"', dir_m.group(1)):
            if not extra.lower().endswith(".png"):
                sub_parts.append(extra)
    base_dir = mod_dir.joinpath(*sub_parts)

    # 1. Explicit string[] *Ids = { ... } arrays used with id + ".png"
    for arr_m in re.finditer(
        r"string\s*\[\s*\]\s+[A-Za-z_]\w*\s*=\s*(?:new\s+string\s*\[\s*\]\s*)?\{([^}]*)\}",
        code,
        re.DOTALL,
    ):
        body = arr_m.group(1)
        for token in body.split(","):
            tok = token.strip()
            if not tok:
                continue
            if tok.startswith('"') and tok.endswith('"'):
                sprite_id = tok[1:-1]
            elif tok in consts:
                sprite_id = consts[tok]
            else:
                errors.append(
                    f"{_rel(cs_path)}: could not resolve sprite ID constant '{tok}'"
                )
                continue
            fname = sprite_id if sprite_id.lower().endswith(".png") else f"{sprite_id}.png"
            png_path = base_dir / fname
            checked += 1
            if not png_path.is_file():
                errors.append(f"{_rel(cs_path)}: referenced sprite '{_rel(png_path)}' does not exist")
            elif not _is_valid_png(png_path):
                errors.append(f"{_rel(png_path)}: invalid PNG header")

    # 2. Direct literal .png references in Path.Combine(...)
    for lit_m in re.finditer(r'Path\s*\.\s*Combine\s*\(([^)]*"([^"]+\.png)"[^)]*)\)', code):
        args_str, fname = lit_m.group(1), lit_m.group(2)
        if "{" in fname or "}" in fname:
            continue
        if "modFolder" in args_str and "GameResources" not in args_str:
            png_path = mod_dir / fname
        else:
            png_path = base_dir / fname
        checked += 1
        if not png_path.is_file():
            errors.append(f"{_rel(cs_path)}: referenced PNG '{_rel(png_path)}' does not exist")
        elif not _is_valid_png(png_path):
            errors.append(f"{_rel(png_path)}: invalid PNG header")

    # 3. Sequence loaders: LoadSequence(dir, "prefix", count) -> prefix_0.png .. prefix_{count-1}.png
    for seq_m in re.finditer(r'LoadSequence\s*\([^,]+,\s*"([A-Za-z0-9_]+)"\s*,\s*(\d+)\s*\)', code):
        prefix, count = seq_m.group(1), int(seq_m.group(2))
        for idx in range(count):
            png_path = base_dir / f"{prefix}_{idx}.png"
            checked += 1
            if not png_path.is_file():
                errors.append(f"{_rel(cs_path)}: referenced animation frame '{_rel(png_path)}' does not exist")
            elif not _is_valid_png(png_path):
                errors.append(f"{_rel(png_path)}: invalid PNG header")

    return checked


def validate_game_resources(repo_root: Path) -> tuple[str, list[str]]:
    errors: list[str] = []
    checked = 0

    # 1. BottomBarIntegration.cs PNG references
    bottom_bar = repo_root / "mods" / "SuiteHub" / "Code" / "BottomBarIntegration.cs"
    if bottom_bar.is_file():
        mod_dir = repo_root / "mods" / "SuiteHub"
        code = _strip_cs_comments(bottom_bar.read_text(encoding="utf-8"), strip_strings=False)
        for m in re.finditer(r'Path\s*\.\s*Combine\s*\(\s*([A-Za-z_]\w*)\s*,\s*"([^"]+\.png)"\s*\)', code):
            var_name, fname = m.group(1), m.group(2)
            png_path = (mod_dir / fname) if var_name == "modFolder" else (mod_dir / "GameResources" / fname)
            checked += 1
            if not png_path.is_file():
                errors.append(f"{_rel(bottom_bar)}: referenced PNG '{_rel(png_path)}' does not exist")
            elif not _is_valid_png(png_path):
                errors.append(f"{_rel(png_path)}: invalid PNG header")

    # 2. VehicleSprites.cs and WardenSprites.cs (if present)
    for mod_dir in _discover_mod_dirs(repo_root):
        code_dir = mod_dir / "Code"
        if not code_dir.is_dir():
            continue
        for target_name in ("VehicleSprites.cs", "WardenSprites.cs"):
            sprite_cs = code_dir / target_name
            if sprite_cs.is_file():
                checked += _validate_sprite_loader_cs(sprite_cs, mod_dir, errors)

    # 3. Verify every PNG currently on disk under any GameResources/ directory has a valid header
    disk_pngs = 0
    for mod_dir in _discover_mod_dirs(repo_root):
        res_dir = mod_dir / "GameResources"
        if not res_dir.is_dir():
            continue
        for png in sorted(res_dir.rglob("*.png")):
            disk_pngs += 1
            if not _is_valid_png(png):
                errors.append(f"{_rel(png)}: invalid PNG header")

    return f"{checked} referenced PNGs, {disk_pngs} GameResources PNGs", errors


def _scan_static_initializer_screen_refs(stripped_code: str) -> list[int]:
    """Return 1-based line numbers where Screen.width or Screen.height is used in a static initializer."""
    bad_lines: list[int] = []
    n = len(stripped_code)

    for m in re.finditer(r"\bstatic\b", stripped_code):
        pos = m.end()
        # Scan forward to find '=' of a field or auto-property initializer.
        # Abort if we hit ';', '(', '=>', or a non-property block '{...}'.
        init_start = -1
        i = pos
        while i < n:
            ch = stripped_code[i]
            if ch in (";", "(", "}"):
                break
            if ch == "=":
                nxt = stripped_code[i + 1] if i + 1 < n else ""
                if nxt in ("=", ">"):
                    break
                init_start = i + 1
                break
            if ch == "{":
                # Could be a static class/struct body or an auto-property accessor block { get; set; } = ...
                depth = 1
                j = i + 1
                while j < n and depth > 0:
                    if stripped_code[j] == "{":
                        depth += 1
                    elif stripped_code[j] == "}":
                        depth -= 1
                    j += 1
                block = stripped_code[i + 1 : j - 1].strip()
                # Auto-property blocks only contain get/set/init keywords, access modifiers, and semicolons
                if re.fullmatch(r"(?:(?:private|protected|internal|public)\s+|get|set|init|;|\s)+", block):
                    i = j
                    continue
                break
            i += 1

        if init_start == -1:
            continue

        # Scan the initializer expression until ';' at nesting depth 0
        depth = 0
        j = init_start
        while j < n:
            ch = stripped_code[j]
            if ch in ("(", "{", "["):
                depth += 1
            elif ch in (")", "}", "]"):
                depth = max(0, depth - 1)
            elif ch == ";" and depth == 0:
                break
            j += 1

        init_expr = stripped_code[init_start:j]
        for sm in re.finditer(r"\bScreen\s*\.\s*(?:width|height)\b", init_expr):
            abs_pos = init_start + sm.start()
            line_no = stripped_code.count("\n", 0, abs_pos) + 1
            if line_no not in bad_lines:
                bad_lines.append(line_no)

    return bad_lines


def validate_cs_invariants(repo_root: Path) -> tuple[str, list[str]]:
    errors: list[str] = []
    cs_files: list[Path] = []

    for parent_name in ("mods", "expansions"):
        parent = repo_root / parent_name
        if not parent.is_dir():
            continue
        for cs_file in sorted(parent.rglob("*.cs")):
            if any(part in SKIP_DIRS for part in cs_file.parts):
                continue
            cs_files.append(cs_file)

    for cs_file in cs_files:
        raw = cs_file.read_text(encoding="utf-8")
        stripped = _strip_cs_comments(raw, strip_strings=True)

        # 1. No Harmony UnpatchAll( calls
        for m in re.finditer(r"\bUnpatchAll\s*\(", stripped):
            line_no = stripped.count("\n", 0, m.start()) + 1
            errors.append(
                f"{_rel(cs_file)}:{line_no}: forbidden 'UnpatchAll(' call (use Harmony 2.3 'UnpatchSelf()')"
            )

        # 2. No Config.parallel_chunk_manager = true
        for m in re.finditer(r"\bparallel_chunk_manager\s*=\s*true\b", stripped):
            line_no = stripped.count("\n", 0, m.start()) + 1
            errors.append(
                f"{_rel(cs_file)}:{line_no}: forbidden 'Config.parallel_chunk_manager = true' (causes RegionPathFinder crash)"
            )

        # 3. No Screen.width or Screen.height in static field/property initializers
        for line_no in _scan_static_initializer_screen_refs(stripped):
            errors.append(
                f"{_rel(cs_file)}:{line_no}: Screen.width/Screen.height used in static initializer"
            )

    return f"{len(cs_files)} C# files", errors


def validate_installers(repo_root: Path) -> tuple[str, list[str]]:
    errors: list[str] = []
    mods_root = repo_root / "mods"
    expected_mods = sorted(
        entry.name
        for entry in mods_root.iterdir()
        if entry.is_dir() and not entry.name.startswith(".") and (entry / "mod.json").is_file()
    )

    required_copy_items = ("GameResources", "WebClient")

    # 1. PowerShell installers
    for rel_path in ("scripts/install.ps1", "scripts/install_mods.ps1"):
        path = repo_root / rel_path
        if not path.is_file():
            errors.append(f"{rel_path}: installer file missing")
            continue
        text = path.read_text(encoding="utf-8")

        mods_match = re.search(r"\$SuiteMods\s*=\s*@\(([^)]+)\)", text)
        if not mods_match:
            errors.append(f"{rel_path}: could not find $SuiteMods array")
        else:
            listed = set(re.findall(r'"([^"]+)"', mods_match.group(1)))
            missing = [m for m in expected_mods if m not in listed]
            if missing:
                errors.append(f"{rel_path}: $SuiteMods missing mods: {', '.join(missing)}")

        copy_match = re.search(r'@\(([^)]*"mod\.json"[^)]*)\)', text)
        if not copy_match:
            errors.append(f"{rel_path}: could not find mod copy item array containing 'mod.json'")
        else:
            copy_items = set(re.findall(r'"([^"]+)"', copy_match.group(1)))
            for req in required_copy_items:
                if req not in copy_items:
                    errors.append(f"{rel_path}: copy items missing '{req}'")

    # 2. Bash installer
    sh_rel = "scripts/install_mods.sh"
    sh_path = repo_root / sh_rel
    if not sh_path.is_file():
        errors.append(f"{sh_rel}: installer file missing")
    else:
        text = sh_path.read_text(encoding="utf-8")
        mods_match = re.search(r"for\s+mod\s+in\s+([^;]+);", text)
        if not mods_match:
            errors.append(f"{sh_rel}: could not find 'for mod in ...;' loop")
        else:
            listed = set(mods_match.group(1).split())
            missing = [m for m in expected_mods if m not in listed]
            if missing:
                errors.append(f"{sh_rel}: mod loop missing mods: {', '.join(missing)}")

        copy_match = re.search(r"for\s+item\s+in\s+([^;]*\bmod\.json\b[^;]*);", text)
        if not copy_match:
            errors.append(f"{sh_rel}: could not find 'for item in ...;' loop containing mod.json")
        else:
            copy_items = set(copy_match.group(1).split())
            for req in required_copy_items:
                if req not in copy_items:
                    errors.append(f"{sh_rel}: copy items missing '{req}'")

    # 3. Studio mod manager
    py_rel = "tools/studio/mod_manager.py"
    py_path = repo_root / py_rel
    if not py_path.is_file():
        errors.append(f"{py_rel}: file missing")
    else:
        tree = ast.parse(py_path.read_text(encoding="utf-8"), filename=py_rel)
        core_mods: set[str] | None = None
        deploy_items: set[str] | None = None

        for node in ast.walk(tree):
            if isinstance(node, ast.Assign):
                for target in node.targets:
                    if isinstance(target, ast.Name) and target.id == "CORE_MOD_NAMES":
                        if isinstance(node.value, (ast.List, ast.Tuple)):
                            core_mods = {
                                elt.value
                                for elt in node.value.elts
                                if isinstance(elt, ast.Constant) and isinstance(elt.value, str)
                            }
            elif isinstance(node, ast.FunctionDef) and node.name == "deploy_suite_mod":
                for sub in ast.walk(node):
                    if isinstance(sub, (ast.Tuple, ast.List)):
                        vals = {
                            elt.value
                            for elt in sub.elts
                            if isinstance(elt, ast.Constant) and isinstance(elt.value, str)
                        }
                        if "mod.json" in vals:
                            deploy_items = vals
                            break

        if core_mods is None:
            errors.append(f"{py_rel}: could not find CORE_MOD_NAMES list")
        else:
            missing = [m for m in expected_mods if m not in core_mods]
            if missing:
                errors.append(f"{py_rel}: CORE_MOD_NAMES missing mods: {', '.join(missing)}")

        if deploy_items is None:
            errors.append(f"{py_rel}: could not find copy items tuple in deploy_suite_mod()")
        else:
            for req in required_copy_items:
                if req not in deploy_items:
                    errors.append(f"{py_rel}: deploy_suite_mod() copy items missing '{req}'")

    return f"4 installers, {len(expected_mods)} core mods", errors


def main() -> int:
    checks = [
        ("mod.json manifests", validate_mod_manifests),
        ("Locales & tooltips", validate_locales),
        ("GameResources PNGs", validate_game_resources),
        ("C# safety invariants", validate_cs_invariants),
        ("Installer parity", validate_installers),
    ]

    all_errors: list[str] = []
    summaries: list[str] = []

    for label, fn in checks:
        detail, errors = fn(REPO_ROOT)
        status = "PASS" if not errors else "FAIL"
        summaries.append(f"  [{status}] {label}: {detail}")
        all_errors.extend(errors)

    print("WorldBox Suite validation summary:")
    for line in summaries:
        print(line)

    if all_errors:
        print(f"\nFound {len(all_errors)} error(s):", file=sys.stderr)
        for err in all_errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print("\nAll checks passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
