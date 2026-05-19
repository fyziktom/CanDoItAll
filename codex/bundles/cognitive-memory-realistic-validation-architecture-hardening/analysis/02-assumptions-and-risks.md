# Assumptions And Risks

## Working Assumptions

- The intended source-truth transfer target is a clean validation database.
- Copying cognitive-memory source manifests/items/evidence anchors is valid only when target memory rows do not already depend on conflicting source-truth rows.
- API additions can be additive and do not need a version bump.

## Critical Path Risks

- EF model changes need migrations for both SQLite and PostgreSQL.
- Source-truth transfer can corrupt an existing target if dependent memory/link rows already reference source-truth rows; the handler must refuse unsafe replacement.
- Multi-cycle automation must not create an unbounded loop.

## Validation Risks

- PostgreSQL/Qdrant services may not be locally available during this implementation turn, so focused service tests must prove contract behavior and the realistic soak remains follow-up evidence.
- Static asset diagnostics are environment-dependent, so tests should assert shape and values rather than one machine-specific path.

## Reopen Triggers

- Any probe asks that still ignore stored policy or projection options.
- Any transfer handler that drops `ContentText`, source locators, or evidence anchors.
- Any dream aggregate that copies restricted source text into candidate text.
- Any scheduled automation run that cannot expose cycle id, cursor continuation, and per-cycle results.
