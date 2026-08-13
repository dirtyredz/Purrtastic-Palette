using System;
using Chicken.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatColorProbe
{
    /// <summary>
    /// One colour swatch, cloned from the game's own CustomizationOptionListWidget so it carries
    /// the real art - the round colour plate, the bat-wing selection frame, the applied checkmark
    /// and the hover click sound - instead of hand-drawn approximations that never quite matched.
    ///
    /// The widget is normally driven by an ItemAsset through a list. Cat colours are not items, so
    /// this drives the pieces directly instead: ColorSegmentView.Show(colour) sets the plate (a
    /// public single-colour overload exists exactly for this), and the selection frame / applied
    /// visual are toggled through the widget's own reflected fields. The list-driven component is
    /// removed so its ItemAsset-dependent code never runs.
    /// </summary>
    internal sealed class CatFormSwatch : MonoBehaviour
    {
        private static bool searched;
        private static GameObject template;

        private static readonly AccessTools.FieldRef<CustomizationOptionListWidget, ColorSegmentView> ColorSegmentRef =
            AccessTools.FieldRefAccess<CustomizationOptionListWidget, ColorSegmentView>("colorSegmentView");
        private static readonly AccessTools.FieldRef<CustomizationOptionListWidget, AnimatedWidget> FrameWidgetRef =
            AccessTools.FieldRefAccess<CustomizationOptionListWidget, AnimatedWidget>("selectionFrameWidget");
        private static readonly AccessTools.FieldRef<CustomizationOptionListWidget, UIColorable> FrameColorableRef =
            AccessTools.FieldRefAccess<CustomizationOptionListWidget, UIColorable>("selectionFrameColorable");
        private static readonly AccessTools.FieldRef<CustomizationOptionListWidget, GameObject> AppliedVisualRef =
            AccessTools.FieldRefAccess<CustomizationOptionListWidget, GameObject>("appliedVisual");
        private static readonly AccessTools.FieldRef<CustomizationOptionListWidget, string> AppliedColorRef =
            AccessTools.FieldRefAccess<CustomizationOptionListWidget, string>("appliedStateColor");
        private static readonly AccessTools.FieldRef<CustomizationOptionListWidget, bool> ColorBackgroundRef =
            AccessTools.FieldRefAccess<CustomizationOptionListWidget, bool>("displayAssetPreviewColorAsBackground");
        private static readonly AccessTools.FieldRef<CustomizationOptionListWidget, GameObject> InventoryIconRef =
            AccessTools.FieldRefAccess<CustomizationOptionListWidget, GameObject>("inventoryIcon");

        private ColorSegmentView colorSegment;
        private AnimatedWidget frame;
        private UIColorable frameColorable;
        private GameObject appliedVisual;
        private string appliedStateColor;

        private Func<bool> isCurrent;
        private Action onClick;
        private bool hovered;

        internal static bool IsAvailable => FindTemplate() != null;

        private static GameObject FindTemplate()
        {
            if (searched)
            {
                return template;
            }

            searched = true;
            try
            {
                // CustomizationOptionListWidget renders two ways from one class: a square with an
                // item icon (glasses, hairstyles) or a colour-filled circle (eye colour, skin
                // colour). The difference is the serialized displayAssetPreviewColorAsBackground
                // flag. Only the colour variant is any use here - grabbing the first instance got
                // a glasses square with no fill. Require the flag, and prefer a real scene
                // instance over a bare prefab.
                var found = Resources.FindObjectsOfTypeAll<CustomizationOptionListWidget>();
                CustomizationOptionListWidget best = null;
                var colourCandidates = 0;
                foreach (var w in found)
                {
                    if (w == null || !ColorBackgroundRef(w))
                    {
                        continue;
                    }

                    colourCandidates++;
                    best = w;
                    if (w.gameObject.scene.IsValid())
                    {
                        break;
                    }
                }

                template = best != null ? best.gameObject : null;
                CatColorProbePlugin.Log.LogInfo(template != null
                    ? $"[CatColorProbe] Colour swatch template: '{template.name}' ({colourCandidates} colour candidate(s) of {found.Length} total)."
                    : $"[CatColorProbe] No colour swatch template among {found.Length} widget(s) - using drawn swatches.");
            }
            catch (Exception e)
            {
                CatColorProbePlugin.Log.LogError($"[CatColorProbe] Swatch template lookup failed: {e}");
                template = null;
            }

            return template;
        }

        /// <param name="fill">The plate colour. Null leaves the cloned plate as-is (used by the
        /// custom "+" tile when nothing custom is set yet).</param>
        internal static CatFormSwatch Create(Transform parent, string name, Color? fill, Func<bool> isCurrent, Action onClick)
        {
            var source = FindTemplate();
            if (source == null)
            {
                return null;
            }

            try
            {
                var clone = Instantiate(source, parent, false);
                clone.name = name;
                clone.SetActive(true);

                var widget = clone.GetComponent<CustomizationOptionListWidget>();
                var swatch = clone.AddComponent<CatFormSwatch>();
                swatch.isCurrent = isCurrent;
                swatch.onClick = onClick;
                swatch.Initialise(widget, fill);
                return swatch;
            }
            catch (Exception e)
            {
                CatColorProbePlugin.Log.LogError($"[CatColorProbe] Swatch clone '{name}' failed: {e}");
                return null;
            }
        }

        private void Initialise(CustomizationOptionListWidget widget, Color? fill)
        {
            if (widget != null)
            {
                colorSegment = ColorSegmentRef(widget);
                frame = FrameWidgetRef(widget);
                frameColorable = FrameColorableRef(widget);
                appliedVisual = AppliedVisualRef(widget);
                appliedStateColor = AppliedColorRef(widget);

                // Hide the leftover inventory ("owned") chest icon the template item carried.
                var inventoryIcon = InventoryIconRef(widget);
                if (inventoryIcon != null)
                {
                    inventoryIcon.SetActive(false);
                }

                // Remove the list-driven component: its OnSetup and UpdateVisual dereference an
                // ItemAsset this clone does not have. The child objects it referenced (plate,
                // frame, applied visual) survive because they are separate components.
                Destroy(widget);
            }

            // Colour the plate directly rather than through ColorSegmentView.Show: Show writes a
            // single colour into the gradient shader, which visibly did nothing (segment was found
            // but every swatch still showed the template's purple gradient). Setting the plate
            // Image's material to the default and tinting it solid-fills the circle sprite, which
            // is what a colour swatch actually is. The gradient component is removed so it cannot
            // re-assert its material.
            if (fill.HasValue && colorSegment != null)
            {
                var plate = colorSegment.GetComponent<Image>();
                if (plate != null)
                {
                    plate.material = null;
                    plate.color = fill.Value;
                }

                Destroy(colorSegment);
                colorSegment = null;
            }

            var trigger = gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => { hovered = true; Refresh(); });
            AddTrigger(trigger, EventTriggerType.PointerExit, () => { hovered = false; Refresh(); });
            AddTrigger(trigger, EventTriggerType.PointerClick, () => onClick?.Invoke());

            if (frame != null)
            {
                frame.Hide(instant: true);
            }

            if (appliedVisual != null)
            {
                appliedVisual.SetActive(false);
            }
        }


        internal void Refresh()
        {
            var current = isCurrent != null && isCurrent();

            if (frame != null && frameColorable != null)
            {
                if (hovered)
                {
                    frameColorable.OverrideColor(AddressableLibrary<ColorLibrary>.Instance.SelectionColor, 0f);
                    frame.Show();
                }
                else if (current)
                {
                    frameColorable.OverrideColor(appliedStateColor, 0f);
                    frame.Show();
                }
                else
                {
                    frame.Hide(instant: false);
                }
            }

            if (appliedVisual != null)
            {
                appliedVisual.SetActive(current);
            }
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }
    }
}
