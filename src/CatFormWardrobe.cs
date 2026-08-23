using System;
using System.Linq;
using Chicken.Utilities;
using HarmonyLib;

namespace PurrtasticPalette
{
    /// <summary>
    /// Harmony host for the mirror's wardrobe screen. It injects the "Cat Form" tab (gated on
    /// ownership) and, across the screen's lifecycle hooks, drives three pieces that do the actual
    /// work: <see cref="CatPreviewController"/> (the preview-body swap + VFX/bloom),
    /// <see cref="WardrobePanelSwap"/> (hide the native rows / build our colour panel), and
    /// <see cref="PreviewColorSession"/> (snapshot colours + revert-on-cancel). This class only
    /// wires the hooks to those pieces; it holds no rig or panel state of its own.
    /// </summary>
    [HarmonyPatch(typeof(WardrobeCustomizationScreen), "OnShow")]
    internal static class CatFormWardrobe
    {
        private static readonly AccessTools.FieldRef<WardrobeCustomizationScreen, BumperMenuWidget> BumperMenuRef =
            AccessTools.FieldRefAccess<WardrobeCustomizationScreen, BumperMenuWidget>("bumperMenuWidget");

        // The open screen, so the Cat Form tab's own handler can reach its widgets. The tab's
        // OnSelect is a plain callback with no screen argument, unlike the game's own tabs which
        // close over their Tab data.
        private static WardrobeCustomizationScreen activeScreen;

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
                // the scroll selector but doesn't re-run the initial selection, so with enough tabs to
                // overflow the strip it stays centred with the first (selected) tab faded at the edge.
                // SelectDefaultMenu re-selects the first tab, and Show() just primed an instant
                // scroll, so the strip snaps to show the selected tab properly.
                bumperMenu.SelectDefaultMenu();

                // Snapshot the cat colours now (before any pick) so picks are a live preview that
                // reverts unless Confirm is pressed. OnCustomizationsConfirmed fires when the screen's
                // Confirm button is clicked; its Signal lives on this screen instance and dies with
                // it, so subscribing per-open needs no explicit unsubscribe.
                PreviewColorSession.Begin();
                __instance.OnCustomizationsConfirmed.AddListener(PreviewColorSession.Confirm);

                PurrtasticPalettePlugin.Log.LogInfo("[PurrtasticPalette] Wardrobe: added the Cat Form tab.");
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Wardrobe: failed to add the Cat Form tab: {e}");
            }
        }

        // The Cat Form tab's OnSelect handler: show the cat in the preview, then swap the game's
        // category rows for our colour panel - but only if the preview actually took, so a failed
        // preview doesn't leave the rows hidden behind an empty panel.
        private static void ShowCatInPreview()
        {
            try
            {
                if (!CatPreviewController.ShowCat())
                {
                    return;
                }

                WardrobePanelSwap.Show(activeScreen, CatPreviewController.ApplyColors);
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Wardrobe: failed to show the cat in the preview: {e}");
            }
        }

        /// <summary>
        /// Puts the human body back whenever one of the game's own tabs is picked. Those tabs go
        /// through HandleTabSelected; the Cat Form tab does not, so this only ever fires for vanilla
        /// tabs. A PREFIX, not a postfix, so it runs before the original repopulates the category
        /// rows: it restores our hidden rows and tears down our panel first, then restores the human
        /// body, leaving the original free to lay out normally.
        /// </summary>
        [HarmonyPatch(typeof(WardrobeCustomizationScreen), "HandleTabSelected")]
        [HarmonyPrefix]
        private static void RestoreHumanPreview()
        {
            try
            {
                // Always re-show the category rows and drop our panel, even if the cat was never
                // shown - cheap, and it guarantees the game's rows are visible.
                WardrobePanelSwap.Hide();
                CatPreviewController.HideCat();
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Wardrobe: failed to restore the human body: {e}");
            }
        }

        /// <summary>
        /// Screen closing: revert an unconfirmed preview, tear down our panel, and forget the cached
        /// body (the preview rig is destroyed with the screen, so a stale reference would silently do
        /// nothing on the next visit).
        /// </summary>
        [HarmonyPatch(typeof(WardrobeCustomizationScreen), "OnHide")]
        [HarmonyPostfix]
        private static void ClearCachedBody()
        {
            PreviewColorSession.RevertUnlessConfirmed();
            WardrobePanelSwap.Discard();
            CatPreviewController.Clear();
            activeScreen = null;
        }
    }
}
