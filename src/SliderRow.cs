using System;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PurrtasticPalette
{
    /// <summary>
    /// A labelled horizontal slider bound to a float config entry, with a live "x" readout. Built
    /// from plain Images (solid track + accent fill) and a circular handle rather than a cloned game
    /// widget - the wardrobe screen has no slider to clone. Its own widget, separate from the colour
    /// swatch panel (<see cref="CatFormColorPanel"/>) that hosts it.
    /// </summary>
    internal static class SliderRow
    {
        /// <param name="addLabel">The host panel's shared decorated-header builder (parent, text, height).</param>
        /// <param name="onChanged">Raised after the setting changes, so the caller can reapply the preview.</param>
        /// <returns>The row height, so the caller can size the scroll content.</returns>
        internal static float Build(
            Transform parent, string label, ConfigEntry<float> setting, float min, float max,
            Action<Transform, string, float> addLabel, Action onChanged)
        {
            const float labelHeight = 40f;
            const float gap = 44f;
            const float sliderHeight = 40f;
            const float bottomPad = 20f;
            var rowHeight = labelHeight + gap + sliderHeight + bottomPad;

            var row = new GameObject($"Row_{label}", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowLayout = row.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = gap;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = true;
            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredHeight = rowHeight;
            rowElement.minHeight = rowHeight;

            addLabel(row.transform, label, labelHeight);

            var host = new GameObject("SliderHost", typeof(RectTransform));
            host.transform.SetParent(row.transform, false);
            var hostLayout = host.AddComponent<HorizontalLayoutGroup>();
            hostLayout.childAlignment = TextAnchor.MiddleCenter;
            hostLayout.childControlWidth = true;
            hostLayout.childControlHeight = true;
            hostLayout.childForceExpandWidth = false;
            hostLayout.spacing = 24f;
            hostLayout.padding = new RectOffset(40, 40, 0, 0);
            var hostElement = host.AddComponent<LayoutElement>();
            hostElement.preferredHeight = sliderHeight;
            hostElement.minHeight = sliderHeight;

            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(host.transform, false);
            var sliderLE = sliderGo.AddComponent<LayoutElement>();
            sliderLE.preferredHeight = 22f;
            sliderLE.minHeight = 22f;
            sliderLE.flexibleWidth = 1f;

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(sliderGo.transform, false);
            ThinCenteredBar((RectTransform)track.transform);
            var trackImg = track.AddComponent<Image>();
            trackImg.color = new Color(1f, 1f, 1f, 0.16f);
            trackImg.raycastTarget = true;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            ThinCenteredBar((RectTransform)fillArea.transform);
            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.sizeDelta = new Vector2(0f, 0f);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = Palette.Accent;
            fillImg.raycastTarget = false;

            // The Slider drives the handle's anchors to full vertical stretch every frame, so the
            // handle's HEIGHT comes from the slide area, not from the handle's own size. Make the
            // slide area a short centred bar (26px, inset by the handle radius each side) so the
            // handle stretches to a 26px circle instead of a tall oval.
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var handleAreaRect = (RectTransform)handleArea.transform;
            handleAreaRect.anchorMin = new Vector2(0f, 0.5f);
            handleAreaRect.anchorMax = new Vector2(1f, 0.5f);
            handleAreaRect.pivot = new Vector2(0.5f, 0.5f);
            handleAreaRect.sizeDelta = new Vector2(-26f, 26f);
            handleAreaRect.anchoredPosition = Vector2.zero;

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = (RectTransform)handle.transform;
            handleRect.sizeDelta = new Vector2(26f, 0f); // width 26; height comes from the 26px slide area
            var handleImg = handle.AddComponent<Image>();
            handleImg.sprite = CircleSprite.Get();
            handleImg.color = Palette.Label;

            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.value = Mathf.Clamp(setting.Value, min, max);

            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(host.transform, false);
            var valueLE = valueGo.AddComponent<LayoutElement>();
            valueLE.preferredWidth = 96f;
            valueLE.minWidth = 96f;
            var valueText = valueGo.AddComponent<TextMeshProUGUI>();
            valueText.text = $"{setting.Value:0.0}x";
            valueText.fontSize = 30f;
            valueText.color = Palette.Label;
            valueText.alignment = TextAlignmentOptions.Left;
            valueText.raycastTarget = false;
            GameFonts.Apply(valueText, preferOutline: false);

            slider.onValueChanged.AddListener(v =>
            {
                setting.Value = v;
                valueText.text = $"{v:0.0}x";
                onChanged?.Invoke();
            });

            return rowHeight;
        }

        // A full-width, vertically-centred thin bar - the slider track/fill, so the slider reads as
        // a slim line with a round handle rather than a fat block filling the whole row height.
        private static void ThinCenteredBar(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 12f);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
