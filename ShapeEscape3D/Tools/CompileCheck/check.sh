#!/usr/bin/env bash
# Compile-checks the runtime and editor scripts against UnityEngine reference assemblies with Mono's mcs.
# This catches C# and API errors without a Unity install. Optional SDK code paths (AdMob, Firebase, Input System
# package) are excluded because their defines are off by default.
# Usage: Tools/CompileCheck/check.sh [unity-refs-dir]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
REFS="${1:-${TMPDIR:-/tmp}/unity-refs}"
[ -d "$REFS/unityengine.modules" ] || "$ROOT/Tools/CompileCheck/fetch-unity-refs.sh" "$REFS" >/dev/null
OUT="${TMPDIR:-/tmp}/shape-escape-compile"
mkdir -p "$OUT"
ENGINE_REFS=$(find "$REFS/unityengine.modules/lib/net45" -name "*.dll" ! -name "UnityEngine.dll" | sed 's/^/-r:/' | tr '\n' ' ')
UI_REF="-r:$REFS/unity3d.unityengine.ui/lib/UnityEngine.UI.dll"
NETSTD=$(find /usr/lib/mono -path "*4.5*/Facades/netstandard.dll" | head -1)
COMMON="-langversion:7 -nologo -nowarn:1701,1702 -define:UNITY_ANDROID -define:ENABLE_LEGACY_INPUT_MANAGER -r:System.Core.dll -r:$NETSTD"
echo "[1/3] Core"
mcs $COMMON -target:library -out:"$OUT/ShapeEscape.Core.dll" "$ROOT"/Assets/ShapeEscape/Scripts/Core/*.cs
echo "[2/3] Runtime"
mcs $COMMON -target:library -out:"$OUT/ShapeEscape.Runtime.dll" $ENGINE_REFS $UI_REF -r:"$OUT/ShapeEscape.Core.dll" \
  "$ROOT/Tools/CompileCheck/Stubs.cs" $(find "$ROOT/Assets/ShapeEscape/Scripts/Runtime" -name "*.cs")
echo "[3/3] Editor (against an older UnityEditor reference; newer-API warnings are expected to be guarded)"
mcs $COMMON -define:UNITY_EDITOR -target:library -out:"$OUT/ShapeEscape.Editor.dll" $ENGINE_REFS $UI_REF \
  -r:"$REFS/unity3d.unityeditor/lib/UnityEditor.dll" -r:"$OUT/ShapeEscape.Core.dll" -r:"$OUT/ShapeEscape.Runtime.dll" \
  $(find "$ROOT/Assets/ShapeEscape/Scripts/Editor" -name "*.cs")
echo "OK"
