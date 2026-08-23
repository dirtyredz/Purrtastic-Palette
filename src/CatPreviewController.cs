using System;
using System.Linq;
using Chicken.Utilities;
using HarmonyLib;
using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// Owns the wardrobe preview rig's cat body: instantiating it alongside the human body, swapping
    /// which body is visible, muting its in-preview VFX, and suppressing bloom while it shows. Split
    /// out of the <see cref="CatFormWardrobe"/> Harmony host so the patch layer stays thin - this
    /// class knows how the preview rig works; the host only decides when to call it.
    ///
    /// The preview is NOT the player entity: CustomizationCharacterPreview is a standalone rig with
    /// its own private bodyView, not driven by EntityCustomization.SetBodyView the way the real
    /// player is, so the cat body has to be instantiated into the rig by hand. The rig's camera
    /// framing, lighting, scale and rotation are all built around the human body.
    /// </summary>
    internal static class CatPreviewController
    {
        private const string CatBodyViewNameFragment = "Hellkitten";

        private static readonly AccessTools.FieldRef<CustomizationCharacterPreview, BodyViewAsset> PreviewBodyRef =
            AccessTools.FieldRefAccess<CustomizationCharacterPreview, BodyViewAsset>("bodyView");

        // The instantiated cat body, so repeated tab presses reuse it and hiding it again is
        // possible. Cleared when the screen closes, since the preview rig itself is torn down.
        private static BodyViewAsset catBodyInstance;

        /// <summary>
        /// Instantiates (once) and shows the cat body in the preview: hides every other body in the
        /// rig, mutes its VFX, suppresses bloom, and colours it. Returns true when the cat is on
        /// screen, false if the preview could not be reached or the body asset was missing - the
        /// caller builds the colour panel only on success.
        /// </summary>
        internal static bool ShowCat()
        {
            if (!MonoBehaviourSingleton<CharacterCustomizer>.Exists)
            {
                PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Wardrobe: no CharacterCustomizer - cannot reach the preview.");
                return false;
            }

            var preview = MonoBehaviourSingleton<CharacterCustomizer>.Instance.CharacterPreview;
            if (preview == null)
            {
                PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Wardrobe: CharacterPreview is null.");
                return false;
            }

            var humanBody = PreviewBodyRef(preview);
            if (humanBody == null)
            {
                PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Wardrobe: the preview has no body view to replace.");
                return false;
            }

            if (catBodyInstance == null)
            {
                var catBodyAsset = Asset.GetAll<BodyViewAsset>()
                    .FirstOrDefault(x => x != null && x.name.IndexOf(CatBodyViewNameFragment, StringComparison.OrdinalIgnoreCase) >= 0);
                if (catBodyAsset == null)
                {
                    PurrtasticPalettePlugin.Log.LogWarning(
                        $"[PurrtasticPalette] Wardrobe: no BodyViewAsset matching '{CatBodyViewNameFragment}' found.");
                    return false;
                }

                // Instantiate alongside the human body and copy its local transform, so the cat lands
                // wherever the rig expects a body to be.
                catBodyInstance = UnityEngine.Object.Instantiate(catBodyAsset, humanBody.transform.parent);
                catBodyInstance.transform.localPosition = humanBody.transform.localPosition;
                catBodyInstance.transform.localRotation = humanBody.transform.localRotation;
                catBodyInstance.transform.localScale = humanBody.transform.localScale;
                PurrtasticPalettePlugin.Log.LogInfo(
                    $"[PurrtasticPalette] Wardrobe: instantiated '{catBodyAsset.name}' into the preview under " +
                    $"'{humanBody.transform.parent?.name}'.");
            }

            // Hide EVERY other body in the preview rig, not just the human - a sibling form mod (e.g.
            // Fangtastic Palette's "Bat Form" tab) instantiates its body into the same parent and gets
            // no teardown callback when this tab is picked, so switching from it to Cat otherwise
            // leaves both bodies on screen. Deactivate all BodyViewAssets under the parent, then show
            // only ours.
            foreach (var body in humanBody.transform.parent.GetComponentsInChildren<BodyViewAsset>(true))
            {
                if (body != null && body != catBodyInstance)
                {
                    body.gameObject.SetActive(false);
                }
            }

            catBodyInstance.gameObject.SetActive(true);
            HidePreviewVfx();
            PreviewBloomSuppressor.Suppress();

            // The preview cat is a separate instance of the body prefab with its own material
            // instances, so the colours applied to the live player don't carry over - without this it
            // renders vanilla black while the real cat outside is coloured.
            ApplyColors();
            return true;
        }

        /// <summary>
        /// Logs the shown cat's renderer count and scale. The host calls this AFTER the colour panel
        /// is built - matching the original ordering, so that a failure in this diagnostic can never
        /// prevent the panel swap that has already completed. No-op if the cat isn't shown.
        /// </summary>
        internal static void LogShownState()
        {
            if (catBodyInstance == null)
            {
                return;
            }

            var renderers = catBodyInstance.GetComponentsInChildren<Renderer>(true).Length;
            PurrtasticPalettePlugin.Log.LogInfo(
                $"[PurrtasticPalette] Wardrobe: showing the cat body in the preview " +
                $"({renderers} renderer(s), scale {catBodyInstance.transform.lossyScale}).");
        }

        /// <summary>
        /// Re-colours the preview's own cat body from the current config. Wired to fire on each
        /// swatch pick; the live player is recoloured separately by the plugin's SettingChanged hook.
        /// No-op when the cat is not currently shown.
        /// </summary>
        internal static void ApplyColors()
        {
            if (catBodyInstance != null)
            {
                CatColorPatch.ApplyToBody(catBodyInstance);
            }
        }

        /// <summary>
        /// Puts the human body back for a vanilla tab: deactivates the cat, drops bloom suppression,
        /// and re-shows the human body view. No-op if the cat was never shown or the preview is gone.
        /// </summary>
        internal static void HideCat()
        {
            if (catBodyInstance == null || !MonoBehaviourSingleton<CharacterCustomizer>.Exists)
            {
                return; // cat was never shown - nothing to undo
            }

            var preview = MonoBehaviourSingleton<CharacterCustomizer>.Instance.CharacterPreview;
            if (preview == null)
            {
                return;
            }

            catBodyInstance.gameObject.SetActive(false);
            PreviewBloomSuppressor.Restore();

            var humanBody = PreviewBodyRef(preview);
            if (humanBody != null)
            {
                humanBody.gameObject.SetActive(true);
            }

            PurrtasticPalettePlugin.Log.LogInfo("[PurrtasticPalette] Wardrobe: restored the human body for a vanilla tab.");
        }

        /// <summary>
        /// Forgets the instantiated body and drops bloom suppression - the preview rig is destroyed
        /// with the screen, so the cached instance must not outlive it (a stale reference would
        /// silently do nothing on the next visit).
        /// </summary>
        internal static void Clear()
        {
            PreviewBloomSuppressor.Restore();
            catBodyInstance = null;
        }

        /// <summary>
        /// Turns off the cat's glow effects in the wardrobe preview only. The preview holds the model
        /// still and close to the camera, which makes the aura sparkles and trail far more prominent
        /// than they are in play - enough to obscure the fur colour being picked. This touches only
        /// the preview's own instantiated copy, so the glow in the world is unaffected; AuraColor
        /// still controls that.
        /// </summary>
        private static void HidePreviewVfx()
        {
            if (catBodyInstance == null)
            {
                return;
            }

            var hidden = 0;
            foreach (var particles in catBodyInstance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.gameObject.SetActive(false);
                hidden++;
            }

            foreach (var trail in catBodyInstance.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.gameObject.SetActive(false);
                hidden++;
            }

            PurrtasticPalettePlugin.Log.LogInfo($"[PurrtasticPalette] Wardrobe: hid {hidden} VFX object(s) in the preview.");
        }
    }
}
