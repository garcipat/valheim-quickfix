using System;
using System.Reflection;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Going to bed always shows the same "zzzZZZzzz" on the black sleep screen. <c>SleepText.OnEnable</c> fades in
    /// <c>m_textField</c> each time the screen comes up, so setting its text just before picks a new line every night.
    /// The dream text shown afterwards is the game's own and is left alone. TextMeshPro is reached through reflection
    /// because the compile-time game libraries don't reference it.
    /// </summary>
    [HarmonyPatch]
    internal static class RandomSleepTextPatch
    {
        private static readonly string[] Lines =
        {
            "zzzZZZzzz",
            "Hrrrnngh... zzz...",
            "Snrrrk... snrrrk...",
            "Krrrrr-pfffff...",
            "Mimimimimi...",
            "zZz... *rolls over*... zZz",
            "...more mead...",
            "...five more minutes, Odin...",
            "Skål... zzz...",
            "...who took my axe...",
            "...just one more tree...",
            "Not the Bonemass... please...",
            "*mumbles about trolls*",
            "*counts Necks jumping over a fence*",
            "...the deer... it's looking at me...",
            "*drools on the furs*",
            "...the roof is leaking again...",
            "...need more fine wood...",
            "*dreams of Valhalla*",
        };

        private static readonly Type SleepTextType = AccessTools.TypeByName("SleepText");
        private static readonly FieldInfo TextField = AccessTools.Field(SleepTextType, "m_textField");
        private static readonly PropertyInfo TextProperty =
            AccessTools.Property(AccessTools.TypeByName("TMPro.TMP_Text"), "text");

        private static int _last = -1;
        private static string _original;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(SleepTextType, "OnEnable");
        }

        private static bool Prepare()
        {
            if (TargetMethod() != null && TextField != null && TextProperty != null)
            {
                return true;
            }

            Plugin.Log.LogWarning("RandomSleepText disabled: SleepText.m_textField is missing in this game version");
            return false;
        }

        private static void Prefix(object __instance)
        {
            try
            {
                var field = TextField.GetValue(__instance);
                if (field == null)
                {
                    return;
                }

                if (_original == null)
                {
                    _original = TextProperty.GetValue(field) as string;
                }

                if (!Plugin.RandomSleepTextEnabled.Value)
                {
                    // Put the game's text back in case the toggle was turned off mid-game.
                    if (_original != null)
                    {
                        TextProperty.SetValue(field, _original);
                    }

                    return;
                }

                // Never the same line two nights in a row.
                int pick;
                if (_last < 0)
                {
                    pick = UnityEngine.Random.Range(0, Lines.Length);
                }
                else
                {
                    pick = UnityEngine.Random.Range(0, Lines.Length - 1);
                    if (pick >= _last)
                    {
                        pick++;
                    }
                }

                _last = pick;
                TextProperty.SetValue(field, Lines[pick]);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"RandomSleepText patch failed: {e}");
            }
        }
    }
}
