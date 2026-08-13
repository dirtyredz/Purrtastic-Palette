using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using Chicken.Utilities;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PurrtasticPalette
{
    internal sealed class ProbeController : MonoBehaviour
    {
        private static readonly FieldInfo MaskColorPropertyField =
            typeof(MaskColorPropertyBlock).GetField("property", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo MaskColorColorField =
            typeof(MaskColorPropertyBlock).GetField("color", BindingFlags.NonPublic | BindingFlags.Instance);

        // Luminance-threshold guessing at the eye atlas's iris/pupil/highlight layout has hit
        // its limit (see PupilColor/EyeHighlightThreshold's commit history) - "colouring the
        // eyeball also recolours the upper half and a second pupil-like region" and "colouring
        // the pupil looks layered underneath the iris colour" both mean the real region
        // boundaries in _Atlas don't follow a clean brightness split. Exporting the actual source
        // textures to PNG is the only way to *see* the layout instead of guessing at it from
        // aggregate brightness. Only exports each distinct texture once per session.
        private static readonly HashSet<string> ExportedTextureNames = new HashSet<string>();

        // Brute-force safety net: something keeps reverting the fur (and, it turns out, the eyes
        // sometimes too) back toward the unmodified original after CatColorPatch's one-shot
        // equip hook applies correctly - confirmed by LogOutput.log showing the regenerated
        // texture being set every time, with the visible result barely (fur) or intermittently
        // (eyes) matching. Most likely the same customization-reapplication system that
        // reinstantiates a fresh material per CustomizationView.ApplyModifiers call for human
        // skin/hair (see mods/ColorProbe).
        //
        // A 1-second reapply interval made this worse in a specific way: whatever's reverting
        // the materials apparently doesn't do it on a fixed schedule, so a 1-second gap was long
        // enough for the wrong state to be visible before the next correction landed - a visible
        // "pop" on eyes, and fur spending most of its time reverted rather than correctly
        // coloured. Reapplying every frame instead closes that gap to ~16ms, which should read as
        // instant rather than flickery. Cheap: TextureRecolor caches the regenerated textures, so
        // a same-material reapply is just a couple of dictionary lookups and a SetTexture call,
        // not a re-run of the pixel regeneration.
        private void Update()
        {
            if (PurrtasticPalettePlugin.ProbeKey.Value.IsDown())
            {
                Probe();
            }

            if (PurrtasticPalettePlugin.GiveFormsKey.Value.IsDown())
            {
                GiveForms();
            }

            // includeEyes: false - eyes are applied once on equip/config-change instead (see
            // CatColorPatch.ApplyCatColors's doc comment on includeEyes for why).
            CatColorPatch.ApplyCatColors(logVerbose: false, includeEyes: false);
        }

        private void Probe()
        {
            if (!MonoBehaviourSingleton<PlayerView>.Exists)
            {
                PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] No PlayerView yet - is a save loaded?");
                return;
            }

            var bodyView = MonoBehaviourSingleton<PlayerView>.Instance.Customization.BodyView;
            if (bodyView == null)
            {
                PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Customization.BodyView is null.");
                return;
            }

            // RootSkinnedMeshRenderer is empty on at least the Cat Form body (HellkittenBodyView) -
            // that field just isn't wired up for every BodyViewAsset. Walk every renderer instead
            // of trusting that one field, so nothing under the body is missed - including whatever
            // renderer carries the eyes.
            var renderers = bodyView.GetComponentsInChildren<Renderer>(true);
            PurrtasticPalettePlugin.Log.LogInfo(
                $"[PurrtasticPalette] Current body: '{bodyView.gameObject.name}', {renderers.Length} renderer(s).");

            foreach (var renderer in renderers)
            {
                var path = GetPath(renderer.transform, bodyView.transform);

                // .materials (not sharedMaterials) auto-instances per Unity's own contract, so
                // this is safe to mutate without affecting any other renderer using the same
                // source asset.
                var materials = renderer.materials;
                // HasPropertyBlock is the whole ballgame: the game's ShaderCustomizationModifier
                // writes customization through MaterialPropertyBlocks, and a block overrides the
                // material's own values at draw time - so a renderer reporting True here is one
                // where editing the Material alone can never show up on screen.
                PurrtasticPalettePlugin.Log.LogInfo(
                    $"[PurrtasticPalette] Renderer '{path}' ({renderer.GetType().Name}), {materials.Length} material(s), " +
                    $"HasPropertyBlock={renderer.HasPropertyBlock()}.");

                for (var i = 0; i < materials.Length; i++)
                {
                    DumpAndMaybeTint(i, materials[i]);
                }

                DumpMaskColorPropertyBlock(renderer);

                if (renderer.gameObject.name == "HellKittenEyes")
                {
                    // Never let a diagnostic kill the probe: the first AcquireReadOnlyMeshData
                    // attempt threw on this non-readable mesh and aborted Probe() partway, so
                    // every renderer after the eyes silently vanished from the log.
                    try
                    {
                        DumpEyeMeshUVs(renderer);
                    }
                    catch (Exception e)
                    {
                        PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] Eye UV dump failed (non-fatal): {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// The exported _Atlas texture turned out to be a big shared swatch palette (many
        /// vertical gradient strips, likely reused across hair/eye/etc. customization elsewhere
        /// in the game), not something built for this mesh - so a single brightness threshold
        /// across the whole 4096x4096 texture can't reliably separate "this eye's pupil" from
        /// "that eye's iris" when there isn't a clean semantic split to find in the first place.
        /// The mesh's own UV data is the only way to know which specific region(s) of that huge
        /// atlas the three eyes actually sample. Clusters vertices by local X position into three
        /// buckets (the eyes sit side by side) since there's no per-vertex "this is the pupil"
        /// label preserved at runtime - a coarse but real signal, better than none.
        /// </summary>
        private static void DumpEyeMeshUVs(Renderer renderer)
        {
            var skinnedRenderer = renderer as SkinnedMeshRenderer;
            var mesh = skinnedRenderer != null ? skinnedRenderer.sharedMesh : null;
            if (mesh == null)
            {
                PurrtasticPalettePlugin.Log.LogWarning(
                    $"[PurrtasticPalette] '{renderer.gameObject.name}' has no readable sharedMesh for UV dump.");
                return;
            }

            PurrtasticPalettePlugin.Log.LogInfo(
                $"[PurrtasticPalette] Eye mesh '{mesh.name}': isReadable={mesh.isReadable} vertexCount={mesh.vertexCount}");

            // mesh.uv / mesh.vertices come back empty for this mesh (confirmed) - it doesn't
            // have "Read/Write Enabled", so the plain C# array accessors have nothing to return.
            // Mesh.AcquireReadOnlyMeshData is the API built specifically to read GPU-resident
            // mesh data without that flag (an async-readback path under the hood), which is
            // exactly this situation.
            Vector2[] uvs;
            Vector3[] positions;
            using (var dataArray = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                if (dataArray.Length == 0)
                {
                    PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] Mesh '{mesh.name}' - AcquireReadOnlyMeshData returned no data.");
                    return;
                }

                var data = dataArray[0];
                var vertexCount = data.vertexCount;
                if (vertexCount == 0)
                {
                    PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] Mesh '{mesh.name}' reports 0 vertices via AcquireReadOnlyMeshData.");
                    return;
                }

                using var nativeUvs = new NativeArray<Vector2>(vertexCount, Allocator.Temp);
                using var nativePositions = new NativeArray<Vector3>(vertexCount, Allocator.Temp);
                data.GetUVs(0, nativeUvs);
                data.GetVertices(nativePositions);
                uvs = nativeUvs.ToArray();
                positions = nativePositions.ToArray();
            }

            if (uvs.Length == 0 || positions.Length != uvs.Length)
            {
                PurrtasticPalettePlugin.Log.LogWarning(
                    $"[PurrtasticPalette] Mesh '{mesh.name}' has no usable UV0 data ({uvs.Length} uvs, {positions.Length} verts).");
                return;
            }

            // Assume the atlas export is 4096x4096 (true as of this build) purely to print a
            // convenience pixel-coordinate alongside the raw 0-1 UV bounds, so the numbers here
            // can be cross-referenced directly against the exported PNG without doing the mental
            // math by hand. If the exported atlas size ever changes, only this log line is stale.
            const int atlasSize = 4096;

            LogUvBounds("ALL", uvs, atlasSize);

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            foreach (var p in positions)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
            }

            var third = (maxX - minX) / 3f;
            var clusterUvs = new List<Vector2>[3] { new List<Vector2>(), new List<Vector2>(), new List<Vector2>() };
            for (var i = 0; i < positions.Length; i++)
            {
                var bucket = positions[i].x < minX + third ? 0 : positions[i].x < minX + 2f * third ? 1 : 2;
                clusterUvs[bucket].Add(uvs[i]);
            }

            var labels = new[] { "Left (X-)", "Center", "Right (X+)" };
            for (var i = 0; i < 3; i++)
            {
                LogUvBounds(labels[i], clusterUvs[i], atlasSize);
            }
        }

        private static void LogUvBounds(string label, IReadOnlyList<Vector2> uvs, int atlasSize)
        {
            if (uvs.Count == 0)
            {
                PurrtasticPalettePlugin.Log.LogInfo($"[PurrtasticPalette]   UV cluster '{label}': 0 verts, skipped.");
                return;
            }

            var minU = float.MaxValue;
            var minV = float.MaxValue;
            var maxU = float.MinValue;
            var maxV = float.MinValue;
            foreach (var uv in uvs)
            {
                if (uv.x < minU) minU = uv.x;
                if (uv.y < minV) minV = uv.y;
                if (uv.x > maxU) maxU = uv.x;
                if (uv.y > maxV) maxV = uv.y;
            }

            // PNG row 0 is the top of the image; Unity UV v=0 is the bottom of the texture, so
            // converting to "pixels from the top of the exported PNG" needs a flip on V.
            var pixelXMin = Mathf.RoundToInt(minU * atlasSize);
            var pixelXMax = Mathf.RoundToInt(maxU * atlasSize);
            var pixelYTop = Mathf.RoundToInt((1f - maxV) * atlasSize);
            var pixelYBottom = Mathf.RoundToInt((1f - minV) * atlasSize);

            PurrtasticPalettePlugin.Log.LogInfo(
                $"[PurrtasticPalette]   UV cluster '{label}': {uvs.Count} verts, UV U[{minU:F4},{maxU:F4}] V[{minV:F4},{maxV:F4}] " +
                $"-> atlas PNG pixels X[{pixelXMin},{pixelXMax}] Y[{pixelYTop},{pixelYBottom}] (Y measured from top of the image).");
        }

        /// <summary>
        /// MaskColorPropertyBlock is the tint mechanism <c>mods/ColorProbe</c> found used almost
        /// everywhere else in the game - a MaterialPropertyBlock override (no material instance,
        /// no texture regeneration) applied once in Start(). If the Cat Form body has one of
        /// these on a renderer, it's worth using directly instead of a Color-typed material
        /// property: unlike straight multiply-tinting a base texture (which is why Serena's
        /// Enchanted Studio's fallback path turns yellow orange - it can never brighten past
        /// the texture's own shading), a dedicated mask property is meant to be tinted freely.
        /// Reflection because both fields are private serialized fields with no public accessor.
        /// </summary>
        private static void DumpMaskColorPropertyBlock(Renderer renderer)
        {
            var block = renderer.GetComponent<MaskColorPropertyBlock>();
            if (block == null)
            {
                return;
            }

            var property = MaskColorPropertyField?.GetValue(block) as string;
            var color = MaskColorColorField != null ? (Color)MaskColorColorField.GetValue(block) : default;

            PurrtasticPalettePlugin.Log.LogInfo(
                $"[PurrtasticPalette]   MaskColorPropertyBlock found: property='{property}' color={color}");
        }

        private static string GetPath(Transform t, Transform root)
        {
            var path = t.name;
            var current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                if (current == root)
                {
                    break;
                }
                current = current.parent;
            }

            return path;
        }

        /// <summary>
        /// Debug-only "give me the forms" aid - same purpose as FormLock's removed GiveFormsKey.
        /// Grants Cat/Bat/Aqua form ownership even if unlocked nowhere on this save, by adding
        /// each form's ItemAsset straight to GameInventory.Forms (the same inventory
        /// GetInventoryForItem routes ToolUseType.Form items to), then equips Cat Form
        /// immediately via TryGrabItem so there's no need to open the tool wheel afterward.
        ///
        /// Item lookup uses Asset.GetAll&lt;ItemAsset&gt;(), the same enumeration
        /// ItemDebugScreen.LoadItemLibrary() uses for the game's own (seemingly non-functional in
        /// this build) debug item browser - ToolTypeAsset.BorrowTool, the reference the game
        /// itself uses to hand out an unowned tool, is unset for all three forms here.
        /// </summary>
        private void GiveForms()
        {
            if (!MonoBehaviourSingleton<GameInventory>.Exists)
            {
                PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] No GameInventory yet - is a save loaded?");
                return;
            }

            ItemAsset catItem = null;
            ItemAsset batItem = null;
            ItemAsset aquaItem = null;

            foreach (var item in Asset.GetAll<ItemAsset>())
            {
                if (item.ToolAddon is CatToolAsset)
                {
                    catItem = item;
                }
                else if (item.ToolAddon is BatToolAsset)
                {
                    batItem = item;
                }
                else if (item.ToolAddon is AquaToolAsset)
                {
                    aquaItem = item;
                }
            }

            var inventory = MonoBehaviourSingleton<GameInventory>.Instance;
            GrantOwnership(inventory, "Cat", catItem);
            GrantOwnership(inventory, "Bat", batItem);
            GrantOwnership(inventory, "Aqua", aquaItem);

            if (catItem == null)
            {
                PurrtasticPalettePlugin.Log.LogWarning("[PurrtasticPalette] Could not find the Cat Form item asset - not equipping.");
                return;
            }

            if (inventory.TryGrabItem(catItem))
            {
                PurrtasticPalettePlugin.Log.LogInfo($"[PurrtasticPalette] Equipped '{catItem.AssetName}'.");
                if (MonoBehaviourSingleton<PlayerView>.Exists)
                {
                    MonoBehaviourSingleton<PlayerView>.Instance.Shouter.Shout("Forms granted (debug)");
                }
            }
            else
            {
                PurrtasticPalettePlugin.Log.LogWarning(
                    $"[PurrtasticPalette] TryGrabItem failed for '{catItem.AssetName}' - already grabbed, or not in a valid position to enter Cat Form.");
            }
        }

        private static void GrantOwnership(GameInventory inventory, string label, ItemAsset item)
        {
            if (item == null)
            {
                PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] Could not find the {label} Form item asset.");
                return;
            }

            if (inventory.AddItem(new ItemEntry(item)))
            {
                PurrtasticPalettePlugin.Log.LogInfo($"[PurrtasticPalette] Granted '{item.AssetName}' ({label} Form).");
            }
            else
            {
                PurrtasticPalettePlugin.Log.LogInfo($"[PurrtasticPalette] '{item.AssetName}' ({label} Form) already owned, or could not be added.");
            }
        }

        /// <summary>
        /// Dumps every shader property, not just Color-typed ones. Added specifically to dig
        /// into the eyes' 'GradientAtlas' material (shader Game/Atlas/Atlas): its only
        /// Color-typed properties are _EmissionColor and _GrainColor, and neither reads like the
        /// iris hue by name. If iris colour is selectable at all, it's most likely a Float/Vector
        /// property that picks a region of a shared atlas texture (a palette-swap index, not a
        /// free RGB channel) - this dump is how we find out whether one exists.
        /// Only Color-typed properties are ever force-tinted; everything else is read-only here,
        /// since guessing at a Float/Vector's valid range and stomping it could visibly break
        /// the mesh instead of just recoloring it.
        /// </summary>
        /// <summary>
        /// Saves _BaseMap/_MainTex/_Atlas to PNG under BepInEx/config/PurrtasticPalette/textures/, so
        /// the actual eye-atlas and fur-texture layout can be looked at directly instead of
        /// inferred from luminance thresholds. Uses the same RenderTexture-blit read path as
        /// TextureRecolor, so it works whether or not the source asset has "Read/Write Enabled".
        /// </summary>
        private static void ExportTextureIfInteresting(Material material, string propertyName, Texture texture)
        {
            if (propertyName != "_BaseMap" && propertyName != "_MainTex" && propertyName != "_Atlas")
            {
                return;
            }

            // Prefer the pristine original CatColorPatch cached before it ever overrode this
            // slot, so pressing F7 with FurColor/EyeColor already set still exports the real
            // source art - not our own regenerated texture re-exported under a filename that
            // looks like it should be the original.
            var isOriginal = !CatColorPatch.TryGetOriginalTexture(material, propertyName, out var original);
            if (isOriginal)
            {
                original = texture;
            }

            if (original == null)
            {
                return;
            }

            var exportKey = $"{propertyName}_{original.name}_{(isOriginal ? "current" : "original")}";
            if (!ExportedTextureNames.Add(exportKey))
            {
                return; // already exported this session
            }

            try
            {
                var pixels = TextureRecolor.ReadPixelsRobust(original, original.width, original.height);
                var readable = new Texture2D(original.width, original.height, TextureFormat.RGBA32, mipChain: false);
                readable.SetPixels(pixels);
                readable.Apply();

                var dir = Path.Combine(Paths.ConfigPath, "PurrtasticPalette", "textures");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{exportKey}.png");
                File.WriteAllBytes(path, readable.EncodeToPNG());
                Destroy(readable);

                PurrtasticPalettePlugin.Log.LogInfo($"[PurrtasticPalette] Exported {propertyName} '{original.name}' -> {path}");
            }
            catch (Exception e)
            {
                PurrtasticPalettePlugin.Log.LogWarning($"[PurrtasticPalette] Failed to export {propertyName} '{original.name}': {e}");
            }
        }

        private static void DumpAndMaybeTint(int index, Material material)
        {
            if (material == null)
            {
                PurrtasticPalettePlugin.Log.LogInfo($"[PurrtasticPalette]   [{index}] Material=null");
                return;
            }

            var shader = material.shader;
            var count = shader.GetPropertyCount();

            PurrtasticPalettePlugin.Log.LogInfo(
                $"[PurrtasticPalette]   [{index}] material='{material.name}' shader='{shader.name}' ({count} shader properties)");

            for (var i = 0; i < count; i++)
            {
                var propName = shader.GetPropertyName(i);
                var propType = shader.GetPropertyType(i);

                switch (propType)
                {
                    case ShaderPropertyType.Color:
                    {
                        var before = material.GetColor(propName);
                        if (PurrtasticPalettePlugin.ForceTestColor.Value)
                        {
                            material.SetColor(propName, PurrtasticPalettePlugin.TestColor);
                            PurrtasticPalettePlugin.Log.LogInfo(
                                $"[PurrtasticPalette]     [Color] {propName}: {before} -> forced to {PurrtasticPalettePlugin.TestColor}");
                        }
                        else
                        {
                            PurrtasticPalettePlugin.Log.LogInfo($"[PurrtasticPalette]     [Color] {propName}: {before}");
                        }

                        break;
                    }
                    case ShaderPropertyType.Vector:
                        PurrtasticPalettePlugin.Log.LogInfo(
                            $"[PurrtasticPalette]     [Vector] {propName}: {material.GetVector(propName)}");
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        PurrtasticPalettePlugin.Log.LogInfo(
                            $"[PurrtasticPalette]     [Float] {propName}: {material.GetFloat(propName)}");
                        break;
                    case ShaderPropertyType.Texture:
                    {
                        var tex = material.GetTexture(propName);
                        PurrtasticPalettePlugin.Log.LogInfo(
                            $"[PurrtasticPalette]     [Texture] {propName}: {(tex != null ? tex.name : "null")}");
                        ExportTextureIfInteresting(material, propName, tex);
                        break;
                    }
                    default:
                        PurrtasticPalettePlugin.Log.LogInfo($"[PurrtasticPalette]     [{propType}] {propName}: (not read)");
                        break;
                }
            }
        }
    }
}
