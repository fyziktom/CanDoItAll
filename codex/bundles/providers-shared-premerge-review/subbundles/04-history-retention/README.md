# SB04 — Bounded orphan input-detail cleanup

## Status

- State: `Ready`
- Proof tier: Behavioral
- Execution: not started; this file is a plan, not proof.

## Objective

Expired orphan input details are eventually deleted while retained retry references, partition isolation and quota remain correct.

## Covered Inputs

- R04/R10; N03/N04/N06; PERF-01

## Prerequisites

- Baseline reconciled; PostgreSQL fixture available for implementation proof. No production DB mutation.
- Read root constraints, analysis evidence and plan/02-validation-strategy.md before edits.

## Exact Source References

- `repo://src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/HistoryRetentionStore.cs`
- `repo://src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/HistoryDetailStore.cs`
- `repo://src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/HistoryDetailConfiguration.cs`
- `repo://src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/HistoryEntryConfiguration.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProviderHistoryPersistenceIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProviderHistorySourceProjectionIntegrationTests.cs`

repo:// paths resolve from the product repository; bundle:// paths resolve from this bundle. Absolute SharedInfo references identify the inspected sibling checkout; resolve its actual root with the shared-standards skill when executing elsewhere. Planned new tests below are not claimed to exist.

## Deliverables

- Add failure-first lifecycle fixture: multiple attempts share one input revision, complete/expire them and run bounded maintenance until idle.
- Delete expired zero-payload input rows only after no entry references them; account for transaction ordering, concurrent retry attachment and FK protection.
- Keep batch bounds/cancellation and existing detail-byte counter semantics. Add an index/migration only if SQL-plan evidence justifies it; never edit already-applied migrations.
- Check transfer behavior after cleanup and measure backlog/candidate query cost at representative retained volume.

## Dependency Impact

- Critical lifecycle foundation; unlocks SB05 capacity evidence and SB07 retention docs. Any schema change invalidates SB08 SQL/export and SB09 upgrade proof.
- Reopen on changes to: retention SQL, detail/entry FK/indexes, quota, input deduplication, transfer mappings, migrations.

## Validation Depth

- Proof tier: Behavioral.
- Test project/check selection: Integration ProviderHistoryPersistenceIntegrationTests and ProviderHistorySourceProjectionIntegrationTests.
- Selection reason: tests own the changed behavior and concrete regression; no unrelated suite substitutes for missing cases.
- Expected discovery: existing selected classes must be nonzero; enumerate and freeze their exact current FQNs/data-row counts before execution. The following exact named/scenario cases are required, with planned new-case counts where stated:
- ExpiredOrphanInput_IsDeletedAfterFinalAttempt (1)
- RetainedRetry_PreservesSharedInput (1)
- ConcurrentInputAttachmentAndCleanup_PreservesReferences (1)
- Cleanup_IsBoundedAndPartitionIsolated (1)
- Invalidation keys: retention SQL, detail/entry FK/indexes, quota, input deduplication, transfer mappings, migrations.
- Broad-gate decision: No broad test run here. Migration/schema trigger runs once at SB09 freeze if changed.

## Acceptance Checklist

- [ ] One still-retained retry prevents input deletion; last-reference expiry allows eventual deletion.
- [ ] No cross-partition deletion, active-attempt deletion, retained canonical-content deletion, quota underflow or unbounded scan/materialization.
- [ ] Repeated bounded passes finish; independent concurrent insertion/cleanup is safe.
- [ ] Keep strong identifiers/enums, explicit errors, safe logs, Egyptian braces and one statement per line.
- [ ] No production XML comments, unrelated refactor, silent fallback or inferred permission expansion.

## Proof Required

- Follow plan/02-validation-strategy.md for exact Release build/discovery/test command form; record commands, exit codes, expected/actual cases, source hashes and dependency mode.
- A zero-byte tombstone or missing HistoryEntry count is not cleanup proof. Assert the orphan detail itself is gone and referenced detail still exists.
- Record realistic positive and adversarial negative proof, source producer/consumer/lifecycle assertions where applicable, and anti-stub review. Failing-first proof must exercise the reported defect.
- Record evidence in reviews/01-execution-report.md; separate governed manifests are not required for this unit.

## C# Architecture Impact

History.Persistence owns storage cleanup behind existing services. Preserve pure capture and application authorization boundaries; no new repository abstraction needed.

## Boundary Ownership

- Keep the responsibility in the named current owner. Any extraction must be independently testable and remove moved logic from the old class.

## Dependency Direction

- Preserve architecture/02-csharp-dependency-direction.md; no new project/reference is assumed. If needed, stop that edit and amend the boundary/checkpoint before proceeding.

## Pattern Decision

- Follow architecture/03-csharp-pattern-selection-records.md. Prefer current adapters/decorators and small functions; avoid abstractions without a concrete boundary.

## Testability Contract

- Pure policies use direct isolated tests; persistence/network behavior uses the selected integration seam and a real production consumer. Do not construct the full runtime for a pure rule.

## Partial Class Policy

- No new runtime partial. Existing generated code and cohesive UI code-behind are allowed; no nested service used to hide responsibility.

## Architecture Proof Required

- Relevant checkpoint: plan/architecture-checkpoints.md. Review .csproj diff, policy placement, production registration, independent tests and no-new-partial proof.
- If behavior is extracted, show old-owner shrink/thin facade and a negative test rejecting delegation back to the monolith. No extraction is required solely for this metric.

## Progression Gate

- Pass only after acceptance and required proof agree; otherwise record precise failed/blocked cases.
- Critical lifecycle foundation; unlocks SB05 capacity evidence and SB07 retention docs. Any schema change invalidates SB08 SQL/export and SB09 upgrade proof.
- Scope beyond the listed repair, new wire support, database destruction, hosted authority or installed-path permission must be handled explicitly; finish all unaffected authorized work first.

## Non-goals

- No merge/push/deployment, paid upstream call, unrelated sibling refactor, invented remote history API or broad UI redesign.
