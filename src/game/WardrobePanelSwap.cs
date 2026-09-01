using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// Swaps the wardrobe's native category rows for the Cat Form colour panel and back. Split out
    /// of the <see cref="CatFormWardrobe"/> Harmony host so the patch layer stays thin - this class
    /// owns the hide/restore of the game's rows and the build/destroy of our panel; the host decides
    /// when. The <see cref="CatFormColorPanel"/> holds the panel's own contents and selection state.
    /// </summary>
    internal static class WardrobePanelSwap
    {
        private static readonly AccessTools.FieldRef<WardrobeCustomizationScreen, CustomizationCategoryListWidget> CategoryListRef =
            AccessTools.FieldRefAccess<WardrobeCustomizationScreen, CustomizationCategoryListWidget>("categoryListWidget");

        // The Content children this mod disabled to show its own panel, so exactly those can be
        // re-enabled again - not blindly re-enabling the inactive template or empty-state view.
        private static readonly List<GameObject> hiddenCategoryObjects = new List<GameObject>();

        /// <summary>
        /// Hides the game's category rows and builds the Cat Form colour panel in their place, wiring
        /// <paramref name="onColorChanged"/> to fire on each swatch pick.
        /// </summary>
        internal static void Show(WardrobeCustomizationScreen screen, Action onColorChanged)
        {
            HideCategoryContent(screen);
            BuildColorPanel(screen, onColorChanged);
        }

        /// <summary>
        /// Restores the game's category rows and destroys our panel - for when a vanilla tab is
        /// picked while the screen keeps living. Safe to call even if the cat panel was never built.
        /// </summary>
        internal static void Hide()
        {
            RestoreCategoryContent();
            CatFormColorPanel.Destroy();
        }

        /// <summary>
        /// Destroys our panel and forgets the hidden-rows list WITHOUT restoring them - for screen
        /// teardown, where the screen's Content is torn down with it anyway.
        /// </summary>
        internal static void Discard()
        {
            CatFormColorPanel.Destroy();
            hiddenCategoryObjects.Clear();
        }

        /// <summary>
        /// Hides the game's category rows so the Cat Form panel can take their place. The serialized
        /// categoryListWidget field points at an inactive TEMPLATE - the rows the game actually
        /// displays are runtime CLONES of it ("CategoryListWidget(Clone)") sitting beside it under
        /// Content. Disabling the template did nothing visible; every currently-active Content child
        /// has to be disabled instead. Doing it by "whatever is active right now" rather than by name
        /// also covers the empty-state view and anything else the screen parks there.
        /// </summary>
        private static void HideCategoryContent(WardrobeCustomizationScreen screen)
        {
            hiddenCategoryObjects.Clear();

            var parent = CategoryContentParent(screen);
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

        /// <summary>
        /// Builds the swatch panel in the space the category rows would occupy, parented to the
        /// category list's own parent so it inherits the screen's layout and scaling rather than
        /// floating in its own canvas.
        /// </summary>
        private static void BuildColorPanel(WardrobeCustomizationScreen screen, Action onColorChanged)
        {
            var parent = CategoryContentParent(screen);
            if (parent == null)
            {
                PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Wardrobe: no panel parent - cannot build the colour panel.");
                return;
            }

            CatFormColorPanel.OnColorChanged = onColorChanged;
            CatFormColorPanel.Build(parent);
        }

        private static Transform CategoryContentParent(WardrobeCustomizationScreen screen)
        {
            if (screen == null)
            {
                return null;
            }

            var categoryList = CategoryListRef(screen);
            return categoryList != null ? categoryList.transform.parent : null;
        }
    }
}
