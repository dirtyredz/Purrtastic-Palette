# Releasing Purrtastic Palette

Repo-wide rules live at the root; this file only covers what is specific to this mod.

- Versioning and archive layout: [12-versioning-and-release.md](../../12-versioning-and-release.md)
- Visual integration: [10-visual-integration.md](../../10-visual-integration.md)
- Save safety: [11-mod-data-and-saves.md](../../11-mod-data-and-saves.md)
- Nexus page: [13-nexus-page-standard.md](../../13-nexus-page-standard.md),
  [14-description-review.md](../../14-description-review.md), [15-page-style.md](../../15-page-style.md)

The first published version is **1.0.0**; bump only when publishing, one CHANGELOG entry per
release. The source directory is `mods/CatColorProbe` for history reasons; the shipped mod,
assembly, GUID and config folder are all **PurrtasticPalette**.

## Build a release

```powershell
powershell -File pack.ps1
```

Produces `dist/PurrtasticPalette-<version>.zip` laid out as
`BepInEx/plugins/PurrtasticPalette/PurrtasticPalette.dll`, reading the version from the csproj and
refusing to pack if `PluginVersion` in `Plugin.cs` disagrees.

There is no test project — every path reads live game state (the wardrobe screen, the preview
rig, the inventory). The checklist below carries the weight.

## Pre-release checklist

Root checklist first: [12-versioning-and-release.md](../../12-versioning-and-release.md). Then:

### The colours (config path)

- [ ] Each of Fur, Whiskers, Eye, Pupil, Eye Highlight and Aura applies in Cat Form from the
      `.cfg` / Mod Nook, live, with no re-equip
- [ ] Blank leaves that part vanilla; a bad hex is ignored rather than crashing

### The wardrobe tab

- [ ] The **Cat Form** tab appears in the mirror only when you **own Cat Form** (gated on
      inventory); it does **not** appear in new-character creation
- [ ] Preview shows the cat; every swatch row recolours it live
- [ ] Panel **scrolls** (mouse wheel over a swatch, and drag)
- [ ] Pick is a live preview: **Confirm keeps it, closing without Confirm reverts** the colours
      (check the world cat too, not just the preview)
- [ ] The custom `+` tile opens the RGB picker; the chosen colour writes back
- [ ] Paw tab icon shows (from `config/PurrtasticPalette/tab-icon.png` if present, else the
      generated glyph); the tab strip is scrolled to the first tab on open, not centred
- [ ] Leaving and re-entering the mirror keeps everything working (no stale template cache)

### Compatibility

- [ ] Works with **Serena's Enchanted Studio disabled** — the tab and swatches still function
      (cloned-swatch template absent → drawn-swatch fallback, no errors)
- [ ] No leak into new-character creation (separate screen; also gated by ownership)

### Housekeeping

- [ ] `<Version>` and `PluginVersion` match — `pack.ps1` enforces this
- [ ] CHANGELOG has one entry for this version
- [ ] `Colors/VerboseLogging` defaults to `false`; a normal session logs only the load line
- [ ] **Dev tools**: decide the F7 probe / `GiveFormsKey` fate before shipping (see below)
- [ ] Fresh install: delete `BepInEx/config/com.dirtyredz.moonlightpeaks.purrtasticpalette.cfg`,
      launch, confirm sensible defaults are written
- [ ] Screenshots show the current build; thumbnail composed at **16:9**
- [ ] Archive extracted onto a clean install and verified in game

## Dev tools before shipping

`ProbeController` powers `ProbeKey` (F7, dumps renderers/materials/textures) and `GiveFormsKey`
(Home, grants Cat/Bat/Aqua form on a save that has not unlocked them). Both are development aids.
`GiveFormsKey` in particular is a cheat that does not belong in a public build. Decide per release
whether to strip them or leave them behind a clearly-labelled Debug section; do not ship a form
grant silently.

## Save safety

The mod stores nothing of its own and adds no persistence type. It only writes its own
`ConfigEntry` values (hex colours) and, in the wardrobe, snapshots/restores those same values
around Confirm. No save collection is touched. See
[11-mod-data-and-saves.md](../../11-mod-data-and-saves.md).

## Licence

**MIT** — see [LICENSE](LICENSE). Set the Nexus permissions to agree with it:

| Nexus permission | Set to |
|---|---|
| Upload to other sites | Allowed |
| Convert to other games | Allowed |
| Modify and release | Allowed |
| Use assets in own files | Allowed |
| Include in mod packs / collections | Allowed |

## Editing note

Do not round-trip these files through `Get-Content -Raw | Set-Content` in PowerShell — it
re-encodes non-ASCII characters and has corrupted em-dashes in this repo before.
