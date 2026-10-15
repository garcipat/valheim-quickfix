#!/usr/bin/env bash
# Turns a mod's Unreleased changelog entries into a release section and writes them out as the release notes.
# Usage: scripts/update-changelog.sh <Mod> <X.Y.Z[-suffix]> [--notes-file <path>]
#
# package/<Mod>/CHANGELOG.md gets "## [X.Y.Z] - <today>" holding the old Unreleased entries, under a fresh empty
# "## [Unreleased]". If a [X.Y.Z] section already exists it is left alone and its text is used as the notes.
# Fails, changing nothing, when there is nothing to release. Used by the Release workflow.
set -euo pipefail
cd "$(dirname "$0")/.."

mod="${1:?usage: update-changelog.sh <Mod> <version> [--notes-file <path>]}"
version="${2:?usage: update-changelog.sh <Mod> <version> [--notes-file <path>]}"
notes_file=""
if [ "${3:-}" = "--notes-file" ]; then
  notes_file="${4:?--notes-file needs a path}"
fi

python3 - "package/$mod/CHANGELOG.md" "$version" "$(date +%F)" "$notes_file" <<'PY'
import re, sys

path, version, today, notes_file = sys.argv[1:5]
text = open(path, encoding="utf-8").read()

existing = re.search(rf"^## \[{re.escape(version)}\][^\n]*\n(.*?)(?=^## \[|\Z)", text, re.S | re.M)
if existing:
    notes = existing.group(1).strip()
    print(f"{path}: already has a [{version}] section, left as is")
else:
    unreleased = re.search(r"^## \[Unreleased\][^\n]*\n(.*?)(?=^## \[|\Z)", text, re.S | re.M)
    if not unreleased or not unreleased.group(1).strip():
        sys.exit(f"::error::{path} has no entries under '## [Unreleased]'. Add what players will notice first; nothing was changed.")
    notes = unreleased.group(1).strip()
    new = text[:unreleased.start()] + f"## [Unreleased]\n\n## [{version}] - {today}\n" + unreleased.group(1) + text[unreleased.end():]
    open(path, "w", encoding="utf-8").write(new)
    print(f"{path}: Unreleased entries moved under [{version}] - {today}")

if not notes:
    sys.exit(f"::error::{path} has an empty [{version}] section.")
if notes_file:
    open(notes_file, "w", encoding="utf-8").write(notes + "\n")
PY
