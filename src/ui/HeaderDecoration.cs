using System;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// A decorated row header cloned from the game's own category header - the swirl flourishes on
    /// each side and the rule underneath - so it matches the vanilla "Right Eye Color" title
    /// instead of a plain left-aligned label.
    ///
    /// The header lives on CustomizationCategoryListWidget.headerText's parent bar. The bar is
    /// cloned for its art, the localised-text component is removed (or it would re-assert its own
    /// text on enable), and the remaining TextMeshPro is set directly to the row name.
    /// </summary>
    internal static class HeaderDecoration
    {
        private static GameObject templateBar;

        internal static bool IsAvailable => FindTemplateBar() != null;

        private static GameObject FindTemplateBar()
        {
            // Re-search when the cached bar has been destroyed (Unity == null), so re-entering the
            // mirror finds a live template instead of reusing a dead one - the same regression the
            // swatch template had.
            if (templateBar != null)
            {
                return templateBar;
            }

            // Wrap the whole locate in try/catch so any failure (reflection, a torn-down widget)
            // degrades to plain labels rather than throwing into the wardrobe UI - GameTemplate
            // guards its own enumeration, but the reflected field lookup and the parent projection
            // that frame it live out here in the caller.
            try
            {
                var headerField = AccessTools.Field(typeof(CustomizationCategoryListWidget), "headerText");
                if (headerField == null)
                {
                    PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Header: no headerText field found.");
                    return null;
                }

                // The header lives on headerText's parent bar - that bar is what we clone. Prefer a
                // bar in a live scene, and fall back to the last eligible widget seen (the original
                // loop's behaviour) when none is scene-valid. The scene check and the result both
                // project through the same parent, so they stay consistent.
                GameObject BarOf(CustomizationCategoryListWidget w)
                    => headerField.GetValue(w) is Component header && header.transform.parent != null
                        ? header.transform.parent.gameObject
                        : null;

                var owner = GameTemplate.Find<CustomizationCategoryListWidget>(
                    "Header",
                    match: w => BarOf(w) != null,
                    preferred: w => BarOf(w).scene.IsValid(),
                    fallbackLast: true);

                templateBar = owner != null ? BarOf(owner) : null;
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Header template lookup failed: {e}");
                templateBar = null;
            }

            return templateBar;
        }

        /// <returns>The cloned header GameObject, or null if the template could not be cloned.</returns>
        internal static GameObject Create(Transform parent, string text)
        {
            var source = FindTemplateBar();
            if (source == null)
            {
                return null;
            }

            try
            {
                var clone = UnityEngine.Object.Instantiate(source, parent, false);
                clone.name = $"Header_{text}";
                clone.SetActive(true);

                // Remove any localised-text driver so it cannot overwrite the text we set. Matched
                // by type name to avoid a compile-time dependency on the game's localisation type.
                foreach (var comp in clone.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (comp != null && comp.GetType().Name.IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        UnityEngine.Object.Destroy(comp);
                    }
                }

                var label = clone.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
                if (label != null)
                {
                    label.text = text;
                }

                return clone;
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Header clone '{text}' failed: {e}");
                return null;
            }
        }
    }
}
