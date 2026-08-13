using UnityEngine;

namespace CatColorProbe
{
    /// <summary>The game's window palette, for the few elements we draw ourselves.</summary>
    internal static class Palette
    {
        internal static readonly Color Label = new Color32(0xF7, 0xD9, 0x94, 0xFF);

        /// <summary>The salmon/coral the game rings a selected swatch with.</summary>
        internal static readonly Color Accent = new Color32(0xE8, 0x7D, 0x6B, 0xFF);
    }
}
