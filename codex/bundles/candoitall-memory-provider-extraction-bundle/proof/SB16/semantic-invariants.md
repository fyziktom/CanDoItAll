# SB16 Semantic Invariants

## Scope

- Generic MAF memory runtime tools are registered through `IAgentRuntimeToolProvider`.
- Agent memory provider selection is driven by agent configuration and runtime tags, then delegated to the shared memory operation handler.
- Tool results expose typed diagnostics and shaped memory context payloads without leaking provider/runtime implementation types.

## Invariants

- MAF memory tools must depend only on generic memory abstractions/application contracts.
- MAF memory tools must not reference native Cognitive Memory implementation namespaces, native `CognitiveMemory` types, Qdrant runtime code, or provider-specific RAG drivers.
- MAF memory tools must not select providers by directly calling memory registries or drivers; provider dispatch belongs to `IMemoryOperationHandler`.
- Memory tools must not silently dispatch to an implicit hidden provider. A provider must come from explicit tool input, agent preferred/default settings, or a matching agent/workflow/process assignment.
- Provider allowlists and capability allow/deny filters must be enforced before dispatch.
- Manual source ingestion must respect allowed source scopes before any source capture request reaches the handler.
- No-provider, disabled-provider, unsupported-capability, denied-capability, denied-provider, denied-source-scope, timeout, and async-accepted outcomes must be returned as typed tool results.
- Context query results must include summary, sections, citations, warnings, confidence, feedback handle, and async status metadata when present.

## Proof Hooks

- `MemoryAgentRuntimeToolProviderTests` invokes production `AITool` functions and verifies tool exposure, metadata, provider selection, no-provider and unsupported-capability diagnostics, async accepted shaping, and manual source-scope denial.
- The dispatch boundary audit proves the provider calls `IMemoryOperationHandler` and does not call provider registries or drivers directly.
- The native dependency audit proves the SB16 tool surface contains no native Cognitive Memory or Qdrant references.
