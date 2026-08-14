using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// Recolours Cat Form - fur, whiskers, iris, pupil, eye highlight and the movement trail -
    /// from config, applied by <see cref="CatColorPatch"/> whenever the form's body becomes
    /// visible, and from the Cat Form tab in the mirror's wardrobe. No standalone picker needed:
    /// Mod Nook already renders BepInEx config entries as a menu, so plain
    /// <c>ConfigEntry&lt;string&gt;</c> hex fields double as UI.
    ///
    /// Began as a diagnostic (answering "is the form body's colour a shader property or baked into
    /// a texture?"), which is where its old CatColorProbe name came from. See
    /// mods/PurrtasticPalette/README.md for the findings and 16-recolouring-characters.md at the repo
    /// root for the parts that generalise to other mods.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Moonlight Peaks.exe")]
    public sealed class PurrtasticPalettePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dirtyredz.moonlightpeaks.purrtasticpalette";
        public const string PluginName = "Purrtastic Palette";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<string> FurColor;
        internal static ConfigEntry<string> WhiskerColor;
        internal static ConfigEntry<string> EyeColor;
        internal static ConfigEntry<string> EyeHighlightColor;
        internal static ConfigEntry<string> PupilColor;
        internal static ConfigEntry<string> AuraColor;
        internal static ConfigEntry<bool> ColorsVerboseLogging;

        // Mod Menu reads these strings out of ConfigDescription.Tags and uses them to title its
        // sections. Display names only - the .cfg section keys are untouched. Same convention as
        // mods/FormLock/src/Plugin.cs.
        private const string ColorsSection = "ModMenu.Section=Colors";

        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;

            FurColor = Config.Bind(
                "Colors", "FurColor", "",
                new ConfigDescription(
                    "Colour for the cat's body. Use a hex code (\"#FF8800\") or a colour name " +
                    "(\"orange\"). Leave blank to keep the default black.",
                    null,
                    ColorsSection, "ModMenu.Label=Fur Color"));

            WhiskerColor = Config.Bind(
                "Colors", "WhiskerColor", "",
                new ConfigDescription(
                    "Colour for the whiskers. Leave blank to keep them the default white - they " +
                    "do not follow Fur Color.",
                    null,
                    ColorsSection, "ModMenu.Label=Whisker Color"));

            EyeColor = Config.Bind(
                "Colors", "EyeColor", "",
                new ConfigDescription(
                    "Colour for the iris - the coloured ring of the eye. Leave blank to keep the " +
                    "default red. The pupil and the bright glint have their own settings below.",
                    null,
                    ColorsSection, "ModMenu.Label=Eye Color"));

            PupilColor = Config.Bind(
                "Colors", "PupilColor", "",
                new ConfigDescription(
                    "Colour for the pupil - the dark centre of the eye. Leave blank to keep the " +
                    "default black. Needs Eye Color set to have any effect.",
                    null,
                    ColorsSection, "ModMenu.Label=Pupil Color"));

            EyeHighlightColor = Config.Bind(
                "Colors", "EyeHighlightColor", "",
                new ConfigDescription(
                    "Colour for the small bright glint on the eye. Leave blank to keep the " +
                    "default white. Needs Eye Color set to have any effect.",
                    null,
                    ColorsSection, "ModMenu.Label=Eye Highlight Color"));

            AuraColor = Config.Bind(
                "Colors", "AuraColor", "",
                new ConfigDescription(
                    "Colour for the sparkle trail that follows the cat while running. Leave " +
                    "blank to keep the default. Only visible while moving.",
                    null,
                    ColorsSection, "ModMenu.Label=Aura Color"));

            ColorsVerboseLogging = Config.Bind(
                "Colors", "VerboseLogging", false,
                new ConfigDescription(
                    "Write details of every colour change to the BepInEx log. Only useful when " +
                    "reporting a problem.",
                    null,
                    ColorsSection, "ModMenu.Label=Verbose Logging"));

            // Live-apply: Mod Nook (and hand-editing the .cfg while the game is running) both
            // change a ConfigEntry's .Value and raise this - same live-reapply UX Serena's
            // Enchanted Studio's Recolor feature already trains the player to expect, so match
            // it rather than requiring a re-equip.
            Config.SettingChanged += (_, _) => CatColorPatch.ApplyCatColors();

            gameObject.AddComponent<CatColorReapplier>();

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(CatColorPatch));
            harmony.PatchAll(typeof(CatFormWardrobe));

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Set the Colors settings in Mod " +
                        "Nook (or the .cfg) to recolour Cat Form; changes apply immediately.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}
