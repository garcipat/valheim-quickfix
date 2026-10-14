#!/usr/bin/env bash
# Builds the plugin and assembles a Thunderstore/Gale-compatible zip in dist/.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet build src/ValheimQuickFix -c Release

version=$(python3 -c "import json; print(json.load(open('package/manifest.json'))['version_number'])")
out="dist/ValheimQuickFix-${version}.zip"
stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT

cp package/manifest.json package/icon.png "$stage/"
cp README.md CHANGELOG.md "$stage/"
cp src/ValheimQuickFix/bin/Release/net48/ValheimQuickFix.dll "$stage/"

mkdir -p dist
rm -f "$out"
(cd "$stage" && zip -q -r "$OLDPWD/$out" .)
echo "Created $out"
