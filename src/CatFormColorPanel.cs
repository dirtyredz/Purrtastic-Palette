using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PurrtasticPalette
{
    /// <summary>
    /// The Cat Form tab's own panel: a scrollable row of colour swatches per part.
    ///
    /// Drawn rather than reusing the game's CustomizationOptionListWidget. Cloning that widget was
    /// tried and reverted: it is driven entirely by an ItemAsset, and with that component stripped
    /// the clone lost its round plate and hover response, keeping only the checkmark - worse than
    /// drawing plain circles, which at least match the vanilla shape and size.
    /// </summary>
    internal static class CatFormColorPanel
    {
        /// <summary>
        /// One row per colourable part. DefaultColor is what the game ships that part as, shown on
        /// the leftmost swatch: a blank config value means "vanilla", and painting that swatch the
        /// actual vanilla colour says so far more clearly than a caption ever did.
        /// </summary>
        private static readonly (string Label, Func<ConfigEntry<string>> Setting, Color DefaultColor)[] Rows =
        {
            ("Fur", () => PurrtasticPalettePlugin.FurColor, new Color32(0x14, 0x10, 0x18, 0xFF)),
            ("Whiskers", () => PurrtasticPalettePlugin.WhiskerColor, new Color32(0xF2, 0xEC, 0xFF, 0xFF)),
            ("Eyes", () => PurrtasticPalettePlugin.EyeColor, new Color32(0xFF, 0x43, 0x39, 0xFF)),
            ("Pupil", () => PurrtasticPalettePlugin.PupilColor, new Color32(0x14, 0x10, 0x18, 0xFF)),
            ("Eye Highlight", () => PurrtasticPalettePlugin.EyeHighlightColor, new Color32(0xF2, 0xEC, 0xFF, 0xFF)),
        };

        private static readonly (string Label, string Hex)[] Presets =
        {
            ("Black", "#141018"),
            ("White", "#F2ECFF"),
            ("Red", "#FF4339"),
            ("Orange", "#FF8A3D"),
            ("Gold", "#FCD34D"),
            ("Green", "#4ADE80"),
            ("Teal", "#2DD4BF"),
            ("Blue", "#5ACEF9"),
            ("Indigo", "#7F54D3"),
            ("Violet", "#E452FC"),
            ("Pink", "#FF7AC6"),
        };

        private const float SwatchSize = 90f;

        private static GameObject root;
        private static Transform lastParent;
        private static readonly List<SwatchEntry> Swatches = new List<SwatchEntry>();
        private static readonly List<CatFormSwatch> ClonedSwatches = new List<CatFormSwatch>();

        internal static Action OnColorChanged;

        internal static bool IsBuilt => root != null;

        internal static void Build(Transform parent)
        {
            lastParent = parent;
            Destroy();

            try
            {
                Swatches.Clear();
                ClonedSwatches.Clear();

                root = new GameObject("PurrtasticPalette_ColorPanel", typeof(RectTransform));
                root.transform.SetParent(parent, false);

                var layout = root.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 28f;
                // childControlHeight true so each row's explicit LayoutElement height is honoured;
                // false makes them collapse onto each other.
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;
                layout.padding = new RectOffset(8, 8, 40, 8); // extra top padding above the first header

                // Sum the row heights so the panel can be given a DEFINITE height. This is the fix
                // for "the panel doesn't scroll": the parent "Content" is the screen's own scroll
                // content (a ScrollRect + Mask viewport above it, a VerticalLayoutGroup +
                // ContentSizeFitter on it). The game's category rows scroll because each is a
                // top-anchored child with a real height, so the fitter grows Content past the
                // viewport. The old panel instead stretch-filled Content (anchorMax y = 1), pinning
                // its height to Content rather than driving it - so Content never overflowed and the
                // Elastic ScrollRect just rubber-banded back. Give the panel its own height and
                // top-anchor it exactly like a game row, and the existing ScrollRect scrolls it - no
                // new mask, which is what blanked the panel twice before (doc §6/§7).
                var totalHeight = (float)(layout.padding.top + layout.padding.bottom);
                var rowCount = 0;
                foreach (var (label, setting, defaultColor) in Rows)
                {
                    totalHeight += AddRow(root.transform, label, setting(), defaultColor);
                    rowCount++;
                }
                totalHeight += layout.spacing * Mathf.Max(0, rowCount - 1);

                var rootRect = (RectTransform)root.transform;
                // Width stretches to Content (minus a 16px inset each side); height is fixed and the
                // panel hangs from the top edge, so the ContentSizeFitter above sizes Content to it.
                rootRect.anchorMin = new Vector2(0f, 1f);
                rootRect.anchorMax = new Vector2(1f, 1f);
                rootRect.pivot = new Vector2(0.5f, 1f);
                rootRect.sizeDelta = new Vector2(-32f, totalHeight);
                rootRect.anchoredPosition = Vector2.zero;

                // Belt and suspenders: if Content's VerticalLayoutGroup turns out to control child
                // height, honour our height through a LayoutElement too, not only the RectTransform.
                var rootElement = root.AddComponent<LayoutElement>();
                rootElement.preferredHeight = totalHeight;
                rootElement.minHeight = totalHeight;
                rootElement.flexibleHeight = 0f;

                RefreshSelection();

                // Recompute the parent Content's ContentSizeFitter and the ScrollRect now, so the
                // panel is the right height and scrollable on the very first frame rather than after
                // a layout pass.
                if (parent is RectTransform parentRect)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                }

                var rect = rootRect.rect;
                PurrtasticPalettePlugin.Log.LogInfo(
                    $"[PurrtasticPalette] Wardrobe: colour panel built - parent '{parent.name}', " +
                    $"panel rect {rect.width:F0}x{rect.height:F0}, target height {totalHeight:F0}, " +
                    $"{root.transform.childCount} row(s), {Swatches.Count} swatch(es), " +
                    $"activeInHierarchy={root.activeInHierarchy}.");
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Wardrobe: failed to build the colour panel: {e}");
            }
        }


        internal static void Destroy()
        {
            ColorPickerPopup.CloseAny();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }

            root = null;
        }


        /// <returns>The row's height, so the caller can size the scroll content deterministically.</returns>
        private static float AddRow(Transform parent, string label, ConfigEntry<string> setting, Color defaultColor)
        {
            const int columns = 5;
            const float spacing = 20f;
            const float labelHeight = 40f;
            // Extra height reserved at the bottom of each row so the selection frame (the bat
            // wings) and the checkmark of the last swatch row - which extend past the swatch cell -
            // don't collide with the next row's header.
            const float frameOverflow = 24f;

            var swatchCount = 1 + Presets.Length + 1; // default + presets + custom
            var gridRows = Mathf.CeilToInt(swatchCount / (float)columns);
            var gridHeight = gridRows * SwatchSize + (gridRows - 1) * spacing;
            var rowHeight = labelHeight + 56f + gridHeight + frameOverflow; // 56f = header-to-grid gap

            var row = new GameObject($"Row_{label}", typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var rowLayout = row.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 56f;
            // Same reason as the root group: true so the label and grid stack by their own
            // heights instead of overlapping.
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;

            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredHeight = rowHeight;
            rowElement.minHeight = rowHeight;

            AddLabel(row.transform, label, labelHeight);

            var grid = new GameObject("Swatches", typeof(RectTransform));
            grid.transform.SetParent(row.transform, false);
            var gridLayout = grid.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(SwatchSize, SwatchSize);
            gridLayout.spacing = new Vector2(spacing, spacing);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columns;
            // Centre the block of swatches in the (full-width) row rather than left-packing it.
            gridLayout.childAlignment = TextAnchor.UpperCenter;

            var gridElement = grid.AddComponent<LayoutElement>();
            gridElement.preferredHeight = gridHeight;
            gridElement.minHeight = gridHeight;

            // Default first, painted in the part's real vanilla colour.
            AddSwatch(grid.transform, "Default", string.Empty, defaultColor, setting);

            foreach (var (presetLabel, hex) in Presets)
            {
                AddSwatch(grid.transform, presetLabel, hex, ParseOr(hex, Color.magenta), setting);
            }

            AddCustomTile(grid.transform, setting);

            return rowHeight;
        }

        private static void AddLabel(Transform parent, string text, float height)
        {
            // Prefer the game's decorated header (swirl flourishes + rule); fall back to a plain
            // left-aligned label if the template cannot be found.
            var decorated = HeaderDecoration.Create(parent, text);
            if (decorated != null)
            {
                var headerElement = decorated.GetComponent<LayoutElement>() ?? decorated.AddComponent<LayoutElement>();
                headerElement.preferredHeight = height;
                headerElement.minHeight = height;
                return;
            }

            var host = new GameObject("Label", typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var label = host.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 32f;
            label.color = Palette.Label;
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            GameFonts.Apply(label, preferOutline: false);

            var element = host.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void AddSwatch(Transform parent, string name, string hex, Color fill, ConfigEntry<string> setting)
        {
            // Prefer the game's own widget (bat wings, checkmark, hover sound); fall back to a
            // drawn swatch only if the template cannot be found.
            if (CatFormSwatch.IsAvailable)
            {
                var cloned = CatFormSwatch.Create(
                    parent, $"Swatch_{name}", fill, () => IsCurrent(setting, hex),
                    () => { Apply(setting, hex); RefreshSelection(); });

                if (cloned != null)
                {
                    ClonedSwatches.Add(cloned);
                    return;
                }
            }

            var swatch = BuildSwatchShell(parent, $"Swatch_{name}", fill, out var ring, out var check);

            var trigger = swatch.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => ring.enabled = true);
            AddTrigger(trigger, EventTriggerType.PointerExit, () => ring.enabled = IsCurrent(setting, hex));
            AddTrigger(trigger, EventTriggerType.PointerClick, () =>
            {
                Apply(setting, hex);
                RefreshSelection();
            });
            swatch.AddComponent<ScrollForwarder>(); // EventTrigger eats the wheel; keep the panel scrollable

            Swatches.Add(new SwatchEntry(setting, hex, ring, check, isCustomTile: false));
        }

        private static void AddCustomTile(Transform parent, ConfigEntry<string> setting)
        {
            var fillColor = IsCustomValue(setting) ? ParseOr(setting.Value, Color.white) : (Color?)null;

            if (CatFormSwatch.IsAvailable)
            {
                var cloned = CatFormSwatch.Create(
                    parent, "Swatch_Custom", fillColor, () => IsCustomValue(setting),
                    () => OpenPicker(parent, setting));

                if (cloned != null)
                {
                    ClonedSwatches.Add(cloned);
                    // Centre the "+" on the colour plate (the visible circle), not the widget root -
                    // the root reserves extra height, so centring on it puts the "+" off-centre.
                    AddCaption(cloned.PlateTransform != null ? cloned.PlateTransform : cloned.transform, "+");
                    return;
                }
            }

            var fill = fillColor ?? new Color(0.22f, 0.18f, 0.30f, 1f);
            var swatch = BuildSwatchShell(parent, "Swatch_Custom", fill, out var ring, out var check);

            AddCaption(swatch.transform, "+");

            var trigger = swatch.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => ring.enabled = true);
            AddTrigger(trigger, EventTriggerType.PointerExit, () => ring.enabled = IsCustomValue(setting));
            AddTrigger(trigger, EventTriggerType.PointerClick, () => OpenPicker(parent, setting));
            swatch.AddComponent<ScrollForwarder>(); // EventTrigger eats the wheel; keep the panel scrollable

            Swatches.Add(new SwatchEntry(setting, null, ring, check, isCustomTile: true));
        }

        /// <summary>
        /// A swatch is three stacked circles: an accent ring, the colour face inset inside it, and
        /// a checkmark on top. The ring is a full-size circle behind a smaller face rather than an
        /// outline sprite, which keeps it to one generated texture for everything.
        /// </summary>
        private static GameObject BuildSwatchShell(Transform parent, string name, Color fill, out Image ring, out TextMeshProUGUI check)
        {
            var swatch = new GameObject(name, typeof(RectTransform));
            swatch.transform.SetParent(parent, false);

            var ringHost = new GameObject("Ring", typeof(RectTransform));
            ringHost.transform.SetParent(swatch.transform, false);
            Stretch((RectTransform)ringHost.transform, 0f);
            ring = ringHost.AddComponent<Image>();
            ring.sprite = CircleSprite.Get();
            ring.color = Palette.Accent;
            ring.raycastTarget = false;
            ring.enabled = false;

            var faceHost = new GameObject("Face", typeof(RectTransform));
            faceHost.transform.SetParent(swatch.transform, false);
            Stretch((RectTransform)faceHost.transform, 7f);
            var face = faceHost.AddComponent<Image>();
            face.sprite = CircleSprite.Get();
            face.color = fill;
            // The face is the click target; the swatch root has no graphic of its own.
            face.raycastTarget = true;

            var checkHost = new GameObject("Check", typeof(RectTransform));
            checkHost.transform.SetParent(swatch.transform, false);
            Stretch((RectTransform)checkHost.transform, 0f);
            check = checkHost.AddComponent<TextMeshProUGUI>();
            check.text = "<b>✓</b>";
            check.fontSize = 46f;
            check.color = Palette.Label;
            check.alignment = TextAlignmentOptions.Center;
            check.raycastTarget = false;
            check.enabled = false;
            GameFonts.Apply(check, preferOutline: true);

            return swatch;
        }

        private static void AddCaption(Transform parent, string text)
        {
            var host = new GameObject("Caption", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            host.transform.SetAsLastSibling();
            Stretch((RectTransform)host.transform, 0f);

            // ignoreLayout so a layout group on the cloned swatch cannot reposition it - the "+"
            // was landing at the bottom because the clone's own layout was placing this host.
            var element = host.AddComponent<LayoutElement>();
            element.ignoreLayout = true;

            var label = host.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 44f;
            label.color = Palette.Label;
            label.alignment = TextAlignmentOptions.Center;   // horizontal centre
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.raycastTarget = false;
            GameFonts.Apply(label, preferOutline: false);
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void OpenPicker(Transform context, ConfigEntry<string> setting)
        {
            var initial = ParseOr(setting.Value, Color.white);
            var canvas = context.GetComponentInParent<Canvas>();
            var pickerParent = canvas != null ? (RectTransform)canvas.transform : (RectTransform)root.transform;
            ColorPickerPopup.Open(pickerParent, initial, chosen =>
            {
                Apply(setting, "#" + ColorUtility.ToHtmlStringRGB(chosen));
                // Rebuild so the custom tile shows the colour that was picked - swatch fill is set
                // when it is created, not bound to the setting.
                if (lastParent != null)
                {
                    Build(lastParent);
                }
            });
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private static bool IsCurrent(ConfigEntry<string> setting, string hex)
        {
            var current = (setting.Value ?? string.Empty).Trim();
            return string.Equals(current, (hex ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCustomValue(ConfigEntry<string> setting)
        {
            var current = (setting.Value ?? string.Empty).Trim();
            if (current.Length == 0)
            {
                return false;
            }

            foreach (var (_, hex) in Presets)
            {
                if (string.Equals(current, hex, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static void RefreshSelection()
        {
            foreach (var entry in Swatches)
            {
                var selected = entry.IsCustomTile ? IsCustomValue(entry.Setting) : IsCurrent(entry.Setting, entry.Hex);

                if (entry.Ring != null)
                {
                    entry.Ring.enabled = selected;
                }

                if (entry.Check != null)
                {
                    entry.Check.enabled = selected;
                }
            }

            foreach (var cloned in ClonedSwatches)
            {
                if (cloned != null)
                {
                    cloned.Refresh();
                }
            }
        }

        private static void Apply(ConfigEntry<string> setting, string hex)
        {
            setting.Value = hex ?? string.Empty;
            OnColorChanged?.Invoke();
        }

        private static Color ParseOr(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return fallback;
            }

            return ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : fallback;
        }

        private readonly struct SwatchEntry
        {
            internal SwatchEntry(ConfigEntry<string> setting, string hex, Image ring, TextMeshProUGUI check, bool isCustomTile)
            {
                Setting = setting;
                Hex = hex;
                Ring = ring;
                Check = check;
                IsCustomTile = isCustomTile;
            }

            internal ConfigEntry<string> Setting { get; }
            internal string Hex { get; }
            internal Image Ring { get; }
            internal TextMeshProUGUI Check { get; }
            internal bool IsCustomTile { get; }
        }
    }
}
