# SB09: initial catalog overlap and nested editor dialogs

Status: implemented and scoped proof passed, 2026-09-05; the unchanged repository documentation gate remains blocked. Baseline: clean components-decoupling at d3ba280a431bfe74ce03a72638ac06dff47de660. The owner requests both final lifecycle repairs, then preparation, implementation and testing of the next provider state/read slice.

Owned review notes: a newer successful reload must end initial presentation loading; stale initial success/failure must not replace it or publish failure; each initial task releases its own slot. Every direct nested editor dialog must use its editor session token; disposal/replacement closes owned dialogs while preserving unrelated presentations.

Scope: AgentCatalogHost load coordination and the three direct DialogService calls in AgentDetailsDialog. Preserve create during initial loading, successful save/delete/capability/approval semantics, existing parent echoes, and current UI composition. No new projects, interfaces, routing, shared-library change or provider work in this phase.

Entry gate: Pass. Source confirms both defects; SB08 outcome, fail-closed editor loading and top-level dialog ownership remain trusted. Compatible manual bundle shape retained. C# ownership stays with the effect host and per-editor session; no new partial type or project reference is justified. Reject global CloseAll or disabling create as a substitute for overlap proof.

Behavioral proof: add eight focused component cases (two initial late outcomes; three nested dialog kinds times disposal/replacement). Freeze filter and confirm expected discovery before execution. Use the real editor Save/Saved callback path with a delayed catalog port; inspect public rendering and DialogService references, never private fields. The prior implementation must fail meaningful regression assertions. Build Modules.AgentFramework directly and refresh Components. Then run the bounded existing catalog/editor lifecycle topics with recorded discovery. No broad stable trigger: no schema, build or cross-cutting composition changes.

UI: retain large-desktop catalog and full editor, compact confirmations and wide capability wizard. No visual redesign; verify nested overlays and startup/disposal through the real browser at 1600x1000. Catalog remains the primary surface; editor body retains scrolling and visible actions.

Closure: focused regression and owning tests, final portability enforcement, reviewed source/architecture, browser ownership check, compact execution note and bundle hashes. Historical proof artifacts stay unchanged. Existing tracked-log documentation blocker is reported separately. PROVIDERS-01 may proceed only after source/tests prove this foundation; reopen its dependent lifetime assumptions if these tests fail later.

## Execution result

All eight regressions failed meaningfully before the repairs and passed afterward. The changed production module built directly; existing adjacent lifecycle coverage also passed. The later Providers-01 analyzer produced its own conservative broad-gate trigger, recorded in that child's validation scope; it does not retroactively change this SB09 entry plan. Final no-write portability enforcement passed with 14251 unchanged findings. Real browser inspection covered the bounded nested wizard and parent close; selective disposal/replacement with an unrelated dialog is proven deterministically by components. See [current closure](../../reviews/05-sb09-closure.md).
