using System.Collections.Generic;
using UnityEngine;

namespace PurrtasticPalette
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
        private static readonly Dictionary<RecolorOptions, Texture2D> Cache =
            new Dictionary<RecolorOptions, Texture2D>();

        // Cap the recolour working resolution. The cat eye atlas ships at 4096² - ~16.7M pixels -
        // and the per-pixel HSV loop over that froze the game for ~1.7s per build (and it builds two
        // atlases on tab-open and on every eye-colour change). The eye is tiny on screen and the fur
        // atlas is only 1024², so 4096² is pure waste. Downscale anything above this cap before the
        // pixel work; the recoloured texture replaces the atlas and its normalised UVs sample the
        // smaller texture fine. Textures already at or under the cap (fur, whiskers, smaller eye
        // atlases) are untouched. Raise this if an eye looks too soft; lower it for more speed.
        private const int MaxRecolorDimension = 1024;

        /// <summary>
        /// Returns the recoloured texture for these options, building and caching it on first use.
        /// See <see cref="RecolorOptions"/> for what each knob means and how the cache is keyed.
        /// </summary>
        internal static Texture2D GetOrBuild(RecolorOptions options)
        {
            if (Cache.TryGetValue(options, out var cached) && cached != null)
            {
                return cached;
            }

            var result = Build(options);
            Cache[options] = result;
            return result;
        }

        private static Texture2D Build(RecolorOptions options)
        {
            var source = options.Source;
            var target = options.Target;
            var splitBelowSaturation = options.SplitBelowSaturation;
            var highlightColor = options.HighlightColor;
            var brightnessFloor = options.BrightnessFloor;
            var splitBelowValue = options.SplitBelowValue;
            var pupilColor = options.PupilColor;
            var originalBlend = options.OriginalBlend;

            // Downscale oversized atlases to the cap before the pixel work (see MaxRecolorDimension).
            // ReadPixelsRobust blits the source into a width×height RenderTexture, so a smaller
            // target here means the GPU downscales on the blit and the loop/output run at that size.
            var scale = Mathf.Min(1f, (float)MaxRecolorDimension / Mathf.Max(source.width, source.height));
            var width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            var height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
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

            var havePupil = pupilColor is Color;
            var pupilH = 0f;
            var pupilS = 0f;
            var pupilV = 0f;
            if (pupilColor is Color pc)
            {
                Color.RGBToHSV(pc, out pupilH, out pupilS, out pupilV);
            }

            var outPixels = new Color[sourcePixels.Length];
            for (var i = 0; i < sourcePixels.Length; i++)
            {
                var src = sourcePixels[i];
                Color.RGBToHSV(src, out _, out var sourceS, out var sourceV);
                var shade = brightnessFloor + sourceV * (1f - brightnessFloor);

                if (sourceS <= splitBelowSaturation)
                {
                    // Desaturated: either the white highlight or the black pupil. Both are fully
                    // desaturated, so only brightness can tell them apart.
                    var isPupil = sourceV <= splitBelowValue;
                    if (isPupil)
                    {
                        if (havePupil)
                        {
                            // The pupil is near-black, so its own value can't scale a colour into
                            // visibility - use the picked colour's value directly instead of
                            // multiplying by shade, or a black pupil would stay black whatever
                            // colour was chosen.
                            var recolouredPupil = Color.HSVToRGB(pupilH, pupilS, pupilV);
                            recolouredPupil.a = src.a;
                            outPixels[i] = recolouredPupil;
                        }
                        else
                        {
                            outPixels[i] = src;
                        }
                    }
                    else if (haveHighlight)
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

                // Fade the recolour back toward the original by originalBlend, so the source's own
                // shading blends in and the flat tint softens. Uniform across every branch above;
                // a no-op at originalBlend 0, which is every caller except the fur.
                outPixels[i] = Overlay(outPixels[i], src, originalBlend);
            }

            var result = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = $"PurrtasticPalette_Recolor_{options.Hex}",
                wrapMode = source.wrapMode,
                filterMode = source.filterMode,
            };
            result.SetPixels(outPixels);
            result.Apply();
            return result;
        }

        // Blends a recoloured pixel back toward the original by `originalBlend` (0 = keep the full
        // recolour, 1 = original untouched), so the source texture's gradients soften the flat tint.
        private static Color Overlay(Color recolored, Color src, float originalBlend)
        {
            if (originalBlend <= 0f)
            {
                return recolored;
            }

            var blended = Color.Lerp(recolored, src, originalBlend);
            blended.a = src.a;
            return blended;
        }

        // A read path that ignores the source texture's Read/Write flag (all of the game's shipped
        // textures have it off): blit into a temporary RenderTexture and ReadPixels from that.
        private static Color[] ReadPixelsRobust(Texture source, int width, int height)
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
