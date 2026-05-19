# Target Solution

## End State

- Cognitive Memory P0 is implemented as small, testable changes that reduce file ownership risk and add explicit operational paths.
- Existing behavior remains compatible for callers unless an error policy is intentionally tightened for process-critical agent execution.
- Docs and roadmap reflect the real post-P0 stage.

## Boundaries

- Durable memory stays EF-backed through `AppDbContext`.
- Projection rebuild uses the existing projection lifecycle service and adapter; Qdrant/RAG remains a rebuildable projection.
- Scheduled automation is explicit and observable; it must not create hidden uncontrolled writes.
- MAF contribution returns agent-facing context messages and metadata, not raw diagnostic recall payloads.

## Allowed Side Effects

- Add focused classes/files under the Cognitive Memory module and web API.
- Add focused tests.
- Update docs and bundle artifacts.
- Avoid schema changes unless required by source reality.
