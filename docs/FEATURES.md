# FEATURES — Purrtastic Palette

Capability inventory. Status: ✅ shipped · 🚧 partial · 🧊 shelved.

## Recolouring
| Feature | Status | Notes |
|---|---|---|
| Fur (body) colour | ✅ | `FurColor`; regenerates `_BaseMap`+`_MainTex` (dual albedo) and the `_Atlas` submesh. |
| Fur Intensity | ✅ | `FurIntensity` 0–1; fades the recolour back toward the original coat shading. Fur only. |
| Whisker colour | ✅ | `WhiskerColor`; independent of fur (separate material on the same mesh). Blank = vanilla white. |
| Iris colour | ✅ | `EyeColor`; saturation-split from pupil/highlight. |
| Pupil colour | ✅ | `PupilColor`; applied at full picked brightness so it isn't stuck black. Needs `EyeColor` set. |
| Eye-highlight colour | ✅ | `EyeHighlightColor`; the bright glint. Needs `EyeColor` set. |
| Trail / sparkle (aura) colour | ✅ | `AuraColor`; HSV hue/sat swap on VFX colour props (HDR-safe). |
| Live apply (no re-equip) | ✅ | `SettingChanged` → reapply; per-frame safety net for the fur. |

## Wardrobe UI (Cat Form tab)
| Feature | Status | Notes |
|---|---|---|
| Cat Form tab in the mirror wardrobe | ✅ | Ownership-gated; paw-print icon. |
| Live cat preview in the rig | ✅ | Separate body instance; bloom + VFX suppressed for colour clarity. |
| Preset swatch rows per part | ✅ | Cloned game swatches (frame/checkmark/hover) with drawn fallback. |
| Custom RGB colour picker (`+` tile) | ✅ | `ColorPickerPopup`, R/G/B sliders + hex readout. |
| Fur-Intensity slider in-panel | ✅ | Built from a cloned game slider style. |
| Decorated row headers | ✅ | Cloned from the game's category header. |
| Live-preview with revert-on-cancel | ✅ | Kept only on Confirm. |
| Custom tab icon override (PNG) | ✅ | `BepInEx/config/PurrtasticPalette/tab-icon.png`. |
| Panel scrolls via the screen's own ScrollRect | ✅ | `ScrollForwarder` keeps the wheel working over swatches. |
| Keyboard / gamepad navigation between swatches | 🧊 | Shelved — the screen's hover-select model nulls a keyboard cursor. Mouse works fully. See workspace `17-wardrobe-ui.md §7`. |

## Config surface
`FurColor`, `FurIntensity`, `WhiskerColor`, `EyeColor`, `PupilColor`, `EyeHighlightColor`, `AuraColor`,
`VerboseLogging` — all under the **Colors** section (Mod Nook labels via `ConfigDescription` tags).
