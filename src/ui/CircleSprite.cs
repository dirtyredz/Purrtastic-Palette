using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// A plain white circle, generated once and tinted per swatch. The vanilla customization
    /// screen uses large round swatches; a square plate next to them reads as obviously bolted on.
    /// Generated rather than cloned from the game's own swatch prefab because the prefab is built
    /// per customization item with its own selection/hover wiring - a bare sprite is far less to
    /// go wrong, and the shape is the only part that matters here.
    /// </summary>
    internal static class CircleSprite
    {
        private const int Size = 128;
        private static Sprite cached;

        internal static Sprite Get()
        {
            if (cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "PurrtasticPalette_Circle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var centre = (Size - 1) * 0.5f;
            var radius = centre - 1f;
            var pixels = new Color[Size * Size];

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    // One-pixel feathered edge - a hard cutoff looks visibly jagged at the size
                    // these are drawn, since the source is only 128px scaled up.
                    var alpha = Mathf.Clamp01(radius - distance);
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            cached = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            return cached;
        }
    }
}
