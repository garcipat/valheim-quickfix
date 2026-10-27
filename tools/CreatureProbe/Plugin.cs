using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;

[BepInPlugin("dev.pgarcia.valheim.creatureprobe", "CreatureProbe", "0.0.1")]
public class Plugin : BaseUnityPlugin
{
    // Part of the creature's prefab name or of its kill key (defeated_<name>), case-insensitive.
    private const string Match = "Writhan";

    private static readonly HashSet<string> Names = new HashSet<string>();
    private bool _done;

    private void Update()
    {
        if (_done || Player.m_localPlayer == null || ZNetScene.instance == null) return;
        _done = true;
        try { Run(); } catch (Exception e) { Logger.LogError(e); }
    }

    private static bool IsMatch(string n)
    {
        return n != null && (n.IndexOf(Match, StringComparison.OrdinalIgnoreCase) >= 0 || Names.Contains(n));
    }

    // Every field of an object, by reflection, so fields newer than the compile-time libs show up too.
    private static string Dump(object o)
    {
        var sb = new StringBuilder();
        foreach (var f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            object v;
            try { v = f.GetValue(o); } catch { continue; }
            var u = v as UnityEngine.Object;
            if (v is IEnumerable && !(v is string)) { v = "[" + string.Join(",", ((IEnumerable)v).Cast<object>().Select(x => x is UnityEngine.Object ? ((UnityEngine.Object)x).name : "" + x).ToArray()) + "]"; }
            sb.Append(f.Name).Append('=').Append(u != null ? u.name : "" + v).Append("; ");
        }
        return sb.ToString();
    }

    private void Run()
    {
        var L = Logger;
        L.LogInfo("=== CreatureProbe start (" + Match + ") ===");
        var rate = typeof(Game).GetField("m_resourceRate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        L.LogInfo("resourceRate=" + (rate != null ? "" + rate.GetValue(null) : "?"));
        try { L.LogInfo("globalKeys=" + string.Join(",", ZoneSystem.instance.GetGlobalKeys().ToArray())); } catch (Exception e) { L.LogInfo("globalKeys n/a: " + e.Message); }

        // Creatures whose kill key matches, in case the prefab is named differently.
        var defeatKey = typeof(Character).GetField("m_defeatSetGlobalKey");
        foreach (var p in ZNetScene.instance.m_prefabs.Where(p => p != null))
        {
            var ch = p.GetComponent<Character>();
            var key = ch != null && defeatKey != null ? defeatKey.GetValue(ch) as string : null;
            if (key != null && key.IndexOf(Match, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Names.Add(p.name);
                L.LogInfo("DEFEATKEY " + p.name + " m_name=" + ch.m_name + " key=" + key + " boss=" + ch.m_boss + " faction=" + ch.m_faction + " health=" + ch.m_health);
            }
        }

        foreach (var p in ZNetScene.instance.m_prefabs.Where(p => p != null && IsMatch(p.name)))
        {
            L.LogInfo("PREFAB " + p.name);
            var cd = p.GetComponent<CharacterDrop>();
            if (cd == null) continue;
            foreach (var d in cd.m_drops) L.LogInfo("  DROP " + Dump(d));
        }

        var listType = typeof(SpawnSystem).Assembly.GetType("SpawnSystemList");
        if (listType == null) L.LogInfo("SpawnSystemList type missing");
        else
            foreach (var list in Resources.FindObjectsOfTypeAll(listType))
                foreach (var s in (IEnumerable)listType.GetField("m_spawners").GetValue(list))
                {
                    var prefab = s.GetType().GetField("m_prefab").GetValue(s) as UnityEngine.Object;
                    if (prefab != null && IsMatch(prefab.name)) L.LogInfo("SPAWN list=" + list.name + " " + Dump(s));
                }

        foreach (var c in Resources.FindObjectsOfTypeAll<CreatureSpawner>())
            if (c.m_creaturePrefab != null && IsMatch(c.m_creaturePrefab.name))
                L.LogInfo("CREATURESPAWNER " + c.name + " " + Dump(c));

        foreach (var a in Resources.FindObjectsOfTypeAll<SpawnArea>())
            foreach (var d in a.m_prefabs)
                if (d.m_prefab != null && IsMatch(d.m_prefab.name))
                    L.LogInfo("SPAWNAREA " + a.name + " " + Dump(d) + " | " + Dump(a));

        L.LogInfo("=== CreatureProbe end ===");
    }
}
