using System;
using System.Linq;
using BepInEx.Configuration;

namespace PurrtasticPalette
{
    /// <summary>
    /// The wardrobe's try-on transaction for cat colours: snapshot the colour config when the mirror
    /// opens, and restore it on close UNLESS the player pressed Confirm - the same revert-on-cancel
    /// the game gives its own clothing. Pure ConfigEntry state, touching no preview GameObjects, so it
    /// lives apart from <see cref="CatFormWardrobe"/>'s rig management.
    ///
    /// Writing a ConfigEntry.Value raises SettingChanged, which reapplies colours to the live player -
    /// so a revert restores the world cat as well as discarding the preview picks.
    /// </summary>
    internal static class PreviewColorSession
    {
        private static string[] colorSnapshot;
        private static bool confirmed;

        private static ConfigEntry<string>[] ManagedColors() => new[]
        {
            PurrtasticPalettePlugin.FurColor,
            PurrtasticPalettePlugin.WhiskerColor,
            PurrtasticPalettePlugin.EyeColor,
            PurrtasticPalettePlugin.PupilColor,
            PurrtasticPalettePlugin.EyeHighlightColor,
            PurrtasticPalettePlugin.AuraColor,
        };

        /// <summary>Snapshot the current colours as the point to revert to. Call when the mirror opens.</summary>
        internal static void Begin()
        {
            colorSnapshot = ManagedColors().Select(c => c.Value).ToArray();
            confirmed = false;
        }

        /// <summary>Mark the picks as kept - the screen's Confirm was pressed.</summary>
        internal static void Confirm()
        {
            confirmed = true;
        }

        /// <summary>
        /// Restores the colour snapshot unless the player pressed Confirm. A no-op when nothing
        /// changed or when confirmed. Clears the snapshot afterwards so a later stray call can't
        /// revert again.
        /// </summary>
        internal static void RevertUnlessConfirmed()
        {
            try
            {
                if (confirmed || colorSnapshot == null)
                {
                    return;
                }

                var colors = ManagedColors();
                var reverted = 0;
                for (var i = 0; i < colors.Length && i < colorSnapshot.Length; i++)
                {
                    if (colors[i] != null && colors[i].Value != colorSnapshot[i])
                    {
                        colors[i].Value = colorSnapshot[i];
                        reverted++;
                    }
                }

                if (reverted > 0)
                {
                    PurrtasticPalettePlugin.Log.LogInfo(
                        $"[PurrtasticPalette] Wardrobe: reverted {reverted} colour(s) - closed without confirming.");
                }
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Wardrobe: colour revert failed: {e}");
            }
            finally
            {
                colorSnapshot = null;
                confirmed = false;
            }
        }
    }
}
