---
id: dk-1abed394
type: task
created: 2026-10-05
status: todo
since: 2026-10-05
area:
priority: P2
rank: zzt
parent:
fixes: []
blocked_by: []
relates: []
---
# Separate the fur/eye/aura strategies from the patch + traversal in CatColorPatch

From docs/BACKLOG.md (P2 — deferred / nice-to-have)

- **Separate the fur/eye/aura strategies from the patch + traversal in `CatColorPatch`** (the larger
  seam). Keep them as sibling methods — don't over-split. A standalone `PropertyBlockWriter.cs` is
  **not** recommended (Codex: over-abstraction, a 2-caller impl detail with no independent policy). — *[verify in-game]*
