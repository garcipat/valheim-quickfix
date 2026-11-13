# PortalTagSign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a local-only vanilla sign with the portal's tag in white inside each tagged portal's ring.

**Architecture:** One QuickFix patch file. A postfix on `TeleportWorld.UpdatePortal` (vanilla runs it every 0.5 s on
portals with a ZDO) creates, updates, hides or removes a child sign. The sign is the `sign` prefab instantiated under
`ZNetView.m_forceDisableInit` (as the placement ghost does), stripped of every game-assembly component and every collider.
It is placed relative to the portal opening's `TeleportWorldTrigger` box, so every portal type gets a position.

**Tech Stack:** C# / .NET 4.8, HarmonyX, BepInEx. TextMeshPro via reflection (no compile-time reference).

**Spec:** `docs/superpowers/specs/2026-11-13-portal-tag-sign-design.md`

## Global Constraints

- Client-side only: no ZDO, no RPC, nothing saved.
- Toggle `PortalTagSign`, section `Building & Travel`, default `true`; patch returns early (and hides signs) when off.
- Text = `<color=#ffffff>` + `TeleportWorld.GetText()` with rich text removed.
- No tests exist in this repo; verification is build + in-game check.

## Review Focus

- Tag cleared → sign removed, not left showing the old tag.
- Toggle turned off at runtime → existing signs hidden.
- Portal with no `TeleportWorldTrigger` / no `sign` prefab → no sign, one warning, no exception spam.
- Teleporting through the portal and the PortalPressOrHold hover collider are unaffected (sign has no colliders).
- Tags the game filters show filtered on the sign (uses `GetText()`, the same source as hover).

---

### Task 1: PortalTagSign patch

**Files:**
- Create: `src/ValheimQuickFix/Patches/PortalTagSignPatch.cs`
- Modify: `src/ValheimQuickFix/Plugin.cs` (field `PortalTagSignEnabled`, bind after `PortalPressOrHold`)
- Modify: `docs/Fixes.md`, `package/ValheimQuickFix/CHANGELOG.md`, `package/ValheimQuickFix/README.md` if it lists fixes

- [ ] Implement `[HarmonyPatch(typeof(TeleportWorld), "UpdatePortal")] Postfix(TeleportWorld __instance)` with the
      child named `QuickFixPortalTagSign`; position = trigger box centre raised by a fraction of its height, rotation =
      portal rotation plus a yaw constant.
- [ ] `dotnet build src/ValheimQuickFix -c Release` → 0 errors, 0 warnings.
- [ ] `./scripts/package.sh ValheimQuickFix`, copy as `ValheimQuickFix-dev.zip` to `~/share`.
- [ ] In-game: wooden portal with tag shows the sign in the upper ring, readable from the front; rename updates it;
      clearing the tag removes it; walking through still teleports.
