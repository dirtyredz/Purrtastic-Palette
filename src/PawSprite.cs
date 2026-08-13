using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// A flat cream-gold paw print, generated once for the Cat Form wardrobe tab. The game's own
    /// tab glyphs (face, eyes, scissors, clothes, hat) are flat single-colour gold shapes, and the
    /// Cat Form item's real icon - a detailed multicolour sprite - stood out against them. A paw
    /// print in the same cream-gold reads as "cat" while matching the tab strip's style, and the
    /// same idea extends to the sibling form mods (a bat/fin glyph in the same colour).
    /// </summary>
    internal static class PawSprite
    {
        private const int Size = 128;

        // A light-brown paw with a dark-brown outline. The tab widget assigns our sprite without
        // tinting it, so both colours are baked in here.
        private static readonly Color Fill = new Color(0.816f, 0.655f, 0.435f, 1f);    // light brown
        private static readonly Color Outline = new Color(0.475f, 0.333f, 0.153f, 1f); // dark brown
        private const float OutlineWidth = 8f;                                         // px on the 128 canvas

        private static Sprite cached;

        internal static Sprite Get()
        {
            if (cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "PurrtasticPalette_Paw",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            // Main pad (a wide ellipse) plus four toe beans fanned above it. Coordinates are in
            // pixels with y pointing up; the layout is centred horizontally and reads as a paw.
            var mainPad = new Ellipse(64f, 46f, 30f, 26f);
            var toes = new[]
            {
                new Ellipse(33f, 78f, 12f, 14f),  // outer left
                new Ellipse(53f, 95f, 12.5f, 15f), // inner left
                new Ellipse(75f, 95f, 12.5f, 15f), // inner right
                new Ellipse(95f, 78f, 12f, 14f),  // outer right
            };

            // Grown copies of each shape (radii + outline width) form the outline silhouette; the
            // original shapes are the fill on top. Where a pixel is in the fill it's the fill
            // colour, where it's only in the grown silhouette it's the outline colour, and the
            // overall alpha is the grown silhouette so the outline wraps the whole paw.
            var mainPadOut = mainPad.Grown(OutlineWidth);
            var toesOut = new Ellipse[toes.Length];
            for (var i = 0; i < toes.Length; i++)
            {
                toesOut[i] = toes[i].Grown(OutlineWidth);
            }

            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var fillA = mainPad.Coverage(x, y);
                    var outA = mainPadOut.Coverage(x, y);
                    for (var i = 0; i < toes.Length; i++)
                    {
                        fillA = Mathf.Max(fillA, toes[i].Coverage(x, y));
                        outA = Mathf.Max(outA, toesOut[i].Coverage(x, y));
                    }

                    // Lerp outline -> fill by fill coverage (the feathered edge blends them), and use
                    // the grown silhouette for alpha so the outline forms a band around the fill.
                    var rgb = Color.Lerp(Outline, Fill, fillA);
                    pixels[y * Size + x] = new Color(rgb.r, rgb.g, rgb.b, outA);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            cached = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            return cached;
        }

        private readonly struct Ellipse
        {
            private readonly float cx;
            private readonly float cy;
            private readonly float rx;
            private readonly float ry;

            internal Ellipse(float cx, float cy, float rx, float ry)
            {
                this.cx = cx;
                this.cy = cy;
                this.rx = rx;
                this.ry = ry;
            }

            /// <summary>The same ellipse with both radii grown by <paramref name="amount"/> pixels,
            /// used to build the outline band.</summary>
            internal Ellipse Grown(float amount) => new Ellipse(cx, cy, rx + amount, ry + amount);

            /// <returns>1 inside, 0 outside, feathered across roughly one pixel at the edge.</returns>
            internal float Coverage(float x, float y)
            {
                var nx = (x - cx) / rx;
                var ny = (y - cy) / ry;
                var d = Mathf.Sqrt(nx * nx + ny * ny); // 1.0 at the ellipse boundary
                var edgePixels = Mathf.Min(rx, ry);    // convert the normalised gap back to pixels
                return Mathf.Clamp01((1f - d) * edgePixels);
            }
        }
    }
}
