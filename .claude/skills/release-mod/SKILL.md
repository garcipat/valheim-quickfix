---
name: release-mod
description: Release a new version of one of the Valheim mods in this repo (ValheimQuickFix or SignIcons) — read its changelog, propose the next version, and push the <Mod>-vX.Y.Z tag that triggers the Release workflow. Use when the user asks to release, publish, tag, or ship a new version of a mod.
argument-hint: <Mod> [version | dry-run]
disable-model-invocation: true
---

# Release a mod

A release is a tag `<Mod>-vX.Y.Z` pushed on `main`. The tag is the only place the version is written: the Release
workflow (`.github/workflows/release.yml`) builds it into the DLL, stamps it into the zip, turns the mod's
`Unreleased` changelog entries into the release notes, creates the GitHub release, and commits the renamed changelog
section back to `main`. There are no version fields in files and nothing to bump.

Each mod is versioned independently. This skill's job is to pick the right version and push that tag.
Invoking it is the user's request to create and push the tag, **after one confirmation** (step 6), because pushing it
publishes a release. Never push anything else, and never move or delete an existing tag.

Arguments: `$ARGUMENTS` — the mod, optionally an exact version, or `dry-run`.

## 1. Which mod

The mod is a folder under `package/` (`ValheimQuickFix`, `SignIcons`, ...). If it is missing or unknown, list the
folders and ask which one.

If the argument says `dry-run` (or the user asks for a test run), skip to **Dry run** at the end.

## 2. Check the starting point

Stop with a clear message if any of these fails; do not work around it.

```bash
git fetch origin --tags
git status --short                         # must be empty
git rev-parse --abbrev-ref HEAD            # must be main (if clean, offer: git switch main)
git rev-parse HEAD origin/main             # both must be the same commit (git pull --ff-only if behind)
```

The tag goes on this commit, so it must be the current `origin/main`, with the changelog entries already merged.

## 3. Read what changed

1. Last release of this mod: `git tag --list '<Mod>-v*' --sort=-v:refname | head -1`. None means this is the first
   release.
2. Read the `## [Unreleased]` section of `package/<Mod>/CHANGELOG.md`. If it has no entries, stop: there is nothing to
   release, and entries belong in the PR that made the change.
3. Cross-check against the code: `git log <lastTag>..HEAD --oneline -- src/<Mod> package/<Mod>` (or all history for a
   first release). If commits changed what players see but no changelog entry covers them, tell the user and offer to
   stop so they can add it. Do not edit the changelog yourself.

## 4. Propose a version

Use semantic versioning, judged from the `Unreleased` entries:

| Entries | Bump |
| --- | --- |
| First release | `0.1.0` (unless the user says otherwise) |
| 0.x, a new feature or any change that breaks how players use the mod (renamed config, changed syntax, removed behavior) | minor |
| 0.x, only fixes | patch |
| 1.x+, breaking change | major |
| 1.x+, new feature | minor |
| 1.x+, only fixes | patch |

Say **why**, quoting the entries that decided it (for example "two new fixes under Added, so minor"), and show the
current version and the proposed tag. Ask with the AskUserQuestion tool: the recommendation first, the other bumps as
alternatives; the user can type an exact version under Other. A version containing a hyphen (`1.0.0-rc.1`) is published
as a pre-release.

If the user gave an exact version in the arguments, check it is valid, greater than the last release, and not already
tagged, and use it without proposing.

## 5. Check the tag is free

```bash
git tag --list '<Mod>-v<version>'                 # must print nothing
git ls-remote --tags origin '<Mod>-v<version>'    # must print nothing
```

## 6. Confirm, then tag

Show a short summary: mod, version, tag, the commit it goes on (`git log -1 --oneline`), and the changelog entries that
will become the release notes. Then ask once with AskUserQuestion whether to push the tag. State what happens: the
workflow builds the zip, publishes the release, and commits the renamed changelog section to `main`.

On yes:

```bash
git tag -a <Mod>-v<version> -m "<Mod> <version>"
git push origin <Mod>-v<version>
```

## 7. Follow the run

- Find the run: `gh run list --workflow release.yml --limit 3` (retry for a few seconds; it starts shortly after the push),
  and give the user its URL.
- `gh run watch <id>` until it finishes. On failure, show the failing step's log (`gh run view <id> --log-failed`) and
  explain; do not retry by moving the tag.
- On success verify: `gh release view <Mod>-v<version> --json name,isPrerelease,assets -q .` shows the zip, and
  `git fetch origin && git log -3 --oneline origin/main` shows the `Changelog: release ...` commit. A workflow
  annotation saying the changelog could not be committed means the user has to rename `[Unreleased]` by hand; say so.
- Tell the user the release is out and suggest importing the zip into Gale for a last check. Publishing to Thunderstore
  is separate (`tcli`, see `docs/Development.md`).

## Dry run

A dry run builds the package from `main` and uploads it as a workflow artifact. It creates no tag, no release, and
commits nothing.

```bash
gh workflow run release.yml -f mod=<Mod> -f version=0.0.0-dryrun
```

Then follow the run as in step 7 and point the user to the artifact. The workflow must already exist on `main`.

## Don'ts

- No AI or tool attribution anywhere (tag message, release notes, commits).
- Do not edit versions in files, edit the changelog, or commit anything; the workflow does the changelog commit.
- Do not push a tag that is not on `origin/main`, and do not force-push or delete tags.
- Do not release a different mod than the one the user named.
