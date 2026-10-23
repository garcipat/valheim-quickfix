# Adding a fix

1. **Find the vanilla method.** Decompile `assembly_valheim.dll` (dnSpy / ILSpy) and locate the
   method that causes the problem.
2. **Add a config toggle** in `Plugin.Awake`:
   `MyFixEnabled = Config.Bind("Section", "MyFix", true, "What it does.");`
   Reuse a section that fits (`Inventory`, `Gear`, `Crafting`, `Building & Travel`, `Enemies & Drops`, `General`) rather than adding a new one (a new section needs the maintainer's agreement first).
3. **Create `Patches/MyFixPatch.cs`** with a `[HarmonyPatch(typeof(X), nameof(X.Method))]` class.
   Return early when the toggle is off, and guard with `Player.m_localPlayer` where relevant.
4. **Build:** `dotnet build src/ValheimQuickFix -c Release`.
5. **Test in-game** with `-p:PluginsDir=<Valheim>/BepInEx/plugins`. Check `BepInEx/LogOutput.log`
   for patch errors.
6. **Document:** add a section to [Fixes.md](Fixes.md) and an entry under `Unreleased` in
   [the changelog](../package/ValheimQuickFix/CHANGELOG.md).
