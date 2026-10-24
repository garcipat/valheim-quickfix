using System;
using BepInEx.Configuration;
using UnityEngine;

namespace ValheimMoreSkills.Core
{
    /// <summary>
    /// A skill this mod adds. Its <see cref="Type"/> is a <c>Skills.SkillType</c> value outside the enum, taken from a
    /// stable hash of its identifier, so it never changes between versions and is what the game saves in the
    /// character file.
    /// </summary>
    internal sealed class CustomSkill
    {
        private readonly Func<Sprite> _icon;

        internal CustomSkill(string identifier, string name, string description, Func<Sprite> icon, ConfigEntry<bool> enabled)
        {
            Type = TypeOf(identifier);
            Name = name;
            Description = description;
            _icon = icon;
            Enabled = enabled;
        }

        internal Skills.SkillType Type { get; }

        /// <summary>
        /// Kept positive: the game localizes the name as <c>$skill_&lt;number&gt;</c>, and a minus sign ends the word, so
        /// a negative number shows as "[skill_]-123".
        /// </summary>
        internal static Skills.SkillType TypeOf(string identifier)
        {
            return (Skills.SkillType)(identifier.GetStableHashCode() & int.MaxValue);
        }

        /// <summary>English display name. Also the name <c>raiseskill</c> accepts.</summary>
        internal string Name { get; }

        internal string Description { get; }

        internal ConfigEntry<bool> Enabled { get; }

        /// <summary>
        /// The skills dialog and the skill-up message localize <c>"$skill_" + type.ToString().ToLower()</c>; for a value
        /// outside the enum that is the number.
        /// </summary>
        internal string NameKey => "skill_" + Type.ToString().ToLower();

        /// <summary>The skill's icon, or null while the game has not loaded what it comes from yet.</summary>
        internal Sprite Icon()
        {
            return _icon();
        }
    }
}
