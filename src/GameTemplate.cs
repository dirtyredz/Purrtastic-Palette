using System;
using System.Collections.Generic;
using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// Locating a native widget to clone follows one shape everywhere in the mod: enumerate every
    /// instance including inactive objects and loaded prefabs (Resources.FindObjectsOfTypeAll reaches
    /// what is off-screen, so a widget stays sourceable while its own screen is hidden), keep the
    /// ones that match, and prefer a laid-out scene instance over a bare prefab - all wrapped in a
    /// try/catch that logs and degrades to null rather than throwing into UI code.
    ///
    /// The three call sites differ in ways that MUST be preserved, so they are parameters here, not
    /// assumptions - a naive merge would silently change which template wins, a runtime-only failure:
    ///   - the <paramref name="match"/> predicate (which instances are eligible at all);
    ///   - an optional <paramref name="secondary"/> middle tier that outranks the plain fallback
    ///     (the settings-slider site prefers scene-valid-and-sized, then scene-valid, then any);
    ///   - the <paramref name="fallbackLast"/> ordering for when nothing is scene-valid (the slider
    ///     site keeps the first eligible match; the two cached-widget sites keep the last).
    /// Callers project the winner to whatever they actually keep (the component, its GameObject, a
    /// parent bar) and own their own caching.
    /// </summary>
    internal static class GameTemplate
    {
        /// <param name="label">Human name for the diagnostic log line.</param>
        /// <param name="match">Which instances are eligible. Nulls are skipped before this is called.</param>
        /// <param name="preferred">The top tier - the first eligible match satisfying this wins
        /// outright (e.g. a real scene instance, optionally also sized).</param>
        /// <param name="secondary">An optional middle tier that beats the plain fallback but loses to
        /// <paramref name="preferred"/>; the first match satisfying it wins. Null means no middle tier.</param>
        /// <param name="fallbackLast">When nothing meets <paramref name="preferred"/> or
        /// <paramref name="secondary"/>, which eligible match to return: the last one seen (true) or
        /// the first (false). Preserves each site's original ordering.</param>
        internal static T Find<T>(
            string label,
            Func<T, bool> match,
            Func<T, bool> preferred,
            Func<T, bool> secondary = null,
            bool fallbackLast = false) where T : Component
        {
            try
            {
                T secondaryPick = null;
                T fallback = null;
                var matches = 0;

                foreach (var candidate in Resources.FindObjectsOfTypeAll<T>())
                {
                    if (candidate == null || !match(candidate))
                    {
                        continue;
                    }

                    matches++;

                    // Top tier: a laid-out scene instance carries the size the game gives that row,
                    // which is what our layout needs. The first one wins, so stop looking.
                    if (preferred(candidate))
                    {
                        return Log(label, candidate, matches);
                    }

                    if (secondary != null && secondaryPick == null && secondary(candidate))
                    {
                        secondaryPick = candidate;
                    }

                    // fallbackLast keeps the most recent eligible match; otherwise keep only the first.
                    if (fallback == null || fallbackLast)
                    {
                        fallback = candidate;
                    }
                }

                var chosen = secondaryPick ?? fallback;
                if (chosen == null)
                {
                    PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] No {label} found.");
                    return null;
                }

                return Log(label, chosen, matches);
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] {label} lookup failed: {e.Message}");
                return null;
            }
        }

        private static T Log<T>(string label, T template, int matches) where T : Component
        {
            PurrtasticPalettePlugin.Log.LogInfo(
                $"[PurrtasticPalette] {label} template: {PathOf(template.transform)} ({matches} match(es)).");
            return template;
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                parts.Add(current.name);
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
