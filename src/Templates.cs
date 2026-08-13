// Ported from mods/ModNook/src/Templates.cs, namespace aside, with the bat-wing-fitting path
// removed (FitBatWings/BatWingFitter) since this mod never clones with keepWings: true - its
// buttons live on their own screen-injected row and its popup buttons are hand-drawn, neither
// wears the pause-menu's corner decoration. Everything else is unchanged. Fix bugs in both
// copies. See 10-visual-integration.md.
using System;
using System.Collections.Generic;
using System.Linq;
using Chicken.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatColorProbe
{
    /// <summary>
    /// Sources the game's own settings widgets so anything built from these reads correctly at
    /// any font size - a cloned SliderButton is the same object the game's Settings screen uses,
    /// with the same anchors, font and controller navigation. Nothing here invents a look.
    /// </summary>
    internal static class Templates
    {
        private static SliderButton slider;
        private static bool searched;

        /// <summary>The row the game uses for render scale and framerate.</summary>
        internal static SliderButton Slider
        {
            get { Search(); return slider; }
        }

        private static void Search()
        {
            if (searched)
            {
                return;
            }
            searched = true;

            slider = Find<SliderButton>("SliderButton");
        }

        /// <summary>
        /// Resources.FindObjectsOfTypeAll reaches inactive objects and loaded prefabs, not just
        /// what is on screen - so a widget stays sourceable while its own screen is hidden.
        /// </summary>
        private static T Find<T>(string label) where T : Component
        {
            try
            {
                var found = Resources.FindObjectsOfTypeAll<T>().Where(x => x != null).ToArray();

                // A laid-out scene instance beats a bare prefab: its rect already carries the size
                // the game gives that row, which is what our layout needs.
                var template =
                    found.FirstOrDefault(x => x.gameObject.scene.IsValid() && HasSize(x)) ??
                    found.FirstOrDefault(x => x.gameObject.scene.IsValid()) ??
                    found.FirstOrDefault();

                if (template == null)
                {
                    CatColorProbePlugin.Log.LogWarning($"No {label} found.");
                }
                else
                {
                    CatColorProbePlugin.Log.LogInfo($"{label} template: {PathOf(template.transform)}");
                }

                return template;
            }
            catch (Exception e)
            {
                CatColorProbePlugin.Log.LogWarning($"{label} lookup failed: {e.Message}");
                return null;
            }
        }

        private static bool HasSize(Component component)
        {
            var rect = component.transform as RectTransform;
            return rect != null && rect.rect.width > 1f && rect.rect.height > 1f;
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

        // ------------------------------------------------------------------ cloning

        private static Transform staging;

        /// <summary>
        /// An inactive holder that clones are born into, so their Awake does not run until we
        /// have finished stripping them - a clone's shared-signal subscriptions must never
        /// register against the live screen it was cloned from before it is detached.
        /// </summary>
        private static Transform Staging
        {
            get
            {
                if (staging == null)
                {
                    var host = new GameObject("CatColorProbe_Staging");
                    host.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    staging = host.transform;
                }

                return staging;
            }
        }

        /// <summary>
        /// Clones a native widget without waking it. The caller strips what it does not want, then
        /// calls Place to put it on screen.
        /// </summary>
        internal static T CloneInactive<T>(T template, string name) where T : Component
        {
            var clone = UnityEngine.Object.Instantiate(template, Staging, false);
            clone.name = name;
            return clone;
        }

        /// <summary>
        /// Moves a staged clone into the live hierarchy, carrying over the height it has as part
        /// of a real settings screen - a layout group cannot measure a clone on its own and would
        /// collapse it to nothing.
        /// </summary>
        internal static void Place<T>(T clone, T template, Transform parent, float fallbackHeight = 64f)
            where T : Component
        {
            var templateRect = template.transform as RectTransform;
            if (templateRect != null)
            {
                var height = templateRect.rect.height;
                var element = clone.gameObject.GetComponent<LayoutElement>() ??
                              clone.gameObject.AddComponent<LayoutElement>();

                element.preferredHeight = height > 1f ? height : fallbackHeight;
                element.minHeight = element.preferredHeight;
            }

            clone.transform.SetParent(parent, false);
            clone.gameObject.SetActive(true);
        }

        /// <summary>The whole dance for a labelled button: stage it, strip it, label it, place it.</summary>
        internal static AnimatedButton CloneButton(
            AnimatedButton template, Transform parent, string name, string label, Action onClick,
            float height = 64f, bool keepWings = false, Action<GameObject> prepare = null)
        {
            var button = CloneInactive(template, name);

            prepare?.Invoke(button.gameObject);

            StripLocalization(button.gameObject, destroy: true);
            RemoveBatWings(button.gameObject);

            if (!keepWings)
            {
                DisableSelectionMarker(button.gameObject);
            }

            SetLabel(button.gameObject, label);

            // Guarded, because OnClick is a Chicken Signal: one throwing handler aborts the single
            // multicast invocation and silently kills every listener registered after it.
            button.OnClick.AddListener(() =>
            {
                try
                {
                    onClick();
                }
                catch (Exception e)
                {
                    CatColorProbePlugin.Log.LogError($"'{label}' failed: {e}");
                }
            });

            Place(button, template, parent, height);
            SetLabel(button.gameObject, label);

            return button;
        }

        /// <summary>Clone, strip and place in one go, for the common case.</summary>
        internal static T Clone<T>(T template, Transform parent, string name) where T : Component
        {
            var clone = CloneInactive(template, name);
            StripDecorations(clone.gameObject);

            // Value widgets only. The game drives sliders from the navigation axes, and the wheel
            // is one of them - so with select-on-hover left on, scrolling quietly edits whichever
            // row the pointer crosses.
            DisableHoverSelect(clone.gameObject);

            Place(clone, template, parent);
            return clone;
        }

        internal static void RemoveBatWings(GameObject root)
        {
            foreach (var wing in root.GetComponentsInChildren<AnimatedBatWing>(true))
            {
                if (wing == null || wing.gameObject == root)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(wing.gameObject);
            }
        }

        internal static void DisableSelectionMarker(GameObject root)
        {
            foreach (var widget in root.GetComponentsInChildren<SelectableWidget>(true))
            {
                SelectionScreenField?.SetValue(widget, false);
            }
        }

        private static readonly System.Reflection.FieldInfo SelectionScreenField =
            typeof(SelectableWidget).GetField(
                "useSelectionScreen",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        internal static void DisableHoverSelect(GameObject root)
        {
            foreach (var button in root.GetComponentsInChildren<AnimatedButton>(true))
            {
                HoverSelectField?.SetValue(button, false);
            }
        }

        private static readonly System.Reflection.FieldInfo HoverSelectField =
            typeof(AnimatedButton).GetField(
                "selectOnHover",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>
        /// Stops the clone's localization components from reasserting the game's own string over
        /// whatever label we set. Disabled, never destroyed, on value widgets - CycleButton and
        /// SliderButton hold their value text as a LocalizedTextField and write through it on
        /// every change, so removing it makes Setup throw into a destroyed object. Destroyed on a
        /// plain button, which has no value text to write through - disabling alone was not
        /// enough there, since something re-runs these on a later show.
        /// </summary>
        internal static void StripLocalization(GameObject root, bool destroy = false)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var name = behaviour == null ? null : behaviour.GetType().FullName;
                if (name == null || name.IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (destroy)
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
                else
                {
                    behaviour.enabled = false;
                }
            }
        }

        internal static void StripDecorations(GameObject root)
        {
            StripLocalization(root);
            RemoveBatWings(root);
            DisableSelectionMarker(root);
        }

        /// <summary>
        /// Labels a cloned value widget, finding the label by elimination rather than by position
        /// - the widget names its own value text in a serialized field, so the label is simply the
        /// text that is not that.
        /// </summary>
        internal static void SetValueWidgetLabel(Component widget, string label)
        {
            if (widget == null)
            {
                return;
            }

            var field = widget.GetType().GetField(
                "valueText",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            var valueHost = (field?.GetValue(widget) as Component)?.gameObject;

            foreach (var text in widget.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null || (valueHost != null && text.gameObject == valueHost))
                {
                    continue;
                }

                text.text = label;

                foreach (var behaviour in text.GetComponents<MonoBehaviour>())
                {
                    var name = behaviour == null ? null : behaviour.GetType().FullName;
                    if (name != null && name.IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        UnityEngine.Object.DestroyImmediate(behaviour);
                    }
                }

                return;
            }

            CatColorProbePlugin.Log.LogWarning(
                $"No label text found on {widget.GetType().Name}; it will keep the template's own.");
        }

        /// <summary>Sets a cloned row's label, all text elements at once (plain buttons only carry one).</summary>
        internal static void SetLabel(GameObject root, string label)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = label;
            }
        }
    }
}
