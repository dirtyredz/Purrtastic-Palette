# GOTCHAS — Purrtastic Palette

Non-obvious traps. The recolour-engine footguns are covered in depth in
[../README.md](../README.md) "Gotchas worth knowing" — the highlights and the UI/structural ones
that aren't in the README are here.

## Recolour engine
- **Property blocks win at draw time.** `material.SetTexture/SetColor` alone is invisible whenever
  the game has set a `MaterialPropertyBlock` on the renderer (it has). Always write through the block
  too. (README has the full story.)
- **Something reverts the fur → per-frame reapply.** Don't "optimise" `CatColorReapplier` to a timed
  interval; the reverter isn't on a schedule and any gap shows. Don't add the **eyes** to the loop —
  they wash out and `PupilColor` starts dragging `EyeColor` down.
- **Disable the pupil/iris split with a *negative* threshold, not 0.** The fur is overwhelmingly
  saturation-0 near-black pixels; a 0 threshold matches them all and copies them through
  unrecoloured, leaving the coat black.
- **The pupil is painted at the picked colour's own value**, not scaled by its (near-zero) source
  brightness — else a black pupil stays black whatever you pick.
- **Emission is dead** (see [DECISIONS.md](DECISIONS.md) ADR-001) — don't rebuild it.

## Wardrobe UI
- **The preview cat is a separate body instance** with its own materials — colours applied to the
  live player do not carry over; call `ApplyToBody` on the preview instance explicitly.
- **Template lookups must re-search after the screen is torn down.** Unity's `== null` is true for a
  destroyed object; caching a template by a one-time bool handed back a dead reference on the second
  mirror visit and silently regressed to the drawn fallback. Every template finder re-searches when
  its cache is `== null`.
- **`categoryListWidget` points at an inactive *template*, not the visible rows.** The rows on screen
  are runtime clones beside it. Hide the *active* Content children, not the serialized field.
- **EventTrigger swallows the mouse wheel** (it implements `IScrollHandler`). Any swatch with an
  EventTrigger also needs a `ScrollForwarder`, or the panel stops scrolling under the cursor.
- **Clone game widgets *inactive* (via `Templates.Staging`)** so their `Awake` and shared-Signal
  subscriptions don't register against the live screen before you've stripped them.
- **`SliderButton.Setup` throws on a null `MaxValueTextOverride`** at max value — pass an empty
  `SinglelineLocalizedText()`, not null.
- **A throwing handler on a Chicken `Signal` kills every listener after it** in that multicast — wrap
  cloned-button `OnClick` bodies in try/catch (Templates does).

## Build / repo
- **`pack.ps1` and `Directory.Build.props` are workspace-synced canonicals** — edit them via
  `../../tools/sync-mod-files.ps1`, never by hand here, or the next sync reverts you.
- **Never hardcode the version** — it flows from the csproj `<Version>` through `ModBuildInfo.Version`.
