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
        internal static ConfigEntry<string> EyeColor;
        internal static ConfigEntry<float> EyeHighlightThreshold;
        internal static ConfigEntry<string> PupilColor;
        internal static ConfigEntry<float> RecolorBrightnessFloor;
        internal static ConfigEntry<float> EyeBrightnessFloor;
        internal static ConfigEntry<string> AuraColor;
        internal static ConfigEntry<float> FurGlow;
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
                    "regenerating the fur's texture(s) with HSV colorize: every pixel takes " +
                    "this colour's hue/saturation but keeps its own original brightness, so " +
                    "shading is preserved without washing the colour out. See " +
                    "RecolorBrightnessFloor if dark regions of the fur still read too dim.",
                    null,
                    ColorsSection, "ModMenu.Label=Fur Color"));

            EyeColor = Config.Bind(
                "Colors", "EyeColor", "",
                new ConfigDescription(
                    "Cat Form eye colour. Accepts a hex code or an HTML colour name. Blank = " +
                    "leave the default eye colour alone. Same texture-regeneration technique as " +
                    "FurColor, applied to the eyes' shared gradient atlas texture. See " +
                    "EyeHighlightThreshold to control how much of the pupil/highlight resists " +
                    "being recoloured.",
                    null,
                    ColorsSection, "ModMenu.Label=Eye Color"));

            EyeHighlightThreshold = Config.Bind(
                "Colors", "EyePupilSaturation", 0.25f,
                new ConfigDescription(
                    "Source eye-atlas pixels with LESS colour saturation than this (0-1) are " +
                    "treated as the pupil and take PupilColor; everything more saturated takes " +
                    "EyeColor. Saturation is the right axis, not brightness: the atlas is built " +
                    "from vertical gradient strips (one swatch colour each), so brightness varies " +
                    "top-to-bottom WITHIN a strip while saturation stays constant - splitting on " +
                    "brightness sliced the eye into a top half and bottom half instead of " +
                    "separating iris from pupil. Raise this if too little counts as pupil; lower " +
                    "it if too much does. 0 disables the split entirely (whole eye takes EyeColor).",
                    new AcceptableValueRange<float>(0f, 1f),
                    ColorsSection, "ModMenu.Label=Eye Pupil Saturation"));

            PupilColor = Config.Bind(
                "Colors", "PupilColor", "",
                new ConfigDescription(
                    "Colour for the bright highlight region EyeHighlightThreshold protects " +
                    "(effectively the pupil) - hex code or HTML colour name. Blank = leave it " +
                    "as its original colour (unrecoloured, not forced white). Only has any " +
                    "effect while EyeColor is also set.",
                    null,
                    ColorsSection, "ModMenu.Label=Pupil Color"));

            FurGlow = Config.Bind(
                "Colors", "FurGlow", 0f,
                new ConfigDescription(
                    "How strongly FurColor is emitted as light by the fur itself (0-1), " +
                    "independent of scene lighting. The fur material is Universal Render " +
                    "Pipeline/Lit, so its albedo gets multiplied by the (very dark, night-time) " +
                    "scene lighting - which is why recolouring the texture alone only ever " +
                    "produced a dim 'shaded' version of the colour no matter how vivid the " +
                    "texture was made. Emission bypasses lighting entirely, the same reason the " +
                    "eyes read as bright. NOTE: this appears to do nothing in the shipped game, " +
                    "and is off by default because of it. Unity strips unused shader variants at " +
                    "build time; this material ships with _EmissionColor black and _EmissionMap " +
                    "unset, so the URP/Lit _EMISSION variant is most likely not in the build at " +
                    "all - EnableKeyword then silently no-ops and nothing renders, however high " +
                    "this is set. Left in because it costs nothing and would work if that " +
                    "assumption is ever wrong. Use RecolorBrightnessFloor for fur brightness " +
                    "instead. Only applies while FurColor is set.",
                    new AcceptableValueRange<float>(0f, 1f),
                    ColorsSection, "ModMenu.Label=Fur Glow"));

            RecolorBrightnessFloor = Config.Bind(
                "Colors", "RecolorBrightnessFloor", 1f,
                new ConfigDescription(
                    "Raises how bright a naturally dark source region of the FUR can still get " +
                    "when recoloured (0-1); the eyes have their own EyeBrightnessFloor. The Cat " +
                    "Form fur texture is near-black almost everywhere " +
                    "by design, so a low value here keeps the recoloured fur nearly black too - " +
                    "the colour reads as a dim tint over black rather than as the colour itself. " +
                    "1.0 (the default) makes the albedo the full target colour. That costs very " +
                    "little on this particular texture, because the fur's visible shading comes " +
                    "mostly from real-time lighting and its normal map (HellKitten_NRM) rather " +
                    "than from brightness baked into the albedo - so the model still looks shaded " +
                    "and rounded, just properly coloured. Lower it if you want the texture's own " +
                    "baked dark patches to show through.",
                    new AcceptableValueRange<float>(0f, 1f),
                    ColorsSection, "ModMenu.Label=Fur Brightness Floor"));

            EyeBrightnessFloor = Config.Bind(
                "Colors", "EyeBrightnessFloor", 0f,
                new ConfigDescription(
                    "The same brightness floor as RecolorBrightnessFloor, but for the eyes only " +
                    "(0-1). Kept separate because the two want opposite settings: the fur needs " +
                    "a high floor to escape its near-black texture, while the eyes need a LOW " +
                    "one so the dark parts of the eye stay dark. Sharing one value forced the " +
                    "black pupils to full brightness along with the fur. 0 (the default) keeps " +
                    "the eye's original brightness exactly and only changes hue/saturation.",
                    new AcceptableValueRange<float>(0f, 1f),
                    ColorsSection, "ModMenu.Label=Eye Brightness Floor"));

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
