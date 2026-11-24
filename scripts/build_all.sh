#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

echo "=== Running Python toolkit tests ==="
python3 -m unittest discover -s tests -v

echo ""
echo "=== Building all 9 core mods + ModernAge expansion ==="
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/dotnet_home}"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

for proj in \
  mods/SuiteHub/SuiteHub.csproj \
  mods/CoreScale/CoreScale.csproj \
  mods/VisualPolish/VisualPolish.csproj \
  mods/GrandWarfare/GrandWarfare.csproj \
  mods/LinkedContinents/LinkedContinents.csproj \
  mods/EraChronicle/EraChronicle.csproj \
  mods/ApexFactions/ApexFactions.csproj \
  mods/WorldRelics/WorldRelics.csproj \
  mods/CoopHost/CoopHost.csproj \
  expansions/ModernAge/ModernAge.csproj; do
  echo "Building $proj..."
  dotnet build "$proj" -m:1 /p:UseSharedCompilation=false
done

echo ""
echo "All checks and builds passed."
