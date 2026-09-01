using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// Parses a colour from a hex code ("#FF8800") or an HTML colour name ("orange"), shared by the
    /// recolour patch's fur/eye/aura paths. A leading "#" is optional - a bare "FF8800" is retried
    /// with one prepended.
    /// </summary>
    internal static class ColorParsing
    {
        /// <summary>
        /// Blank means "leave this region alone" (null); an unparseable value warns once and is also
        /// treated as leave-alone rather than silently colouring something wrong.
        /// </summary>
        internal static Color? ParseOptional(string hex, string settingName)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return null;
            }

            if (TryParse(hex, out var parsed))
            {
                return parsed;
            }

            PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] {settingName} '{hex}' is not a valid colour.");
            return null;
        }

        internal static bool TryParse(string value, out Color color)
        {
            if (ColorUtility.TryParseHtmlString(value, out color))
            {
                return true;
            }

            return !value.StartsWith("#") && ColorUtility.TryParseHtmlString("#" + value, out color);
        }
    }
}
