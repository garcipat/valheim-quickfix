using System;
using System.IO;
using UnityEngine;

namespace ValheimMoreSkills.Core
{
    /// <summary>
    /// A skill icon drawn for this mod (<c>tools/SkillIcons/</c>) and embedded in the DLL, for skills the game has no
    /// fitting sprite for. Loaded once on first use; null if loading failed.
    /// </summary>
    internal sealed class EmbeddedIcon
    {
        private readonly string _name;
        private Sprite _sprite;
        private bool _loaded;

        internal EmbeddedIcon(string name)
        {
            _name = name;
        }

        internal Sprite Get()
        {
            if (!_loaded)
            {
                _loaded = true;
                _sprite = Load(_name);
            }
            return _sprite;
        }

        private static Sprite Load(string name)
        {
            try
            {
                using (var stream = typeof(EmbeddedIcon).Assembly.GetManifestResourceStream("ValheimMoreSkills." + name))
                {
                    if (stream == null)
                    {
                        Plugin.Log.LogError($"Embedded {name} not found");
                        return null;
                    }
                    var bytes = new byte[stream.Length];
                    var read = 0;
                    while (read < bytes.Length)
                    {
                        var n = stream.Read(bytes, read, bytes.Length - read);
                        if (n <= 0)
                        {
                            throw new EndOfStreamException();
                        }
                        read += n;
                    }
                    // Mipmaps keep the icon smooth when the skills list draws it much smaller than its 128 px.
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear };
                    if (!tex.LoadImage(bytes))
                    {
                        Plugin.Log.LogError($"Loading {name} failed: not a valid image");
                        return null;
                    }
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Loading {name} failed: {e}");
                return null;
            }
        }
    }
}
