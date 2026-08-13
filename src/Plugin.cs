using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CatColorProbe
{
    /// <summary>
    /// Started as a throwaway diagnostic (does the Cat/Bat/Aqua form body carry a usable
    /// <c>Color</c> shader property, or is its colour baked into a texture?) and grew a real
    /// feature once the answer turned out to be "yes for fur, sort of for eyes": config-driven
    /// recoloring of Cat Form, applied by <see cref="CatColorPatch"/> every time the form's body
    /// becomes visible. No in-game picker here - Mod Nook already renders BepInEx config entries
    /// as a menu, so plain <c>ConfigEntry&lt;string&gt;</c> hex fields are the UI for now.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Moonlight Peaks.exe")]
    public sealed class CatColorProbePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dirtyredz.moonlightpeaks.catcolorprobe";
        public const string PluginName = "Cat Color Probe";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> ProbeKey;
        internal static ConfigEntry<bool> ForceTestColor;
        internal static ConfigEntry<KeyboardShortcut> GiveFormsKey;
        internal static ConfigEntry<string> FurColor;
        internal static ConfigEntry<string> WhiskerColor;
        internal static ConfigEntry<string> EyeColor;
        internal static ConfigEntry<string> EyeHighlightColor;
        internal static ConfigEntry<string> PupilColor;
        internal static ConfigEntry<string> AuraColor;
        internal static ConfigEntry<bool> ColorsVerboseLogging;
        internal static readonly Color TestColor = new Color(1f, 0f, 1f); // neon magenta, nothing in-game looks like this by accident

        // Mod Menu reads these strings out of ConfigDescription.Tags and uses them to title its
        // sections. Display names only - the .cfg section keys are untouched. Same convention as
        // mods/FormLock/src/Plugin.cs.
        private const string ColorsSection = "ModMenu.Section=Colors";
        private const string ProbeSection = "ModMenu.Section=Probe";
        private const string DebugSection = "ModMenu.Section=Debug";

        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;

            FurColor = Config.Bind(
                "Colors", "FurColor", "",
                new ConfigDescription(
                    "Cat Form fur colour. Accepts a hex code (\"#FF8800\") or an HTML colour " +
                    "name (\"orange\"). Blank = leave the default fur colour alone. Works by " +
                    "regenerating the fur's albedo texture, which is near-black by design, so " +
                    "the colour is applied at full brightness - the body still looks shaded and " +
                    "rounded because that shading comes from real-time lighting and the normal " +
                    "map rather than from the texture.",
                    null,
                    ColorsSection, "ModMenu.Label=Fur Color"));

            WhiskerColor = Config.Bind(
                "Colors", "WhiskerColor", "",
                new ConfigDescription(
                    "Cat Form whisker colour - hex code or HTML colour name. Blank = leave the " +
                    "whiskers at their vanilla colour (white), NOT matching FurColor. The " +
                    "whiskers are a separate material from the body (GradientAtlas vs " +
                    "HellKitten01) on the same mesh, so they can be coloured independently " +
                    "without any pixel-level masking.",
                    null,
                    ColorsSection, "ModMenu.Label=Whisker Color"));

            EyeColor = Config.Bind(
                "Colors", "EyeColor", "",
                new ConfigDescription(
                    "Cat Form eye colour. Accepts a hex code or an HTML colour name. Blank = " +
                    "leave the default eye colour alone. This colours the iris only - the pupil " +
                    "and the bright highlight are detected separately and have their own " +
                    "settings (PupilColor and EyeHighlightColor).",
                    null,
                    ColorsSection, "ModMenu.Label=Eye Color"));


            EyeHighlightColor = Config.Bind(
                "Colors", "EyeHighlightColor", "",
                new ConfigDescription(
                    "Colour for the eye's bright highlight glint - hex code or HTML colour name. " +
                    "Blank = leave it at its original colour. This was previously (and wrongly) " +
                    "called PupilColor; it is the small bright catchlight, not the pupil. Only " +
                    "has any effect while EyeColor is also set.",
                    null,
                    ColorsSection, "ModMenu.Label=Eye Highlight Color"));

            PupilColor = Config.Bind(
                "Colors", "PupilColor", "",
                new ConfigDescription(
                    "Colour for the actual pupil (the dark centre) - hex code or HTML colour " +
                    "name. Blank = leave it vanilla black. Because the pupil is near-black, the " +
                    "colour set here is applied at its own full brightness rather than being " +
                    "scaled by the pupil's original darkness, which would leave it black no " +
                    "matter what was picked. Only has any effect while EyeColor is also set.",
                    null,
                    ColorsSection, "ModMenu.Label=Pupil Color"));


            AuraColor = Config.Bind(
                "Colors", "AuraColor", "",
                new ConfigDescription(
                    "Colour for Cat Form's glow VFX (the Aura sparkle particles and the Trail " +
                    "whisker-glow effect) - hex code or HTML colour name. Blank = leave the " +
                    "default glow colour alone. The fur's own diffuse texture turned out to be " +
                    "almost pure black everywhere (confirmed by exporting it - this creature is " +
                    "a shadow silhouette by design), so these VFX, not the fur texture, are most " +
                    "likely what actually reads as the character's colour on screen. Preserves " +
                    "each property's own HDR brightness - only the hue/saturation change.",
                    null,
                    ColorsSection, "ModMenu.Label=Aura Color"));

            ColorsVerboseLogging = Config.Bind(
                "Colors", "VerboseLogging", true,
                new ConfigDescription(
                    "Log every fur/eye colour apply and restore to LogOutput.log - which " +
                    "materials were found, what was parsed, what got set. On by default while " +
                    "this feature is still being shaken out.",
                    null,
                    ColorsSection, "ModMenu.Label=Verbose Logging"));

            ProbeKey = Config.Bind(
                "Probe", "ProbeKey", new KeyboardShortcut(KeyCode.F7),
                new ConfigDescription(
                    "While a form (Cat/Bat/Aqua) is equipped, press this to dump the form " +
                    "body's renderer materials and every shader property to LogOutput.log.",
                    null,
                    ProbeSection, "ModMenu.Label=Probe Key"));

            ForceTestColor = Config.Bind(
                "Probe", "ForceTestColor", true,
                new ConfigDescription(
                    "If true, every Color-typed shader property found by the probe is forced " +
                    "to neon magenta so the override is visually obvious. If false, the probe " +
                    "only logs what it finds.",
                    null,
                    ProbeSection, "ModMenu.Label=Force Test Color"));

            // Same debug aid FormLock briefly had (and removed before release, since it was
            // testing-only there too): grants Cat/Bat/Aqua form ownership on a save that doesn't
            // have them unlocked, and equips Cat Form immediately so there's no need to dig
            // through a tool wheel for it. Default is Home, not F10 - F10 is MoonlightMinimap's
            // show/hide-map key on this machine and silently ate the keypress the first time
            // this was built (see mods/FormLock/TESTING.md).
            GiveFormsKey = Config.Bind(
                "Debug", "GiveFormsKey", new KeyboardShortcut(KeyCode.Home),
                new ConfigDescription(
                    "Grants Cat/Bat/Aqua form ownership (even if not unlocked on this save) " +
                    "and equips Cat Form. Debug aid, not a real unlock.",
                    null,
                    DebugSection, "ModMenu.Label=Give Forms Key"));

            // Live-apply: Mod Nook (and hand-editing the .cfg while the game is running) both
            // change a ConfigEntry's .Value and raise this - same live-reapply UX Serena's
            // Enchanted Studio's Recolor feature already trains the player to expect, so match
            // it rather than requiring a re-equip.
            Config.SettingChanged += (_, _) => CatColorPatch.ApplyCatColors();

            gameObject.AddComponent<ProbeController>();

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(CatColorPatch));

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Set Colors/FurColor and " +
                        "Colors/EyeColor (Mod Nook or the .cfg) to recolor Cat Form. " +
                        $"Diagnostics: {ProbeKey.Value} probes the current body, " +
                        $"{GiveFormsKey.Value} grants all three forms and equips Cat Form.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}
