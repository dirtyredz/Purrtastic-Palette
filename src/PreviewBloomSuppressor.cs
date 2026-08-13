using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CatColorProbe
{
    /// <summary>
    /// Turns post-processing bloom off while the Cat Form tab is open, and back on when it isn't.
    ///
    /// The cat's whiskers (and eyes) use the Game/Atlas/Atlas shader, which is effectively unlit -
    /// it renders at full colour regardless of scene lighting, so those parts are always much
    /// brighter than the lit body. Bloom then smears that brightness outward, which reads as a
    /// glow and makes the actual picked colour hard to judge. Hiding the particle VFX did nothing
    /// for this because it was never a particle effect.
    ///
    /// Works by adding a high-priority global Volume with a bloom override of zero, rather than
    /// editing the scene's existing bloom settings. VolumeProfiles are shared assets: switching
    /// their bloom off would mutate the asset in memory and disable bloom for the whole game until
    /// restart, and would stay off if the game exited while the menu was open. Adding and then
    /// destroying our own Volume cannot leave that kind of mess behind.
    /// </summary>
    internal static class PreviewBloomSuppressor
    {
        private static GameObject host;

        internal static void Suppress()
        {
            if (host != null)
            {
                return;
            }

            try
            {
                host = new GameObject("CatColorProbe_BloomSuppressor");
                UnityEngine.Object.DontDestroyOnLoad(host);

                var volume = host.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1000f; // above anything the game is likely to use
                volume.weight = 1f;

                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "CatColorProbe_NoBloom";
                volume.profile = profile;

                var bloom = profile.Add<Bloom>(overrides: true);
                bloom.active = true;
                bloom.intensity.overrideState = true;
                bloom.intensity.value = 0f;

                CatColorProbePlugin.Log.LogInfo("[CatColorProbe] Wardrobe: bloom suppressed for the preview.");
            }
            catch (Exception e)
            {
                CatColorProbePlugin.Log.LogError($"[CatColorProbe] Wardrobe: failed to suppress bloom: {e}");
                Restore();
            }
        }

        internal static void Restore()
        {
            if (host == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(host);
            host = null;
            CatColorProbePlugin.Log.LogInfo("[CatColorProbe] Wardrobe: bloom restored.");
        }
    }
}
