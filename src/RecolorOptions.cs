using System;
using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// The full input to <see cref="TextureRecolor.GetOrBuild"/>: the source texture, the target
    /// colour, and the HSV-shaping knobs. Built through the <see cref="Fur"/> / <see cref="Eye"/>
    /// factories rather than a 9-positional-parameter call, so each call site reads as what it is.
    ///
    /// This is also the recolour cache key. Equality and hashing use exactly the fields the cache
    /// keyed on before this type existed - notably <see cref="Hex"/> is in the key but
    /// <see cref="Target"/> is NOT: the target colour is a deterministic parse of the hex string, so
    /// it is redundant in the key (<see cref="Hex"/> already distinguishes every distinct input) and
    /// is excluded to keep the key bit-identical to the original. The key is delegated to a ValueTuple
    /// so it stays identical to the original <c>Dictionary&lt;(...8 fields...), Texture2D&gt;</c> semantics.
    /// </summary>
    internal readonly struct RecolorOptions : IEquatable<RecolorOptions>
    {
        /// <summary>The texture to recolour (read via a blit round-trip, so Read/Write need not be set).</summary>
        internal readonly Texture Source;

        /// <summary>The hex/name the target was parsed from. Names the output texture and keys the cache.</summary>
        internal readonly string Hex;

        /// <summary>The target colour the main region is recoloured toward. Drives Build, but NOT the cache key.</summary>
        internal readonly Color Target;

        /// <summary>
        /// Source pixels whose HSV saturation is at or below this are treated as the separate "pupil"
        /// region; everything else takes the main target colour. Saturation, not brightness, is the
        /// right axis here: the atlas is built from vertical gradient strips where each strip is one
        /// swatch colour, so within a strip the VALUE varies top-to-bottom while hue/saturation stay
        /// ~constant. Splitting on luminance therefore cut the eye horizontally into "top half" and
        /// "bottom half" instead of separating iris from pupil - it was slicing across the gradient
        /// rather than between strips. Splitting on saturation separates strip-from-strip, the actual
        /// semantic boundary.
        ///
        /// Pass a NEGATIVE value to disable the split - not 0. Saturation is exactly 0 for any pure
        /// black/grey pixel, so a 0 threshold still matches, and every such pixel gets copied through
        /// unrecoloured. On the near-black fur texture that meant almost the entire coat was preserved
        /// as black while only the few saturated pixels took the target colour.
        /// </summary>
        internal readonly float SplitBelowSaturation;

        /// <summary>
        /// What the desaturated-and-BRIGHT region is remapped toward - the eye's white highlight
        /// glint. Null leaves those pixels exactly as they were.
        /// </summary>
        internal readonly Color? HighlightColor;

        /// <summary>
        /// Source value (the V in HSV, i.e. brightness) is remapped from [0, 1] to [brightnessFloor,
        /// 1], then multiplied by the TARGET colour's own value - so the output is always anchored to
        /// how bright the colour you actually picked is, not an independent brightness computed from
        /// the floor alone. Without that anchor, a fully vivid target (value 1.0) over a mostly-dark
        /// source could still only ever reach `floor + sourceV*(1-floor)`, capping out well below full
        /// vibrancy no matter how high the floor was pushed. 0 = no floor, full original shading range
        /// preserved (darkest source pixels can still go near-black).
        /// </summary>
        internal readonly float BrightnessFloor;

        /// <summary>
        /// Within the desaturated region (see <see cref="SplitBelowSaturation"/>), pixels at or below
        /// this HSV value are the actual pupil rather than the highlight. Saturation alone cannot tell
        /// these apart: a white highlight and a black pupil are both fully desaturated, so they land in
        /// the same bucket and can only be separated by brightness. Pass a negative value to disable
        /// the pupil split, leaving the whole desaturated region as highlight.
        /// </summary>
        internal readonly float SplitBelowValue;

        /// <summary>
        /// What the desaturated-and-DARK region (the actual pupil) is remapped toward. Null leaves
        /// those pixels exactly as they were, which is the vanilla black pupil.
        /// </summary>
        internal readonly Color? PupilColor;

        /// <summary>
        /// How far each output pixel is faded back toward the ORIGINAL source pixel: 0 keeps the full
        /// recolour, 1 leaves the texture untouched. This is the Fur Intensity control - a lower
        /// intensity blends the original coat's own shading/gradient back in so the flat recolour looks
        /// less uniform. Alpha is always taken from the source. 0 for every other caller (eyes).
        /// </summary>
        internal readonly float OriginalBlend;

        private RecolorOptions(Texture source, string hex, Color target, float splitBelowSaturation,
            Color? highlightColor, float brightnessFloor, float splitBelowValue, Color? pupilColor, float originalBlend)
        {
            Source = source;
            Hex = hex;
            Target = target;
            SplitBelowSaturation = splitBelowSaturation;
            HighlightColor = highlightColor;
            BrightnessFloor = brightnessFloor;
            SplitBelowValue = splitBelowValue;
            PupilColor = pupilColor;
            OriginalBlend = originalBlend;
        }

        /// <summary>
        /// Fur/whisker/aura recolour: no pupil/highlight split (whole texture takes the target),
        /// a brightness floor, and the Fur-Intensity blend back toward the original coat.
        /// </summary>
        internal static RecolorOptions Fur(Texture source, string hex, Color target, float brightnessFloor,
            float originalBlend)
            => new RecolorOptions(source, hex, target, splitBelowSaturation: -1f, highlightColor: null,
                brightnessFloor: brightnessFloor, splitBelowValue: -1f, pupilColor: null, originalBlend: originalBlend);

        /// <summary>
        /// Eye-atlas recolour: split the iris from the desaturated highlight/pupil on saturation, then
        /// the pupil from the highlight on value; optional highlight and pupil colours; no blend.
        /// </summary>
        internal static RecolorOptions Eye(Texture source, string hex, Color target, float splitBelowSaturation,
            Color? highlightColor, float brightnessFloor, float splitBelowValue, Color? pupilColor)
            => new RecolorOptions(source, hex, target, splitBelowSaturation, highlightColor, brightnessFloor,
                splitBelowValue, pupilColor, originalBlend: 0f);

        // The cache key: exactly the eight fields the original Dictionary tuple used (Target omitted -
        // see the type summary). Delegating equality and hashing to the ValueTuple keeps the cache
        // behaviour bit-identical to the pre-refactor key.
        private (Texture, string, float, Color?, float, float, Color?, float) Key =>
            (Source, Hex, SplitBelowSaturation, HighlightColor, BrightnessFloor, SplitBelowValue, PupilColor, OriginalBlend);

        public bool Equals(RecolorOptions other) => Key.Equals(other.Key);

        public override bool Equals(object obj) => obj is RecolorOptions other && Equals(other);

        public override int GetHashCode() => Key.GetHashCode();
    }
}
