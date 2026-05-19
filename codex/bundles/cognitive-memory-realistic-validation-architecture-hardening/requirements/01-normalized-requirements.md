# Normalized Requirements

## Functional Requirements

- FR-01: Provide a repeatable clean-validation environment flow for PostgreSQL and Qdrant.
- FR-02: Make the active Cognitive Memory database/profile visible in API status and operator UI.
- FR-03: Add source-truth transfer support for external files/data manifests with content hashes, redaction policy, and exclusions.
- FR-04: Preserve explicit policy context across consolidation, quality planning, dreaming, probes, recall, and review decisions.
- FR-05: Preserve explicit vector projection options across recall and probe recall.
- FR-06: Add operator-visible reasons when restricted source truth is excluded from a run.
- FR-07: Add resumable consolidation/dreaming cursors when budgets stop before source-truth evaluation is complete.
- FR-08: Improve dream aggregate text so approval candidates carry concrete source-backed facts, not only structural labels.
- FR-09: Add durable audit rows for dream aggregate review decisions and application state changes.
- FR-10: Add long-run validation orchestration with cycle IDs, operation IDs, approval checkpoints, trend metrics, and stop criteria.

## Non-Functional Requirements

- NFR-01: Keep UI proof large-screen only unless a later bundle explicitly adds responsive work.
- NFR-02: Do not silently fall back from Qdrant/vector recall to lexical recall without a clear stage trace and UI warning.
- NFR-03: Treat Qdrant as a rebuildable projection; PostgreSQL remains the source of truth.
- NFR-04: Avoid direct writes to Cognitive Memory truth tables outside application services.
- NFR-05: All potentially long lists must remain server-paged or cursor-based.
- NFR-06: Validation failures must produce actionable trouble rows, not only log messages.

## Out Of Scope

- Replacing the existing Review UI service boundary.
- Adding mobile/tablet layouts.
- Introducing a new vector database.
- Approving all generated memories automatically.
