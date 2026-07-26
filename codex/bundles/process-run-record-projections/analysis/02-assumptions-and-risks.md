# Assumptions And Risks

## Assumptions

- A process run is uniquely identified by its run ID across snapshot versions.
- The current manager-loop escalation is an active attention signal, not a process-ending disposition. `Escalated` remains reserved until an explicit ending transition exists.
- Projection/background infrastructure is the correct finalization trigger; API GETs are not.
- JSON payload fields are persisted as serialized strongly typed records, never manipulated as arbitrary dictionaries by consumers.
- Existing visual composition remains unchanged unless implementation proves a small data-source indicator is necessary.

## Critical Path Risks

- SB02 is the critical schema/contract foundation. A wrong identity, disposition, schema-version, or indexing model reopens every downstream subbundle.
- SB03 is the critical lifecycle foundation. Non-idempotent triggering, synchronous LLM work, or incomplete evidence rules reopen API and project-node work.
- SB04 controls whether the performance outcome is real. Any normal history path that still hydrates canonical detail per row reopens SB02/SB03 as necessary.
- Removing foreground catch-up can expose stale projections. The API must report freshness or use the dedicated record store whose write lifecycle is explicit.

## Validation Risks

- CodeAnalytics MCP is unavailable.
- PostgreSQL integration may require environment configuration; EF model tests and generated migration inspection remain mandatory even if a live database is unavailable.
- Agent Framework evidence and real LLM execution are provider-dependent; deterministic fakes must prove orchestration and failure semantics.
- Stopwatch comparisons are noisy; call/query-count characterization is the primary regression gate.

## Resolved Risks

- The authoritative SharedInfo Processes API skill was writable and has been updated; its diff passes whitespace validation.
- Bounded execution and usage reads now carry `IsComplete`, so caps cannot silently publish partial evidence as complete.
- Narrative lease expiry cannot create duplicate same-source executions: lookup and reservation are atomic under the workspace cross-process lock, an active execution defers without consuming an attempt, and a completed same-source execution is reused.
- Backfill revalidates current terminal state and the latest lifecycle sequence during the guarded store mutation, so a stale seed captured before reactivation is rejected even when supersession previously found no record.
- Analytics now separates all matching records from the facts-available metric denominator, reports complete/partial/unavailable counts, and exposes source-derived time/sequence watermarks separately from record maintenance time.

## Residual Risks

- Live PostgreSQL migration and PostgreSQL-backed API execution require Docker/PostgreSQL, which is unavailable in the current environment. The additive migration, EF model snapshot, deterministic EF persistence tests, and two passing in-memory HTTP route/serialization tests are the available substitutes.
- Real provider/LLM narrative execution remains environment-dependent; deterministic orchestration tests cover selection, prompt boundaries, structured output, reuse, deferral, retry, and failure.
- A host crash after an Agent Framework same-source run is reserved but before it is finalized can leave that execution active. This is duplicate-safe and visibly deferred, but recovery or cancellation of the orphaned execution remains an Agent Framework operational concern.
- Historic backfill can represent only retained evidence and must remain partial when source data has expired.
- `Escalated` remains reserved until runtime exposes an explicit terminal escalation event.
- Active or unassembled project discovery still performs a bounded assignment-JSON lookup because the canonical assignment has no typed `ProjectId`; record-covered history bypasses it.
- Run-record seed availability shares the runtime projector retry/dead-letter lifecycle. A separately offset projector would improve isolation but would be a broader operational change.
- Generated step result text is intentionally excluded from durable facts and the manager prompt. The narrative therefore summarizes classified step identity/outcome/attempt/timing/usage data, not arbitrary generated bodies.
- Existing `NU1903` advisories for `System.Security.Cryptography.Xml` 10.0.7 and the EF CLI/runtime patch-version mismatch are repository-level issues outside this change.

## Reopen Triggers

- A consumer requires data absent from the compact hard-fact contract.
- A filter/order requires deserializing JSON instead of using scalar indexed columns.
- A future explicit escalated ending transition could produce stale records if its continuation/supersession policy is not defined atomically with that transition.
- Summary generation can run in a GET request or block runtime completion.
- A list/analytics test observes runtime state, assignments, or execution-detail hydration.
- Migration/model validation finds an ORM navigation, cascade relation, missing unique run index, or provider-incompatible JSON mapping.
- API implementation changes route or payload semantics after the SharedInfo skill is updated.
