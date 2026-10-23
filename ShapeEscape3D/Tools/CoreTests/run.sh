#!/usr/bin/env bash
# Compiles the Unity-independent puzzle core with Mono and runs the core test-suite.
# Requires: mono-mcs + mono-runtime (apt install mono-mcs mono-runtime).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="${TMPDIR:-/tmp}/shape-escape-core-tests"
mkdir -p "$OUT"
mcs -langversion:7 -nologo -warnaserror+ -out:"$OUT/CoreTests.exe" \
  "$ROOT"/Assets/ShapeEscape/Scripts/Core/*.cs "$ROOT"/Tools/CoreTests/CoreTestRunner.cs
mono "$OUT/CoreTests.exe" "$@"
