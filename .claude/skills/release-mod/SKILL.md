---
name: release-mod
description: Release a new version of one of the Valheim mods in this repo — read its changelog, propose the next version, and open the release pull request that closes the changelog; merging it makes the Release workflow tag and publish <Mod>-vX.Y.Z, and optionally upload it to Thunderstore. Use when the user asks to release, publish, tag, or ship a new version of a mod.
argument-hint: "[Mod] [version | dry-run]"
disable-model-invocation: true
---

# Release a mod

A release is a **release pull request** from a branch `release/<Mod>-vX.Y.Z` that renames the mod's `## [Unreleased]`
changelog section to `## [X.Y.Z] - date`. Merging it into `main` starts the Release workflow
(`.github/workflows/release.yml`), which tags the merge commit `<Mod>-vX.Y.Z`, builds that version into the DLL, stamps it
into the zip, uses the closed section as the release notes, and creates the GitHub release. If the pull request carries
the label `publish-thunderstore`, a second job also uploads the package to Thunderstore. So the changelog is closed on
`main` before the tag exists, and the tagged commit carries it. The tag is the only place the version is written; there
are no version fields in files and nothing to bump.

Each mod is versioned independently. This skill's job is to pick the right version and open that pull request.
Invoking it is the user's request to push the release branch and open the pull request, **after one confirmation**
(step 6). The release itself happens only when the user merges it. Never push anything else, never push a tag yourself
(the workflow tags), and never move or delete an existing tag.

Arguments (all optional): `$ARGUMENTS` — the mod, then optionally an exact version or `dry-run`. Anything left out is asked
for with a selection, so nothing has to be typed exactly.

## 1. Which mod, and what to do

The mods are the folders under `package/` (`ls package/`).

- **Mod given:** if `$ARGUMENTS` names one, use it. Match case-insensitively and accept an unambiguous prefix (`sign`
  for `SignIcons`, `quick` for `ValheimQuickFix`).
- **Mod missing or unclear:** ask with AskUserQuestion, with **one option per mod folder**, so the user just picks. Make
  each option's description say where the mod stands, so the choice is informed:
  `last release <Mod>-v0.1.0 · 3 unreleased changelog entries`, or `never released · 4 unreleased entries`
  (last tag: `git tag --list '<Mod>-v*' --sort=-v:refname | head -1`; entries: the `- ` lines under `## [Unreleased]`
  in `package/<Mod>/CHANGELOG.md`). Put a mod with unreleased entries first and mark it as recommended. With more than
  four mods, offer the four with the most unreleased entries; the rest can be typed under Other.
- **Release or dry run:** if the arguments don't say, ask it in the same AskUserQuestion call as a second question:
  *Release* (recommended) or *Dry run* (builds the zip as a workflow artifact, publishes nothing). Skip the question when
  the arguments gave an exact version or `dry-run`.

If the choice is a dry run, skip to **Dry run** at the end.

## 2. Check the starting point

Stop with a clear message if any of these fails; do not work around it.

```bash
git fetch origin --tags
git status --short                         # must be empty
git rev-parse --abbrev-ref HEAD            # must be main (if clean, offer: git switch main)
git rev-parse HEAD origin/main             # both must be the same commit (git pull --ff-only if behind)
```

The release branch starts from this commit, so it must be the current `origin/main`, with the changelog entries already
merged. Also check no release pull request for this mod is open (`gh pr list --search 'head:release/<Mod>-v'`); if one
is, point the user to it instead of opening another.

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

## 5. Check the tag and branch are free

```bash
git tag --list '<Mod>-v<version>'                              # must print nothing
git ls-remote --tags origin '<Mod>-v<version>'                 # must print nothing
git ls-remote --heads origin 'release/<Mod>-v<version>'        # must print nothing
```

## 6. Confirm, then open the release pull request

Show a short summary: mod, version, tag, the commit the branch starts from (`git log -1 --oneline`), and the changelog
entries that will become the release notes. Then ask once with AskUserQuestion whether to open the release pull request.
State what happens: it closes the changelog section; merging it tags the merge commit and publishes the release.

In the same AskUserQuestion call, ask a second question: **publish to Thunderstore too?** (*No, GitHub release only* /
*Yes, also upload to Thunderstore*). Skip it, and say why, when the version has a hyphen: Thunderstore has no
pre-release versions, so the workflow would not upload it. Before offering it, check the upload can work:
`gh secret list --json name -q '.[].name' | grep -x TCLI_AUTH_TOKEN`. If the secret is missing, say so in the question
(the upload job would fail after the GitHub release is already out) and point to the setup in `docs/Development.md`
under *Publishing to Thunderstore*. Never ask for, print, or set the token yourself.

On yes, close the section with the same script the workflow uses, on a branch named after the tag:

```bash
git switch -c release/<Mod>-v<version>
./scripts/update-changelog.sh <Mod> <version> --notes-file <scratch>/release-notes.md
git commit -m "Release <Mod> <version>" package/<Mod>/CHANGELOG.md
git push -u origin release/<Mod>-v<version>
gh pr create --base main --head release/<Mod>-v<version> --title "Release <Mod> <version>" --body-file <body>
git switch main
```

When the user chose Thunderstore, create the label if the repository does not have it yet and add it to the pull
request (`--label` fails for a label that doesn't exist):

```bash
gh label create publish-thunderstore --color 1d76db --description "Also upload this release to Thunderstore" 2>/dev/null || true
gh pr create ... --label publish-thunderstore
```

The label is the only switch: the user can still add or remove it on the pull request before merging, and the workflow
reads it at merge time.

`git diff` before the commit must show only `package/<Mod>/CHANGELOG.md`: a new `## [<version>] - <today>` heading
under a fresh, empty `## [Unreleased]`. The body says that merging releases `<Mod>-v<version>` (and uploads it to Thunderstore, if chosen), then quotes the
notes file. The date is the day the pull request is opened; if it is merged on a later day, that is fine.

## 7. Merge, then follow the run

- Give the user the pull request link. `main` requires a code-owner review, so the user merges it, typically with
  `gh pr merge <n> --squash --admin --delete-branch`. Do not merge it yourself unless the user asks you to.
- After the merge, find the run: `gh run list --workflow release.yml --event pull_request --limit 3` (retry for a few
  seconds), and give the user its URL. A merge into `main` from another branch, or a closed but unmerged release pull
  request, starts no release.
- `gh run watch <id>` until it finishes (it includes the `publish` job when the label was set). On failure, show the failing step's log (`gh run view <id> --log-failed`) and
  explain. Re-running the failed run is safe for a passing problem (it keeps a tag it already made on the same commit),
  but it runs the merge commit's code and workflow again; a problem in those needs a fix on `main` and a new release.
  Never move the tag.
- On success verify: `gh release view <Mod>-v<version> --json name,isPrerelease,assets -q .` shows the zip and
  `git ls-remote --tags origin '<Mod>-v<version>'` shows the tag. There is no follow-up changelog pull request.
- If the package went to Thunderstore, its page is `https://thunderstore.io/c/valheim/p/garcipat/<Mod>/` (a new version
  can take a minute to appear, and Gale lists it shortly after). If only the `publish` job failed, the release is fine:
  show its log, fix the cause (usually the `TCLI_AUTH_TOKEN` secret), and re-run just that job with
  `gh run rerun <id> --job <publish job id>`. Thunderstore refuses a version it already has, so check the page before
  publishing by hand after a partial success.
- Tell the user the release is out. Without Thunderstore, suggest importing the zip into Gale for a last check; to
  publish later, upload the zip by hand (`docs/Development.md`) rather than re-releasing.

## By hand (fallback)

Pushing a tag `<Mod>-vX.Y.Z` on `main` still releases: the workflow renames `[Unreleased]` on its own copy for the notes
and afterwards pushes a `release/changelog-<Mod>-X.Y.Z` branch with the renamed section, which needs a pull request to
`main` (GitHub Actions may not open one in this repo, so open it by hand). Only use this when the user asks for it.

## Dry run

A dry run builds the package from `main` and uploads it as a workflow artifact. It creates no tag, no release, and
commits nothing.

```bash
gh workflow run release.yml -f mod=<Mod> -f version=0.0.0
```

Then follow the run as in step 7 and point the user to the artifact. It holds both zips: the Gale one and, under
`thunderstore/`, the one that would be uploaded; building it also proves the tcli config. Nothing is uploaded to
Thunderstore. The workflow must already exist on `main`. Keep the version at `X.Y.Z` (`0.0.0`): tcli rejects a version
with a hyphen, so the run would skip the Thunderstore package.

## Don'ts

- No AI or tool attribution anywhere (pull request, release notes, commits).
- Do not edit versions in files or the changelog by hand; the only commit is the one `scripts/update-changelog.sh` makes
  on the release branch.
- Do not push tags (the workflow does), and do not force-push or delete tags.
- Do not release a different mod than the one the user named.
- Do not add the `publish-thunderstore` label unless the user said yes to it, and never touch the Thunderstore token.
