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

## P0 — none
Nothing blocking. The mod ships and works.

## P1 — none
Both P1 items (the `GameTemplate.Find<T>` unification and the `CatFormWardrobe` decomposition) are
done and confirmed in-game — see "Done after the review pass" above. What remains is all P2.

## P2 — deferred / nice-to-have
- **Separate the fur/eye/aura strategies from the patch + traversal in `CatColorPatch`** (the larger
  seam). Keep them as sibling methods — don't over-split. A standalone `PropertyBlockWriter.cs` is
  **not** recommended (Codex: over-abstraction, a 2-caller impl detail with no independent policy). — *[verify in-game]*
- **`TextureRecolor.GetOrBuild` → a `RecolorOptions` value type** with `Fur(...)`/`Eye(...)` factories,
  collapsing the 9-positional-param signature and 8-tuple cache key. Low urgency (heavily doc-commented);
  touches the cache key. — *[verify in-game]*
- **Fold the four original-value caches in `CatColorPatch`** (`:105-111`) into an
  `OriginalValueCache<K,V>` — *with care*: Codex flags that restore semantics differ (pupil uses the
  picked colour's full value, not source brightness). Consider, don't assume clean. — *[verify in-game]*
- **Move the drawn-swatch fallback** (`BuildSwatchShell`/`AddCaption`) out of `CatFormColorPanel` into
  a `DrawnSwatch` file paralleling `CatFormSwatch` — only worthwhile if both share a minimal
  refreshable-view contract. — *[verify in-game]*
- **Keyboard / gamepad swatch navigation** — shelved feature, see [FEATURES.md](FEATURES.md).

## Known issues
- None open. Keyboard/gamepad nav in the wardrobe panel is intentionally unsupported (mouse only).
