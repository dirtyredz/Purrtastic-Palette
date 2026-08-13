using System;
using System.Collections.Generic;
using Chicken.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PurrtasticPalette
{
    /// <summary>
    /// Trimmed adaptation of mods/ModNook/src/ColorPicker.cs: same dialog shape (dim overlay,
    /// rounded plate, preview swatch, RGB sliders built from the game's own SliderButton), minus
    /// the palette-from-ColorLibrary section and all BepInEx ConfigEntry plumbing - this edits a
    /// plain Color and hands it back through a callback, nothing here knows about config files.
    /// </summary>
    internal sealed class ColorPickerPopup : MonoBehaviour
    {
        private static ColorPickerPopup open;

        /// <summary>True while the dialog is up.</summary>
        internal static bool IsOpen => open != null;

        private Action<Color> onChosen;
        private GameObject root;
        private Image preview;
        private TextMeshProUGUI hexText;
        private Color current = Color.white;
        private readonly List<SliderButton> channels = new List<SliderButton>();

        internal static void Open(RectTransform parent, Color initial, Action<Color> onChosen)
        {
            CloseAny();

            var host = new GameObject("PurrtasticPalette_ColorPicker", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            host.transform.SetAsLastSibling();

            var picker = host.AddComponent<ColorPickerPopup>();
            picker.current = initial;
            picker.onChosen = onChosen;

            // Registered before it is built, not after - a dialog assigned only on success is one
            // CloseAny cannot reach if building throws partway through.
            open = picker;
            picker.Build(host);
        }

        internal static void CloseAny()
        {
            if (open != null)
            {
                open.Close();
            }
            open = null;
        }

        private void Step(string name, Action build)
        {
            try
            {
                build();
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogError($"[PurrtasticPalette] Colour picker '{name}' failed: {e}");
            }
        }

        private void Build(GameObject host)
        {
            root = host;

            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;

            var dim = host.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.01f, 0.04f, 0.8f);
            dim.raycastTarget = true;

            var panel = new GameObject(
                "Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(host.transform, false);

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700f, 0f);

            var plate = panel.GetComponent<Image>();
            plate.sprite = PanelSprite.Get();
            plate.type = Image.Type.Sliced;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(44, 44, 32, 32);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Step("title", () => Text(panel.transform, "Custom Colour", 34f, Palette.Label));
            Step("preview", () => BuildPreview(panel.transform));
            Step("sliders", () => BuildSliders(panel.transform));
            Step("buttons", () => BuildButtons(panel.transform));

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        }

        private void BuildPreview(Transform parent)
        {
            var row = new GameObject("Preview", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;

            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 76f;
            element.minHeight = 76f;

            var swatch = new GameObject(
                "Swatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            swatch.transform.SetParent(row.transform, false);

            preview = swatch.GetComponent<Image>();
            preview.sprite = PanelSprite.Plain();
            preview.type = Image.Type.Sliced;
            preview.color = current;

            var swatchElement = swatch.AddComponent<LayoutElement>();
            swatchElement.preferredWidth = 120f;
            swatchElement.minWidth = 120f;
            swatchElement.flexibleWidth = 0f;

            hexText = Text(row.transform, Hex(), 30f, Palette.Label);
            hexText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private void BuildSliders(Transform parent)
        {
            var template = Templates.Slider;
            if (template == null)
            {
                PurrtasticPalettePlugin.Log.LogWarning(
                    "[PurrtasticPalette] No SliderButton template found - colour picker has no sliders.");
                return;
            }

            channels.Clear();
            AddChannel(parent, template, "Red", 0);
            AddChannel(parent, template, "Green", 1);
            AddChannel(parent, template, "Blue", 2);
        }

        private void AddChannel(Transform parent, SliderButton template, string label, int index)
        {
            var row = Templates.Clone(template, parent, $"Channel_{label}");
            Templates.SetValueWidgetLabel(row, label);

            row.Setup(
                new SliderButton.Settings
                {
                    MinValue = 0f,
                    MaxValue = 255f,
                    SliderStep = 1f,
                    ButtonStep = 1f,
                    ShowValueAsPercentage = false,
                    // SliderButton.HandleSliderValueChanged dereferences this unconditionally
                    // once the slider hits MaxValue - null throws there, which happens inside
                    // Setup() itself (before OnValueChanged is even wired up), aborting the rest
                    // of BuildSliders. An empty (non-null) instance reports HasText = false and
                    // is otherwise inert. Every channel can reach 255 in normal use, not just a
                    // white seed colour, so this isn't optional.
                    MaxValueTextOverride = new SinglelineLocalizedText(),
                },
                Channel(index));

            row.OnValueChanged.AddListener(value =>
            {
                var amount = Mathf.Clamp01(value / 255f);
                switch (index)
                {
                    case 0: current.r = amount; break;
                    case 1: current.g = amount; break;
                    default: current.b = amount; break;
                }
                Refresh();
            });

            channels.Add(row);
        }

        private float Channel(int index)
        {
            var amount = index == 0 ? current.r : index == 1 ? current.g : current.b;
            return Mathf.Round(amount * 255f);
        }

        private void Refresh()
        {
            preview.color = current;
            hexText.text = Hex();
        }

        private string Hex() => "#" + ColorUtility.ToHtmlStringRGB(current);

        private void BuildButtons(Transform parent)
        {
            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 72f;
            element.minHeight = 72f;

            PlainButton(row.transform, "Save", Save);
            PlainButton(row.transform, "Cancel", Close);
        }

        /// <summary>Hand-drawn rather than cloned - this popup has no natural button template of its own to borrow.</summary>
        private static void PlainButton(Transform parent, string label, Action onClick)
        {
            var host = new GameObject(
                label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            host.transform.SetParent(parent, false);

            var image = host.GetComponent<Image>();
            image.sprite = PanelSprite.Get();
            image.type = Image.Type.Sliced;

            var button = host.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());

            var text = Text(host.transform, label, 28f, Palette.Label);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Escape always leaves, whatever state the rest of the dialog is in.</summary>
        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void Save()
        {
            onChosen?.Invoke(current);
            Close();
        }

        private void Close()
        {
            if (open == this)
            {
                open = null;
            }
            DestroyImmediate(root);
        }

        private static TextMeshProUGUI Text(Transform parent, string content, float size, Color colour)
        {
            var host = new GameObject("Text", typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var text = host.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = TextAlignmentOptions.Center;
            text.color = colour;
            text.fontSize = size;
            text.enableWordWrapping = true;
            GameFonts.Apply(text, preferOutline: false);

            return text;
        }
    }
}
