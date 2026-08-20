# Persistence Model

## Goal

Persist enough data to rehydrate a MAF workflow safely without making MAF objects part of CanDoItAll's domain or public API.

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

A single transaction across MAF execution and external systems is neither possible nor required. Use durable state transitions and idempotent boundaries.

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

Use repository migration naming and placement conventions discovered by SB00.
