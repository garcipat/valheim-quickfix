# PortalTagSign — design

## Intent

A portal's tag is only visible on hover, so in a room of portals you can't see where each one leads. Players work
around it by hand-placing a sign inside each portal's ring with `<color=#ffffff>` in front of the tag. That works but
is tedious, and the sign has to be redone when the tag changes. This fix places that sign automatically.

## Goals

- Every tagged portal shows a sign centred in the upper half of its ring, facing out, with the tag in white —
  looking like the hand-placed version.
- The sign follows the portal's tag (rename → new text; tag cleared → no sign).
- Works for the wooden portal (tuned in-game) and the stone portal and other `TeleportWorld` portals
  (best-guess placement until they can be checked in-game).

## Non-goals

- No sign on the back of the portal (can be added later).
- No editing, hover text or interaction on the generated sign.

## Anti-goals

- Nothing is saved to the world or synced: other players never see or receive the sign.
- The sign must never block walking through the portal or be hittable/removable.
- No tag text the game would hide on hover (filtered/blocked authors) shows on the sign.

## Approaches

- **Local copy of the vanilla `sign` prefab inside the ring (picked).** Looks exactly like the hand-placed sign.
  Created under `ZNetView.m_forceDisableInit` so it gets no ZDO, then `Sign`, `Piece`, `WearNTear`, `ZNetView` and
  colliders are removed.
- Floating text with no sign model — rejected: the user wants the real sign.
- Sign on the ground in front of the portal — rejected: the user's screenshot hangs it inside the ring, which also
  avoids clipping into terrain and nearby builds.

## Decisions

- QuickFix, file `Patches/PortalTagSignPatch.cs`, toggle `PortalTagSign` under `Building & Travel`, default on.
  (AGENTS.md would put new functionality in its own mod; the user chose QuickFix — it fixes tags being hover-only.)
- Text is `<color=#ffffff>` + the tag as the portal's hover shows it.
- Position/rotation per portal prefab name, with a fallback for unknown portals.
- The text widget is TextMeshPro, driven through reflection as in SignIcons.

## Left to the builder

- The exact offsets (tuned in-game), the hook for creating and refreshing the sign, and how often the tag is re-read.
