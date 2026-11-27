#!/usr/bin/env bash
# Build LOAB for WebGL into site/games/loab and refresh the arcade manifest.
# Usage: projects/LOAB/build.sh            (from anywhere)
#        UNITY=/path/to/Unity.exe projects/LOAB/build.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
UNITY="${UNITY:-C:/Program Files/Unity/Hub/Editor/6000.2.9f1-x86_64/Editor/Unity.exe}"
LOG="$HERE/Logs/build.log"
mkdir -p "$HERE/Logs"

echo "Compiling simulation (headless check)..."
dotnet build "$HERE/Tools/SimRunner" -nologo -v q

echo "Building WebGL with Unity (takes a few minutes)..."
start=$(date +%s)
if ! "$UNITY" -batchmode -quit -nographics -projectPath "$HERE" -executeMethod LoabBuild.BuildWebGL -logFile "$LOG"; then
  echo "Unity build failed. Errors:"; grep -E "error CS|Error building|BuildFailed" "$LOG" | head -40; exit 1
fi
echo "Unity build finished in $(( $(date +%s) - start ))s"
grep -E "LOAB build result" "$LOG" || true

cd "$REPO"
npm run -s add-game -- loab
npm run -s check
