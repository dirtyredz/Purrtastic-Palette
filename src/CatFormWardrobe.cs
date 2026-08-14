using System;
using System.Linq;
using BepInEx.Configuration;
using Chicken.Utilities;
using HarmonyLib;
using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// Phase 2, step 1: put a "Cat Form" tab in the mirror's wardrobe screen and show the cat in
    /// the preview when it's picked.
    ///
    /// This is a spike, not the finished feature. It proves (or kills) the one assumption the
    /// whole design rests on: that the customization preview can be made to show the Cat Form
    /// body at all. Everything else - colour swatch rows, live apply, confirm/cancel - is
    /// ordinary UI work that only makes sense if this works.
    ///
    /// Why it might not: the preview is NOT the player entity. CustomizationCharacterPreview is a
    /// standalone rig with its own private BodyViewAsset field, so it is not driven by
    /// EntityCustomization.SetBodyView the way the real player is - the cat body has to be
    /// instantiated into the rig by hand. The rig's camera framing, lighting, scale and rotation
    /// are all built around the human body, and the cat is a completely different shape and size.
    /// It may render off-centre, mis-scaled, T-posed, or not at all.
    /// </summary>
    [HarmonyPatch(typeof(WardrobeCustomizationScreen), "OnShow")]
    internal static class CatFormWardrobe
    {
        private const string CatBodyViewNameFragment = "Hellkitten";

        private static readonly AccessTools.FieldRef<WardrobeCustomizationScreen, BumperMenuWidget> BumperMenuRef =
            AccessTools.FieldRefAccess<WardrobeCustomizationScreen, BumperMenuWidget>("bumperMenuWidget");

        private static readonly AccessTools.FieldRef<CustomizationCharacterPreview, BodyViewAsset> PreviewBodyRef =
            AccessTools.FieldRefAccess<CustomizationCharacterPreview, BodyViewAsset>("bodyView");

        private static readonly AccessTools.FieldRef<WardrobeCustomizationScreen, CustomizationCategoryListWidget> CategoryListRef =
            AccessTools.FieldRefAccess<WardrobeCustomizationScreen, CustomizationCategoryListWidget>("categoryListWidget");

        // The open screen, so the Cat Form tab's own handler can reach its widgets. The tab's
        // OnSelect is a plain callback with no screen argument, unlike the game's own tabs which
        // close over their Tab data.
        private static WardrobeCustomizationScreen activeScreen;

        // The instantiated cat body, so repeated tab presses reuse it and hiding it again is
        // possible. Cleared when the screen closes, since the preview rig itself is torn down.
        private static BodyViewAsset catBodyInstance;

        // Live-preview support: a swatch pick applies immediately (so the cat updates as you browse),
        // but is only KEPT if the player presses Confirm. Snapshot the colour config when the
        // wardrobe opens and restore it on close-without-confirm - the same revert-on-cancel the
        // game gives its own try-on clothing.
        private static string[] colorSnapshot;
        private static bool customizationsConfirmed;

        private static ConfigEntry<string>[] ManagedColors() => new[]
        {
            PurrtasticPalettePlugin.FurColor,
            PurrtasticPalettePlugin.WhiskerColor,
            PurrtasticPalettePlugin.EyeColor,
            PurrtasticPalettePlugin.PupilColor,
            PurrtasticPalettePlugin.EyeHighlightColor,
            PurrtasticPalettePlugin.AuraColor,
        };

        /// <summary>
        /// Whether the player owns Cat Form. Forms are ItemAssets whose ToolAddon is a form tool
        /// (CatToolAsset here), kept in GameInventory's Forms inventory; Contains routes the item to
        /// that inventory. Returns false if the inventory or item can't be reached, so an unknown
        /// state hides the tab rather than showing it wrongly.
        /// </summary>
        private static bool PlayerOwnsCatForm()
        {
            try
            {
                if (!MonoBehaviourSingleton<GameInventory>.Exists)
                {
                    return false;
                }

                var catItem = Asset.GetAll<ItemAsset>()
                    .FirstOrDefault(x => x != null && x.ToolAddon is CatToolAsset);
                if (catItem == null)
                {
                    return false;
                }

                return MonoBehaviourSingleton<GameInventory>.Instance.Contains(new ItemEntry(catItem));
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Wardrobe: Cat Form ownership check failed: {e}");
                return false;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(WardrobeCustomizationScreen __instance)
        {
            try
            {
                activeScreen = __instance;
                var bumperMenu = BumperMenuRef(__instance);
                if (bumperMenu == null)
                {
                    PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Wardrobe: no bumperMenuWidget found - cannot add the Cat Form tab.");
                    return;
                }

                // Only offer the tab once the player actually owns Cat Form - recolouring a form you
                // cannot turn into makes no sense, and the tab would otherwise show from the very
                // first mirror use. The widget has no "disabled" state, so gate by not adding it.
                if (!PlayerOwnsCatForm())
                {
                    PurrtasticPalettePlugin.Log.LogInfo("[PurrtasticPalette] Wardrobe: Cat Form not owned - tab hidden.");
                    return;
                }

                bumperMenu.AddItem(new BumperMenuWidget.WidgetData
                {
                    Text = "Cat Form",
                    Icon = TabIcon.Get(),
                    OnSelect = ShowCatInPreview,
                });

                // OnShow already called Show()/SelectDefaultMenu() before this postfix ran, so the
                // new item needs the widget rebuilt to appear. If the tab doesn't show up in-game,
                // this is the first thing to suspect.
                bumperMenu.Show();

                // Re-select the default tab AFTER adding ours. Show() reflows the strip and refreshes
                // the scroll selector but doesn't re-run the initial selection, so with enough tabs
                // to overflow the strip it stays centred with the first (selected) tab faded at the
                // edge. SelectDefaultMenu re-selects the first tab, and Show() just primed an instant
                // scroll, so the strip snaps to show the selected tab properly.
                bumperMenu.SelectDefaultMenu();

                // Snapshot the cat colours now (before any pick) so picks are a live preview that
                // reverts unless Confirm is pressed. OnCustomizationsConfirmed fires when the
                // screen's Confirm button is clicked; its Signal lives on this screen instance and
                // dies with it, so subscribing per-open needs no explicit unsubscribe.
                var colors = ManagedColors();
                colorSnapshot = colors.Select(c => c.Value).ToArray();
                customizationsConfirmed = false;
                __instance.OnCustomizationsConfirmed.AddListener(HandleCustomizationsConfirmed);

                PurrtasticPalettePlugin.Log.LogInfo("[PurrtasticPalette] Wardrobe: added the Cat Form tab.");
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Wardrobe: failed to add the Cat Form tab: {e}");
            }
        }

        private static void ShowCatInPreview()
        {
            try
            {
                if (!MonoBehaviourSingleton<CharacterCustomizer>.Exists)
                {
                    PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Wardrobe: no CharacterCustomizer - cannot reach the preview.");
                    return;
                }

                var preview = MonoBehaviourSingleton<CharacterCustomizer>.Instance.CharacterPreview;
                if (preview == null)
                {
                    PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Wardrobe: CharacterPreview is null.");
                    return;
                }

                var humanBody = PreviewBodyRef(preview);
                if (humanBody == null)
                {
                    PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Wardrobe: the preview has no body view to replace.");
                    return;
                }

                if (catBodyInstance == null)
                {
                    var catBodyAsset = Asset.GetAll<BodyViewAsset>()
                        .FirstOrDefault(x => x != null && x.name.IndexOf(CatBodyViewNameFragment, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (catBodyAsset == null)
                    {
                        PurrtasticPalettePlugin.Log.LogWarning(
                            $"[PurrtasticPalette] Wardrobe: no BodyViewAsset matching '{CatBodyViewNameFragment}' found.");
                        return;
                    }

                    // Instantiate alongside the human body and copy its local transform, so the cat
                    // lands wherever the rig expects a body to be. Whether that framing suits a cat
                    // is exactly what this spike is testing.
                    catBodyInstance = UnityEngine.Object.Instantiate(catBodyAsset, humanBody.transform.parent);
                    catBodyInstance.transform.localPosition = humanBody.transform.localPosition;
                    catBodyInstance.transform.localRotation = humanBody.transform.localRotation;
                    catBodyInstance.transform.localScale = humanBody.transform.localScale;
                    PurrtasticPalettePlugin.Log.LogInfo(
                        $"[PurrtasticPalette] Wardrobe: instantiated '{catBodyAsset.name}' into the preview under " +
                        $"'{humanBody.transform.parent?.name}'.");
                }

                catBodyInstance.gameObject.SetActive(true);
                humanBody.gameObject.SetActive(false);
                HidePreviewVfx();
                PreviewBloomSuppressor.Suppress();

                // The preview cat is a separate instance of the body prefab with its own material
                // instances, so the colours applied to the live player don't carry over - without
                // this it renders vanilla black while the real cat outside is coloured.
                CatColorPatch.ApplyToBody(catBodyInstance);

                ClearCategoryPanel();
                BuildColorPanel();

                var renderers = catBodyInstance.GetComponentsInChildren<Renderer>(true).Length;
                PurrtasticPalettePlugin.Log.LogInfo(
                    $"[PurrtasticPalette] Wardrobe: showing the cat body in the preview " +
                    $"({renderers} renderer(s), scale {catBodyInstance.transform.lossyScale}).");
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Wardrobe: failed to show the cat in the preview: {e}");
            }
        }

        /// <summary>
        /// Turns off the cat's glow effects in the wardrobe preview only. The preview holds the
        /// model still and close to the camera, which makes the aura sparkles and trail far more
        /// prominent than they are in play - enough to obscure the fur colour being picked. This
        /// touches only the preview's own instantiated copy, so the glow in the world is
        /// unaffected; AuraColor still controls that.
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

        /// <summary>
        /// Blanks the category rows on the right. Without this the panel keeps showing whatever
        /// the previously selected tab built (skin colour swatches, accessories, ...), which is
        /// both wrong and clickable - picking one would apply a human customization while the
        /// cat is on screen. The real colour panel replaces this; for now an empty list is the
        /// honest state.
        /// </summary>
        // The Content children this mod disabled to show its own panel, so exactly those can be
        // re-enabled again - not blindly re-enabling the inactive template or empty-state view.
        private static readonly System.Collections.Generic.List<GameObject> hiddenCategoryObjects =
            new System.Collections.Generic.List<GameObject>();

        private static void ClearCategoryPanel()
        {
            HideCategoryContent();
        }

        /// <summary>
        /// Hides the game's category rows so the Cat Form panel can take their place. The
        /// serialized categoryListWidget field points at an inactive TEMPLATE - the rows the game
        /// actually displays are runtime CLONES of it ("CategoryListWidget(Clone)") sitting beside
        /// it under Content (confirmed from a hierarchy dump). Disabling the template did nothing
        /// visible; every currently-active Content child has to be disabled instead. Doing it by
        /// "whatever is active right now" rather than by name also covers the empty-state view and
        /// anything else the screen parks there.
        /// </summary>
        private static void HideCategoryContent()
        {
            hiddenCategoryObjects.Clear();

            var parent = CategoryContentParent();
            if (parent == null)
            {
                return;
            }

            foreach (Transform child in parent)
            {
                if (child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                    hiddenCategoryObjects.Add(child.gameObject);
                }
            }
        }

        private static void RestoreCategoryContent()
        {
            foreach (var go in hiddenCategoryObjects)
            {
                if (go != null)
                {
                    go.SetActive(true);
                }
            }

            hiddenCategoryObjects.Clear();
        }

        private static Transform CategoryContentParent()
        {
            if (activeScreen == null)
            {
                return null;
            }

            var categoryList = CategoryListRef(activeScreen);
            return categoryList != null ? categoryList.transform.parent : null;
        }

        /// <summary>
        /// Builds the swatch panel in the space the category rows would occupy, parented to the
        /// category list's own parent so it inherits the screen's layout and scaling rather than
        /// floating in its own canvas.
        /// </summary>
        private static void BuildColorPanel()
        {
            if (activeScreen == null)
            {
                return;
            }

            var categoryList = CategoryListRef(activeScreen);
            var parent = categoryList != null ? categoryList.transform.parent : null;
            if (parent == null)
            {
                PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Wardrobe: no panel parent - cannot build the colour panel.");
                return;
            }

            // Recolour the preview as soon as a swatch is picked. The live player is handled by
            // the plugin's own SettingChanged hook; this covers the preview's separate body.
            CatFormColorPanel.OnColorChanged = () => CatColorPatch.ApplyToBody(catBodyInstance);
            CatFormColorPanel.Build(parent);
        }

        /// <summary>
        /// Puts the human body back whenever one of the game's own tabs is picked. Those tabs go
        /// through HandleTabSelected; the Cat Form tab does not, so this only ever fires for
        /// vanilla tabs. A PREFIX, not a postfix, so it runs before the original repopulates the
        /// category rows: it re-enables the category widget (which the cat tab disabled) and tears
        /// down our panel first, leaving the original free to lay out normally.
        /// </summary>
        [HarmonyPatch(typeof(WardrobeCustomizationScreen), "HandleTabSelected")]
        [HarmonyPrefix]
        private static void RestoreHumanPreview()
        {
            try
            {
                // Always re-show the category rows and drop our panel, even if the cat was never
                // shown - cheap, and it guarantees the game's rows are visible.
                RestoreCategoryContent();
                CatFormColorPanel.Destroy();

                if (catBodyInstance == null || !MonoBehaviourSingleton<CharacterCustomizer>.Exists)
                {
                    return; // cat was never shown - nothing further to undo
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
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Wardrobe: failed to restore the human body: {e}");
            }
        }

        /// <summary>
        /// The preview rig is destroyed with the screen, so the cached instance must not outlive
        /// it - a stale reference would silently do nothing on the next visit.
        /// </summary>
        [HarmonyPatch(typeof(WardrobeCustomizationScreen), "OnHide")]
        [HarmonyPostfix]
        private static void ClearCachedBody()
        {
            RevertUnlessConfirmed();
            CatFormColorPanel.Destroy();
            PreviewBloomSuppressor.Restore();
            hiddenCategoryObjects.Clear(); // the screen and its Content are torn down with it
            catBodyInstance = null;
            activeScreen = null;
        }

        private static void HandleCustomizationsConfirmed()
        {
            customizationsConfirmed = true;
        }

        /// <summary>
        /// Restores the colour snapshot taken when the wardrobe opened, unless the player pressed
        /// Confirm. Writing a ConfigEntry.Value raises SettingChanged, which reapplies colours to the
        /// live player - so this reverts the world cat as well as discarding the preview picks. A
        /// no-op when nothing changed or when confirmed.
        /// </summary>
        private static void RevertUnlessConfirmed()
        {
            try
            {
                if (customizationsConfirmed || colorSnapshot == null)
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
                customizationsConfirmed = false;
            }
        }
    }
}
