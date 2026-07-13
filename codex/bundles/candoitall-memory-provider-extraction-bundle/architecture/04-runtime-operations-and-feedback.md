# Runtime Operations And Feedback

## Operation lifecycle

1. Caller creates a structured memory request through the shared operation handler.
2. Provider selection resolves a provider profile, capability match, timeout policy, and access policy.
3. Operation ledger records request metadata, correlation ids, source context, and requested capability.
4. Driver executes synchronously or returns `MemoryOperationAccepted`.
5. Async operations are advanced through worker polling, callback events, or provider event inbox processing.
6. Completed context packs create delivery correlation rows for later feedback.
7. Feedback service links delivered context packs to immediate or delayed outcomes.
8. Retention policy expires ledgers and optional snapshots; IPFS pins are released when configured.

If no provider is configured, the lifecycle stops at provider selection with a typed no-provider result. The handler may record a rejected operation attempt when useful for audit, but it must not call native Cognitive Memory, OpenAI, Qdrant, or a mock provider unless the caller selected an explicitly configured provider profile.

## Event lifecycle

1. Provider emits or exposes an event.
2. Generic event inbox validates provider identity, protocol version, event id, causation, priority, and policy.
3. Dedupe prevents duplicate processing.
4. Loop guard rejects recursive memory-agent-memory chains beyond configured thresholds.
5. Router maps allowed events to host actions such as source snapshot request, workflow start, agent verification request, UI review item, or no-op acknowledgement.
6. Outbox records acknowledgements and outbound provider calls.

## Feedback lifecycle

Feedback must support multiple phases:

- `context.delivered`: context pack returned and attached to an agent/process/workflow session.
- `context.used`: caller confirms the context was used or ignored.
- `process.completed`: process/workflow ended with outcome metadata.
- `customer.accepted`: deliverable accepted by real customer or downstream user.
- `economic.impact`: measurable business/economic signal is later attached.
- `feedback.closed`: provider acknowledged ingestion of feedback or TTL expired.

## Shared async primitives

The memory runtime should reuse or extract shared primitives where compatible with existing LLM provider batching/resilience patterns:

- bounded concurrency;
- retry policy per provider profile;
- timeout and cancellation tokens;
- request batching where provider supports it;
- health and circuit-breaker style provider status;
- structured error categories;
- status polling and operation watchers.

## Source Gateway alignment

The current repo already has `MemorySourceSnapshot*` contracts under MAF. The generic memory runtime should not create a parallel source snapshot family. SB04 must choose one of these explicit paths:

- move the existing contracts into the generic memory abstraction boundary and update current producers/consumers;
- keep the current contracts in place temporarily and wrap them behind a generic Source Gateway compatibility adapter;
- or introduce a new versioned contract only with a migration adapter and proof that old Workbench/Workflow providers still work.

Any implementation path must preserve provenance, redaction, sensitivity, cursor, hash-policy, and source-item identity semantics from the current contracts.
