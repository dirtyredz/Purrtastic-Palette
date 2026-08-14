# Purrtastic Palette — Nexus page source

The description field is **SCEditor with a BBCode source**, so the block below is the literal value
that goes in it. Structure per [13-nexus-page-standard.md](../../13-nexus-page-standard.md), look
per [15-page-style.md](../../15-page-style.md).

Like the other live pages: **no `[list]` blocks** — every bullet is a literal `-` on its own line,
so the editor's hard line breaks do the spacing. Palette: warm gold `#F7D994` (title/headings),
gold rim `#C7A25B` (tagline, at-a-glance), muted plum `#7A6A9B` (rules), body `#D4D4D8` (prose).

## Other fields

| Field | Value |
|---|---|
| Name | `Purrtastic Palette` — kept clean; the search words (recolour, cat form, Hellkitten) ride in the summary |
| Category | a visual/cosmetic category if the game has one, else Gameplay — confirm on the upload form |
| Tags | customization, cosmetic, quality of life — confirm each is in Nexus's fixed vocabulary |

**Short description** (lowercase start, no full stop — it opens to someone who does not yet know
the mod):

```
your cat form is always the same little Hellkitten — recolour its fur, eyes, whiskers and sparkle trail, live, from a picker in the mirror
```

## Description source

```bbcode
[size=6][color=#F7D994]🐾  Purrtastic Palette[/color][/size]
[color=#C7A25B][i]Your cat form is always the same little Hellkitten — recolour its fur, eyes, whiskers and sparkle trail, live, from a picker in the mirror.[/i][/color]
[color=#C7A25B]🎨 Six colours  ·  🪞 Pick in the mirror  ·  ⚡ Applies live  ·  💾 Save-safe[/color]
[color=#7A6A9B]────────────────────────────────────────[/color]
[quote]🪞  [color=#F7D994][b]Try before you commit.[/b][/color] Colours preview live in the mirror and only stick when you press Confirm — back out and nothing changed.[/quote]

[size=5][color=#F7D994]🎨  What it does[/color][/size]
[color=#D4D4D8]Every cat form in Moonlight Peaks is the same little black Hellkitten. You unlock it, you turn into it, and it looks exactly like everyone else's.

Purrtastic Palette lets you make it yours. Recolour the fur, the eyes — iris, pupil and the bright glint each on their own — the whiskers, and the sparkle trail the cat leaves when it runs.

There are two ways to set the colours. Open the mirror in your house, choose Change clothes, and a Cat Form tab is waiting at the end of the row: it shows your cat and gives a swatch picker for each part, presets plus a + tile for any colour you like. Or set them as plain hex codes or colour names in the config — or in Mod Nook, in game — and they apply on the spot.

In the mirror the colours are a live preview: they change the cat as you browse, and only stick when you press Confirm. Back out without confirming and everything returns to how it was, the same as trying on clothes.

Nothing new is written to your save — only your own colour settings.[/color]

[size=5][color=#F7D994]✨  Main features[/color][/size]
[color=#D4D4D8]- Recolour six parts of Cat Form: fur, whiskers, iris, pupil, the bright eye glint, and the sparkle trail
- A Cat Form tab in the mirror's wardrobe, with a live preview of your cat
- Eleven preset swatches per part, plus a + tile that opens a full RGB colour picker
- Colours preview live and only stick on Confirm — leave without confirming and they revert
- Or set them as hex codes or colour names in the config / Mod Nook, applied instantly with no re-equip
- The tab appears only once you have unlocked Cat Form
- Whiskers stay white by default rather than following the fur, so you choose whether they match
- Save-safe — only your own colour settings are stored; nothing new is written to your save
- Works with or without Serena's Enchanted Studio[/color]

[size=5][color=#F7D994]📋  Requirements[/color][/size]
[color=#D4D4D8][b]Required[/b]
- BepInEx 5 (win_x64), version 5.4.23.5 or newer

[b]Recommended companion[/b]
- Mod Nook — my in-game settings menu. Purrtastic Palette's colours show up in it as editable fields, so you can change them from the pause menu instead of a text file. Not needed; the mirror picker is the main way in. https://www.nexusmods.com/moonlightpeaks/mods/127
- Mod Menu by Elsiabeth does the same job and is also supported. Both can be installed; neither interferes with the other.

PC/Steam only. The Switch and mobile builds cannot load BepInEx.[/color]

[size=5][color=#F7D994]📥  Installation[/color][/size]
[color=#D4D4D8][b]🟢 With Vortex[/b]
Open the Files tab, click the Vortex button, and enable the mod. Done.

[b]🔧 Manually[/b]
1. Install BepInEx 5 (win_x64) into your Moonlight Peaks folder, if you do not have it already. The BepInEx folder sits beside Moonlight Peaks.exe.
2. Launch the game once, then quit. This creates the BepInEx/plugins folder.
3. Download the archive from the Files tab and extract it over your Moonlight Peaks folder, so the file ends up at BepInEx/plugins/PurrtasticPalette/PurrtasticPalette.dll.
4. Launch the game.

To uninstall, delete the BepInEx/plugins/PurrtasticPalette folder. Your cat goes back to its default colours; nothing was ever written to your save.[/color]

[size=5][color=#F7D994]🎛️  Configuration[/color][/size]
[color=#D4D4D8]Settings are written to BepInEx/config/com.dirtyredz.moonlightpeaks.purrtasticpalette.cfg on first launch. Every value is a hex code (#FF8800) or an HTML colour name (orange); leave one blank to keep that part vanilla.[/color]
[quote]🎛️  [color=#F7D994][b]Nicer in Mod Nook.[/b][/color] Install it and the six colours become editable fields under Colors in the pause menu — the same colours the mirror picker sets, so the world cat and the mirror can never disagree. Nothing here needs it; it just makes the mod easier to live with.[/quote]

[size=5][color=#F7D994]🤝  Compatibility[/color][/size]
[color=#D4D4D8]Serena's Enchanted Studio touches the same mirror, but the two do not conflict — install them together or run Purrtastic Palette on its own. Without Serena's, if the swatch template it clones can't be found, the picker falls back to plain drawn swatches rather than failing.[/color]

[size=5][color=#F7D994]💜  Shout outs[/color][/size]
[color=#D4D4D8]- Little Chicken Game Company for making a game worth spending this much time inside.
- The BepInEx and HarmonyX teams, without whom none of this scene exists.
- SerenaEnchanted, whose Enchanted Studio does the mirror-recolour work for the human side.
- Elsiabeth for Mod Menu, which made the case that in-game settings were worth having.
- My Mate, for being my inspiration.[/color]
```
