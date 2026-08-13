// Ported from mods/LastSwing/src/GameFonts.cs, namespace aside.
// Shared look-and-feel: see 10-visual-integration.md. Fix bugs in both copies.
using System;
using TMPro;
using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// Locates the game's own typeface so mod text does not stand out as foreign.
    ///
    /// Moonlight Peaks uses Gelica, and ships material presets alongside it -
    /// Gelica-Bold-Outline and Gelica-Bold-Glow among them. Everything is looked up by name
    /// from already-loaded assets, so this cannot fail in a way that stalls or throws.
    /// </summary>
    internal static class GameFonts
    {
        private static bool searched;
        private static TMP_FontAsset font;
        private static Material outlineMaterial;

        internal static TMP_FontAsset Font
        {
            get { Search(); return font; }
        }

        internal static Material OutlineMaterial
        {
            get { Search(); return outlineMaterial; }
        }

        /// <summary>Apply the game's font, and its outline preset when one exists.</summary>
        internal static void Apply(TextMeshProUGUI text, bool preferOutline)
        {
            if (text == null)
            {
                return;
            }

            if (Font != null)
            {
                text.font = Font;
            }
            else if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            if (preferOutline && OutlineMaterial != null)
            {
                text.fontSharedMaterial = OutlineMaterial;
            }
        }

        private static void Search()
        {
            if (searched)
            {
                return;
            }
            searched = true;

            try
            {
                var preferred = new[] { "Gelica-Bold", "Gelica-Black", "Gelica-Regular", "Gelica" };

                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (var candidate in preferred)
                {
                    foreach (var asset in fonts)
                    {
                        if (asset != null && string.Equals(asset.name, candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            font = asset;
                            break;
                        }
                    }
                    if (font != null)
                    {
                        break;
                    }
                }

                if (font == null)
                {
                    foreach (var asset in fonts)
                    {
                        if (asset != null && asset.name.IndexOf("Gelica", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            font = asset;
                            break;
                        }
                    }
                }

                var materials = Resources.FindObjectsOfTypeAll<Material>();
                foreach (var name in new[] { "Gelica-Bold-Outline", "Gelica-Black-Outline", "Gelica-Bold-Glow" })
                {
                    foreach (var material in materials)
                    {
                        if (material != null && string.Equals(material.name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            outlineMaterial = material;
                            break;
                        }
                    }
                    if (outlineMaterial != null)
                    {
                        break;
                    }
                }

                PurrtasticPalettePlugin.Log.LogInfo(
                    $"Game font: {(font == null ? "not found, using TMP default" : font.name)}; " +
                    $"outline preset: {(outlineMaterial == null ? "none" : outlineMaterial.name)}");
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogWarning($"Font lookup failed, falling back to TMP default: {e.Message}");
            }
        }
    }
}
