#!/usr/bin/env bash
# Downloads the Thunderstore CLI (tcli) release pinned below, checks its checksum, and puts it on PATH for the
# rest of the GitHub Actions job. Used by .github/workflows/release.yml; runnable locally (prints the install dir).
#
# tcli has no installer; the release is a single-file executable. To update: pick a release on
# https://github.com/thunderstore-io/thunderstore-cli/releases, change the version, and set the checksum to
# `sha256sum tcli-<version>-linux-x64.tar.gz`.
set -euo pipefail

version=0.2.4
sha256=bd7df48875e0c819a23a3bfaccf493c8cdf45288d9445558157a51126d0eab32

dir="${RUNNER_TEMP:-$(mktemp -d)}/tcli"
mkdir -p "$dir"
archive="$dir/tcli.tar.gz"

curl -fsSL -o "$archive" \
  "https://github.com/thunderstore-io/thunderstore-cli/releases/download/$version/tcli-$version-linux-x64.tar.gz"
echo "$sha256  $archive" | sha256sum -c - >&2
tar -xzf "$archive" -C "$dir"

bin="$dir/tcli-$version-linux-x64"
chmod +x "$bin/tcli"
if [ -n "${GITHUB_PATH:-}" ]; then
  echo "$bin" >> "$GITHUB_PATH"
fi
echo "$bin"
