using System.Collections.Generic;
using UnityEngine;

namespace CatColorProbe
{
    /// <summary>
    /// HSV colorize: for each source pixel, take the TARGET colour's hue and saturation but keep
    /// the source pixel's own value (brightness) for shading. This replaced an earlier
    /// RGB-multiply-by-luminance approach (target.rgb * luminance) that turned out to have a real
    /// problem: a source region that's naturally dark - most of the fur coat, and the iris region
    /// of the eye atlas - can only ever produce a dark, desaturated, muddy result under
    /// multiplication, no matter how saturated the target colour is, while naturally bright
    /// regions (whiskers, the pupil) recoloured strongly. That's exactly the "whiskers vivid, coat
    /// barely changed" / "eye colour alone barely visible" pattern that kept showing up. HSV
    /// colorize doesn't have that failure mode: a dark source pixel becomes a *darker version of
    /// the exact target hue*, not a grayed-out blend, so shading is preserved without washing out
    /// the colour itself.
    ///
    /// Also fixes what a flat multiply-tint (the very first version of this mod, and what Serena's
    /// Enchanted Studio falls back to) can't do at all: introduce a channel the source barely has
    /// (blue, on an orange fur texture) - HSV colorize uses the target's own hue outright, so any
    /// colour is reachable regardless of the source texture's own channel makeup.
    ///
    /// Reads pixels via a RenderTexture blit + ReadPixels round-trip rather than
    /// Texture2D.GetPixels() directly, so this works regardless of whether the source asset has
    /// "Read/Write Enabled" set - the game's shipped textures almost certainly don't.
    /// </summary>
    internal static class TextureRecolor
    {
        private static readonly Dictionary<(Texture Source, string Hex, float Threshold, Color? Highlight, float Floor), Texture2D> Cache =
            new Dictionary<(Texture, string, float, Color?, float), Texture2D>();

        /// <param name="splitBelowSaturation">
        /// Source pixels whose HSV saturation is at or below this are treated as the separate
        /// "pupil" region; everything else takes the main target colour. Saturation, not
        /// brightness, is the right axis here: the atlas is built from vertical gradient strips
        /// where each strip is one swatch colour, so within a strip the VALUE varies top-to-
        /// bottom while hue/saturation stay ~constant. Splitting on luminance therefore cut the
        /// eye horizontally into "top half" and "bottom half" (observed in-game) instead of
        /// separating iris from pupil - it was slicing across the gradient rather than between
        /// strips. Splitting on saturation separates strip-from-strip, which is the actual
        /// semantic boundary.
        ///
        /// Pass a NEGATIVE value to disable the split - not 0. Saturation is exactly 0 for any
        /// pure black/grey pixel, so a 0 threshold still matches, and every such pixel gets
        /// copied through unrecoloured. On the near-black fur texture that meant almost the
        /// entire coat was deliberately preserved as black while only the few saturated pixels
        /// took the target colour, which read in-game as "the colour is overlaying on top of the
        /// black fur".
        /// </param>
        /// <param name="highlightColor">
        /// What the highlight region (see protectAboveLuminance) is remapped toward, using the
        /// same luminance-multiply as everywhere else. Null leaves it as the original pixel,
        /// unrecoloured - the "protect the pupil" behaviour. A caller that wants an explicitly
        /// colourable pupil passes a colour here instead.
        /// </param>
        /// <param name="brightnessFloor">
        /// Source value (the V in HSV, i.e. brightness) is remapped from [0, 1] to
        /// [brightnessFloor, 1], then multiplied by the TARGET colour's own value - so the
        /// output is always anchored to how bright the colour you actually picked is, not an
        /// independent brightness computed from the floor alone. Without that anchor, a fully
        /// vivid target (value 1.0) picked over a mostly-dark source texture could still only
        /// ever reach `floor + sourceV*(1-floor)`, capping out well below full vibrancy no
        /// matter how high the floor was pushed - that was the "had to turn the brightness way
        /// up and still didn't get the vibrancy" result. 0 = no floor, full original shading
        /// range preserved (darkest source pixels can still go near-black).
        /// </param>
        internal static Texture2D GetOrBuild(
            Texture source, string hex, Color target, float splitBelowSaturation = -1f, Color? highlightColor = null,
            float brightnessFloor = 0f)
        {
            var key = (source, hex, splitBelowSaturation, highlightColor, brightnessFloor);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var result = Build(source, target, hex, splitBelowSaturation, highlightColor, brightnessFloor);
            Cache[key] = result;
            return result;
        }

        private static Texture2D Build(
            Texture source, Color target, string cacheKey, float splitBelowSaturation, Color? highlightColor, float brightnessFloor)
        {
            var width = source.width;
            var height = source.height;
            var sourcePixels = ReadPixelsRobust(source, width, height);

            Color.RGBToHSV(target, out var targetH, out var targetS, out var targetV);
            var haveHighlight = highlightColor is Color;
            var highlightH = 0f;
            var highlightS = 0f;
            var highlightV = 0f;
            if (highlightColor is Color hc)
            {
                Color.RGBToHSV(hc, out highlightH, out highlightS, out highlightV);
            }

            var outPixels = new Color[sourcePixels.Length];
            for (var i = 0; i < sourcePixels.Length; i++)
            {
                var src = sourcePixels[i];
                Color.RGBToHSV(src, out _, out var sourceS, out var sourceV);
                var shade = brightnessFloor + sourceV * (1f - brightnessFloor);

                if (sourceS <= splitBelowSaturation)
                {
                    if (haveHighlight)
                    {
                        var recoloredHighlight = Color.HSVToRGB(highlightH, highlightS, highlightV * shade);
                        recoloredHighlight.a = src.a;
                        outPixels[i] = recoloredHighlight;
                    }
                    else
                    {
                        outPixels[i] = src;
                    }
                }
                else
                {
                    var recolored = Color.HSVToRGB(targetH, targetS, targetV * shade);
                    recolored.a = src.a;
                    outPixels[i] = recolored;
                }
            }

            var result = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = $"CatColorProbe_Recolor_{cacheKey}",
                wrapMode = source.wrapMode,
                filterMode = source.filterMode,
            };
            result.SetPixels(outPixels);
            result.Apply();
            return result;
        }

        // internal, not private: ProbeController's texture-export reuses this same robust
        // (Read/Write-flag-independent) read path to save the eyes' and fur's raw source
        // textures to disk for visual inspection - see ExportTextureIfInteresting.
        internal static Color[] ReadPixelsRobust(Texture source, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var previousActive = RenderTexture.active;

            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            var readable = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply();

            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = readable.GetPixels();
            Object.Destroy(readable);
            return pixels;
        }
    }
}
