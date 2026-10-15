#!/usr/bin/env bash
# Builds mod plugins and assembles Thunderstore/Gale-compatible zips in dist/.
# Usage: scripts/package.sh [--version X.Y.Z] [ModName ...]   (default: every folder under package/)
#
# The version comes from the release tag (see docs/Development.md), not from files in the repo: it is passed to the
# build, so the DLL carries it, and stamped into the zip's manifest.json. Without --version it is a dev build (0.0.0-dev).
# Each mod <Name> has src/<Name>/<Name>.csproj and package/<Name>/{manifest.json,thunderstore.toml,icon.png,README.md,CHANGELOG.md}.
set -euo pipefail
cd "$(dirname "$0")/.."

version="0.0.0-dev"
mods=()
while [ $# -gt 0 ]; do
  case "$1" in
    --version) version="${2:?--version needs a value}"; shift 2 ;;
    *) mods+=("$1"); shift ;;
  esac
done
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

  # Every mod must be selectable in the release workflow, or it can't be released.
  grep -q "^          - $mod\$" .github/workflows/release.yml \
    || { echo "Add $mod to the options and tag patterns in .github/workflows/release.yml" >&2; exit 1; }

  dotnet build "src/$mod" -c Release -p:Version="$version"

  python3 - "$mod" <<'PY'
import json, sys, tomllib
mod = sys.argv[1]
manifest = json.load(open(f"package/{mod}/manifest.json"))
toml = tomllib.load(open(f"package/{mod}/thunderstore.toml", "rb"))["package"]
deps = {name: version for name, version in (d.rsplit("-", 1) for d in manifest["dependencies"])}
# The version is deliberately not compared: both files hold a placeholder, the real one comes from the tag.
pairs = [
    ("name", manifest["name"], toml["name"]),
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

  cp "package/$mod"/icon.png "package/$mod"/README.md "package/$mod"/CHANGELOG.md "$stage/"
  python3 - "package/$mod/manifest.json" "$stage/manifest.json" "$version" <<'PY'
import json, sys
src, dst, version = sys.argv[1:4]
manifest = json.load(open(src, encoding="utf-8"))
manifest["version_number"] = version
open(dst, "w", encoding="utf-8").write(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")
PY
  cp LICENSE "$stage/"
  cp "src/$mod/bin/Release/net48/$mod.dll" "$stage/"

  rm -f "$out"
  (cd "$stage" && zip -q -r "$OLDPWD/$out" .)
  rm -rf "$stage"
  echo "Created $out"
done
