# Implementation Prompt

Implement `01-ef-query-hotspots-and-repair` only.

- Preserve the switchable `AppDbContext` architecture and all public service contracts.
- Patch only high-confidence EF query-shape issues: materialization before order/filter/take and missing no-tracking in read-only paths.
- Do not add provider-specific SQL or schema changes.
- Keep write paths tracked when the loaded entity is mutated.
- Validate with targeted tests and build proof, then update `reviews/01-execution-report.md`.

