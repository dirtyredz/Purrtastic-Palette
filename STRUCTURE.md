# STRUCTURE — Purrtastic Palette

Where things live in the code, and where the structural debt is. A map, not documentation — for how
the recolour works and why, read [README.md](README.md) and
[docs/DECISIONS.md](docs/DECISIONS.md); for the system flow, [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

**Last full review: 2026-08-22**

## What this mod is

A BepInEx 5 / HarmonyX plugin for the Unity Mono game *Moonlight Peaks* that recolours **Cat Form**
(fur, whiskers, iris, pupil, eye highlight, movement trail) two ways: BepInEx config entries (which
Mod Nook renders as a menu) and a **Cat Form tab injected into the mirror's wardrobe** with a live
preview, swatch pickers, and an RGB colour picker. netstandard2.1; plugin `.cs` sit flat in `src/`.

## The three layers

The code splits into three concerns. **Call direction is downward only** — a grep confirms the UI
files never appear inside the engine or the patch classes. Two nuances the review corrected:

- **`Plugin` is a cross-cutting config foundation, not just the top of the stack.** Its
  `ConfigEntry<T>` statics (the whole colour surface) are read directly by all three layers — 10 of
  18 files reference `PurrtasticPalettePlugin.*`. That's a shared foundation everyone reads from, not
  a layering violation, but it means the mod has no separate "config model" type; the statics *are* it.
- **The UI reaches the engine only through the Patches layer's wiring, never directly.**
  `CatFormColorPanel` exposes an `OnColorChanged` delegate; `CatFormWardrobe` wires it to
  `CatColorPatch.ApplyToBody`. The panel references neither `CatColorPatch` nor `TextureRecolor` — a
  cleaner inversion than a bare downward call.

```
  Plugin (config statics) ───────── read by all three layers ─────────┐
                                                                       │
  Patches / bootstrap      Plugin(Awake) · CatColorPatch · CatColorReapplier · CatFormWardrobe(patch)
        │  drives                                    ▲
        ▼                                            │ OnColorChanged delegate, wired by the patch layer
  Recolour engine          TextureRecolor · ColorParsing · (property-block writing still inside
        ▲                   CatColorPatch)
        │  reads colours from
  Wardrobe UI (view)       CatFormColorPanel · CatFormSwatch · ColorPickerPopup · Templates · SliderRow
                           + wardrobe orchestration (driven by the CatFormWardrobe host):
                             CatPreviewController (preview body) · WardrobePanelSwap (row/panel swap)
                           + widget locate: GameTemplate (shared by Templates/CatFormSwatch/HeaderDecoration)
                           + drawing helpers: CircleSprite · PanelSprite · PawSprite · TabIcon
                           · HeaderDecoration · GameFonts · ScrollForwarder · PreviewBloomSuppressor
                           · Palette (UI colours)
```

## File-by-file (25 files, ~3.6k lines)

### Bootstrap
| File | Lines | Responsibility |
|---|---|---|
| [src/Plugin.cs](src/Plugin.cs) | 136 | BepInEx entry: binds the `Colors` config entries, wires `SettingChanged`→reapply, attaches `CatColorReapplier`, `PatchAll`. Single source of the config surface. |

### Recolour engine (the "model")
| File | Lines | Responsibility |
|---|---|---|
| [src/TextureRecolor.cs](src/TextureRecolor.cs) | 231 | Pure HSV-colorize texture regeneration + cache. **No game/UI deps — the reuse surface** shared in spirit with the sibling *Palette* mods. |
| [src/CatColorPatch.cs](src/CatColorPatch.cs) | 489 | The equip-time Harmony patch **and** the apply logic: renderer traversal/dispatch, fur/whisker/eye/aura strategies, MaterialPropertyBlock writing, original-value caches. Still the largest engine file — see Structural debt for the remaining strategy seam. |
| [src/ColorParsing.cs](src/ColorParsing.cs) | 42 | Hex/name → `Color?` parsing, shared by the fur/eye/aura paths. Extracted from `CatColorPatch`. |
| [src/CatColorReapplier.cs](src/CatColorReapplier.cs) | 30 | Per-frame `Update()` safety net that re-applies fur (not eyes). One job. |

### Wardrobe UI (the "view")
| File | Lines | Responsibility |
|---|---|---|
| [src/CatFormWardrobe.cs](src/CatFormWardrobe.cs) | 173 | **Thin Harmony host** for 3 wardrobe-screen hooks (OnShow/HandleTabSelected/OnHide): ownership gate + tab injection, then wires the lifecycle to `CatPreviewController`, `WardrobePanelSwap`, and `PreviewColorSession`. Holds no rig/panel state. |
| [src/CatPreviewController.cs](src/CatPreviewController.cs) | 193 | Owns the preview rig's cat body: instantiate/swap alongside the human body, mute in-preview VFX, suppress bloom, recolour. Extracted from `CatFormWardrobe`. |
| [src/WardrobePanelSwap.cs](src/WardrobePanelSwap.cs) | 123 | Swaps the native category rows for our colour panel and back (hide/restore rows, build/destroy panel). Extracted from `CatFormWardrobe`. |
| [src/CatFormColorPanel.cs](src/CatFormColorPanel.cs) | 492 | Builds the swatch panel: rows/presets, layout math, selection state, and the drawn-swatch fallback shell. Hosts a `SliderRow` for Fur Intensity. |
| [src/SliderRow.cs](src/SliderRow.cs) | 149 | A labelled float slider widget (track/fill/handle), built for the Fur-Intensity row. Extracted from `CatFormColorPanel`. |
| [src/PreviewColorSession.cs](src/PreviewColorSession.cs) | 86 | The try-on transaction: snapshot colours on open, revert on close-without-Confirm. Pure `ConfigEntry` state. Extracted from `CatFormWardrobe`. |
| [src/CatFormSwatch.cs](src/CatFormSwatch.cs) | 210 | One swatch cloned from the game's `CustomizationOptionListWidget` (real frame/checkmark/hover sound). Template locate via `GameTemplate`. |
| [src/ColorPickerPopup.cs](src/ColorPickerPopup.cs) | 303 | The RGB picker dialog (trimmed port of ModNook's `ColorPicker`). |
| [src/Templates.cs](src/Templates.cs) | 246 | Game-widget cloning utility (stage inactive → strip localization/wings → place). Ported from ModNook; the locate step now delegates to `GameTemplate`. |
| [src/GameTemplate.cs](src/GameTemplate.cs) | 110 | Shared widget-locate: enumerate all instances (incl. inactive/prefabs), prefer a scene-valid one, try/catch + log. One `Find<T>` with per-caller predicate/tie-break policy; used by `Templates`, `CatFormSwatch`, `HeaderDecoration`. |

### Drawing / asset helpers (small, single-responsibility)
| File | Lines | Responsibility |
|---|---|---|
| [src/PanelSprite.cs](src/PanelSprite.cs) | 132 | Generated 9-sliced plum/gold plate (+ plain white variant). Ported from ModNook. |
| [src/GameFonts.cs](src/GameFonts.cs) | 123 | Locates the game's Gelica font + outline preset. Ported from LastSwing. |
| [src/PawSprite.cs](src/PawSprite.cs) | 117 | Generated paw-print glyph for the tab icon. |
| [src/HeaderDecoration.cs](src/HeaderDecoration.cs) | 113 | Clones the game's decorated category header. Template locate via `GameTemplate`. |
| [src/PreviewBloomSuppressor.cs](src/PreviewBloomSuppressor.cs) | 74 | Adds/removes a global zero-bloom Volume while the tab is open. |
| [src/TabIcon.cs](src/TabIcon.cs) | 70 | Loads a user PNG override, else falls back to `PawSprite`. |
| [src/CircleSprite.cs](src/CircleSprite.cs) | 54 | Generated white circle sprite (swatch faces, slider handle). |
| [src/ScrollForwarder.cs](src/ScrollForwarder.cs) | 32 | Forwards mouse-wheel from a swatch's EventTrigger up to the ScrollRect. |
| [src/PointerTriggers.cs](src/PointerTriggers.cs) | 20 | Shared `EventTrigger` hover/click wiring helper (was duplicated in the panel + swatch). |
| [src/Palette.cs](src/Palette.cs) | 13 | Two UI colours (label, accent) the mod draws itself. UI-only. |

## Structural debt

Overall the code is **well-shaped**: call direction is downward-only (grep-confirmed), the drawing
helpers are each one responsibility, and the comments are exceptional. The original review found the
debt concentrated in **three large files** — `CatColorPatch` (518), `CatFormColorPanel` (630),
`CatFormWardrobe` (474). Two of the three are now addressed: `CatFormColorPanel` shed `SliderRow`
(→492), and `CatFormWardrobe` was decomposed into a thin host + `CatPreviewController` +
`WardrobePanelSwap` (474→173). `CatColorPatch` (489) is the remaining large engine file — see below.
This list is the output of a full-depth review (componentization + abstraction Claude
lenses + an independent Codex cross-model pass, **2026-08-22**); full triage in
[docs/BACKLOG.md](docs/BACKLOG.md).

The review distinguished two risk classes, which drives what got fixed now vs backlogged:

- **Pure moves** — relocating code with no logic change; build-verifiable here (the game DLLs + dotnet
  are present), behaviour-neutral. Safe to do without launching the game.
- **Logic-touching** — anything that changes dispatch, tie-break, or cache semantics. This mod is
  *published (v1.1.0) and in-game-verified* and the game can't be launched here, so these are
  **backlogged until a change can be tested in-game** — a deliberate preserve-verified-behaviour
  tradeoff, not an oversight.

**Fixed in this review pass** (all build-verified green, behaviour-neutral, and **confirmed in-game
2026-08-22** — wardrobe slider, swatches, and revert-on-cancel all working):
- ✅ **Removed dead code** — `Templates.CloneButton` + its only-caller-is-dead helper `SetLabel`
  (~48 lines, no callers; Codex-flagged).
- ✅ **Extracted `SliderRow`** — the Fur-Intensity slider (`AddSliderRow` + `ThinCenteredBar`) left
  `CatFormColorPanel` (630→492). The panel passes its `AddLabel` in and gets an `onChanged` callback.
- ✅ **Extracted `ColorParsing`** from `CatColorPatch` (518→489). `CatFormColorPanel.ParseOr` was
  **left alone** — different semantics (no `#`-retry); unifying it stays backlogged as a behaviour change.
- ✅ **Extracted `PointerTriggers`** — the byte-identical `AddTrigger` helper, previously copy-pasted
  in `CatFormColorPanel` and `CatFormSwatch`, is now one shared method.
- ✅ **Extracted `PreviewColorSession`** from `CatFormWardrobe` (474→409) — the colour snapshot +
  revert-on-cancel, pure `ConfigEntry` state, fully decoupled from the preview rig.

**Fixed after the review pass (logic-touching, confirmed in-game 2026-08-22):**
- ✅ **Unified template-locate into `GameTemplate.Find<T>`** — absorbed the three copies of the
  "enumerate all instances incl. inactive → prefer scene-valid → try/catch + log" pattern
  (`Templates.Find`, `CatFormSwatch.FindTemplate`, `HeaderDecoration.FindTemplateBar`). The sites'
  **differing tie-break policies are preserved as per-caller predicates**, not flattened: Templates'
  3-tier (scene+size › scene › any) first-fallback via `preferred`+`secondary`; the two cached-widget
  sites' scene-valid + last-fallback via `fallbackLast`; Header projects through the reflected
  `headerText` to its parent bar for both the scene check and the result. Callers keep their own
  caching/projection. Verified in-game: slider, swatches, and decorated headers all still locate.
- ✅ **Decomposed the `CatFormWardrobe` God-patch** (474→173) into a thin Harmony host plus
  `CatPreviewController` (preview-body instantiate/swap + VFX/bloom) and `WardrobePanelSwap`
  (hide/restore native rows + build/destroy our panel). The host now only wires the three lifecycle
  hooks to those pieces + `PreviewColorSession`. Behaviour-neutral; confirmed in-game (tab, preview
  swap, live recolour, vanilla-tab restore, confirm/cancel).

**Backlogged — logic-touching (verify in-game before shipping):**
- **[P2] Separate the fur/eye/aura strategies from the patch + traversal in `CatColorPatch`** — the
  larger seam. Do **not** over-split: keep them as sibling methods, and note Codex judged a standalone
  `PropertyBlockWriter.cs` to be over-abstraction (a 2-caller impl detail, no independent policy) — so
  that one is *not* recommended on its own.
- **[P2] `TextureRecolor.GetOrBuild`'s 9-positional-param signature / 8-tuple cache key** → a
  `RecolorOptions` value type with `Fur(...)`/`Eye(...)` factories. Low urgency (heavily doc-commented);
  touches the cache key, so verify.
- **[P2] Original-value caches in `CatColorPatch`** (four dictionaries with a repeated
  capture/restore shape) *could* fold into an `OriginalValueCache<K,V>` — but Codex warns their
  **restore semantics differ** (pupil uses full picked value, not source brightness), so this is a
  "consider, with care," not a clean win.
- **[P2] Move the drawn-swatch fallback** (`BuildSwatchShell`/`AddCaption`) out of `CatFormColorPanel`
  into a `DrawnSwatch` file to parallel `CatFormSwatch` — only worthwhile if both share a minimal
  refreshable-view contract, else it just relocates spread-out state.

- **[Note, not debt] `TextureRecolor` duplication across the *Palette* mods is intentional.** Each mod
  is a standalone git repo that must build and ship alone; the recolour methodology is shared as
  *documentation* ([16-recolouring-characters.md](../../16-recolouring-characters.md)), not code. Do
  not refactor across repos. (See [docs/DECISIONS.md](docs/DECISIONS.md) ADR-009.)

## Conventions (this repo)

- Plugin `.cs` flat in `src/` (no `src/PurrtasticPalette/`); docs + `pack.ps1` at the mod root.
- Version is single-sourced from `src/PurrtasticPalette.csproj` `<Version>` via `GenerateModBuildInfo`
  in [Directory.Build.props](Directory.Build.props) → `ModBuildInfo.Version`. Never hardcode it.
- `pack.ps1` and `Directory.Build.props` are **workspace-synced canonicals** (generated by
  `../../tools/sync-mod-files.ps1`) — do not hand-edit them here.
- Commit identity: `dirtyredz <dirtyredz@live.com>`.
