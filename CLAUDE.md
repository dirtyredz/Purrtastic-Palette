# CLAUDE.md — working on Purrtastic Palette

This is a **standalone git repo** nested in the Moonlight Peaks workspace. When you're editing files
here, **this** repo is the active project — honor its gate and baseline, not the workspace root's.

## Orientation — read the maps, don't reconstruct
- **[README.md](README.md)** — what it is + the deep recolour findings (how it works, gotchas).
- **[STRUCTURE.md](STRUCTURE.md)** — where code lives; the three layers; structural debt.
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** · **[DECISIONS.md](docs/DECISIONS.md)** ·
  **[FEATURES.md](docs/FEATURES.md)** · **[ROADMAP.md](docs/ROADMAP.md)** ·
  **[BACKLOG.md](docs/BACKLOG.md)** · **[GOTCHAS.md](docs/GOTCHAS.md)**

## What this mod is
BepInEx 5 / HarmonyX plugin for the Unity Mono game *Moonlight Peaks* (netstandard2.1). Recolours
Cat Form from config and from a wardrobe tab. Published: Nexus mod 142.

## Conventions
- **Layout:** plugin `.cs` are foldered by responsibility — `src/game/` (Harmony patches +
  live-game bridges), `src/ui/` (panels, widgets, sprites, dialogs), `src/core/` (the mod's own
  logic/state). Only `Plugin.cs` stays at `src/` root beside the `.csproj`; docs + `pack.ps1` at
  the repo root. The homes are declared in [STRUCTURE.md](STRUCTURE.md#layout) under `## Layout` and
  a placement hook enforces them — declare a new folder there before putting code in it.
- **Version:** single-sourced from `src/PurrtasticPalette.csproj` `<Version>` via `GenerateModBuildInfo`
  (`Directory.Build.props`) → `ModBuildInfo.Version`. Never hardcode a version in `Plugin.cs`. Bump
  only when publishing.
- **Synced canonicals:** `pack.ps1` and `Directory.Build.props` come from
  `../../tools/sync-mod-files.ps1` — don't hand-edit them here.
- **Commit identity:** `dirtyredz <dirtyredz@live.com>`.
- **Recolour methodology** is shared *as docs* with the sibling *Palette* mods
  ([../../16-recolouring-characters.md](../../16-recolouring-characters.md)); **do not** refactor
  recolour code across repos — each mod builds and ships standalone.

## Build / release
- Build/pack: `pack.ps1` → `dist/PurrtasticPalette-<version>.zip` (Nexus layout). Building needs the
  game's managed DLLs at the Steam paths in `Directory.Build.props`.
- **Do not launch the game** unless asked; it can't be driven from CI. In-game behaviour must be
  verified by the user before shipping a behavioural change.

## Structure-review gate
This repo is gated (pre-push hook). Edit/debug/commit freely; the review fires once at **push** on
the accumulated change. Claude runs the review and pushes (asking first) when work is ready.
`/gate status` shows what's pending.
