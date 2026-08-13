using System;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CatColorProbe
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

            try
            {
                var headerField = AccessTools.Field(typeof(CustomizationCategoryListWidget), "headerText");
                if (headerField == null)
                {
                    CatColorProbePlugin.Log.LogWarning("[CatColorProbe] Header: no headerText field found.");
                    return null;
                }

                foreach (var widget in Resources.FindObjectsOfTypeAll<CustomizationCategoryListWidget>())
                {
                    if (widget == null)
                    {
                        continue;
                    }

                    if (!(headerField.GetValue(widget) is Component header) || header.transform.parent == null)
                    {
                        continue;
                    }

                    templateBar = header.transform.parent.gameObject;
                    if (templateBar.scene.IsValid())
                    {
                        break;
                    }
                }

                CatColorProbePlugin.Log.LogInfo(templateBar != null
                    ? $"[CatColorProbe] Header template: '{templateBar.name}'."
                    : "[CatColorProbe] No header template found - using plain labels.");
            }
            catch (Exception e)
            {
                CatColorProbePlugin.Log.LogError($"[CatColorProbe] Header template lookup failed: {e}");
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
                CatColorProbePlugin.Log.LogError($"[CatColorProbe] Header clone '{text}' failed: {e}");
                return null;
            }
        }
    }
}
