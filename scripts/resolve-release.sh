#!/usr/bin/env bash
# Decides which mod and version a release run is for, and checks that releasing it is safe.
# Used by .github/workflows/release.yml; runnable locally for testing.
#
# The version always comes from a tag <Mod>-vX.Y.Z, or from a merged release pull request's branch
# release/<Mod>-vX.Y.Z, whose merge commit the workflow then tags; nothing in the repo stores it. A manual run is a
# dry run that builds the package from a version typed in, and creates no release.
#
# Inputs (environment):
#   EVENT          push | pull_request | workflow_dispatch
#   REF_NAME       the tag name (push) or the release branch (pull_request)
#   SHA            commit being released: the tagged commit (push) or the merge commit (pull_request)
#   MOD_INPUT      mod picked in a manual run
#   VERSION_INPUT  version for a manual (dry) run
# Output (stdout, key=value lines, suitable for $GITHUB_OUTPUT): mod, version, tag, dry_run, prerelease
set -euo pipefail
cd "$(dirname "$0")/.."

fail() {
  echo "::error::$*" >&2
  exit 1
}

if [ "$EVENT" = "push" ] || [ "$EVENT" = "pull_request" ]; then
  if [ "$EVENT" = "pull_request" ]; then
    [[ "$REF_NAME" == release/*-v* ]] || fail "Not a release branch: $REF_NAME (expected release/<Mod>-vX.Y.Z)."
    tag="${REF_NAME#release/}"
  else
    tag="$REF_NAME"
  fi
  [[ "$tag" == *-v* ]] || fail "Not a release tag: $tag (expected <Mod>-vX.Y.Z)."
  mod="${tag%%-v*}"
  version="${tag#*-v}"
  dry_run=false
  git merge-base --is-ancestor "$SHA" origin/main || fail "$tag is not on main; release from main only."
  if [ "$EVENT" = "pull_request" ]; then
    # The pull request must have closed the changelog section, or main would still list the entries as unreleased.
    grep -q "^## \[${version//./\\.}\]" "package/$mod/CHANGELOG.md" 2>/dev/null \
      || fail "package/$mod/CHANGELOG.md has no [$version] section; the release pull request must rename [Unreleased] to it."
    existing="$(git rev-parse -q --verify "refs/tags/$tag^{commit}" || true)"
    [ -z "$existing" ] || [ "$existing" = "$SHA" ] || fail "Tag $tag already exists on another commit."
  fi
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
