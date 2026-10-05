---
id: dk-d0f534ca
type: task
created: 2026-10-05
status: done
since: 2026-10-05
area:
priority: P2
rank: zt
parent:
fixes: []
blocked_by: []
relates: []
---
# Unified template-locate into GameTemplate.Find<T>

From docs/BACKLOG.md (Done after the review pass (logic-touching, confirmed in-game 2026-08-22))

- ✅ **Unified template-locate into `GameTemplate.Find<T>`** — absorbed the locate pattern from
  `Templates.Find`, `CatFormSwatch.FindTemplate`, `HeaderDecoration.FindTemplateBar` into one
  `GameTemplate.cs`. The three differing tie-break policies are preserved as per-caller predicates
  (`match`/`preferred`/`secondary`/`fallbackLast`), not flattened; callers keep their own caching +
  projection. Build-verified and in-game smoke-tested (slider, swatches, decorated headers all locate).
