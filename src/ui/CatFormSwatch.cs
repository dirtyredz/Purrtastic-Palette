using System;
using Chicken.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PurrtasticPalette
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

        // The colour plate's transform (the visible circle). It is a child of the widget root and
        // does not fill it - the widget reserves vertical space - so overlays like the custom tile's
        // "+" must be centred on THIS, not on the swatch root, or they sit off-centre. Captured
        // before the ColorSegmentView component may be destroyed; the GameObject and its Image
        // survive that, so the transform stays valid.
        private Transform plateTransform;

        internal Transform PlateTransform => plateTransform;

        internal static bool IsAvailable => FindTemplate() != null;

        private static GameObject FindTemplate()
        {
            // Unity's == null is true for a destroyed object, so this re-searches after the screen
            // that owned the previous template is torn down. Caching by a one-time bool instead
            // handed back a destroyed reference on the second visit - the clone then failed and the
            // panel silently regressed to the drawn fallback.
            if (template != null)
            {
                return template;
            }

            // CustomizationOptionListWidget renders two ways from one class: a square with an item
            // icon (glasses, hairstyles) or a colour-filled circle (eye colour, skin colour). The
            // difference is the serialized displayAssetPreviewColorAsBackground flag. Only the colour
            // variant is any use here - grabbing the first instance got a glasses square with no
            // fill. Require the flag, prefer a real scene instance, and fall back to the last colour
            // candidate seen (the original loop's behaviour) when none is scene-valid.
            var widget = GameTemplate.Find<CustomizationOptionListWidget>(
                "Colour swatch",
                match: w => ColorBackgroundRef(w),
                preferred: w => w.gameObject.scene.IsValid(),
                fallbackLast: true);

            template = widget != null ? widget.gameObject : null;
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
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Swatch clone '{name}' failed: {e}");
                return null;
            }
        }

        private void Initialise(CustomizationOptionListWidget widget, Color? fill)
        {
            if (widget != null)
            {
                colorSegment = ColorSegmentRef(widget);
                plateTransform = colorSegment != null ? colorSegment.transform : null;
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
            PointerTriggers.Add(trigger, EventTriggerType.PointerEnter, () => { hovered = true; Refresh(); });
            PointerTriggers.Add(trigger, EventTriggerType.PointerExit, () => { hovered = false; Refresh(); });
            PointerTriggers.Add(trigger, EventTriggerType.PointerClick, () => onClick?.Invoke());
            // EventTrigger swallows the mouse wheel (it implements IScrollHandler); forward it so the
            // wardrobe ScrollRect still scrolls while the cursor is over a swatch.
            gameObject.AddComponent<ScrollForwarder>();

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

    }
}
