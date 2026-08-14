# Nexus Mod Page — Purrtastic Palette

> **Pasting into the upload form? Use [nexus-paste.md](nexus-paste.md), not this file.**
> The copy here is wrapped for reading; the editor turns every wrap into a `<br>`. The paste file
> is the literal BBCode. Structure: [13-nexus-page-standard.md](../../13-nexus-page-standard.md);
> look: [15-page-style.md](../../15-page-style.md).

Draft copy for the Nexus listing. Same shape as the sibling pages; read
[Transplant/NEXUS.md](../Transplant/NEXUS.md)'s notes on the upload form, thumbnail ratio and art
direction first — they all still apply.

---

## Fields

| Field | Value |
|---|---|
| **Name** | Purrtastic Palette |
| **Summary** (short, shows in listings) | your cat form is always the same little Hellkitten — recolour its fur, eyes, whiskers and sparkle trail, live, from a picker in the mirror |
| **Category** | Confirm against the game's list. Best fit is a visual/cosmetic category (where recolour and reskin mods sit); fall back to Gameplay if there is no cosmetic one. |
| **Version** | 1.0.0 |
| **Nexus page** | *(new — no mod id yet)* |
| **Requirements** | BepInEx 5 (win_x64), 5.4.23.5 or newer — required |
| | [Mod Nook](https://www.nexusmods.com/moonlightpeaks/mods/127) — optional, for in-game settings |
| | Mod Menu — optional, the alternative to Mod Nook |
| **Tags** | customization, cosmetic, quality of life *(Nexus tags are a fixed vocabulary — confirm each exists before relying on it)* |
| **Licence** | MIT |

**Searchable words.** The name is kept clean. The words a player would search — *recolour*, *cat
form*, *Hellkitten*, *colour* — are carried by the summary instead, so they still land in search
without turning the title into a sentence.

---

## Full description — paste into Nexus

### Description

Every cat form in Moonlight Peaks is the same little black Hellkitten. You unlock it, you turn
into it, and it looks exactly like everyone else's.

Purrtastic Palette lets you make it yours. Recolour the fur, the eyes — iris, pupil and the bright
glint each on their own — the whiskers, and the sparkle trail the cat leaves when it runs.

There are two ways to set the colours. Open the mirror in your house, choose Change clothes, and a
**Cat Form** tab is waiting at the end of the row: it shows your cat and gives a swatch picker for
each part, presets plus a **+** tile for any colour you like. Or set them as plain hex codes or
colour names in the config — or in Mod Nook, in game — and they apply on the spot.

In the mirror the colours are a live preview: they change the cat as you browse, and only stick
when you press **Confirm**. Back out without confirming and everything returns to how it was, the
same as trying on clothes.

Nothing new is written to your save — only your own colour settings.

---

### Main features

- Recolour six parts of Cat Form: fur, whiskers, iris, pupil, the bright eye glint, and the sparkle trail
- A Cat Form tab in the mirror's wardrobe, with a live preview of your cat
- Eleven preset swatches per part, plus a + tile that opens a full RGB colour picker
- Colours preview live and only stick on Confirm — leave without confirming and they revert
- Or set them as hex codes or colour names in the config / Mod Nook, applied instantly with no re-equip
- The tab appears only once you have unlocked Cat Form
- Whiskers stay white by default rather than following the fur, so you choose whether they match
- Save-safe — only your own colour settings are stored; nothing new is written to your save
- Works with or without Serena's Enchanted Studio

---

### Requirements

**Required**

- BepInEx 5 (win_x64), version 5.4.23.5 or newer

**Recommended companion**

- **Mod Nook** — my in-game settings menu. Purrtastic Palette's colours show up in it as editable
  fields, so you can change them from the pause menu instead of a text file — handy when you want
  the exact same colour on the world cat as in the mirror. Not needed; the mirror picker is the
  main way in, and without Mod Nook the settings live in a plain config file.
  https://www.nexusmods.com/moonlightpeaks/mods/127
- **Mod Menu** by Elsiabeth does the same job and is also supported. Mod Nook and Mod Menu can both
  be installed — each adds its own button and neither interferes with the other.

PC/Steam only. The Switch and mobile builds cannot load BepInEx.

**Compatibility**

Serena's Enchanted Studio touches the same mirror, but the two do not conflict — install them
together or run Purrtastic Palette on its own. Without Serena's, if the swatch template it clones
can't be found, the picker falls back to plain drawn swatches rather than failing.

---

### Installation instructions

**With Vortex**

Open the Files tab, click the Vortex button, and enable the mod. Done.

**Manually**

1. Install BepInEx 5 (win_x64) into your Moonlight Peaks folder, if you do not have it already.
   The BepInEx folder sits beside Moonlight Peaks.exe.
2. Launch the game once, then quit. This creates the BepInEx/plugins folder.
3. Download the archive from the Files tab and extract it over your Moonlight Peaks folder, so the
   file ends up at BepInEx/plugins/PurrtasticPalette/PurrtasticPalette.dll.
4. Launch the game.

Settings are written to BepInEx/config/com.dirtyredz.moonlightpeaks.purrtasticpalette.cfg on first
launch. With Mod Nook installed you never need to open it — every colour appears under
Pause > Mod Nook and applies immediately, without a restart.

To uninstall, delete the BepInEx/plugins/PurrtasticPalette folder. Your cat goes back to its
default colours; nothing was ever written to your save.

---

### Configuration

Settings are written to BepInEx/config/com.dirtyredz.moonlightpeaks.purrtasticpalette.cfg on first
launch. Every value is a hex code (#FF8800) or an HTML colour name (orange); leave one blank to
keep that part vanilla.

Install Mod Nook and you can change them in game instead. Purrtastic Palette shows up in it on its
own, under Colors — the same six colours the mirror picker sets, so the world cat and the mirror
can never disagree. Nothing here needs it; it just makes the mod easier to live with.

---

### Shout outs

- **Little Chicken Game Company** for making a game worth spending this much time inside.
- The **BepInEx** and **HarmonyX** teams, without whom none of this scene exists.
- **SerenaEnchanted**, whose Enchanted Studio does the mirror-recolour work for the human side and
  showed how much nicer picking a colour in game is than editing a file.
- **Elsiabeth** for Mod Menu, which made the case that in-game settings were worth having.
- **My Mate**, for being my inspiration.

---

## Changelog entries for the Nexus page

Player-facing. Describe the **symptom**, not the cause — the repo README names the Harmony patches.

### 1.0.0

```
First release.

- Recolour your cat form: fur, whiskers, iris, pupil, the bright eye glint and the sparkle trail.
- Pick colours in the mirror's new Cat Form tab, with a live preview, or set them in the config.
- In the mirror, colours only stick when you press Confirm; back out and they revert.
- Nothing new is written to your save.
```

---

## Screenshots

Files live in `screenshots/`. The thumbnail is set separately in the upload form.

| # | Shot | File | Notes |
|---|---|---|---|
| - | Thumbnail, **16:9** | `Purrtastic Palette Thumbnail.png` | ✅ delivered — proofread the lettering at 4–8x before upload |
| - | Title banner | `Purrtastic Palette Banner.png` | ✅ delivered |
| 1 | The whole thing: Cat Form tab, swatch rows, a recoloured cat | `Pink Purrtastic Palette.png` | ✅ the money shot — tab, decorated headers, selected swatch + `+` tile, scrollbar, live preview, and the Confirm/Cancel prompts all in one |
| 2 | Another colourway | `Purple Purrtastic Palette.png` | ✅ |
| 3 | Another colourway | `Red Purrtastic Palette.png` | ✅ |
| 4 | The custom RGB picker | `Color Picker Purrtastic Palette.png` | ✅ shows the `+` tile's picker |
| 5 | The paw tab in the strip | `Cat Tab Purrtastic Palette.png` | ✅ shows the tab icon sitting with the vanilla tabs |

**Shot 1 is the one that sells it** — it shows a recoloured cat *and* the whole UI at once
(decorated headers, the selection ring and checkmark on the chosen swatch, the `+` custom tile, the
scrollbar, and the F Confirm / Esc Cancel prompts that make the live-preview promise visible). Lead
with it.

### Thumbnail must be composed at 16:9

Listing tiles use `object-fit: fill`, so an off-ratio thumbnail is **stretched, not cropped**.
Proofread any generated lettering at 5x or more before accepting it — the title art reads
"Purrtastic Palette", which must match the mod name exactly.

### Art direction

Palette is fixed by [10-visual-integration.md](../../10-visual-integration.md): plum fill, gold
rim, warm gold text. The delivered thumbnail already follows it — the cozy night-market scene, the
palette-and-swatches motif, and the paw-crest banner all match the tab icon in game.

---

## Notes before publishing

- ✅ **Play-tested.** Confirmed in game: all six colours, the wardrobe tab (preview, swatches,
  picker, decorated headers), live preview with revert-on-cancel, the paw tab icon, the
  ownership gate, tab-strip scrolling, and running with Serena's Enchanted Studio disabled.
- State plainly that it is **save-safe** — this community reads for that. Only the mod's own colour
  settings are stored.
- Say **cosmetic, not a cheat**: it recolours the form, nothing else.
- List BepInEx as **required** and Mod Nook / Mod Menu as **optional**.
- Set the Nexus permissions to agree with the MIT licence — see [RELEASING.md](RELEASING.md).
- Run the [RELEASING.md](RELEASING.md) checklist, then `pack.ps1`, and upload
  `dist/PurrtasticPalette-1.0.0.zip`.
