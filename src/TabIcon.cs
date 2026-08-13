using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace CatColorProbe
{
    /// <summary>
    /// The Cat Form wardrobe tab icon. Prefers a user-supplied PNG dropped at
    /// <c>BepInEx/config/CatColorProbe/tab-icon.png</c> so custom art can be swapped in without a
    /// rebuild; falls back to the generated <see cref="PawSprite"/> when that file is absent or
    /// unreadable. The tab widget assigns the sprite without tinting, so the PNG's own colours show.
    /// </summary>
    internal static class TabIcon
    {
        private const string FileName = "tab-icon.png";

        private static Sprite cached;
        private static bool tried;

        internal static Sprite Get()
        {
            if (tried)
            {
                return cached != null ? cached : PawSprite.Get();
            }

            tried = true;
            try
            {
                var path = Path.Combine(Paths.ConfigPath, "CatColorProbe", FileName);
                if (!File.Exists(path))
                {
                    CatColorProbePlugin.Log.LogInfo(
                        $"[CatColorProbe] Wardrobe: no custom tab icon at '{path}' - using the generated paw.");
                    return PawSprite.Get();
                }

                var data = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
                {
                    name = "CatColorProbe_TabIcon",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };

                // LoadImage resizes the texture to the PNG's dimensions and decodes it (alpha kept).
                if (ImageConversion.LoadImage(texture, data))
                {
                    cached = Sprite.Create(
                        texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                    CatColorProbePlugin.Log.LogInfo(
                        $"[CatColorProbe] Wardrobe: loaded custom tab icon {texture.width}x{texture.height} from '{path}'.");
                }
                else
                {
                    UnityEngine.Object.Destroy(texture);
                    CatColorProbePlugin.Log.LogWarning(
                        $"[CatColorProbe] Wardrobe: '{path}' is not a decodable PNG - using the generated paw.");
                }
            }
            catch (Exception e)
            {
                CatColorProbePlugin.Log.LogError($"[CatColorProbe] Wardrobe: failed to load custom tab icon: {e}");
            }

            return cached != null ? cached : PawSprite.Get();
        }
    }
}
