using UnityEngine;

namespace PurrtasticPalette
{
    /// <summary>
    /// Reapplies the cat's colours every frame. Something in the game keeps reverting the fur (and
    /// sometimes the eyes) back toward the unmodified original after <see cref="CatColorPatch"/>'s
    /// one-shot equip hook applies correctly - most likely the same customization-reapplication
    /// system that reinstantiates a fresh material per <c>CustomizationView.ApplyModifiers</c> call
    /// for human skin/hair.
    ///
    /// A 1-second reapply interval made this visibly worse: whatever reverts the materials does not
    /// run on a fixed schedule, so a 1-second gap was long enough for the wrong state to show - a
    /// "pop" on the eyes, and fur spending most of its time reverted. Reapplying every frame closes
    /// that gap to ~16ms, which reads as instant. It is cheap: <see cref="TextureRecolor"/> caches
    /// the regenerated textures, so a same-material reapply is a couple of dictionary lookups and a
    /// SetTexture call, not a re-run of the pixel regeneration.
    /// </summary>
    internal sealed class CatColorReapplier : MonoBehaviour
    {
        private void Update()
        {
            // includeEyes: false - the eyes are applied once on equip / config-change instead. They
            // got visibly worse behind the every-frame loop (washed out, and clearing PupilColor
            // sometimes took EyeColor with it) - the signature of fighting another writer at matched
            // frequency rather than winning. See CatColorPatch.ApplyCatColors's includeEyes note.
            CatColorPatch.ApplyCatColors(logVerbose: false, includeEyes: false);
        }
    }
}
