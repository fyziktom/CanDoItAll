# 01 Query Shape And Architecture Repairs

## Status

- `Completed`

## Objective

Repair concrete Cognitive Memory query-shape defects found during senior architecture, .NET performance, and EF Core review.

## Success Criteria

- Recall lexical scans order and limit records/source items in the database before materialization.
- Signal queries apply recency and access filters before page limiting.
- Regression coverage proves a newer signal is not hidden behind older records.
- No public API route or persistence schema contract changes.

## Covered Inputs

- SR-001, SR-002, SR-003, SR-010, SR-011, SR-012.

## Prerequisites

- Existing architecture and follow-up bundle completed-stage validators pass.
- Performance and EF query scans are recorded in `analysis/01-current-state.md`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Signals\CognitiveMemorySignalServices.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemorySignalLedgerTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryRecallOrchestratorTests.cs`

## Deliverables

- Server-side `OrderByDescending`/`Take` for recall lexical record and source-text queries.
- Correct signal query ordering: filter by `SinceUtc`, enforce policy access, order by `ObservedAtUtc`, then take the requested page.
- Unit regression test `QueryAsync_AppliesSinceFilterBeforePagingSignals`.

## Dependency Impact

- API memory-quality validation depends on this phase.
- Weak proof here would make later recall/probe results untrustworthy because the agent-facing memory context could be incomplete or stale.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Inspect recall lexical activation and signal query code.
2. Move ordering/limits before materialization where behavior is unchanged.
3. Add a focused signal ledger regression test.
4. Run targeted recall and signal tests.
5. Record proof in `reviews/01-execution-report.md`.

## Scope Exceptions

- Large-file splits in recall, advanced services, API route mapping, and Blazor page are not completed in this phase because the safe repair is query-shape focused.
- Provider-specific case-insensitive search tuning is deferred; this phase preserves current cross-provider behavior.

## Do Not Do

- Do not change API routes.
- Do not change database schema.
- Do not add a new repository or abstraction layer for these small query repairs.
- Do not introduce silent fallback behavior.

## Acceptance Checklist

- [x] Prior bundle validators pass.
- [x] Performance/EF scan findings recorded.
- [x] Recall query materialization reduced.
- [x] Signal query paging bug fixed.
- [x] Targeted unit tests pass.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemorySignalLedgerTests|FullyQualifiedName~CognitiveMemoryRecallOrchestratorTests" --logger "console;verbosity=minimal" -m:1`

## Browser Validation Logging

- N/A - no browser-visible UI, route markup, or host-visible behavior changed.

## Progression Gate

- Downstream API validation may continue only after targeted recall/signal tests pass and the execution report records the query-shape repair proof.

## Suggested Agent Prompt

```text
Implement this subbundle only. Keep the repair inside existing Cognitive Memory services, preserve public contracts, add focused regression coverage, run targeted tests, and stop if query-shape proof cannot honestly pass.
```
