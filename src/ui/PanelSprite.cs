// Ported from mods/ModNook/src/ui/PanelSprite.cs, namespace aside. See 10-visual-integration.md.
// Fix bugs in both copies.
using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// Generates the rounded, gold-edged plate the mods in this repo use for their own panels.
    /// 9-sliced, so the corners hold their radius at any size.
    /// </summary>
    internal static class PanelSprite
    {
        private const int Size = 48;
        private const int Corner = 14;
        private const float EdgeWidth = 2.0f;

        // The game's window palette: deep plum fill, muted gold rim. See 10-visual-integration.md.
        private static readonly Color Fill = new Color32(0x1B, 0x0F, 0x2E, 0xFF);
        private static readonly Color Edge = new Color32(0xC7, 0xA2, 0x5B, 0xFF);

        private static Sprite cached;

        internal static Sprite Get()
        {
            if (cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "PurrtasticPalette_Panel"
            };

            var pixels = new Color[Size * Size];

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var distance = RoundedRectDistance(x + 0.5f, y + 0.5f);

                    var coverage = Mathf.Clamp01(0.5f - distance);
                    var rim = Mathf.Clamp01(distance + EdgeWidth) * Mathf.Clamp01(0.5f - distance);

                    var colour = Color.Lerp(Fill, Edge, Mathf.Clamp01(rim));
                    colour.a = coverage;

                    pixels[y * Size + x] = colour;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var border = new Vector4(Corner, Corner, Corner, Corner);
            cached = Sprite.Create(
                texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            cached.name = "PurrtasticPalette_Panel";
            return cached;
        }

        private static Sprite plain;

        /// <summary>
        /// A white rounded plate, for anything whose colour is set by an Image tint.
        ///
        /// <see cref="Get"/> bakes the game's plum and gold into its pixels, and an Image's colour
        /// multiplies with what is there - so tinting it renders every swatch through a plum filter
        /// and none of them are the colour they claim. White multiplies to exactly the tint.
        /// </summary>
        internal static Sprite Plain()
        {
            if (plain != null)
            {
                return plain;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "PurrtasticPalette_Plain"
            };

            var pixels = new Color[Size * Size];

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var distance = RoundedRectDistance(x + 0.5f, y + 0.5f);

                    var colour = Color.white;
                    colour.a = Mathf.Clamp01(0.5f - distance);

                    pixels[y * Size + x] = colour;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var border = new Vector4(Corner, Corner, Corner, Corner);
            plain = Sprite.Create(
                texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            plain.name = "PurrtasticPalette_Plain";
            return plain;
        }

        private static float RoundedRectDistance(float x, float y)
        {
            var halfSize = Size * 0.5f;
            var innerHalf = halfSize - Corner;

            var dx = Mathf.Abs(x - halfSize) - innerHalf;
            var dy = Mathf.Abs(y - halfSize) - innerHalf;

            var outsideX = Mathf.Max(dx, 0f);
            var outsideY = Mathf.Max(dy, 0f);
            var outside = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);

            var inside = Mathf.Min(Mathf.Max(dx, dy), 0f);

            return outside + inside - Corner;
        }
    }
}
