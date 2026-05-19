# Requirement Traceability

| Requirement | Subbundle(s) | Proof Required |
|---|---|---|
| H-01 | 01 | Audit doc/checklist and failing or pending regression tests committed before implementation refactor. |
| H-02 | 06 | File split with clean build and no accidental public API churn. |
| H-03 | 02, 03, 07 | Repeated planner and second dream-run integration tests prove persisted cluster IDs and FK integrity. |
| H-04 | 02 | Source-item member tests or explicit contract narrowing with architecture note. |
| H-05 | 03, 07 | Failure injection test proves run marked `Failed` and no partial completed state. |
| H-06 | 03 | Dry-run test proves no writes, or contract is renamed/removed with tests. |
| H-07 | 03 | Mode-policy tests cover every explicit consolidation mode and unsupported-mode behavior. |
| H-08 | 04, 05, 07 | Aggregate text/synthesis tests prove cluster-level brief, no raw dump, and source refs. |
| H-09 | 04 | Validation tests cover contradiction, stale/superseded, generated-only, restricted/redacted, weak evidence, access policy. |
| H-10 | 04, 07 | Double-apply/race/idempotency tests prove one memory record and complete provenance. |
| H-11 | 05 | Recall synthesis tests prove concise merged brief and per-statement source refs. |
| H-12 | 05 | Reference resolver tests prove excluded references hide locator/summary and expose typed exclusion reason. |
| H-13 | 03, 06 | Logs/diagnostics tests or review assertions prove actionable masked state and no silent fallback. |
| H-14 | 06, 07 | `dotnet build` passes for SQLite and PostgreSQL migration projects. |
| H-15 | 07 | End-to-end corpus tests cover the listed adversarial cases. |
| H-16 | 07 | Prior bundle README/execution report or a closure note is updated to reference follow-up completion and remaining risk. |
