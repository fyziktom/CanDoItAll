# Current State

The last commit added the expected quality-foundation surface area, but the shape is still closer to a broad scaffold than a hardened production subsystem.

## What The Last Commit Added

- New contracts and service interfaces in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityContracts.cs`.
- New persistence records and EF mappings in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality`.
- New migrations for SQLite and PostgreSQL.
- New services in `CognitiveMemoryQualityServices.cs` for diagnostics, cluster planning, dream consolidation, validation, aggregate application, recall synthesis, reference resolving, support loading, and text helpers.
- Module registration for those services in `CognitiveMemoryModuleServiceCollectionExtensions`.
- A recall focus fix in `CognitiveMemoryRecallEvaluation.SelectFocus` so `SideContext` and `Excluded` candidates are preserved.
- Unit tests for happy-path clustering, dream runs, validation, aggregate apply, recall synthesis/reference lookup, and the recall focus fix.
- Integration tests for EF model registration.

## Review Findings

1. `CognitiveMemoryQualityServices.cs` is too large and mixes application service orchestration, persistence, text normalization, policy checks, support loading, and synthesis utilities in one file. This is hard to review and raises regression risk.
2. Cluster planning persists only new cluster hashes. If a hash already exists, the method skips persistence but returns a newly generated `CognitiveMemoryQualityClusterId` in the result. Downstream dream runs can then reference cluster IDs that do not exist in the database.
3. Cluster planning exposes `SourceItem` as a member kind but currently plans only memory-record members. Source items influence keys through existing memory support, but source-item clustering is not really implemented.
4. Dream runs save a `Running` record before cluster planning and candidate creation, then rely on a happy path to mark `Succeeded`. There is no obvious failure update path that marks the run `Failed` with a failure code/message.
5. `CognitiveMemoryDreamRunRequest.PersistChanges = false` does not read as a true dry run. The dream service still creates a dream run and candidate/validation records; only the cluster planner receives the flag.
6. Dream mode behavior is thin. `ProjectNightly`, `ProcedureMining`, `FailureLearning`, and `KnowledgeCoverageRefresh` get limited filters, while other explicit modes fall through to broad selection.
7. Aggregate candidate creation is essentially a bullet list of selected source records. It does not yet provide a true cluster-level synthesis strategy or clear separation between deterministic and semantic synthesis.
8. Validation catches missing source maps, weak evidence, restricted/redacted evidence, stale sources, contradictions via attacking source maps, and generated-only sources. It does not prove all relevant contradiction paths from relation records or temporal supersession paths.
9. Aggregate application has useful provenance writes but needs stronger idempotency/race coverage and should support legitimate evidence-anchor/source-memory cases without requiring every source map to have a source item.
10. Recall synthesis currently trims the first line of each selected context section and formats bullets. That proves references are hidden, but not that the consumer gets a synthesized brief.
11. Tests that passed during review are useful but insufficient: they are mostly first-run happy paths, not adversarial repeat-run or failure-path proof.

## Validation Baseline

- Prior bundle structural completion validator passed.
- Targeted unit slice passed 22 tests.
- Targeted integration slice passed 3 tests.
- These passing tests should be preserved, but they are not sufficient closure evidence for the follow-up.
