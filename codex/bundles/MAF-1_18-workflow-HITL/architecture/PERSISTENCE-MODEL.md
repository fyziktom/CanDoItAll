# Persistence Model

## Goal

Persist enough data to rehydrate a MAF workflow safely without making MAF objects part of CanDoItAll's domain or public API.

## Authority boundary

The Runtime in-memory implementations exist for isolated proof and test-only compatibility
construction. They are process-local, non-durable, and non-snapshot-isolated. They do not
prove host-restart, multi-host race, or transactional recovery behavior and must never be
described as production persistence.

The focused EF/PostgreSQL implementations in `Modules.AgentFramework` are authoritative
for production checkpoint ordinal allocation, payload/session persistence, response-operation
create/replay/conflict and lease CAS, atomic resume-boundary commits, and participating
executor-invocation deduplication. PostgreSQL unique constraints, conditional writes, and
transactions—not an in-process lock or staged memory copy—establish these guarantees.

## Checkpoint payload record

Suggested logical entity: `WorkflowBackendCheckpointPayloadRecord`

Fields:

- `Id`
- `RunId`
- `WorkflowId`
- `WorkflowVersionId`
- `Backend`
- `BackendSessionId`
- `BackendCheckpointId`
- `ParentBackendCheckpointId`
- `CommitOrdinal`
- `PayloadJson`
- `PayloadSha256`
- `CheckpointFormat`
- `CheckpointFormatVersion`
- `CompilerContractVersion`
- `TopologyFingerprint`
- `ExternalRequestId`
- `CreatedAtUtc`
- concurrency token where repository convention requires it

Constraints/indexes:

- unique `(Backend, BackendSessionId, BackendCheckpointId)`;
- unique `(Backend, BackendSessionId, CommitOrdinal)`;
- index `(RunId, CommitOrdinal)`;
- optional unique linkage for the active request boundary;
- payload hash verified on read;
- index retrieval ordered explicitly by `CommitOrdinal ASC`.
- filtered unique native linkage `(SessionId, BackendRequestId, BackendRequestPortId)` when
  both native request fields are non-null.

Do not use GUID lexical order or creation timestamp alone as the MAF checkpoint index contract.

## Response operation record

Suggested logical entity: `WorkflowExternalResponseOperationRecord`

Fields:

- `Id`
- `RequestId`
- `RunId`
- `State`
- `IdempotencyKeyHash`
- `ResponsePayloadHash`
- protected response payload or payload reference
- `ExpectedRequestVersion`
- `ActorKind`
- `ActorId`
- `CorrelationId`
- `Attempt`
- `LeaseOwner`
- `LeaseExpiresAtUtc`
- `AcceptedAtUtc`
- `StartedAtUtc`
- `CompletedAtUtc`
- `OutcomeCode`
- `SafeMessage`
- `ResultRunState`
- `ResultCheckpointId`
- `NextExternalRequestId`
- concurrency token

Constraints/indexes:

- unique `(RequestId, IdempotencyKeyHash)`;
- at most one non-terminal operation per request;
- at most one active resume claim per run;
- payload hash mismatch on idempotency replay is conflict.

## External request evolution

Existing request records should gain or derive:

- request version;
- response schema/version;
- native MAF request ID;
- native port ID;
- checkpoint metadata ID;
- request payload hash;
- policy/authorization metadata reference;
- state or reliable derivation from operation records;
- responder/outcome references.

Keep original governed arguments immutable. A response cannot replace them.

## Invocation deduplication record

Suggested logical entity: `WorkflowExecutorInvocationRecord`

Stable key material:

- run ID;
- workflow version;
- node ID;
- causation external request/operation ID where present;
- logical attempt/generation;
- executor contract version.

Fields:

- invocation key/hash;
- state (`Claimed`, `Completed`, `FailedRetryable`, `FailedTerminal`);
- owner/lease;
- input hash;
- result payload/reference;
- artifact/effect references;
- timestamps;
- failure code;
- concurrency token.

Rules:

- a completed invocation returns the persisted result on replay;
- an active valid lease rejects parallel duplicate execution;
- an expired claim can be recovered according to executor policy;
- non-idempotent external systems require an outbox or propagated idempotency key;
- do not cache/replay secrets outside payload policy.

## Transaction boundaries

At minimum, use atomic database operations for:

1. response-operation create/replay/conflict decision;
2. request/run claim for resume;
3. checkpoint payload + checkpoint metadata linkage;
4. run transition + lifecycle event where current store already supports it;
5. final response-operation state + request response state;
6. invocation deduplication claim/result.

A single transaction across MAF execution and external systems is neither possible nor required. Use durable state transitions and idempotent boundaries. The precise guarantee is exactly-once response acceptance and deduplicated participating governed effects; arbitrary external effects require their own idempotency key, transactional outbox, or equivalent participation protocol.

### PostgreSQL lock order

Response create/replay/conflict, claim, lease renewal, and commit serialize through the request
before mutating the response operation. Replay loads the operation through a relational
`SELECT ... WHERE "RequestId" = ... FOR UPDATE`; claim and commit retain the same
request-before-operation order. This prevents replay-count persistence from racing a lease or
state update into an EF concurrency failure while avoiding an operation-before-request deadlock
in the owned paths.

The deterministic PostgreSQL replay test holds the request row from a second connection,
observes lease renewal queued behind it, and verifies the replay query contains `FOR UPDATE`,
the renewed lease/version survives, and replay increments exactly once.

### Native checkpoint-link conflict

The application precheck remains useful for a typed early result, but it is not authoritative
across DbContext instances. The filtered unique index is the cross-context authority. The linker
classifies only the known external-request and native-request constraint names as `LinkConflict`;
unexpected database errors propagate. PostgreSQL marks a transaction aborted after a unique
violation, so both current callers immediately roll it back before returning the typed conflict.

## Retention

Checkpoint payloads may be large and sensitive.

Define:

- retention for terminal runs;
- retention for audit metadata versus raw payload;
- deletion/cascade behavior;
- maximum payload size;
- encryption/storage policy consistent with existing application data;
- cleanup only after no active response operation references the checkpoint.

Do not add automatic destructive cleanup in the same migration unless the repository already has a safe retention framework.

## Migration compatibility

The migration must preserve existing run/request/checkpoint rows.

Legacy checkpoint metadata without a backend payload remains readable but non-resumable. Do not synthesize fake payloads.

Migration `20260821021747_AddWorkflowHitlRecovery` follows the repository naming and
placement conventions, preserves legacy rows, and passes
`dotnet ef migrations has-pending-model-changes` with no pending model change.

Wave C adds migration
`20260822013043_AddWorkflowNativeCheckpointRequestUniqueness`. It first checks for duplicate
non-null `(SessionId, BackendRequestId, BackendRequestPortId)` tuples and raises a PostgreSQL
exception when any exist. Only a clean database receives the filtered unique index; the
migration never deletes or rewrites conflicting data. The migration and model snapshot build,
apply, and no-pending-model checks pass in focused proof.

## Provider-specific test profile

The supported EF InMemory profile uses exact provider detection, nullable transactions, and a
process-wide mutation semaphore across the affected run, idempotency, checkpoint, boundary,
response, resume, and executor-dedup stores. Unknown non-relational providers fail explicitly.
This is deterministic single-process test/development behavior only; PostgreSQL constraints,
locks, and transactions remain authoritative for production.

## Residual risks

- Existing duplicate native tuples block the additive migration and require explicit operator
  remediation; no dirty-data migration fixture or runbook is included in SB07.
- Future native-link callers must preserve rollback after `LinkConflict` on PostgreSQL.
- Lease renewal is the directly raced replay mutation. Other response-operation mutations share
  the locked request path but are not each covered by a distinct two-connection race.
- The InMemory semaphore cannot coordinate multiple processes or hosts.
