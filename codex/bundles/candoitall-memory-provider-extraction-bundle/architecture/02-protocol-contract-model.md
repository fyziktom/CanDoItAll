# Protocol Contract Model

## Core envelope

Every provider operation should use a common envelope shape:

```text
MemoryOperationEnvelope
  operationId
  correlationId
  causationId
  providerInstanceId
  operationKind
  requestedBy
  workspaceContext
  executionContext
  policyContext
  budget
  payload
  extensionData
```

The envelope must support simple providers by allowing drivers to reduce the payload to a plain query, but the generic module should keep the full envelope for traceability and feedback.

## Required request models

- `MemoryContextQueryRequest`: structured recall/query request.
- `MemoryIngestionRequest`: provider ingestion from a source snapshot or manual payload.
- `MemoryFeedbackRequest`: feedback on context usage, outcome, or economic impact.
- `MemorySourceRequest`: provider request for host source data through Source Gateway.
- `MemoryEventAcknowledgeRequest`: host acknowledgement of provider events.
- `MemoryOperationStatusRequest`: status polling for long-running operations.

## Required response models

- `MemoryContextPack`: normalized context sections, summary, citations, warnings, provider confidence, and `contextPackId`.
- `MemoryOperationAccepted`: async operation id, expected status path, TTL, polling hint, and callback capability.
- `MemoryOperationResult`: terminal status, output payload, warnings, feedback handles, and source refs.
- `MemoryProviderEvent`: hypothesis, source request, feedback request, verification request, maintenance signal, or health event.
- `MemoryProviderHealth`: reachable/unreachable/degraded, last error category, and capability snapshot.

## Structured context fields

The protocol must capture structured context such as:

- project id, project name, project tags, budget, customer, and domain;
- process id, process step id/name, workflow node, role, and artifact ids;
- agent id/name/role, provider profile, tool/executor invocation id, and session id;
- requester reason, user-visible task, sensitivity, allowed source scopes, and approval posture;
- source provenance, source snapshot id, source module, source record ids, and redaction level;
- arbitrary extension facts in a typed dictionary or JSON extension bag.

## Capability negotiation

Provider manifests should declare capabilities such as:

- `context.query.sync`
- `context.query.async`
- `ingestion.snapshot`
- `ingestion.provider-requested-source`
- `feedback.immediate`
- `feedback.delayed`
- `events.provider-push`
- `events.host-poll`
- `ui.rcl`
- `ui.iframe`
- `native.probe`
- `native.cluster-browser`
- `native.review-queue`

Unsupported capabilities must fail predictably at provider selection or operation dispatch, not halfway through a workflow.

## Versioning

Use `memoryProtocolVersion` and per-capability version metadata. Drivers may translate between versions, but the generic operation ledger should record both requested and effective versions.

## Extension policy

Extensions are allowed only behind a stable namespace such as `native.cognitiveMemory.*`, `provider.vendor.*`, or `host.candoitall.*`. Generic code may store and pass through extensions but must not branch on native extension keys except in provider-specific adapter packages.
