---
id: dk-cba89af6
type: task
created: 2026-10-05
status: todo
since: 2026-10-05
area:
priority: P2
rank: zzzn
parent:
fixes: []
blocked_by: []
relates: []
---
# Split src/ui/ before it hits the flat-bucket cap

From docs/BACKLOG.md (Placement follow-up (from the 2026-09-01 structure review))

- **P2 — `src/ui/` sits at exactly 12 files, the flat-bucket cap.** Not a violation, but the next UI
  file added forces a split, so it is worth doing deliberately rather than under pressure. The seam is
  already latent in this doc's own file-by-file grouping: panel/swatch *composition* versus *drawing
  primitives* (sprites, icons, palette, scroll/pointer helpers). Roughly 6 files each.
