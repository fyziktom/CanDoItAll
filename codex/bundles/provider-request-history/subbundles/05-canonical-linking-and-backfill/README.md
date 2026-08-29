# SB05 — Canonical Linking And Backfill

## Status

- Execution: Completed

## Objective

- Link and incrementally index retained canonical evidence without duplicated bodies/charges, and prove replay, late commit and deletion across DB and file owners.

## Covered Inputs

- N006–N011; R006–R011, R014.
- [Normalized requirements](../../requirements/01-normalized-requirements.md).

## Prerequisites

- SB03 store/outbox/profile gate and SB04 trusted attempt/capture gate passed.
- Source-specific identity/version mappings are locked by SB01; exact actual save/delete call sites are inventoried.
- No destructive cleanup activation before this phase's deletion/late-owner gate.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowUsageObservationStore.cs`
- `repo://src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime/LlmChatConversationEngine.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageQueryService.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatPersistenceIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/WorkflowUsagePersistenceIntegrationTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderUsageAggregationTests.cs`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/05-history-data-lifecycle.md`
- `bundle://architecture/09-search-security-contract.md`
- `bundle://architecture/10-pricing-and-capture-contract.md`

Linked source context:

[Agent file evidence](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs).
[Workflow canonical store](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowUsageObservationStore.cs).
[Chat conversation engine](C:/repositories/CanDoItAll/src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime/LlmChatConversationEngine.cs).
[Existing usage query](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageQueryService.cs).
[Chat persistence fixture](C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/LlmChatPersistenceIntegrationTests.cs).
[Workflow persistence fixture](C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/WorkflowUsagePersistenceIntegrationTests.cs).
[Usage deduplication fixture](C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/ProviderUsageAggregationTests.cs).
Normative [boundary map](../../architecture/01-csharp-boundary-map.md),
  [lifecycle](../../architecture/05-history-data-lifecycle.md),
  [query/security](../../architecture/09-search-security-contract.md) and
  [pricing/capture](../../architecture/10-pricing-and-capture-contract.md).

## Deliverables

- Existing owners publish metadata-only create/update/delete intent; EF stages in the actual source transaction, file owners use the mandatory durable journal for first canonical creation/attachment and all later mutations/deletions, even after a pending reservation expires.
- Attach trusted owner evidence monotonically; one exact shared observation/attempt has multiple links without another cost-bearing entry.
- Add resumable bounded source-specific legacy backfill and checkpoints with explicit TimeBasis/LegacyAggregate/coverage; source versions update stable identities.
- Repair pending first attach by exact source reference, and separately recover linked-owner updates/deletes from the journal/outbox.
- Implement owner-lifetime deletion/expiry suppression and legitimate late-canonical indexing after orphan expiry; never restore expired bodies or deleted sources.
- Expose lag/coverage/reconciliation errors without scanning sources on Search; activate bounded retention only after these lifecycle gates pass.

## C# Architecture Impact

Owner-specific projection collaborators remain beside source persistence. The neutral query/application does not depend on those owners, and the old aggregate source interface is not repurposed as search.

## Boundary Ownership

Canonical content remains at its original owner. Canonical price/usage is authoritative only at matching granularity: a legacy/operation aggregate supplies lineage/content but never overwrites per-attempt tokens, cost or tariff thresholds. History projection stores minimal scalar facts and links; logical inputs for already tracked calls are never recaptured during pending/recovery.

## Dependency Direction

EF owner adapters may use the same-context staging integration in History.Persistence; all source contracts are neutral. History.Persistence/Application never references concrete chat/agent/workflow modules.

## Pattern Decision

ADR01/03/04: canonical projection, durable outbox/file journal and stable versioned identity. Prepared/committed file operations are explicit; two files are not assumed atomically written.

## Testability Contract

Extend chat/workflow persistence and usage tests; add ProviderHistoryProjectionIntegrationTests for the new cross-source behavior. Proposed cases: Agent_workflow_owner_links_count_once; Legacy_aggregate_is_not_invented_attempts; Source_update_preserves_entry_and_sort_key; Late_canonical_after_orphan_expiry_is_searchable; Delete_tombstone_wins_over_stale_replay; File_journal_recovers_each_commit_handoff; First_canonical_commit_after_orphan_expiry_replays_after_crash.

## Partial Class Policy

No new runtime partial. Existing Razor code-behind/generated files are exceptions only for
their established framework role. New cohesive classes follow the 250-line review and
400-line redesign/exception gate; extraction removes the original behavior.

## Architecture Proof Required

- Record actual changed files, public signatures and project edges against the allowed
  dependency table. Review DI factories and old call sites, not only the new collaborator.
- Trace each actual canonical save/delete to its durable intent and index consumer; show no body serialization in outbox/index and no entire workspace lock held across database/network waits.

## Dependency Impact

- SB06 depends on correct indexed coverage/ownership/retention; SB07 must show honest gaps and content availability.
- Any missed deletion/update path or approximate duplicate detection invalidates query totals/rows and prevents retention activation.

## Validation Depth

- Proof tier: `Governed`.
- Critical foundation: Yes; existing history reuse, deletion safety, multi-owner identity and complete indexed coverage..
- Test project/filter: `C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` / `FullyQualifiedName~ProviderUsageAggregationTests|FullyQualifiedName~AgentProviderUsageObservationAssemblerTests|FullyQualifiedName~ProviderHistoryProjectionTests`; `C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` / `FullyQualifiedName~LlmChatPersistenceIntegrationTests|FullyQualifiedName~LlmChatConversationTransactionIntegrationTests|FullyQualifiedName~LlmChatTurnTransactionIntegrationTests|FullyQualifiedName~WorkflowUsagePersistenceIntegrationTests|FullyQualifiedName~ProviderHistoryProjectionIntegrationTests`.
- Selection reason: Existing canonical persistence/deduplication and actual conversation/turn transaction producers, plus new journal/backfill/owner lifecycle. The conversation and turn transaction fixtures are separate classes in LlmChatPersistenceIntegrationTests.cs and must be selected explicitly.
- Expected discovery: BothEqualsDeduplicatedSourceSum, RetriesAddAttemptCostButOnlyOneOperationExecution, Create_rolls_back_product_and_transcript_when_the_command_fails_after_store_flush and Success_finalization_rolls_back_assistant_usage_and_terminal_status_together, plus all seven proposed projection cases and a source deletion preserving a second legitimate owner. Record exact actual cases/counts at execution;
  zero discovery or a missing named expected case fails the gate. Discovery has not run now.
- Invalidation keys: CanonicalOwnerMapping; FileMutationJournal; SourceOutboxProducer; LegacyBackfillCursor; DeleteReplayFence; ProjectionCoverage.
- Broad-gate decision: Required once at frozen SB08 only if public-contract/schema/DI
  changes made here trigger it. No broad suite here or repeated run without invalidation.
- Future focused commands (after implementing the named cases; use the same unchanged
  source revision for discovery/build and the subsequent no-build execution):

```powershell
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --list-tests --filter 'FullyQualifiedName~ProviderUsageAggregationTests|FullyQualifiedName~AgentProviderUsageObservationAssemblerTests|FullyQualifiedName~ProviderHistoryProjectionTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --no-build --filter 'FullyQualifiedName~ProviderUsageAggregationTests|FullyQualifiedName~AgentProviderUsageObservationAssemblerTests|FullyQualifiedName~ProviderHistoryProjectionTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --list-tests --filter 'FullyQualifiedName~LlmChatPersistenceIntegrationTests|FullyQualifiedName~LlmChatConversationTransactionIntegrationTests|FullyQualifiedName~LlmChatTurnTransactionIntegrationTests|FullyQualifiedName~WorkflowUsagePersistenceIntegrationTests|FullyQualifiedName~ProviderHistoryProjectionIntegrationTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --no-build --filter 'FullyQualifiedName~LlmChatPersistenceIntegrationTests|FullyQualifiedName~LlmChatConversationTransactionIntegrationTests|FullyQualifiedName~LlmChatTurnTransactionIntegrationTests|FullyQualifiedName~WorkflowUsagePersistenceIntegrationTests|FullyQualifiedName~ProviderHistoryProjectionIntegrationTests'
```

## Implementation Steps

1. Instrument actual source commit/update/delete using typed intent/version mappings; test atomicity before backfill.
2. Implement pending attach and source journal/outbox replay with exact source lookups and monotonic updates.
3. Build scalar DB keyset backfill and bounded resumable file manifest enumeration; release source locks before DB work.
4. Add owner lifetime/deletion/late-source rules, tombstone retention and explicit projection coverage/errors.
5. Verify old/new mixed evidence, retries, duplicate source observation and profile transfer before enabling cleanup.

## Acceptance Checklist

- [x] Existing retained canonical records older than30d remain indexable without copied transcripts; actual authorized range search is SB06.
- [x] One physical observation/attempt with multiple owners is not charged twice; identical unrelated calls stay separate.
- [x] Aggregate canonical owner amounts never overwrite or get guessed/distributed into per-attempt costs; the H09 granularity fixture proves this.
- [x] Source version changes preserve EntryId/SortAtUtc; legacy actual-start/attempt fields are not fabricated.
- [x] Late trusted canonical commit can index after orphan expiry; expired detail and newer deleted source cannot revive.
- [x] Journal recovery covers first canonical creation after orphan expiry and crash-before-publication, plus all linked-source updates/deletes; a pending entry is only supplementary.
- [x] Backfill runs only in maintenance with durable coverage/error checkpoints; SB06 must expose them without source work on Search.
- [x] Safety markers survive every possible stale replay; their eventual purge requires drained/reconciled source generations.

## Proof Required

- Store a proof manifest, exact command transcripts, discovered cases/exit codes, changed-source revision, artifact paths/hashes and semantic positive/negative evidence under `proof/SB05/` at the bundle root.
- Record real producer/intent/consumer artifacts and fault-injection transcripts at every DB/file handoff; include old retained fixtures, multi-owner row/usage counts, bounded file batch/lock measurements and tombstone replay cases.
- Follow [validation strategy](../../plan/02-validation-strategy.md); distinguish existing
  test anchors from proposed new cases, and source proof from executed behavior.

## Browser Validation Logging

N/A for direct UI changes in this phase. Production host/SQL/lifecycle proof is required where listed; the two-tab desktop acceptance remains SB07/SB08.

## Scope Exceptions

- This phase alone does not close the complete product request. Deferred IDM/EGCP person
  mapping, global federation, exact wire replay, mobile redesign and unrelated refactors
  remain outside the bundle.
- No paid inference, user-database mutation or deployment without explicit authorization.

## Do Not Do

- Do not copy a pending canonical prompt as fallback or add the history index as a duplicate ProviderUsageProjectionSource.
- Do not deduplicate by provider/model/time/text equality or infer attempts from old aggregated usage.
- Do not mark coverage complete because a search returned no rows; do not purge a tombstone while stale replay is possible.

## Progression Gate

- SB06 and retention activation require canonical ownership, replay/delete/late-commit and bounded-backfill proof, including actual source save/delete producer paths.
- Update [execution report](../../reviews/01-execution-report.md) with actual proof and
  downstream dependencies checked. A planned command or passed intermediary is not closure.

## Reopen Triggers

- New owner/source format, delete path, replay horizon, legacy identity mapping or retention policy invalidates this phase and dependent query/UI completeness.
