# Purrtastic Palette

Recolours Cat Form: body, whiskers, iris, pupil, eye highlight, and the sparkle trail. Two ways
to set the colours - config values, and a **Cat Form tab in the mirror's wardrobe** with a live
preview and swatch pickers.

**Status:** 🚀 **Published** — v1.1.0 live on Nexus as
[mod 142](https://www.nexusmods.com/moonlightpeaks/mods/142).

Config settings live under **Colors** in Mod Nook (or
`BepInEx/config/com.dirtyredz.moonlightpeaks.purrtasticpalette.cfg`). Every value is a hex code
(`#FF8800`) or an HTML colour name (`orange`); blank means "leave vanilla". Changes apply
immediately — no re-equip, no restart.

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

## The wardrobe tab (phase 2)

Interact with the mirror the game places in the player's house, choose "Change clothes", and a
**Cat Form** tab appears at the end of the tab strip. Selecting it shows the cat in the preview
(the same rig the character creator uses) with a swatch panel - one row per colourable part,
preset colours plus a "+" tile that opens an RGB picker. Picking a swatch writes straight to the
config entry, so the wardrobe, the `.cfg`, and Mod Nook can never disagree. The swatches and row
headers are clones of the game's own customization widgets, so they carry the real selection
frame, checkmark, hover sound and decorated titles rather than hand-drawn approximations.

The panel scrolls: it feeds itself into the wardrobe screen's own `ScrollRect` (mouse wheel and
drag) rather than building a clip mask, which blanked the whole panel twice. Known gap: keyboard
(WASD) / gamepad navigation between swatches is not wired - the screen's hover-based selection model
keeps nulling a keyboard cursor, so that was shelved. Mouse works fully. See the design notes at the
repo root ([17-wardrobe-ui.md](../../17-wardrobe-ui.md)).

A swatch pick is a live **preview**: it applies immediately but only sticks if you press Confirm -
closing the wardrobe without confirming reverts to the colours from before you opened it, the same
as the game's own try-on clothing.

The **Cat Form** tab shows a paw-print icon. Drop a PNG at
`BepInEx/config/PurrtasticPalette/tab-icon.png` to override it (transparent, square, ~256px, art kept
within a centred circle since the tab is a diamond); without one, a generated paw glyph is used.

## Status

Working and confirmed in-game: all six colours, and the wardrobe tab (preview, swatches, picker,
decorated headers, live-preview with revert-on-cancel, paw tab icon, ownership-gated, stable across
leaving and re-entering the mirror). Prepared for a **1.0.0** release: renamed from its
diagnostic-era name `CatColorProbe` to **Purrtastic Palette** (plugin GUID
`com.dirtyredz.moonlightpeaks.purrtasticpalette`; the source directory is `mods/PurrtasticPalette`),
dev tools stripped, packaging in `pack.ps1` / `RELEASING.md`. The cat entry in a themed set with the
planned Fangtastic Palette (bat) and Fintastic Palette (mermaid) mods. Nexus description and
screenshots still to do.

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

**Something reverts the fur.** Colours are reapplied every frame from `CatColorReapplier` for
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

## History

Grew out of a diagnostic ("is the form's colour a shader property or baked into a texture?"),
which is where the old `CatColorProbe` name came from. The developer probe (an F7 renderer/material
dump, texture export, and a form-grant key) drove every finding above and was **removed for the
1.0.0 release**; it lives in git history if the sibling bat/mermaid mods need it again.

## Reusable findings

The parts that apply to any Moonlight Peaks mod touching character colour are written up at the
repo root: [16-recolouring-characters.md](../../16-recolouring-characters.md).
