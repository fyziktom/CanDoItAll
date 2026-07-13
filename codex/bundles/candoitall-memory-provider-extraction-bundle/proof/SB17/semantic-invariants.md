# SB17 Semantic Invariants

## Scope

- Generic workflow memory execution is exposed through the current `IWorkflowExecutor` infrastructure.
- Workflow memory operations use the same generic memory operation handler and typed result contracts as MAF memory tools.
- Legacy native workflow executor ids are mapped as compatibility data without registering duplicate native executor implementations.

## Invariants

- Workflow memory execution must depend only on generic memory abstractions/application contracts.
- Workflow memory execution must not reference native Cognitive Memory implementation namespaces, native `CognitiveMemory` types, Qdrant runtime code, or provider-specific RAG drivers.
- Workflow memory execution must not call memory provider registries or drivers directly; dispatch belongs to `IMemoryOperationHandler`.
- Workflow memory execution must not silently dispatch to an implicit hidden provider. A provider must come from explicit settings, workflow/node assignment, or a configured default provider.
- Provider allowlists and capability allow/deny filters must be enforced before dispatch.
- Manual source ingestion must respect allowed source scopes before any source capture request reaches the handler.
- No-provider, disabled-provider, unsupported-capability, denied-capability, denied-provider, denied-source-scope, timeout, and async-accepted outcomes must be returned as typed workflow results.
- Context query results must include summary, sections, citations, warnings, confidence, feedback handle, and async status metadata when present.
- Compatibility mapping for old native executor ids must remain isolated and must not register duplicate executor implementations while the native module still exists.

## Proof Hooks

- `MemoryWorkflowExecutorTests` invokes the production executor and verifies descriptor discovery, context result shaping, input query fallback, async accepted shaping, no-provider behavior, capability denial, manual source-scope denial, DI registration, and old-id compatibility mapping.
- The dispatch boundary audit proves the executor calls `IMemoryOperationHandler` and does not call provider registries or drivers directly.
- The native dependency audit proves the SB17 workflow executor surface contains no native Cognitive Memory or Qdrant references.
