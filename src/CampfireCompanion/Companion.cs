using System;
using System.Collections.Generic;

namespace ValheimCampfireCompanion
{
    /// <summary>The creatures offered in the config, by biome. Flyers, sea creatures and huge bosses are left out.</summary>
    public enum Companion
    {
        None,
        Random,
        Deer,
        Greydwarf,
        Troll,
        Bear,
        Draugr,
        Skeleton,
        Ghost,
        Wraith,
        Blob,
        Oozer,
        Kvastur,
        Surtling,
        Wolf,
        Fenring,
        Ulv,
        Drake,
        Cultist,
        Lox,
        Unbjorn,
        Fuling,
        FulingBerserker,
        FulingShaman,
        SeekerSoldier,
        Dvergr,
        Asksvin,
        CharredMage,
        Morgen,
        FallenValkyrie,
        Moose,
        Haldor,
        Hildir,
        BogWitch,
    }

    /// <summary>How one creature stands at the fire.</summary>
    internal sealed class CompanionSetup
    {
        internal readonly string Prefab;
        // Size relative to the creature in the world.
        internal readonly float Scale;
        // Metres from the character's spot, past the fire on the left, to the creature's nearest side as its renderer
        // bounds measure it. Wide models (wings, long tails) measure wider than they look, so theirs can be negative.
        internal readonly float Gap;
        // Degrees clockwise (seen from above) for models that don't face their own forward (Haldor sits facing back).
        internal readonly float Turn;

        internal CompanionSetup(string prefab, float scale, float gap, float turn = 0f)
        {
            Prefab = prefab;
            Scale = scale;
            Gap = gap;
            Turn = turn;
        }
    }

    internal static class Companions
    {
        private static readonly Random Rng = new Random();

        private static readonly Dictionary<Companion, CompanionSetup> Setups = new Dictionary<Companion, CompanionSetup>
        {
            { Companion.Deer, new CompanionSetup("Deer", 1f, 3.14f) },
            { Companion.Greydwarf, new CompanionSetup("Greydwarf", 1f, 2.89f) },
            { Companion.Troll, new CompanionSetup("Troll", 0.72f, 1.14f) },
            { Companion.Bear, new CompanionSetup("Bjorn", 1.05f, 2.64f) },
            { Companion.Draugr, new CompanionSetup("Draugr", 1f, 3.64f) },
            { Companion.Skeleton, new CompanionSetup("Skeleton", 1.05f, 3.14f) },
            { Companion.Ghost, new CompanionSetup("Ghost", 1f, 3.19f) },
            { Companion.Wraith, new CompanionSetup("Wraith", 1f, 1.89f) },
            { Companion.Blob, new CompanionSetup("Blob", 1f, 3.19f) },
            { Companion.Oozer, new CompanionSetup("BlobElite", 1f, 2.94f) },
            { Companion.Kvastur, new CompanionSetup("BogWitchKvastur", 1f, 3.19f) },
            { Companion.Surtling, new CompanionSetup("Surtling", 1f, 3.19f) },
            { Companion.Wolf, new CompanionSetup("Wolf", 1f, 2.64f) },
            { Companion.Fenring, new CompanionSetup("Fenring", 0.9f, 1.64f) },
            { Companion.Ulv, new CompanionSetup("Ulv", 1.1f, 2.89f) },
            { Companion.Drake, new CompanionSetup("Hatchling", 1f, 2.44f) },
            { Companion.Cultist, new CompanionSetup("Fenring_Cultist", 0.95f, 2.19f) },
            { Companion.Lox, new CompanionSetup("Lox", 0.7f, 3.14f) },
            { Companion.Unbjorn, new CompanionSetup("Unbjorn", 0.9f, 2.94f) },
            { Companion.Fuling, new CompanionSetup("Goblin", 1f, 3.39f) },
            { Companion.FulingBerserker, new CompanionSetup("GoblinBrute", 1f, 1.39f) },
            { Companion.FulingShaman, new CompanionSetup("GoblinShaman", 1f, 2.89f) },
            { Companion.SeekerSoldier, new CompanionSetup("SeekerBrute", 0.9f, 2.89f) },
            { Companion.Dvergr, new CompanionSetup("Dverger", 1f, 2.89f) },
            { Companion.Asksvin, new CompanionSetup("Asksvin", 1f, 3.14f) },
            { Companion.CharredMage, new CompanionSetup("Charred_Mage", 1f, 2.94f) },
            { Companion.Morgen, new CompanionSetup("Morgen", 0.72f, 0.89f) },
            { Companion.FallenValkyrie, new CompanionSetup("FallenValkyrie", 0.64f, -2.11f) },
            { Companion.Moose, new CompanionSetup("Moose", 0.9f, 2.69f) },
            { Companion.Haldor, new CompanionSetup("Haldor", 1f, 3.64f, 180f) },
            { Companion.Hildir, new CompanionSetup("Hildir", 1f, 3.14f) },
            { Companion.BogWitch, new CompanionSetup("BogWitch", 0.95f, 1.94f) },
        };

        /// <summary>The setup for a creature, a random one for Random, or null for None.</summary>
        internal static CompanionSetup Get(Companion companion)
        {
            if (companion == Companion.Random)
            {
                // Every value after None and Random is a creature.
                var all = (Companion[])Enum.GetValues(typeof(Companion));
                companion = all[Rng.Next(2, all.Length)];
            }

            return Setups.TryGetValue(companion, out var setup) ? setup : null;
        }
    }
}
