# C# Architecture Review Gate

Status: `Pass`

## Gate Questions

| Question | Decision | Evidence |
| --- | --- | --- |
| Is canonical runtime state still independent of read projections? | Pass | Runtime emits lifecycle events; `ProcessRuntimeProjectionProjector.cs` derives records after commit. Runtime has no dependency on the record query/API/Workbench layers. |
| Is the run record explicitly derived, versioned, and completeness-aware? | Pass | `ProcessRunRecordContracts.cs` carries schema version, source sequences, lifecycle, evidence sources, completeness, warnings, and independent facts/narrative states. |
| Are list filters/order backed by scalar indexed columns? | Pass | `EfProcessRunRecordStore.cs` applies scalar filters and `(EndedAtUtc, RunId)` keyset ordering before payload hydration. `20260724224501_AddProcessRunRecords` adds the covering and participant indexes. |
| Are IDs stored without ORM navigation/joins while remaining strongly typed in C#? | Pass | Persistence entities store scalar GUID/string/JSON columns without navigation properties or foreign keys; contracts use typed run, step, plan, definition, and participant identifiers. |
| Is hard-fact finalization idempotent for canonical terminal dispositions, with manager attention events excluded? | Pass | Production-projector tests cover succeeded, failed, cancelled, manager-loop nonterminal escalation, duplicate delivery, reactivation supersession, and later terminal revision. |
| Is stale backfill safe across reactivation? | Pass | Validated backfill rechecks runtime status and the latest terminal/reactivation source inside the guarded store mutation. `Stale_backfill_seed_is_rejected_after_run_reactivation` covers the no-existing-record race. |
| Is LLM generation asynchronous, leased, retryable, and explicitly failed? | Pass | `ProcessRunRecordBatchProcessor.cs` separates facts and narrative claims, retry scheduling, diagnostic references, and non-consuming deferral from runtime completion and GET paths. |
| Is same-source narrative launch atomic? | Pass | `ExecutionRunSourceContracts.cs`, `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`, and `FileSandboxWorkspaceStore.cs` reserve lookup-plus-create under the workspace cross-process lock. `GenerateAsync_TwoWorkersAfterLeaseReclaim_CreateOneSameSourceExecution` proves one provider call and one persisted execution. |
| Are provider, persistence, application, project-structure, and HTTP concerns isolated? | Pass | Agent Framework owns execution reservation, Persistence owns EF storage, Application owns assembly/query seams, Workbench consumes `IProcessRunRecordReader`, and the Web project maps DTOs/routes. |
| Did implementation avoid extending the Workbench god class or adding new partial clusters? | Pass | Durable paging/adaptation/rendering moved into `ProjectStructureProcessRunRecordProjector.cs`; the contributor shrank from 1,622 to 1,380 lines. Workbench contains no `IProcessRunRecordStore` reference and no new partial was introduced. |
| Do normal history paths prove absence of canonical deep hydration? | Pass | Compact list selects scalar summaries only; selected summary/graph loads one record; analytics is one scalar aggregate; terminal workspace/project/dashboard/cost tests use throwing/counting boundaries against hidden deep reads. |
| Are freshness and analytics denominators honest? | Pass | Analytics exposes source-derived `DataThroughUtc` and `SourceGlobalSequenceWatermark`, while list/summary expose `RecordUpdatedAtUtc`. Metrics use facts-available records; complete, partial, and unavailable counts remain separate. |
| Are sensitive evidence bodies excluded and errors masked? | Pass | Generated step `ResultSummary` and raw runtime-event bodies are absent from durable facts, prompts, and HTTP DTOs. Failures retain error class plus diagnostic reference rather than sensitive provider content. |
| Are tests/migration/build/performance findings sufficient for closure? | Pass | Final solution build has zero errors; changed-surface units pass 185/185; an independent gate rerun passes 149/149; record HTTP/project integrations pass 6/6; EF reports no model drift; performance budgets and diff checks pass. |

## Independent Recheck

- Decision: `Pass; no actionable architecture blocker`.
- The independent skeptic explicitly rechecked atomic narrative reservation, stale backfill after reactivation, source-derived analytics watermarks, and the narrow/extracted Workbench boundary.
- It also rechecked canonical direction, terminal classification, relation-free persistence, bounded evidence, privacy, denominator correctness, leased retry behavior, compact/full read separation, background catch-up, and migrated consumers.

## CodeAnalytics Availability

The CanDoItAll CodeAnalytics MCP was not callable. Manual project-reference inspection, exact `rg` searches, solution compilation, focused behavioral tests, integration tests, EF model validation, and a separate read-only architecture review are the compensating controls.

## Residual Operational Limits

- Live PostgreSQL migration/contention execution was unavailable because Docker/PostgreSQL was not running.
- A real provider/LLM narrative run was not executed.
- A host crash after same-source reservation can leave an active Agent Framework execution that defers narrative work until recovery or cancellation.
- These are visible operating/test-environment limits, not hidden fallback behavior or architecture blockers.

## Final Decision

`Pass`
