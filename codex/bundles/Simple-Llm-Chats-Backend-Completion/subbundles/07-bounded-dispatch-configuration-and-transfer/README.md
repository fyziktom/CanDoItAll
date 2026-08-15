# SB07 — Bounded Dispatch, Configuration, And Transfer

## Status

- `Ready`

## Objective

- Turn the documented streaming/dispatch/retention bounds into validated runtime configuration, allow bounded independent progress, and make transfer safe and complete for the final schema.

## Success Criteria

- Typed streaming/dispatcher/transfer limits bind from configuration and fail startup on invalid combinations.
- Chunk/aggregate/persistence bounds share canonical constants and cannot contradict each other.
- Configured worker concurrency is enforced, unrelated conversations progress, same-conversation/durable claim invariants hold, and shutdown drains.
- Queue age and total operation duration produce typed durable evidence without unsafe redispatch.
- Transfer validates complete enum/state/relationship invariants and rejects over-bound input before graph materialization.

## Covered Inputs

- BC-044, BC-060 through BC-065.

## Prerequisites

- SB06 `Pass`; final event/audit/high-water schema is known.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatStreamingOptions.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/LlmChatsModuleServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationDispatcher.cs`
- `repo://src/App/CanDoItAll.Composition/LlmChatOperationDispatcherHostedService.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/DatabaseTransfer/LlmChatsTransferDocument.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/DatabaseTransfer`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/EntityConfigurations/LlmChatOperationConfigurations.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatOperationTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatProviderRuntimeTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatPersistenceIntegrationTests.cs`

## UI Composition Contract

- N/A — hosting/configuration/transfer backend only.

## Deliverables

- Configuration-bound `LlmChatStreamingOptions` and dispatcher/transfer options with `ValidateOnStart`.
- Shared constants/invariants aligning message, event payload, aggregate, replay, cleanup, age, duration, concurrency, and import limits.
- Bounded database-backed worker fan-out with safe queue/operation expiration.
- Complete bounded transfer validation and final schema parity.
- Safe structured saturation/expiration operational logging.

## Dependency Impact

- Completes CP2's runtime/schema/transfer surface and blocks final focused architecture/SSE checkpoint.

## Validation Depth

- Proof tier: `Behavioral`.
- Test solutions: Unit and Integration lanes.
- Filters: exact new options/dispatcher/transfer cases under `CanDoItAll.Tests.Unit.LlmChats` and LLM Chat persistence integration.
- Selection reason: startup binding and deterministic worker caps can be direct; leases/transfer require durable integration.
- Expected named cases: `Configured_streaming_and_dispatch_values_bind_from_configuration`, `Omitted_configuration_preserves_validated_safe_defaults`, `Invalid_streaming_bound_combination_fails_startup`, `Chunk_bound_cannot_exceed_persisted_event_text_bound`, `Aggregate_bound_cannot_exceed_canonical_message_bound`, `Configured_workers_never_exceed_concurrency_cap`, `Slow_conversation_does_not_starve_unrelated_conversation`, `Workers_never_execute_two_active_turns_for_one_conversation`, `Queued_age_expires_without_provider_dispatch`, `Operation_duration_after_dispatch_becomes_evidence_safe_outcome`, `Shutdown_drains_all_started_workers`, `Availability_distinguishes_registration_from_progress`, `Transfer_rejects_invalid_operation_invocation_event_graph`, and `Transfer_rejects_over_bound_document_before_materialization` (14 cases).
- Invalidation keys: options contracts/section names/defaults, DI binding, hosted service/dispatcher, lease claim, message/event bounds, transfer schema/validator/materializer.
- Broad-gate decision: deferred to SB10 for Composition/DI/schema/transfer cross-cutting changes.

## Implementation Steps

1. Introduce minimal typed dispatcher/transfer options and bind all LLM Chat options with validation on startup; prove configured values and omitted-section defaults, including worker concurrency 1.
2. Replace numeric duplicates with existing/shared domain constants and add upper/cross-field validation.
3. Fan out a fixed configured number of dispatcher workers over existing database claims; no in-memory queue.
4. Add deterministic fake-time queue-age and total-duration transitions with safe pre/post-dispatch classification.
5. Make operational availability/progress/saturation logs accurate and allowlisted.
6. Validate transfer counts before allocating child graphs, then enum/state/relation integrity; include every final schema field.
7. Prove worker cap, independent progress, same-conversation serialization, shutdown drain, expiration, and transfer bounds.
8. Build Core/Persistence/Composition/Web/Migrations; list/run exact Unit/PostgreSQL cases and pending-model/transfer checks.

## C# Architecture Impact

- Composition hosts bounded workers and binds options; Core/Persistence own policy/claims/transfer. No new queue or project.

## Boundary Ownership

- Core defines options/invariants; Composition binds/hosts; Persistence claims/transfers; Web is unchanged except configuration exposure if already canonical.

## Dependency Direction

- Hosted service calls existing dispatcher; no Composition-specific type enters Core.

## Pattern Decision

- PSR-08; database remains durable queue.

## Testability Contract

- Fake time and barrier providers; assert maximum observed concurrency and canonical final rows, not elapsed-wall-time guesses.

## Partial Class Policy

- No partials and no interface solely for option binding.

## Architecture Proof Required

- DI lifetime/options validation review, worker ownership diagram, no-shadow-queue source assertion, schema/transfer/pending-model evidence.

## Scope Exceptions

- This is bounded throughput, not autoscaling, quotas, tenant fairness, or general rate limiting.

## Do Not Do

- Do not use unbounded `Task.Run`, an in-memory work queue, silent configuration fallback, or retry ambiguous timed-out dispatch.
- Do not materialize an untrusted transfer graph before enforcing aggregate limits.

## Acceptance Checklist

- [ ] Fourteen named cases discover and pass.
- [ ] Invalid configuration fails before traffic.
- [ ] Worker/age/duration/transfer bounds are deterministic and durable.
- [ ] Pending-model and changed project builds pass.

## Proof Required

- Failing-first/passing transcripts, exact discovery, startup validation messages, worker timeline/max concurrency, durable expiration rows, transfer bound/round-trip evidence, safe log samples, pending-model output, and builds under `proof/SB07`.

## Browser Validation Logging

- N/A — no rendered UI.

## Progression Gate

- SB08 starts after SB07; CP2 waits for SB08 to pass.

## Reopen Triggers

- Any later option/default/DI/dispatcher/hosted-service/lease/bound/transfer/migration change reopens SB07 and SB09-SB10.

## Suggested Agent Prompt

```text
Execute SB07 only. Bind and validate explicit limits, add bounded database-backed worker concurrency, and harden transfer without an in-memory queue. Use deterministic time/concurrency proof and stop on unsafe post-dispatch retry semantics.
```
