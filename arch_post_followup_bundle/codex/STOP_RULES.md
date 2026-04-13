# Stop rules

Stop immediately and open a corrective subbundle if any of the following is true:

1. A self-loop or larger dependency cycle can still be saved or published.
2. `StartRunAsync` still falls back to the first step when no legal roots exist.
3. The canvas topological-order path still silently appends unresolved nodes after the topological pass.
4. The DB still permits multiple `ProcessStepRun` rows for the same `(ProcessRunId, StepDefinitionId)`.
5. The DB still permits duplicate `ProcessRunAssignment` rows for the same logical role scope.
6. `PublishAsync`, `DeleteAsync`, or `ExportAsync` still bypass pending definition-persistence quiescence.
7. An existing-definition editor can still be loaded without `DefinitionConcurrencyToken`.
8. Query-cohesion work keeps the same torn-read pattern or widens into a risky rewrite.
9. Template helper isolation introduces a shared mutable pack across scopes without immutability or defensive cloning.
10. The execution report claims a suite or artifact that the live proof does not actually show.
11. Structural cleanup begins to reopen already-closed correctness invariants.

When a stop rule fires:
- create the corrective subbundle;
- update `codex/MASTER_TASKS.json`, `codex/TASKS.json`, and the gate memo log;
- complete the corrective work and rerun the failed gate before downstream work resumes.
