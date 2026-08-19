# C# Testability Plan

## Existing Seams To Reuse

| Behavior | Test seam |
| --- | --- |
| Clock/retention/lease/queue age | `TimeProvider` and deterministic fake time |
| Provider start/cancellation/completion | Existing deterministic invocation/provider ports with task barriers |
| Heartbeat/lease/profile changes | Existing operation scope/runtime lease and repository test doubles |
| Cancellation registry races | Barrier-controlled registration/callback test doubles |
| CAS and definition-pin races | Two PostgreSQL contexts plus deterministic pre-commit barriers |
| HTTP/auth/model binding | Existing `ApiTestHost` and exact scope identities |
| SSE frame/read lifecycle | Existing `ServerSentEventResponseWriter` integration harness |
| Replay/retention/high-water | PostgreSQL journal/repository integration fixture |
| Log redaction | Capturing `ILogger` provider and distinctive secret sentinels |
| Migration/transfer | Existing migration bootstrap, pending-model, and database transfer fixtures |

## Testability Contracts

- No production-only sleep is added to make concurrency tests pass.
- No test reaches private state through reflection when an observable durable/API outcome exists.
- Deterministic barriers identify the exact race point: after provider start, between read and CAS commit, between registration lookup and disposal, or between operation/event replay queries.
- Every concurrency test asserts both returned result and canonical durable side effects (dispatch count, message/audit/event count, status, token/high-water).
- Provider-redaction tests put unique sentinel values in raw response body, exception/inner exception, endpoint, credential, prompt, system prompt, and path, then scan all captured log state and exception text.
- Host tests exercise real middleware/model binding/authorization/Problem Details, not direct handler calls.
- PostgreSQL tests use real transactions/isolation and do not substitute an in-memory EF provider.
- Capacity tests assert maximum observed concurrency, unrelated-conversation progress, queue expiration, ambiguous post-dispatch recovery, and graceful shutdown drain.

## Proof Tiers

- SB01, SB09, and SB10 are `Governed` because downstream work trusts their baseline/architecture/release conclusions. They require portable manifests, changed-file hashes, semantic invariants, transcripts, and independent review.
- SB02-SB08 are `Behavioral`. Each requires realistic positive and meaningful negative evidence, exact discovery, changed project builds, and source assertions where a prohibited path matters.
- A work unit may be raised, but never lowered after implementation to avoid proof obligations.

## Anti-Stub Checks

- No test-only production branches, `InternalsVisibleTo` added only to bypass public behavior, placeholder reconcile results, fake high-water counters disconnected from append, no-op eviction, or log “redaction” that merely truncates raw text.
- Public invocation evidence must be produced from durable rows after restart/retention, not copied from in-memory provider results.
- Queue-age/duration failures must be persisted and replayable, not only logged.

## Final Test Selection

- Development uses exact named cases or bounded LLM Chat topics in Unit/Integration lane solutions.
- Every filter is listed first and its expected/actual discovery recorded.
- The broad stable aggregate runs once in SB10 because the union changes shared ProviderRuntime, Web API contracts, Composition/DI, PostgreSQL schema/transfer, and migrations.
- Browser/Playwright tests are not an affected proof surface for this backend-only bundle; the existing CI workflow may still run its normal unrelated lanes.
