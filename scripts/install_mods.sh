#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
WORLDBOX_DIR="${1:-}"

if [ -z "$WORLDBOX_DIR" ]; then
  for c in \
    "$HOME/Library/Application Support/Steam/steamapps/common/worldbox" \
    "$HOME/.local/share/Steam/steamapps/common/worldbox"; do
    if [ -d "$c" ]; then
      WORLDBOX_DIR="$c"
      break
    fi
  done
fi

if [ -z "$WORLDBOX_DIR" ] || [ ! -d "$WORLDBOX_DIR" ]; then
  echo "Could not auto-detect WorldBox. Pass your Steam install directory:"
  echo "  ./scripts/install_mods.sh \"/path/to/steamapps/common/worldbox\""
  exit 1
fi

if [ -d "$WORLDBOX_DIR/worldbox.app/Contents/Resources/Data/StreamingAssets" ]; then
  LOADER_DIR="$WORLDBOX_DIR/worldbox.app/Contents/Resources/Data/StreamingAssets/mods"
else
  LOADER_DIR="$WORLDBOX_DIR/worldbox_Data/StreamingAssets/mods"
fi
MODS_DIR="$WORLDBOX_DIR/Mods"

mkdir -p "$LOADER_DIR" "$MODS_DIR"

if [ ! -f "$LOADER_DIR/NeoModLoader.dll" ]; then
  echo "Downloading latest NeoModLoader.dll..."
  curl -fL "https://github.com/WorldBoxOpenMods/ModLoader/releases/latest/download/NeoModLoader.dll" -o "$LOADER_DIR/NeoModLoader.dll"
else
  echo "NeoModLoader.dll already present."
fi

for mod in SuiteHub CoreScale VisualPolish GrandWarfare LinkedContinents EraChronicle ApexFactions Arsenal; do
  SRC="$ROOT_DIR/mods/$mod"
  DST="$MODS_DIR/$mod"
  rm -rf "$DST"
  mkdir -p "$DST"
  for item in mod.json icon.png Code Locales GameResources; do
    if [ -e "$SRC/$item" ]; then
      cp -R "$SRC/$item" "$DST/$item"
    fi
  done
  echo "Installed $mod -> $DST"
done

echo ""
echo "Done! Enable 'Experimental Mode' in WorldBox Settings, restart, and click the 'WorldBox Suite' tab on the bottom bar."
