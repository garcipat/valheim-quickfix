#!/usr/bin/env bash
# Builds mod plugins and assembles Thunderstore/Gale-compatible zips in dist/.
# Usage: scripts/package.sh [ModName ...]   (default: every folder under package/)
# Each mod <Name> has src/<Name>/<Name>.csproj and package/<Name>/{manifest.json,icon.png,README.md,CHANGELOG.md}.
set -euo pipefail
cd "$(dirname "$0")/.."

mods=("$@")
if [ ${#mods[@]} -eq 0 ]; then
  for dir in package/*/; do
    mods+=("$(basename "$dir")")
  done
fi

mkdir -p dist
for mod in "${mods[@]}"; do
  if [ ! -d "package/$mod" ] || [ ! -f "src/$mod/$mod.csproj" ]; then
    echo "Unknown mod: $mod" >&2
    exit 1
  fi

  dotnet build "src/$mod" -c Release

  version=$(python3 -c "import json,sys; print(json.load(open(sys.argv[1]))['version_number'])" "package/$mod/manifest.json")
  python3 - "$mod" <<'PY'
import json, sys, tomllib
mod = sys.argv[1]
manifest = json.load(open(f"package/{mod}/manifest.json"))
toml = tomllib.load(open(f"package/{mod}/thunderstore.toml", "rb"))["package"]
deps = {name: version for name, version in (d.rsplit("-", 1) for d in manifest["dependencies"])}
pairs = [
    ("name", manifest["name"], toml["name"]),
    ("version", manifest["version_number"], toml["versionNumber"]),
    ("description", manifest["description"], toml["description"]),
    ("website", manifest["website_url"], toml["websiteUrl"]),
    ("dependencies", deps, toml["dependencies"]),
]
bad = [f"{field}: manifest={a!r} toml={b!r}" for field, a, b in pairs if a != b]
if bad:
    sys.exit(f"package/{mod}: manifest.json and thunderstore.toml disagree:\n  " + "\n  ".join(bad))
PY
  out="dist/${mod}-${version}.zip"
  stage=$(mktemp -d)

  cp "package/$mod"/manifest.json "package/$mod"/icon.png "package/$mod"/README.md "package/$mod"/CHANGELOG.md "$stage/"
  cp LICENSE "$stage/"
  cp "src/$mod/bin/Release/net48/$mod.dll" "$stage/"

  rm -f "$out"
  (cd "$stage" && zip -q -r "$OLDPWD/$out" .)
  rm -rf "$stage"
  echo "Created $out"
done
