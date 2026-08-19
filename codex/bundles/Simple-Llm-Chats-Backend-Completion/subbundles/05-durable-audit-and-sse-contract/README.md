# SB05 — Durable Audit And SSE Contract

## Status

- `Complete — Pass`

## Objective

- Make durable invocation, operation status, and SSE completion evidence complete, sanitized, consistent, and monotonic across restart and retention.

## Success Criteria

- Operation status includes bounded ordered allowlisted invocation evidence and no internal fingerprint/sensitive audit fields.
- Attempt completion durably preserves and emits model, finish reason, delivery mode, usage, ordinal, and outcome.
- Stream-limit failure has one typed classification across provider adapter, invocation row, operation, and terminal SSE.
- Event high-water is atomically durable, migration-backed, transferred, and does not regress when event rows are deleted.
- EF migration/model snapshot/pending-model check agree.

## Covered Inputs

- BC-040 through BC-044.

## Prerequisites

- SB04/CP1 `Pass`.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Operations/LlmChatInvocationRecord.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Operations/LlmChatOperationEvents.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationDetailsReader.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatStreamingPipeline.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/AuditedLlmChatStreamingInvocationPort.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Entities/LlmChatPersistenceRows.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/EntityConfigurations/LlmChatOperationConfigurations.cs`
- `repo://src/App/CanDoItAll.Web/Api/LlmChatOperationApiContracts.cs`
- `repo://src/App/CanDoItAll.Web/Api/LlmChatOperationApiMapper.cs`
- `repo://src/App/CanDoItAll.Web/Api/LlmChatOperationEventApiMapper.cs`
- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql`

## UI Composition Contract

- N/A — API/SSE transport only.

## Deliverables

- Sanitized invocation response DTO and mapper with explicit collection bound.
- Durable completed-attempt model/finish reason/delivery mode fields and v1-compatible SSE projection.
- Typed consumer-abort/stream-limit audit path.
- Operation event high-water domain/entity/repository/migration/transfer field.
- Updated docs and schema/transfer parity.

## Dependency Impact

- Blocks replay/retention and final SSE proof; SB06 consumes the high-water and completed evidence.

## Validation Depth

- Proof tier: `Behavioral`.
- Test solutions: Unit and Integration lanes.
- Filters: exact new methods in `LlmChatDurableStreamEventTests`, provider runtime/audit tests, `LlmChatOperationEventJournalIntegrationTests`, and `LlmChatsApiPostgreSqlIntegrationTests`.
- Selection reason: domain mapping plus real durable restart/API/SSE/migration behavior.
- Expected named cases: `Operation_status_returns_bounded_sanitized_invocation_attempts`, `Invocation_projection_excludes_profile_name_id_correlation_and_raw_failure`, `Completed_event_preserves_model_finish_reason_delivery_mode_and_usage`, `Stream_limit_records_one_consistent_failure_across_all_evidence`, `Event_append_advances_high_water_atomically`, `High_water_survives_full_event_retention_and_restart`, `Migration_backfills_high_water_from_existing_events`, and `Database_transfer_round_trips_completion_and_high_water_fields` (8 cases).
- Invalidation keys: invocation/event/operation domain contracts, audited port, entity/configuration, migration/snapshot, transfer DTO, API/SSE mapper/schema.
- Broad-gate decision: deferred to SB10 for schema/migration/public-contract changes.

## Implementation Steps

1. Add failing API/security tests with multiple attempts and sentinel internal fields; define the exact invocation collection maximum.
2. Extend durable attempt/event contracts for bounded finish reason and delivery mode without provider-name inference or raw response capture.
3. Introduce a typed consumer-abort path so stream bounds do not masquerade as provider unavailability.
4. Add operation high-water; update it in the same locked transaction as event append and consume it in details/SSE gap mapping.
5. Add one migration and model snapshot update with SQL backfill from existing event maximum; update transfer schema/mapper.
6. Prove restart, full retention, migration from pre-change state, and transfer round-trip using PostgreSQL.
7. Build Abstractions/ProviderRuntime/Core/Persistence/Web/Migrations as changed; list/run exact cases and pending-model check.

## C# Architecture Impact

- Adds domain facts and persistence schema inside existing owners; public DTO remains an allowlist.

## Boundary Ownership

- Core owns facts/limits; Persistence stores them; Web projects a sanitized subset; ProviderRuntime reports provider-neutral completion.

## Dependency Direction

- No Web-to-Persistence or Core-to-EF edge; external finish reason remains bounded protocol text.

## Pattern Decision

- PSR-05 and PSR-10; typed abort classification, not disposal inference.

## Testability Contract

- Evidence must be reloaded after a new scope/restart and after event deletion; no in-memory result may satisfy persistence proof.

## Partial Class Policy

- No partial types.

## Architecture Proof Required

- Schema/contract ownership review, DTO secret allowlist, append transaction proof, and pending-model result.

## Scope Exceptions

- Finish reason is persisted/exposed as bounded provider-neutral protocol text; this bundle does not add provider-specific success inference.

## Do Not Do

- Do not serialize internal invocation records directly or expose provider profile/correlation/raw errors.
- Do not derive high-water from retained rows after this migration.

## Acceptance Checklist

- [x] Eight named cases discover and pass.
- [x] API/SSE secret scan passes.
- [x] High-water survives retention/restart/transfer.
- [x] Migration snapshot and pending-model check pass.
- [x] Changed project Release builds pass.

## Proof Required

- Failing-first/passing transcripts, exact discovery, serialized API/SSE samples, durable row/migration/transfer evidence, secret scan, pending-model output, and build results under `proof/SB05`.

## Browser Validation Logging

- N/A — HTTP/SSE only.

## Progression Gate

- SB06 starts only when all three evidence views agree and high-water is durable.

## Reopen Triggers

- Any later invocation/event/operation contract, mapper, migration, repository append, transfer, or stream-limit change reopens SB05-SB10.

## Suggested Agent Prompt

```text
Execute SB05 only. Complete and sanitize durable operation/invocation/SSE evidence, add atomic high-water, and prove it after restart and retention. Stop on schema/transfer/pending-model disagreement.
```
