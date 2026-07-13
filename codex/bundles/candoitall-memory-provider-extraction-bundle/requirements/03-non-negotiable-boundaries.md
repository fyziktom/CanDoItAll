# Non-Negotiable Boundaries

## Dependency rules

- Generic memory contracts must not reference native `CognitiveMemory*` engine types.
- MAF must reference only generic memory abstractions/tooling packages, never native service/domain/UI projects.
- Main CanDoItAll composition may reference `CanDoItAll.Modules.Memory` but must not require `CanDoItAll.Modules.CognitiveMemory` after the migration closes.
- Qdrant references must live only in optional provider/projection packages, not in base runtime startup.
- Native Cognitive Memory can depend on MAF abstractions for curator/professor agents, but not on the main Agent module or main CanDoItAll module composition.
- Source Gateway adapters are the only supported path from host modules to memory providers.
- Agent-memory runtime integration must have an explicit project and namespace owner. It must not remain hidden inside a broad Agent module partial-class cluster.
- Application services may depend on generic memory contracts and ports, not on MAF runtime implementations, HTTP/MCP clients, persistence, UI, or the external Cognitive Memory service.
- Driver, persistence, and external-service projects point inward through contracts; the composition root is the only place that selects concrete implementations.

## Data boundaries

- Generic memory integration metadata may live in the main app DB.
- Native memory records, clusters, recalls, review items, scoring, taxonomy, temporal replay, self-model, and projections belong to the native memory DB/service.
- Providers must receive DTO snapshots and provenance metadata, not EF entities or DbContext references.
- Feedback ledger entries store correlation metadata and optional snapshot pointers, not unbounded copies of all source data by default.
- Persisted agent settings use typed invocation modes and provider bindings. Provider IDs and aliases are validated at the boundary rather than interpreted as arbitrary command strings.
- Provider profiles may persist secret references, never raw API keys, bearer tokens, passwords, or equivalent credentials.
- A malformed or unauthorized project identifier must fail closed. It must never be normalized into global memory scope.

## Runtime boundaries

- Tools, workflow executors, and context contributors must call a shared memory operation handler.
- Long-running provider calls must use accepted-operation status rather than blocking indefinitely.
- Provider events must be deduplicated and loop guarded before creating agent/workflow work.
- Every provider operation must have cancellation, timeout, and observable status.
- No configured provider is a valid runtime state. It must produce typed no-provider/disabled/capability-mismatch results, not fallback calls to native Cognitive Memory, OpenAI, Qdrant, or mock providers.
- Current MAF integration must use existing `IAgentRuntimeToolProvider`, `IWorkflowExecutor`, and `IAgentContextContributor` seams. Do not add a separate memory-only MAF runtime path.
- Provider selection must honor the agent's allowed provider bindings and explicit fallback policy. Registry enumeration order is never a selection policy.
- One agent may bind multiple providers. Automatic fan-out and explicit alias selection must be deterministic, bounded, independently observable, and labelled by provider in merged context.
- `/mem:<alias>` is routing metadata, not user content. It must be parsed and authorized before dispatch and removed before any prompt or provider query is sent.
- Required agent, requester, session, workspace, project, process, workflow, and step identity must travel in typed runtime/protocol context. Required authorization state must not be reconstructed from optional magic tags.
- Operation status and cancellation require ownership authorization using the persisted operation requester/runtime scope. A GUID alone grants no access.
- The external provider endpoint must authenticate callers, authorize provider/project access, apply access and redaction policy, constrain requests, and advertise only behavior implemented across the HTTP seam.

## UI boundaries

- Generic UI must work with no providers and with mock providers.
- Provider-specific UI must be optional and load via declared surface registration.
- Native rich UI cannot be required for generic provider setup or simple query usage.
- Agent settings UI must expose typed memory invocation mode and zero/one/many provider bindings without requiring users to edit raw JSON.
- Editing a provider profile must preserve unrecognized and driver-specific configuration. The UI must not erase connection details, selection metadata, or secret references it does not own.

## Source Gateway boundaries

- The existing MAF `MemorySourceSnapshot*` contracts must be reused, rehomed, or explicitly migrated. Do not create a second incompatible source snapshot family.
- Source Gateway adapters must remain module-owned and policy-governed; provider drivers must not reference source modules or `AppDbContext` directly.

## C# architecture boundaries

- Capability-grouping partial classes are prohibited. Partial classes are allowed only for generated code, framework-required UI code-behind, platform variants, or a time-bounded migration shim documented in the architecture record.
- Configuration codecs, directive parsing, policy resolution, provider routing, protocol mapping, transport invocation, operation authorization, event processing, and persistence queries must be cohesive top-level types with focused tests.
- Moving methods into another partial file is not modular refactoring. The owning type must shrink and dependencies must move with the extracted responsibility.
- New interfaces require a real boundary, alternate implementation, or isolated-test seam. Do not manufacture one-interface/one-trivial-implementation layers.
- Architecture repair must include current-state inventory, target boundary map, dependency direction, pattern decisions, testability plan, and an independent final architecture gate.
