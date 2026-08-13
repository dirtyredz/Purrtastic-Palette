using System.Collections.Generic;
using Chicken.Utilities;
using HarmonyLib;
using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// Applies Colors/FurColor and Colors/EyeColor to the Cat Form body whenever it becomes
    /// visible. Patched on FormToolView&lt;CatToolAsset&gt;.HandleEnterVisuallySwitchedBodyViews
    /// (CatToolView doesn't override it, so this closed-generic MethodInfo is the one that
    /// actually runs) rather than HandleEquipped: HandleEquipped fires before
    /// EntityCustomization.SetBodyView's load coroutine finishes, while
    /// HandleEnterVisuallySwitchedBodyViews is the callback for
    /// EntityCustomization.OnVisuallySwitchedBodyViews, which only fires once the new body is
    /// fully loaded and active - by design, the first correct moment to touch its renderers.
    /// Fires on every equip, including from a save that loads directly into Cat Form.
    /// </summary>
    [HarmonyPatch(typeof(FormToolView<CatToolAsset>), "HandleEnterVisuallySwitchedBodyViews")]
    internal static class CatColorPatch
    {
        // Confirmed by probing (see mods/PurrtasticPalette/src/ProbeController.cs and the research
        // that led here): the fur's SkinnedMeshRenderer carries two materials - "HellKitten01"
        // (URP/Lit, texture on _BaseMap) and "GradientAtlas" (the same Game/Atlas/Atlas shader
        // the eyes use, texture on _Atlas). Regenerating only HellKitten01's _BaseMap produced
        // zero visible change across five very different test colours (including cyan and
        // white, which should be unmissable against an orange base) - not the muted-tint
        // physics problem, since that would still show *some* shift. The visible fur must
        // actually be driven mostly or entirely by the GradientAtlas submesh instead (the same
        // material that demonstrably works for the eyes), so both are recoloured for FurColor.
        // The eyes are a distinct SkinnedMeshRenderer named "HellKittenEyes" using GradientAtlas
        // too, with iris/sclera/pupil picked by UV baked into the mesh rather than anything
        // settable here. All three are handled by regenerating the relevant texture via
        // TextureRecolor rather than a flat multiply-tint or flat texture swap: a flat tint on
        // _BaseColor can't introduce a channel the base texture doesn't have, and a flat
        // solid-colour texture swap flattens every UV region - including the pupil - to one
        // color. Regenerating by source luminance fixes both: the target's own channels are what
        // get scaled, and a source region's original brightness controls how strongly it takes
        // the new colour.
        // Two materials share the fur mesh: HellKitten01 is the body surface, GradientAtlas is
        // the whiskers. Keeping them separate is what lets WhiskerColor differ from FurColor.
        // (The eyes use a GradientAtlas material too, but on their own renderer, handled
        // separately by ApplyEyeColor - so the name is only ambiguous across renderers, not
        // within this loop.)
        private const string BodyMaterialPrefix = "HellKitten01";
        private const string WhiskerMaterialPrefix = "GradientAtlas";
        private const string EyeRendererName = "HellKittenEyes";

        // These were config entries while the recolouring was being worked out. They're constants
        // now because the right values turned out to be properties of this specific character's
        // art, not user preferences - every one of them has exactly one setting that works, and
        // the ones in between just produce the broken results that were hit along the way.
        //
        // FurBrightnessFloor 1: the fur's albedo is near-black almost everywhere by design, so
        // preserving its source brightness keeps the recolour near-black too. Discarding it costs
        // nothing visible because the fur's shading comes from real-time lighting and its normal
        // map, not from brightness baked into the albedo.
        private const float FurBrightnessFloor = 1f;

        // EyeBrightnessFloor 0: the opposite of the fur - the eye's dark regions have to stay
        // dark, so the source brightness is preserved exactly and only hue/saturation change.
        private const float EyeBrightnessFloor = 0f;

        // EyePupilSaturation 0.7: separates the iris (saturated) from the pupil and highlight
        // (less saturated, but nowhere near pure grey - which is why lower values missed them).
        private const float EyePupilSaturation = 0.7f;

        // EyePupilValue 0.35: within that desaturated region, separates the black pupil from the
        // white highlight. Saturation can't tell those apart - both are fully desaturated.
        private const float EyePupilValue = 0.35f;

        // HellKitten01 turned out to carry TWO albedo slots at once: _BaseMap/_BaseColor (URP/Lit's
        // own) and _MainTex/_Color (Standard-shader legacy, still present because this material
        // was converted from Standard rather than authored fresh - _WorkflowMode and
        // _Glossiness/_GlossyReflections are the other tells). Regenerating only _BaseMap left
        // _MainTex pointing at the original, untouched HellKitten_DIF texture, which explains the
        // "changed a little, but very lightly" result - whatever this shader variant actually
        // samples for albedo, only recoloring one of the two slots could only ever partially win.
        // Both get regenerated now, plus _Atlas for GradientAtlas.
        private static readonly (string TexProperty, string TintProperty)[] FurTexturePairs =
        {
            ("_BaseMap", "_BaseColor"),
            ("_MainTex", "_Color"),
            ("_Atlas", null),
        };

        // HellKitten_DIF (the fur's diffuse texture) turned out to be almost pure black
        // everywhere once exported and actually looked at - this creature is a shadow silhouette
        // by design. What actually reads as its colour on screen is most likely one of these VFX
        // instead. First guess was "Aura" (ParticleFireFlies, Game/Particle/Unlit/Additive) alone
        // - recoloring it had no visible effect, which fits "FireFlies" being a handful of small
        // sparkle-dust particles rather than the broad glow seen in screenshots. "Trail"
        // (VFXKittenTrail, Shader Graphs/Trail) is a much better match for the whisker-spike
        // glow: its _ColorA/_ColorB are a purple-blue/red-pink HDR pair (values above 1.0 - an
        // additive gradient blend), exactly the magenta rim look in the reference screenshots.
        // "White" is left alone - almost certainly a highlight/blend-strength channel, not a hue.
        private static readonly string[] GlowRendererNames = { "Aura", "Trail" };
        private static readonly string[] GlowColorProperties = { "_EmissionColor", "_Color", "_ColorA", "_ColorB" };

        // Original values, captured the first time a given material+property is overridden, so
        // clearing the config back to blank has something to restore instead of just no-op'ing
        // and leaving the last override in place. Keyed by the live Material instance - a fresh
        // one exists every time EntityCustomization.SetBodyView instantiates a new copy of the
        // body prefab (i.e. every re-equip), so this never needs explicit eviction.
        private static readonly Dictionary<(Material Material, string Property), Texture> OriginalFurTextures =
            new Dictionary<(Material, string), Texture>();
        private static readonly Dictionary<(Material Material, string Property), Color> OriginalFurTints =
            new Dictionary<(Material, string), Color>();
        private static readonly Dictionary<Material, Texture> OriginalEyeAtlases = new Dictionary<Material, Texture>();
        private static readonly Dictionary<(Material Material, string Property), Color> OriginalAuraColors =
            new Dictionary<(Material, string), Color>();

        /// <summary>
        /// Looks up the pristine, never-recoloured texture for a material+property, if this mod
        /// has ever overridden it - used by ProbeController's texture export so pressing F7 while
        /// FurColor/EyeColor are already set exports the real source art instead of re-exporting
        /// our own regenerated result under a misleadingly "original-looking" filename.
        /// </summary>
        internal static bool TryGetOriginalTexture(Material material, string property, out Texture original)
        {
            if (property == "_Atlas" && OriginalEyeAtlases.TryGetValue(material, out original))
            {
                return true;
            }

            return OriginalFurTextures.TryGetValue((material, property), out original);
        }

        // Suppresses Debug() while the periodic reapply safety net (see ProbeController.Update)
        // runs, so a once-a-second background call doesn't flood LogOutput.log the same way an
        // equip or a config edit legitimately should.
        private static bool suppressLogging;

        [HarmonyPostfix]
        private static void Postfix()
        {
            ApplyCatColors();
        }

        /// <param name="includeEyes">
        /// False skips the eye renderer entirely. The eyes never showed the same "reverts back
        /// to default" symptom fur did when applied once per equip/config-change - they visibly
        /// got *worse* once put behind the same every-frame reapply loop fur needed (colour
        /// reading as washed/"shaded", and clearing PupilColor sometimes taking EyeColor down
        /// with it). That's the signature of us fighting something else at matched frequency
        /// rather than reliably winning against it, so eyes are back to single-shot: applied by
        /// the Harmony postfix on equip and by Config.SettingChanged on a config edit, but not
        /// from ProbeController's per-frame safety net (which passes includeEyes: false).
        /// </param>
        internal static void ApplyCatColors(bool logVerbose = true, bool includeEyes = true)
        {
            suppressLogging = !logVerbose;

            if (!MonoBehaviourSingleton<PlayerView>.Exists)
            {
                Debug("No PlayerView yet - skipping.");
                return;
            }

            // No further "is this actually Cat Form" check needed: this patch targets the
            // closed generic FormToolView<CatToolAsset>, which Bat/Aqua Form don't share a
            // RuntimeMethodHandle with even though the source is inherited, so this postfix
            // only ever runs as a result of Cat Form's own equip flow.
            var bodyView = MonoBehaviourSingleton<PlayerView>.Instance.Customization.BodyView;
            if (bodyView == null)
            {
                Debug("Customization.BodyView is null - skipping.");
                return;
            }

            ApplyToBody(bodyView, logVerbose, includeEyes);
        }

        /// <summary>
        /// Recolours any Cat Form body, not just the live player's. The wardrobe preview
        /// instantiates its own copy of the body prefab, which is a different object with its own
        /// material instances - so it needs applying to explicitly or it renders vanilla black
        /// while the real player is correctly coloured.
        /// </summary>
        internal static void ApplyToBody(BodyViewAsset bodyView, bool logVerbose = true, bool includeEyes = true)
        {
            if (bodyView == null)
            {
                return;
            }

            suppressLogging = !logVerbose;

            var furColor = PurrtasticPalettePlugin.FurColor.Value;
            var whiskerColor = PurrtasticPalettePlugin.WhiskerColor.Value;
            var eyeColor = PurrtasticPalettePlugin.EyeColor.Value;
            var auraColor = PurrtasticPalettePlugin.AuraColor.Value;
            var furMatches = 0;
            var whiskerMatches = 0;
            var eyeMatches = 0;
            var auraMatches = 0;

            // Always walk every renderer, even with FurColor blank: a blank field still needs to
            // reach ApplyFurColor so a previously-applied override on this renderer's
            // already-instanced material gets restored, not just skipped.
            foreach (var renderer in bodyView.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name == EyeRendererName)
                {
                    if (includeEyes)
                    {
                        eyeMatches++;
                        ApplyEyeColor(renderer, eyeColor);
                    }

                    continue;
                }

                var isGlowRenderer = false;
                foreach (var glowName in GlowRendererNames)
                {
                    if (renderer.gameObject.name == glowName)
                    {
                        isGlowRenderer = true;
                        break;
                    }
                }

                if (isGlowRenderer)
                {
                    auraMatches++;
                    ApplyAuraColor(renderer, auraColor);
                    continue;
                }

                var rendererMaterials = renderer.materials;
                for (var materialIndex = 0; materialIndex < rendererMaterials.Length; materialIndex++)
                {
                    var material = rendererMaterials[materialIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    // Body and whiskers are separate materials on this one mesh: HellKitten01 is
                    // the body, GradientAtlas is the whiskers (vanilla is white whiskers on
                    // black fur). Colouring them independently needs no pixel-level masking -
                    // just route each material to its own config value. WhiskerColor blank falls
                    // back to FurColor, which is the old always-matching behaviour.
                    if (material.name.StartsWith(BodyMaterialPrefix))
                    {
                        furMatches++;
                        ApplyFurColor(renderer, materialIndex, material, furColor);
                    }
                    else if (material.name.StartsWith(WhiskerMaterialPrefix))
                    {
                        whiskerMatches++;
                        // Blank deliberately means "leave the whiskers untouched" rather than
                        // "follow FurColor" - untouched restores the original texture, which is
                        // vanilla's white, so the default look is right without hardcoding a
                        // colour or dragging the fur's colour onto them.
                        ApplyFurColor(renderer, materialIndex, material, whiskerColor);
                    }
                }
            }

            Debug($"ApplyCatColors on '{bodyView.gameObject.name}': FurColor='{furColor}' matched " +
                  $"{furMatches} material(s), WhiskerColor='{whiskerColor}' matched {whiskerMatches} material(s), " +
                  $"EyeColor='{eyeColor}' matched {eyeMatches} renderer(s), " +
                  $"AuraColor='{auraColor}' matched {auraMatches} renderer(s), includeEyes={includeEyes}.");
        }

        private static void Debug(string message)
        {
            if (!suppressLogging && PurrtasticPalettePlugin.ColorsVerboseLogging.Value)
            {
                PurrtasticPalettePlugin.Log.LogInfo($"[PurrtasticPalette] {message}");
            }
        }

        private static void ApplyFurColor(Renderer renderer, int materialIndex, Material material, string hex)
        {
            // A material may carry more than one of these slots at once (HellKitten01 has both
            // _BaseMap and _MainTex) - every one present gets regenerated independently, so
            // there's no slot left showing the stale original texture underneath.
            foreach (var (texProperty, tintProperty) in FurTexturePairs)
            {
                if (material.HasProperty(texProperty))
                {
                    ApplyFurTextureSlot(renderer, materialIndex, material, texProperty, tintProperty, hex);
                }
            }
        }


        /// <summary>
        /// Writes through the renderer's MaterialPropertyBlock rather than the Material itself.
        /// This is the fix for the long-running "the log says it applied but nothing changes"
        /// problem: the game's own ShaderCustomizationModifier.Apply() sets a
        /// MaterialPropertyBlock on these renderers (the cat body has a CustomizationTargets
        /// node, so it runs here), and a property block overrides the material's own values at
        /// draw time. Every material.SetTexture/SetColor call this mod made was therefore
        /// invisible whenever a block was present - correctly stored, never rendered.
        /// The block is read-modify-written per material index, exactly as the game does it, so
        /// any properties the game set in the same block are preserved rather than clobbered.
        /// </summary>
        private static void ApplyFurTextureSlot(
            Renderer renderer, int materialIndex, Material material, string texProperty, string tintProperty, string hex)
        {
            var key = (material, texProperty);

            if (string.IsNullOrWhiteSpace(hex))
            {
                if (OriginalFurTextures.TryGetValue(key, out var originalMap))
                {
                    material.SetTexture(texProperty, originalMap);
                    if (tintProperty != null && OriginalFurTints.TryGetValue(key, out var originalTint))
                    {
                        material.SetColor(tintProperty, originalTint);
                    }

                    WriteToPropertyBlock(renderer, materialIndex, texProperty, originalMap, tintProperty,
                        OriginalFurTints.TryGetValue(key, out var tint) ? tint : (Color?)null);
                    Debug($"Fur: restored '{material.name}' {texProperty} to original.");
                }

                return;
            }

            if (!TryParseColor(hex, out var color))
            {
                PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] FurColor '{hex}' is not a valid colour.");
                return;
            }

            if (!OriginalFurTextures.ContainsKey(key))
            {
                OriginalFurTextures[key] = material.GetTexture(texProperty);
                if (tintProperty != null)
                {
                    OriginalFurTints[key] = material.GetColor(tintProperty);
                }
            }

            // splitBelowSaturation: -1 disables the pupil/iris split entirely. It must be
            // negative, not 0 - the fur texture is overwhelmingly near-black desaturated pixels
            // whose saturation is exactly 0, so a 0 threshold matched them all and copied them
            // through unrecoloured, leaving the coat black.
            var recolored = TextureRecolor.GetOrBuild(
                OriginalFurTextures[key], hex, color, splitBelowSaturation: -1f,
                brightnessFloor: FurBrightnessFloor);

            // Set both: the material (harmless, and correct if no block is ever present) and the
            // property block (what actually wins at draw time when the game has set one).
            material.SetTexture(texProperty, recolored);
            // The regenerated texture already encodes the target colour via source luminance -
            // the paired tint multiplier needs to be neutral (white) or it would
            // multiply-darken the regenerated result the same way the old flat-tint approach
            // did. _Atlas has no paired tint property, hence the null check.
            if (tintProperty != null)
            {
                material.SetColor(tintProperty, Color.white);
            }

            WriteToPropertyBlock(renderer, materialIndex, texProperty, recolored, tintProperty, Color.white);
            Debug($"Fur: regenerated '{material.name}' {texProperty} toward {color} (parsed from '{hex}').");
        }

        private static void WriteToPropertyBlock(
            Renderer renderer, int materialIndex, string texProperty, Texture texture, string tintProperty, Color? tint)
        {
            if (renderer == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            if (renderer.HasPropertyBlock())
            {
                renderer.GetPropertyBlock(block, materialIndex);
            }

            // texProperty is null for colour-only writes (e.g. emission), texture is null when
            // there's nothing cached to restore - either way there's no texture to set.
            if (texProperty != null && texture != null)
            {
                block.SetTexture(texProperty, texture);
            }

            if (tintProperty != null && tint.HasValue)
            {
                block.SetColor(tintProperty, tint.Value);
            }

            renderer.SetPropertyBlock(block, materialIndex);
        }

        private static void ApplyEyeColor(Renderer renderer, string hex)
        {
            var materials = renderer.materials;
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null || !material.HasProperty("_Atlas"))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(hex))
                {
                    if (OriginalEyeAtlases.TryGetValue(material, out var originalAtlas))
                    {
                        material.SetTexture("_Atlas", originalAtlas);
                        WriteToPropertyBlock(renderer, materialIndex, "_Atlas", originalAtlas, null, null);
                        Debug($"Eyes: restored '{material.name}' _Atlas to '{(originalAtlas != null ? originalAtlas.name : "null")}'.");
                    }

                    continue;
                }

                if (!TryParseColor(hex, out var color))
                {
                    PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] EyeColor '{hex}' is not a valid colour.");
                    continue;
                }

                if (!OriginalEyeAtlases.ContainsKey(material))
                {
                    OriginalEyeAtlases[material] = material.GetTexture("_Atlas");
                }

                var highlightColor = ParseOptionalColor(PurrtasticPalettePlugin.EyeHighlightColor.Value, "EyeHighlightColor");
                var pupilColor = ParseOptionalColor(PurrtasticPalettePlugin.PupilColor.Value, "PupilColor");

                var recolored = TextureRecolor.GetOrBuild(
                    OriginalEyeAtlases[material], hex, color, EyePupilSaturation, highlightColor, EyeBrightnessFloor,
                    EyePupilValue, pupilColor);
                material.SetTexture("_Atlas", recolored);
                WriteToPropertyBlock(renderer, materialIndex, "_Atlas", recolored, null, null);
                Debug($"Eyes: regenerated '{material.name}' _Atlas toward {color} (parsed from '{hex}'); pupil=" +
                      $"{(pupilColor.HasValue ? pupilColor.Value.ToString() : "untouched")} / highlight=" +
                      $"{(highlightColor.HasValue ? highlightColor.Value.ToString() : "untouched")}.");
            }
        }

        /// <summary>
        /// HSV hue/saturation swap, keeping each property's own original value - correct for HDR
        /// colours too (Color.RGBToHSV/HSVToRGB don't clamp to [0,1], so a value of 30+ round-trips
        /// fine), which matters here since additive particle emission commonly runs well above 1.0.
        /// No texture regeneration needed - this is a plain Color property, the TrueColors-style
        /// case that was never the hard part.
        /// </summary>
        private static void ApplyAuraColor(Renderer renderer, string hex)
        {
            foreach (var material in renderer.materials)
            {
                if (material == null)
                {
                    continue;
                }

                foreach (var property in GlowColorProperties)
                {
                    if (!material.HasProperty(property))
                    {
                        continue;
                    }

                    var key = (material, property);

                    if (string.IsNullOrWhiteSpace(hex))
                    {
                        if (OriginalAuraColors.TryGetValue(key, out var original))
                        {
                            material.SetColor(property, original);
                            Debug($"Aura: restored '{material.name}' {property} to original.");
                        }

                        continue;
                    }

                    if (!TryParseColor(hex, out var color))
                    {
                        PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] AuraColor '{hex}' is not a valid colour.");
                        continue;
                    }

                    if (!OriginalAuraColors.ContainsKey(key))
                    {
                        OriginalAuraColors[key] = material.GetColor(property);
                    }

                    Color.RGBToHSV(color, out var targetH, out var targetS, out _);
                    Color.RGBToHSV(OriginalAuraColors[key], out _, out _, out var originalV);
                    var recolored = Color.HSVToRGB(targetH, targetS, originalV, hdr: true);
                    recolored.a = OriginalAuraColors[key].a;
                    material.SetColor(property, recolored);
                    Debug($"Aura: set '{material.name}' {property} to {recolored} (hue/sat from '{hex}', original brightness kept).");
                }
            }
        }

        /// <summary>
        /// Blank means "leave this region alone" (null), an unparseable value warns once and is
        /// also treated as leave-alone rather than silently colouring something wrong.
        /// </summary>
        private static Color? ParseOptionalColor(string hex, string settingName)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return null;
            }

            if (TryParseColor(hex, out var parsed))
            {
                return parsed;
            }

            PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] {settingName} '{hex}' is not a valid colour.");
            return null;
        }

        private static bool TryParseColor(string value, out Color color)
        {
            if (ColorUtility.TryParseHtmlString(value, out color))
            {
                return true;
            }

            return !value.StartsWith("#") && ColorUtility.TryParseHtmlString("#" + value, out color);
        }
    }
}
