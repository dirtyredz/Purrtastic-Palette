# DECISIONS — Purrtastic Palette

Design/architecture decisions and their rationale, newest first. The pixel-level findings behind
several of these are written up at length in [../README.md](../README.md); this file is the
short "why we chose X over Y" index.

## ADR-009 — Recolour engine duplicated per mod, shared only as docs
Each *Palette* mod (Purr/Fang/Fin) is a **standalone git repo** that must build and ship alone, so
`TextureRecolor` and the recolour methodology are copied per repo, not extracted to a shared library.
The knowledge is shared as workspace docs ([../../16-recolouring-characters.md](../../16-recolouring-characters.md)).
**Rejected:** a shared assembly — it would couple release cycles and break standalone builds.

## ADR-008 — Draw our own widgets, but clone the game's when it carries real art
Swatches, headers, and the slider prefer a **clone** of the game's own widget (real selection frame,
checkmark, hover sound, decorated title) and fall back to **generated sprites** only when no template
is found. **Rejected:** cloning `CustomizationOptionListWidget` wholesale — stripped of its
`ItemAsset` it lost its plate and hover, worse than a drawn circle. So `CatFormSwatch` drives the
clone's pieces directly and the drawn shell is the fallback.

## ADR-007 — Wardrobe picks are a live preview with revert-on-cancel
A swatch/slider pick writes the `ConfigEntry` immediately (so the world cat updates too) but is only
kept if the player presses **Confirm**; closing without confirming restores the colour snapshot taken
on open — matching the game's own try-on-clothing behaviour.

## ADR-006 — Writes go through MaterialPropertyBlock, not the Material
The game's `ShaderCustomizationModifier.Apply()` sets a property block on these renderers, and a
block overrides the material's own values at draw time. Every `material.SetTexture` was stored and
never rendered. All writes are read-modify-written per material index through the block, as the game
does. **This was the single longest bug in the project.**

## ADR-005 — Reapply every frame; exclude the eyes
Something reverts the fur toward vanilla after a one-shot apply, so `CatColorReapplier` re-applies
every frame (~16 ms; cheap because `TextureRecolor` caches). A 1-second interval was visibly worse.
**Eyes are excluded** — under the loop they washed out and clearing `PupilColor` dragged `EyeColor`
with it, the signature of fighting another writer at matched frequency. Eyes are single-shot
(equip + config-change) instead.

## ADR-004 — Recolour thresholds are constants, not config
Eye saturation/value split points and the fur/eye brightness floors are compile-time constants in
`CatColorPatch`. They describe *this character's art*, not user taste — every one has exactly one
value that works; the values in between only reproduce known bugs.

## ADR-003 — HSV colorize, not RGB multiply-tint
Each pixel takes the target's hue+saturation and keeps its own brightness. An RGB multiply can't add
a channel the source lacks (blue on orange fur stays black) and turns dark regions to mud. HSV
colorize reaches any colour regardless of the source's channel makeup. Read via a `RenderTexture`
blit + `ReadPixels` so it works on textures without Read/Write Enabled (all shipped textures).

## ADR-002 — No standalone config UI; ride Mod Nook
Colours are plain `ConfigEntry<string>` hex/name fields. Mod Nook already renders BepInEx config as
a menu, so the fields double as UI; the wardrobe tab is the richer optional path. `WhiskerColor`
deliberately does **not** follow `FurColor` (blank = vanilla white).

## ADR-001 — Emission is a dead end
The body is URP/Lit; Unity stripped the `_EMISSION` variant from the build (black `_EmissionColor`,
unset `_EmissionMap`), so `EnableKeyword` silently no-ops. Built, tested, removed — don't rebuild
without new evidence.
