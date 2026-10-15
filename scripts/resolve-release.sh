#!/usr/bin/env bash
# Decides which mod and version a release run is for, and checks that releasing it is safe.
# Used by .github/workflows/release.yml; runnable locally for testing.
#
# The version always comes from a tag <Mod>-vX.Y.Z; nothing in the repo stores it. A manual run is a dry run
# that builds the package from a version typed in, and creates no release.
#
# Inputs (environment):
#   EVENT          push | workflow_dispatch
#   REF_NAME       the tag name (push only)
#   SHA            commit being released (push only)
#   MOD_INPUT      mod picked in a manual run
#   VERSION_INPUT  version for a manual (dry) run
# Output (stdout, key=value lines, suitable for $GITHUB_OUTPUT): mod, version, tag, dry_run, prerelease
set -euo pipefail
cd "$(dirname "$0")/.."

fail() {
  echo "::error::$*" >&2
  exit 1
}

if [ "$EVENT" = "push" ]; then
  tag="$REF_NAME"
  [[ "$tag" == *-v* ]] || fail "Not a release tag: $tag (expected <Mod>-vX.Y.Z)."
  mod="${tag%%-v*}"
  version="${tag#*-v}"
  dry_run=false
  git merge-base --is-ancestor "$SHA" origin/main || fail "Tag $tag is not on main; release from main only."
else
  mod="${MOD_INPUT:-}"
  version="${VERSION_INPUT:-0.0.0-dryrun}"
  dry_run=true
fi

[ -n "$mod" ] && [ -d "package/$mod" ] || fail "Unknown mod '$mod' (no package/$mod folder)."
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] || fail "Not a valid version: '$version' (expected X.Y.Z or X.Y.Z-suffix)."

echo "mod=$mod"
echo "version=$version"
echo "tag=$mod-v$version"
echo "dry_run=$dry_run"
case "$version" in *-*) echo "prerelease=true" ;; *) echo "prerelease=false" ;; esac
