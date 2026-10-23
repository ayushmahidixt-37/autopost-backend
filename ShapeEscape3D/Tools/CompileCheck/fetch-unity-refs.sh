#!/usr/bin/env bash
# Downloads UnityEngine reference assemblies from NuGet (compile-only, no Unity install needed).
set -euo pipefail
DEST="${1:-${TMPDIR:-/tmp}/unity-refs}"
mkdir -p "$DEST" && cd "$DEST"
get() { curl -sSL -m 180 -o "$1.nupkg" "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg" && unzip -o -q "$1.nupkg" -d "$1"; }
get unityengine.modules 2021.3.33
get unity3d.unityengine.ui 2020.3.21
get unity3d.unityeditor 2018.1.6-f1
echo "$DEST"
