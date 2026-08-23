# BACKLOG — Purrtastic Palette

Prioritised trough. P0 = do next / blocking · P1 = should do · P2 = nice-to-have / deferred.

Structural items are the output of the full review stamped in [../STRUCTURE.md](../STRUCTURE.md)
(**2026-08-22**): componentization + abstraction Claude lenses + an independent Codex cross-model
pass. Each is tagged **[pure move]** (behaviour-neutral relocation, build-verifiable here — the game
DLLs and dotnet are present) or **[verify in-game]** (changes dispatch/tie-break/cache semantics; the
mod is published and the game can't be launched from this environment, so test before shipping).

## Done in the review pass (build-verified, behaviour-neutral, confirmed in-game 2026-08-22)
- ✅ **Removed dead code** — `Templates.CloneButton` + `SetLabel` (~48 lines, no live caller; Codex-flagged).
- ✅ **Extracted `SliderRow.cs`** — the Fur-Intensity slider left `CatFormColorPanel` (630→492 lines).
- ✅ **Extracted `ColorParsing.cs`** from `CatColorPatch` (518→489). `CatFormColorPanel.ParseOr` left
  untouched (different semantics — unifying it is still backlogged below as logic-touching).
- ✅ **Extracted `PointerTriggers.cs`** — the byte-identical `AddTrigger` helper, de-duplicated.
- ✅ **Extracted `PreviewColorSession.cs`** from `CatFormWardrobe` (474→409) — snapshot + revert-on-cancel.
- ✅ **Deleted stale `HANDOFF.md`** — the completed pre-1.0.0 folder-rename hand-off (mod is published at 1.1.0).

## Done after the review pass (logic-touching, confirmed in-game 2026-08-22)
- ✅ **Unified template-locate into `GameTemplate.Find<T>`** — absorbed the locate pattern from
  `Templates.Find`, `CatFormSwatch.FindTemplate`, `HeaderDecoration.FindTemplateBar` into one
  `GameTemplate.cs`. The three differing tie-break policies are preserved as per-caller predicates
  (`match`/`preferred`/`secondary`/`fallbackLast`), not flattened; callers keep their own caching +
  projection. Build-verified and in-game smoke-tested (slider, swatches, decorated headers all locate).
- ✅ **Decomposed the `CatFormWardrobe` God-patch** (474→173) into a thin Harmony host +
  `CatPreviewController` (preview-body instantiate/swap + VFX/bloom suppression) + `WardrobePanelSwap`
  (hide/restore native category rows + build/destroy our colour panel). The host only wires the three
  lifecycle hooks to those pieces and `PreviewColorSession`. Behaviour-neutral; confirmed in-game.
- ✅ **Unified hex parsing** — `CatFormColorPanel.ParseOr` now routes through the shared
  `ColorParsing.TryParse` (the parser `CatColorPatch` already uses) instead of calling
  `ColorUtility.TryParseHtmlString` directly, so both paths get the same `#`-optional retry. No new
  class needed — `ColorParsing` was already the shared parser. Happy path unchanged (every written
  value carries `#`); the only new behaviour is that a bare `FF8800` now parses instead of falling back.
- ✅ **`RecolorOptions` value type** — collapsed `TextureRecolor.GetOrBuild`'s 9-positional-param
  signature + 8-tuple cache key into a `RecolorOptions` struct (own file) with `Fur(...)`/`Eye(...)`
  factories; the param docs moved onto its fields. Cache-identical by construction (equality/hash
  delegate to the same 8-field tuple, `Target` excluded as before). Confirmed in-game — fur + eye
  recolour render, and repeat picks hit the cache (instant).
- ✅ **Fixed the eye-recolour freeze (`MaxRecolorDimension` cap)** — the cat eye atlas ships at
  **4096²**; the per-pixel HSV loop over 16.7M pixels froze the game ~1.7s per build, ×2 atlases, on
  every Cat-Form transform, wardrobe tab-open, and eye-colour change. Profiled it (stopwatch: ~85% is
  the CPU loop), proved it predates all recent refactors *and* is independent of Fangtastic (repro'd
  on a first-version, Fangtastic-free PC). Fix: cap the recolour working resolution at 512²
  (`TextureRecolor.MaxRecolorDimension`) — the source is downscaled on the blit before the pixel work,
  so builds drop **~1700ms → ~30ms**. Justified because the eye is tiny on screen and Fangtastic's
  smaller bat eye atlas already recolours fine at low res. Confirmed in-game: no freeze, and both eyes
  and fur still crisp at 512². The cap is global, so the 1024² fur/whisker atlases are downscaled too;
  if fur ever needs to stay sharp, make the cap per-caller (eyes low, fur high) rather than raising it.

## P0 — none
Nothing blocking. The mod ships and works.

## P1 — none
Both P1 items (the `GameTemplate.Find<T>` unification and the `CatFormWardrobe` decomposition) are
done and confirmed in-game — see "Done after the review pass" above. What remains is all P2.

## P2 — deferred / nice-to-have
- **Separate the fur/eye/aura strategies from the patch + traversal in `CatColorPatch`** (the larger
  seam). Keep them as sibling methods — don't over-split. A standalone `PropertyBlockWriter.cs` is
  **not** recommended (Codex: over-abstraction, a 2-caller impl detail with no independent policy). — *[verify in-game]*
- **Fold the four original-value caches in `CatColorPatch`** (`:105-111`) into an
  `OriginalValueCache<K,V>` — *with care*: Codex flags that restore semantics differ (pupil uses the
  picked colour's full value, not source brightness). Consider, don't assume clean. — *[verify in-game]*
- **Move the drawn-swatch fallback** (`BuildSwatchShell`/`AddCaption`) out of `CatFormColorPanel` into
  a `DrawnSwatch` file paralleling `CatFormSwatch` — only worthwhile if both share a minimal
  refreshable-view contract. — *[verify in-game]*
- **Keyboard / gamepad swatch navigation** — shelved feature, see [FEATURES.md](FEATURES.md).

## Known issues
- None open. Keyboard/gamepad nav in the wardrobe panel is intentionally unsupported (mouse only).
