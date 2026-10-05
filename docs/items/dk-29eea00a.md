---
id: dk-29eea00a
type: task
created: 2026-10-05
status: done
since: 2026-10-05
area:
priority: P2
rank: zzn
parent:
fixes: []
blocked_by: []
relates: []
---
# Fixed the eye-recolour freeze (MaxRecolorDimension cap)

From docs/BACKLOG.md (Done after the review pass (logic-touching, confirmed in-game 2026-08-22))

- ✅ **Fixed the eye-recolour freeze (`MaxRecolorDimension` cap)** — the cat eye atlas ships at
  **4096²**; the per-pixel HSV loop over 16.7M pixels froze the game ~1.7s per build, ×2 atlases, on
  every Cat-Form transform, wardrobe tab-open, and eye-colour change. Profiled it (stopwatch: ~85% is
  the CPU loop), proved it predates all recent refactors *and* is independent of Fangtastic (repro'd
  on a first-version, Fangtastic-free PC). Fix: cap the recolour working resolution at 512²
  (`TextureRecolor.MaxRecolorDimension`) — the source is downscaled on the blit before the pixel work,
  so builds drop **~1700ms → ~30ms**. Justified because the eye is tiny on screen and Fangtastic's
  smaller bat eye atlas already recolours fine at low res. Confirmed in-game: no freeze, and both eyes
  and fur still crisp at 512². The cap is global, so the 1024² fur/whisker atlases are downscaled too;
  if fur ever needs to stay sharp, make the cap per-caller (eyes low, fur high) rather than raising it.
