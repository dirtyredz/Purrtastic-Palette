# Purrtastic Palette — Nexus page source

**Nexus page:** not yet published

The description field is **SCEditor with a BBCode source**, so the block below is the literal
value to set. Every paragraph is a single unbroken line — see
[15-page-style.md](../../15-page-style.md) on why: the editor turns every wrap in a pasted
paragraph into a `<br>`, which is what happened to Last Swing and Transplant. Same layout as
[FormLock/nexus-paste.md](../FormLock/nexus-paste.md).

Style reference: [15-page-style.md](../../15-page-style.md). Mechanics:
[13-nexus-page-standard.md](../../13-nexus-page-standard.md).

## Other fields

| Field | Value |
|---|---|
| Name | `Purrtastic Palette` — no subtitle, per the standing decision in 13-nexus-page-standard.md; the search words (recolour, cat form, Hellkitten) ride in the short description |
| Category | a visual/cosmetic category if the game has one, else Gameplay — confirm on the upload form |
| Tags | Customization, Quality of Life — confirm both exist in the fixed per-game tag list before saving |
| Short description | your cat form is always the same little Hellkitten — recolour its fur, eyes, whiskers and sparkle trail, live, from a picker in the mirror |

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

Open the mirror in your house, choose Change clothes, and a Cat Form tab is waiting at the end of the row: it shows your cat and gives a swatch picker for each part, presets plus a + tile for any colour you like. Or set the colours as plain hex codes or colour names in the config — or in Mod Nook, in game — and they apply on the spot.

In the mirror the colours are a live preview: they change the cat as you browse, and only stick when you press Confirm. Back out without confirming and everything returns to how it was, the same as trying on clothes. Nothing new is written to your save — only your own colour settings.[/color]

[size=5][color=#F7D994]✨  Main features[/color][/size]
[list]
[*][b]Six parts, recoloured[/b] — fur, whiskers, iris, pupil, the bright eye glint, and the running sparkle trail
[*][b]A Fur Intensity slider[/b] — fade the fur colour toward the coat's own shading for a softer, less flat look
[*][b]A Cat Form tab in the mirror[/b], with a live preview of your cat
[*][b]Preset swatches per part, plus a + tile[/b] that opens a full RGB colour picker
[*][b]Live preview[/b] — colours only stick on Confirm; leave without confirming and they revert
[*][b]Or set them in the config[/b] as hex codes or colour names, applied instantly with no re-equip
[*][b]Appears only once you own Cat Form[/b]
[*][b]Whiskers stay white by default[/b] — you choose whether they match the fur
[*][b]Save-safe[/b] — only your own colour settings are stored, nothing new in your save
[*][b]Works with or without Serena's Enchanted Studio[/b]
[/list]

[size=5][color=#F7D994]📋  Requirements[/color][/size]
[list]
[*][b]BepInEx 5 (win_x64)[/b], version 5.4.23.5 or newer
[/list]
[color=#D4D4D8]PC/Steam only. The Switch and mobile builds can't load BepInEx.[/color]

[size=5][color=#F7D994]📥  Installation[/color][/size]
[b]🟢 With Vortex[/b]
[color=#D4D4D8]Open the Files tab, click the Vortex button, and enable the mod. Done.[/color]

[b]🔧 Manually[/b]
[list=1]
[*]Install [b]BepInEx 5 (win_x64)[/b] into your Moonlight Peaks folder, if you do not have it already. The BepInEx folder sits beside Moonlight Peaks.exe.
[*]Launch the game once, then quit. This creates the BepInEx/plugins folder.
[*]Download Purrtastic Palette from the Files tab and extract the archive over your Moonlight Peaks folder, so the file ends up at BepInEx/plugins/PurrtasticPalette/PurrtasticPalette.dll
[*]Launch the game.
[/list]
[color=#D4D4D8]To uninstall, delete BepInEx/plugins/PurrtasticPalette. Your cat goes back to its default colours; nothing was ever written to your save.[/color]

[size=5][color=#F7D994]🎛️  Configuration[/color][/size]
[quote]🎛️  [color=#F7D994][b]Nicer in Mod Nook.[/b][/color] The six colours become editable fields under Colors — the same colours the mirror picker sets, so the world cat and the mirror can never disagree.[/quote]
[color=#D4D4D8]Settings are written to BepInEx/config/com.dirtyredz.moonlightpeaks.purrtasticpalette.cfg on first launch. Every value is a hex code (#FF8800) or an HTML colour name (orange); leave one blank to keep that part vanilla.

Install [url=https://www.nexusmods.com/moonlightpeaks/mods/127][b]Mod Nook[/b][/url] and change them in game instead. Purrtastic Palette shows up in it on its own, under Colors. Nothing here needs it — it just makes this mod easier to live with.[/color]

[size=5][color=#F7D994]🤝  Compatibility[/color][/size]
[color=#D4D4D8]Serena's Enchanted Studio touches the same mirror, but the two do not conflict — install them together or run Purrtastic Palette on its own. Without Serena's, if the swatch template it clones can't be found, the picker falls back to plain drawn swatches rather than failing.[/color]

[size=5][color=#F7D994]💜  Shout outs[/color][/size]
[list]
[*][b]Little Chicken Game Company[/b], for making a game worth spending this much time inside.
[*]The [b]BepInEx[/b] and [b]HarmonyX[/b] teams, without whom none of this scene exists.
[*][b]SerenaEnchanted[/b], whose Enchanted Studio does the mirror-recolour work for the human side.
[*][b]Elsiabeth[/b], for [b]Mod Nook[/b] — settings in-game instead of a text file is the difference between a config being used and being ignored.
[*][b]My Mate[/b], for being my inspiration.
[/list]
```
