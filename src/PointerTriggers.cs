using System;
using UnityEngine.EventSystems;

namespace PurrtasticPalette
{
    /// <summary>
    /// Attaches a pointer callback to an <see cref="EventTrigger"/>. Both the drawn swatch shells
    /// (<see cref="CatFormColorPanel"/>) and the cloned game swatch (<see cref="CatFormSwatch"/>)
    /// wire hover/click this way; this is their single shared helper.
    /// </summary>
    internal static class PointerTriggers
    {
        internal static void Add(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }
    }
}
