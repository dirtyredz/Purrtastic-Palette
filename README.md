# Cat Color Probe

Recolours Cat Form: body, whiskers, iris, pupil, eye highlight, and the sparkle trail.

Settings live under **Colors** in Mod Nook (or `BepInEx/config/com.dirtyredz.moonlightpeaks.catcolorprobe.cfg`).
Every value is a hex code (`#FF8800`) or an HTML colour name (`orange`); blank means "leave
vanilla". Changes apply immediately — no re-equip, no restart.

| Setting | Colours | Vanilla |
|---|---|---|
| `FurColor` | the body | black |
| `WhiskerColor` | the whiskers | white |
| `EyeColor` | the iris | red |
| `PupilColor` | the dark centre of the eye | black |
| `EyeHighlightColor` | the small bright glint | white |
| `AuraColor` | the trail while running | pink/purple |

`WhiskerColor` deliberately does **not** follow `FurColor` — blank leaves the whiskers vanilla
white rather than matching the body.

## Status

Working and confirmed in-game for all six colours. Not published. Still named `CatColorProbe`
after the diagnostic it started as; a rename means changing the plugin GUID and the config path,
so it should happen before any release, not after.

## How it works

Cat Form is not a recoloured version of the player — equipping it swaps the entire body for a
different prefab (`HellkittenBodyView`) via `EntityCustomization.SetBodyView`. Colours are applied
by a Harmony postfix on `FormToolView<CatToolAsset>.HandleEnterVisuallySwitchedBodyViews`, which is
the first moment the new body is fully loaded and active.

Everything is a **texture regeneration**, not a tint. Each source texture is read, recoloured
pixel by pixel, and the result assigned back:

- **HSV colorize** — each pixel takes the target colour's hue and saturation but keeps its own
  brightness. An RGB multiply cannot introduce a channel the source lacks (blue onto a red
  texture stays black) and turns dark regions to mud.
- **Reading is done through a `RenderTexture` blit + `ReadPixels`**, so it works on textures
  without "Read/Write Enabled" — which is all of the game's shipped textures.
- **Results are cached** by (source texture, colour, thresholds), so the per-frame reapply is a
  dictionary lookup rather than a 4096×4096 rebuild.

### The four material targets

| Part | Renderer | Material | Texture property |
|---|---|---|---|
| Body | `HellKitten/HellKitten` | `HellKitten01` (URP/Lit) | `_BaseMap` **and** `_MainTex` |
| Whiskers | same renderer, second material | `GradientAtlas` (`Game/Atlas/Atlas`) | `_Atlas` |
| Eyes | `HellKittenEyes` | `GradientAtlas` | `_Atlas` |
| Trail / sparkles | `VFX/Trail`, `VFX/Aura` | `VFXKittenTrail`, `ParticleFireFlies` | colour properties, no texture |

Body and whiskers being separate materials on one mesh is why they can be coloured independently
with no masking at all.

`HellKitten01` carries **two** albedo slots — `_BaseMap`/`_BaseColor` (URP) and `_MainTex`/`_Color`
(Standard-shader leftovers from a converted material; `_WorkflowMode` and `_Glossiness` are the
other tells). Both are written, because recolouring only one leaves the other showing the
original.

### Splitting the eye into three regions

The eye's `_Atlas` is a shared palette used across the game, not eye-specific art: a grid of
narrow vertical strips, each strip one swatch colour running dark to light. Which strip a pixel
belongs to is baked into the mesh UVs, which are unreadable at runtime.

So the three regions are separated by colour instead:

1. **Saturation** splits iris (saturated) from the rest. Brightness cannot do this — it varies
   *within* each strip, so a brightness split cuts the eye into a top and bottom half.
2. **Brightness** then splits the remainder into pupil (dark) and highlight (bright). Saturation
   cannot do this — white and black are both fully desaturated.

Both thresholds are constants in `CatColorPatch.cs`, not settings: they describe this character's
art, and the values in between only reproduce bugs.

The pupil is applied at the chosen colour's own full brightness rather than scaled by the pupil's
original darkness, which would leave it black whatever was picked.

## Gotchas worth knowing

**Writes must go through `MaterialPropertyBlock`.** The game applies customization via
`ShaderCustomizationModifier.Apply()`, which sets a property block on these renderers — and a
property block overrides the material's own values at draw time. Every `material.SetTexture`
call was stored correctly and never rendered. This cost the longest stretch of the work: the log
said "applied" every frame while nothing changed on screen. Property blocks are read-modify-written
per material index, the same way the game does it, so anything the game set is preserved.

**Something reverts the fur.** Colours are reapplied every frame from `ProbeController.Update` for
this reason. The eyes are deliberately *excluded* from that loop — they showed no reversion, and
putting them in it made them flicker and wash out, the signature of fighting another writer at
matched frequency rather than winning.

**Emission is a dead end.** The body is URP/Lit, so its albedo is multiplied by the (very dark,
night-time) scene lighting. Emission would bypass that, but the material ships with
`_EmissionColor` black and `_EmissionMap` unset, so Unity has stripped the `_EMISSION` shader
variant from the build — `EnableKeyword` silently no-ops. This was built, tested, and removed;
don't rebuild it without new evidence.

**The body albedo is near-black by design.** It is recoloured at full brightness (the internal
brightness floor is 1.0). That looks correct rather than flat because the body's shading comes
from real-time lighting and its normal map, not from the texture.

## The probe

`ProbeKey` (default F7) is a developer tool, kept because it is how every finding above was made:

- dumps every renderer under the current form body, its materials, and **all** shader properties
  with values — not just colours
- reports `HasPropertyBlock` per renderer, which is what identified the property-block problem
- exports source textures to `BepInEx/config/CatColorProbe/textures/` as PNG, using the same
  read path as the recolouring so non-readable textures work
- `ForceTestColor` additionally forces every colour property to magenta, to see which ones are
  actually wired to anything

`GiveFormsKey` (default Home) grants all three forms on a save that has not unlocked them.

The eye-mesh UV dump is the one part that does not work: the mesh is not readable, and
`Mesh.AcquireReadOnlyMeshData` throws on it too. It is wrapped in a try/catch because when it
first threw, it aborted the whole probe and silently truncated the log. Solving it would allow
UV-based region masking instead of colour-based.

## Reusable findings

The parts that apply to any Moonlight Peaks mod touching character colour are written up at the
repo root: [16-recolouring-characters.md](../../16-recolouring-characters.md).
