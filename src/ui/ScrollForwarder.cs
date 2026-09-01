using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PurrtasticPalette
{
    /// <summary>
    /// Forwards mouse-wheel scroll events up to the nearest parent ScrollRect.
    ///
    /// The swatches use EventTrigger for hover/click, and EventTrigger implements IScrollHandler -
    /// so it swallows the wheel event, and hovering a swatch (which is most of the panel) stops the
    /// wardrobe's own ScrollRect from scrolling. Adding this alongside the EventTrigger re-forwards
    /// the wheel to the ScrollRect so the panel scrolls even while the cursor is over a swatch.
    /// </summary>
    internal sealed class ScrollForwarder : MonoBehaviour, IScrollHandler
    {
        private ScrollRect scrollRect;

        public void OnScroll(PointerEventData eventData)
        {
            if (scrollRect == null)
            {
                scrollRect = GetComponentInParent<ScrollRect>();
            }

            if (scrollRect != null)
            {
                scrollRect.OnScroll(eventData);
            }
        }
    }
}
