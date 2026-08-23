# Changelog

## 1.1.1 — 2026-08-23

Fixes a multi-second freeze when recolouring Cat Form.

- **Fixed:** a long stutter (up to ~1.7s, and more when several parts changed at once) every time
  you transformed into Cat Form, opened the Cat Form wardrobe tab, or changed the eye colour. The
  eye/whisker texture was being regenerated at its full 4096² resolution on the main thread; it's
  now recoloured at a capped resolution, so the same operation takes a few milliseconds — with no
  visible change to how the cat looks.
- Internal: assorted behaviour-neutral code cleanups; no functional change.

## 1.1.0 — 2026-08-20

Adds a **Fur Intensity** slider, and fixes a preview clash with sibling form mods.

- **Fur Intensity** — a new slider in the Cat Form wardrobe tab (also under **Colors** in Mod Nook
  / the `.cfg`). At **1.0** the fur is the full vivid recolour, as before; turn it down to fade the
  recolour back toward the original fur texture, so the coat's own shading and gradient show
  through and the colour reads less flat. Affects the **fur only** — whiskers, eyes and the sparkle
  trail are untouched.
- **Fixed:** switching to the Cat Form tab from another recolour mod's form tab in the same mirror
  (e.g. Fangtastic Palette's Bat Form) could leave both bodies on screen in the preview at once.
  The preview now hides every other form's body, not just the human's.

## 1.0.0 — 2026-08-13

First release. Recolour Cat Form — the whole cat, not just a tint.

- **Six colours, from config or the mirror:** fur, whiskers, iris, pupil, eye highlight, and the
  sparkle trail. Set them as hex (`#FF8800`) or colour names (`orange`) under **Colors** in Mod
  Nook / the `.cfg`, or pick them visually in the wardrobe.
- **Changes apply live** — no re-equip, no restart. Blank leaves a part vanilla; whiskers stay
  white by default rather than following the fur.
- **A Cat Form tab in the mirror's wardrobe**, shown only once you own Cat Form. It previews the
  cat and gives one swatch row per part — presets plus a `+` tile that opens an RGB picker.
- **Picks are a live preview:** they apply immediately as you browse but only stick if you press
  Confirm; leaving the wardrobe without confirming reverts to the colours you started with, the
  same as the game's own try-on clothing.
- **The swatches are the game's own widgets**, cloned — so they carry the real selection frame,
  applied checkmark, hover sound and decorated row titles rather than hand-drawn stand-ins.
- **The panel scrolls** using the wardrobe screen's own scroll view (mouse wheel and drag).
- **A paw tab icon**, overridable by dropping a PNG at `config/PurrtasticPalette/tab-icon.png`.
- Independent of other mirror mods; if the swatch template can't be found (e.g. a very stripped
  setup) it falls back to drawn swatches rather than failing.

Writes nothing new to your save — only its own colour settings.

### How the colour actually works

Each part is a **texture regeneration**, not an RGB multiply: every pixel takes the target hue
and saturation but keeps its own brightness, so a colour the source lacks still shows (blue onto
a red texture would otherwise stay black). The eye is split into iris / pupil / highlight by
saturation and brightness thresholds because the region is baked into unreadable mesh UVs. Writes
go through a `MaterialPropertyBlock`, the same way the game applies its own customization, or they
never render.

### Folded in from development

Kept because the reasoning is worth having, per
[12-versioning-and-release.md](../../12-versioning-and-release.md) — none of these were published.

- **The mod began as a diagnostic named CatColorProbe**, answering "is the form's colour a shader
  property or baked into a texture?". Renamed to Purrtastic Palette for release (the cat entry in
  a planned set with Fangtastic and Fintastic Palette); the source directory keeps the old name.
- **Emission was a dead end.** The body is URP/Lit and its emission variant is stripped from the
  build, so `EnableKeyword("_EMISSION")` silently no-ops. Built, tested, removed.
- **Scrolling took three tries.** Two attempts at a fresh `ScrollRect` + `RectMask2D` blanked the
  whole panel (a zero-height content behind a mask clips everything, invisibly). The fix was to
  stop building a mask and feed the panel into the screen's *own* scroll view, sized like a real
  category row.
- **A wheel-scroll regression, then its cause:** `EventTrigger` implements `IScrollHandler` and
  swallows the mouse wheel, so hovering a swatch stopped the panel scrolling; a small forwarder
  re-sends the wheel to the ScrollRect.
- **Keyboard/gamepad (WASD) navigation between swatches was attempted and shelved.** The wardrobe
  screen's hover-based selection keeps nulling a keyboard cursor; mouse works fully. Left as a
  future task, written up in the design notes.
