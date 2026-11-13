using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace ValheimSignIcons.Patches
{
    /// <summary>
    /// The English text of every translation key, whatever language the player has selected, so a sign can name an
    /// item the way an English player sees it and every player with the mod gets the same icon.
    /// </summary>
    /// <remarks>
    /// The game only keeps the selected language in memory, so the English column is read from its own localization
    /// files, once. Those files and the CSV splitter are private, so they are reached through reflection.
    /// </remarks>
    internal static class EnglishNames
    {
        private const string English = "English";

        private static Dictionary<string, string> _names;

        // Keyed by translation key without the '$', for example "item_roundlog". Empty if the files can't be read.
        public static Dictionary<string, string> Get()
        {
            if (_names != null)
            {
                return _names;
            }

            _names = new Dictionary<string, string>();
            try
            {
                Load(_names);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"English item names unavailable, only prefab names work on signs: {e.Message}");
            }

            return _names;
        }

        private static void Load(Dictionary<string, string> names)
        {
            var localization = Localization.instance;
            var settings = AccessTools.Field(typeof(Localization), "m_localizationSettings")?.GetValue(null);
            var files = settings != null
                ? AccessTools.Property(settings.GetType(), "Localizations")?.GetValue(settings, null) as IEnumerable
                : null;
            var split = AccessTools.Method(typeof(Localization), "DoQuoteLineSplit");
            if (files == null || split == null)
            {
                throw new MissingMemberException("Localization files or DoQuoteLineSplit not found");
            }

            // Later files overwrite earlier ones, as in the game.
            foreach (var file in files)
            {
                var asset = file as TextAsset;
                if (asset == null)
                {
                    continue;
                }

                var reader = new StringReader(asset.text);
                var header = reader.ReadLine();
                if (header == null)
                {
                    continue;
                }

                // The game falls back to column 1 for an empty translation, so that is English too.
                var column = Array.FindIndex(header.Split(','), cell => cell.Trim().Trim('"') == English);
                if (column < 0)
                {
                    column = 1;
                }

                var rows = (List<List<string>>)split.Invoke(localization, new object[] { reader });
                foreach (var row in rows)
                {
                    if (row.Count <= column || row[0].Length == 0 || row[0].StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var text = row[column].Trim();
                    if (text.Length != 0)
                    {
                        names[row[0]] = text;
                    }
                }
            }
        }
    }
}
