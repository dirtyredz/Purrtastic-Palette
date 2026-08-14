# Hand-off — finish Purrtastic Palette 1.0.0 (folder rename + upload)

Everything for the 1.0.0 release is done and committed **except** the source-folder rename and the
Nexus upload, which are left for a session started **outside** the mod folder (you can't rename a
directory you're running inside).

## Current state

- **Repo:** `C:\Users\dirty\Moonlight Peaks\mods\CatColorProbe` — its own git repo, branch
  **`master`**, HEAD **`e22cb74`**. All work is on `master`.
- **Worktree:** the old `claude/nervous-lalande-480080` worktree has been **unregistered** from git
  (`git worktree list` shows only the main checkout). Its folder under `.claude/worktrees/` was
  still locked by the previous session, so it's left on disk for you to delete (step 1).
- **Build/package:** `pack.ps1` produces `dist/PurrtasticPalette-1.0.0.zip`, verified to lay out as
  `BepInEx/plugins/PurrtasticPalette/PurrtasticPalette.dll`.
- The mod identity is already fully renamed (name, GUID
  `com.dirtyredz.moonlightpeaks.purrtasticpalette`, DLL, namespace, config folder). **Only the
  source *directory* name is still `CatColorProbe`** — cosmetic, doesn't affect the shipped mod.

## Steps (fresh terminal, not inside the mod folder)

### 1. Delete the leftover worktree folder

```bash
rm -rf "/c/Users/dirty/Moonlight Peaks/mods/CatColorProbe/.claude/worktrees/nervous-lalande-480080"
# optional: drop the now-unused branch
git -C "/c/Users/dirty/Moonlight Peaks/mods/CatColorProbe" branch -D claude/nervous-lalande-480080
```

### 2. Rename the folder

```bash
cd "/c/Users/dirty/Moonlight Peaks/mods"
mv CatColorProbe PurrtasticPalette
```

Git content is unaffected (`.git` is inside the folder). The build still works — `Directory.Build.props`
uses absolute Steam paths, not the folder name.

### 3. Fix the path references left behind

These point at `mods/CatColorProbe` and should become `mods/PurrtasticPalette`. **Leave** the
*historical* mentions ("its old CatColorProbe name", "where the old CatColorProbe name came from") —
those are about the past and are correct.

Parent workspace (`Moonlight Peaks/`):
- `16-recolouring-characters.md`
- `17-wardrobe-ui.md`
- `README.md`

Inside the renamed mod repo:
- `README.md` (~line 54) — "the source directory is still `mods/CatColorProbe`" → `mods/PurrtasticPalette`
- `RELEASING.md` (~line 12) — "The source directory is `mods/CatColorProbe`" → update
- `src/Plugin.cs` (~line 18) — `mods/CatColorProbe/README.md` → `mods/PurrtasticPalette/README.md`

Sweep for any missed:
```bash
grep -rIn "mods/CatColorProbe" . ..
```
Then in the mod repo: `git commit -am "Rename source directory to PurrtasticPalette; fix path references"`
(and commit the parent-repo doc edits in that repo).

### 4. Verify the build from the new location

```bash
cd "/c/Users/dirty/Moonlight Peaks/mods/PurrtasticPalette/src"
dotnet build -c Release
```
Expect **0 errors**. It deploys to `BepInEx/plugins/MoonlightPeaksMods/PurrtasticPalette`.

### 5. Final in-game pass, then package

- Run the checklist in `RELEASING.md` — colours; wardrobe tab (preview, swatches, scroll,
  revert-on-cancel, `+` picker, ownership gate); no leak into new-character creation; works with
  Serena's Enchanted Studio disabled.
- From the mod root: `powershell -File pack.ps1` → `dist/PurrtasticPalette-1.0.0.zip`.

### 6. Upload to Nexus

- Create the page. **Name / category / tags / short description** from `nexus-paste.md` → *Other
  fields* (confirm the tags exist in Nexus's fixed vocabulary).
- Paste the BBCode block from **`nexus-paste.md`** into the description (SCEditor source view).
- **Thumbnail** = `screenshots/Purrtastic Palette Thumbnail.png` (16:9), **banner** =
  `Purrtastic Palette Banner.png`. Lead the screenshot gallery with **`Pink Purrtastic Palette.png`**.
- Upload `dist/PurrtasticPalette-1.0.0.zip`.
- Set **MIT** permissions per the table in `RELEASING.md`.
- Add the **1.0.0 changelog** (the block in `NEXUS.md` / `CHANGELOG.md`).

## Done already (commits on `master`)

| Commit | What |
|---|---|
| `cd9cc7c` | Panel scrolls via the wardrobe's own ScrollRect |
| `c5287cb` | Live-preview + revert-on-cancel; centre the `+` tile |
| `6bcf76d` | Paw tab icon (+ PNG override); fix wheel-scroll regression |
| `efb3c7b` | Rename mod identity → Purrtastic Palette |
| `b7fdaca` | Gate tab on Cat Form ownership; fix tab-strip centering |
| `5da0565` | Publish scaffold: 1.0.0, pack.ps1, LICENSE, RELEASING, CHANGELOG |
| `6357f95` | Strip dev tools (F7 probe + give-forms); keep the per-frame reapply loop |
| `e22cb74` | Nexus copy (NEXUS.md, nexus-paste.md) + screenshots |

## Shelved (documented, not lost)

- **WASD / gamepad navigation** between swatches — the wardrobe screen's `selectOnHover` selection
  model keeps nulling a keyboard cursor. Written up in `17-wardrobe-ui.md §7`. Mouse works fully.

## Notes

- The GUID and config path are **not** affected by the folder rename — they're set in code and are
  already `purrtasticpalette`.
- Delete this `HANDOFF.md` once the upload is done; it's not part of the shipped mod.
