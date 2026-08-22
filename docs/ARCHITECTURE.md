# ARCHITECTURE — how Purrtastic Palette works

System-level view. The pixel-level *why* (HSV colorize, the four material targets, the eye split)
is in [../README.md](../README.md) "How it works" — not repeated here. Code shape is in
[../STRUCTURE.md](../STRUCTURE.md).

## The core problem

Cat Form is **not** a recoloured player — equipping it swaps the whole body for a different prefab
(`HellkittenBodyView`) via `EntityCustomization.SetBodyView`. So colour can't ride on the player's
customization; it has to be applied to the Cat body's own renderers, every time that body appears,
and re-asserted because the game keeps reverting it.

## Two entry points, one engine

```
 config (.cfg / Mod Nook)          wardrobe Cat Form tab
        │                                   │
        │ SettingChanged                    │ swatch/slider pick → ConfigEntry.Value = …
        ▼                                   ▼
   CatColorPatch.ApplyCatColors ◄───────────┘   (writing a ConfigEntry raises SettingChanged,
        │                                         so both paths converge on the same apply)
        │ walks the live player's Cat body renderers
        ▼
   fur/whisker → ApplyFurColor ─┐
   eyes        → ApplyEyeColor ─┼─► TextureRecolor.GetOrBuild (cached) ─► MaterialPropertyBlock
   trail/aura  → ApplyAuraColor ┘                                          write on the renderer
```

`CatColorReapplier.Update()` calls `ApplyCatColors(includeEyes:false)` every frame — the safety net
that beats whatever reverts the fur. Eyes are excluded (they got *worse* under the loop). See
[DECISIONS.md](DECISIONS.md).

## Apply timing (the equip path)

Harmony postfix on `FormToolView<CatToolAsset>.HandleEnterVisuallySwitchedBodyViews` — the first
moment the freshly-loaded Cat body is active. Targeting the **closed generic** means Bat/Aqua Form
never trigger it, so no "is this really the cat" guard is needed.

## The wardrobe tab (a self-contained UI subsystem)

`CatFormWardrobe` patches `WardrobeCustomizationScreen`:
- **OnShow** → ownership-gate, inject the "Cat Form" tab, snapshot colours for revert-on-cancel.
- **tab OnSelect** (`ShowCatInPreview`) → instantiate the Cat body into the preview rig, hide other
  bodies + VFX, suppress bloom, apply colours to *that* body instance, build the swatch panel.
- **HandleTabSelected** (prefix, vanilla tabs) → tear the panel down, restore the human body.
- **OnHide** → revert-unless-confirmed, destroy the panel, restore bloom, drop cached refs.

The preview cat is a **separate instance** with its own materials, so `ApplyToBody` is called on it
explicitly (the live player's colours don't carry over). A swatch pick is a live **preview**:
applied immediately, kept only if Confirm is pressed, else reverted from the snapshot on close.

The panel and picker reuse the game's own widgets where possible (`CatFormSwatch`, `Templates`,
`HeaderDecoration`) and fall back to generated sprites (`CircleSprite`, `PanelSprite`, `PawSprite`)
when a template can't be found, so it always renders *something* on-theme.

## Build / release

netstandard2.1 → `PurrtasticPalette.dll`. References resolve against the game's shipped managed
assemblies (`Directory.Build.props`, workspace-synced). `pack.ps1` → `dist/PurrtasticPalette-<ver>.zip`
in Nexus layout. Full chain: workspace [../../docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md).
Published on Nexus as [mod 142](https://www.nexusmods.com/moonlightpeaks/mods/142).
