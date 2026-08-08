# C# target boundary map

## Boundary principles

- Product modules own product semantics and context contributors.
- Core/Application owns execution admission, context capture, authority resolution orchestration, and durable run coordination.
- The MAF project maps runtime-neutral requests to MAF and maps MAF results back.
- Composition roots may know implementations.
- Contracts stay SDK-free.
- UI observation contracts do not depend on Razor components.
- Process recovery does not live in MAF.

## Target project roles

### `CanDoItAll.AgentFramework.Models`

Keep repository-owned data records that are shared across layers.

Allowed additions:

- conversation binding records,
- context transition records,
- turn context reference,
- execution authority persisted record,
- runtime state envelope metadata.

Do not add:

- MAF SDK types,
- Razor component types,
- EF Core dependencies,
- concrete process services,
- broad behavior.

### `CanDoItAll.AgentFramework.Core`

Own application-level implementations:

- UI observation registry,
- conversation context service,
- transition classifier,
- turn context capture service,
- authority resolution coordinator,
- execution run coordinator,
- typed workspace runtime services factory contract,
- generic governance and recovery coordination.

It must not reference:

- `CanDoItAll.AgentFramework.Maf`,
- product module implementations,
- MAF SDK packages.

### `CanDoItAll.AgentFramework.Runtime.Abstractions`

Create this SDK-free project as the target runtime port boundary. A deviation is allowed only when SB00 proves that an existing equally narrow SDK-free abstractions project already has the correct role; record that decision as an ADR amendment.

It should contain:

- narrow execution/continuation/diagnostics/administration ports,
- runtime-neutral request/result records when they do not belong in Models,
- generic runtime failure evidence,
- generic runtime recovery contracts.

It must be SDK-free and module-free.

Do not place these ports back into the implementation-heavy Core merely to avoid one project. The compile-time boundary is intentional.

### `CanDoItAll.AgentFramework.Llm.Abstractions`

Create this small SDK-free project, or prove in SB00 that an existing equally narrow project already has this exact application-facing role. It should contain:

- `ILlmInvocationPort`;
- optional `IStreamingLlmInvocationPort`;
- repository-owned ordered message, attachment, model-setting, response-format, streaming-update, usage, finish, and failure contracts.

It must not reference:

- `CanDoItAll.AgentFramework.Maf`;
- agent execution/session/context/tool contracts;
- product modules or UI;
- provider SDK packages.

These contracts describe stateless model inference, not an agent. They intentionally contain no workspace scope, authority, tools, memory, handoff, approval, finalizer, process, or UI-context property.

### Provider-backed lightweight LLM implementation

Prefer a focused implementation project such as `CanDoItAll.AgentFramework.Llm.ProviderRuntime`, unless SB00 proves that a cohesive existing provider application project can own the implementation without becoming broad.

Own:

- mapping `LlmInvocationRequest` to provider runtime/driver requests;
- provider/profile/model resolution delegated through existing provider policy contracts;
- invocation through `IProviderRuntimePool` and `IProviderChatCompletionDriver`;
- mapping provider results, streaming updates, usage, and sanitized failures back to LLM contracts.

Do not duplicate credentials, HTTP clients, dispatch lanes, provider retry rules, blank-response handling, model normalization, or usage extraction. Those remain owned by the existing provider runtime/driver layer.

A future ordinary-chat application project/service depends on `Llm.Abstractions`, persists its own transcript and conversation metadata, and delegates each turn to the stateless port. It does not depend on MAF or construct a disabled agent.

### `CanDoItAll.AgentFramework.Maf`

Target role: MAF anti-corruption adapter.

Own:

- MAF agent construction,
- provider/MAF mapping,
- MAF session serialization,
- MAF event/response mapping,
- MAF-native handoff construction,
- provider compatibility behavior,
- adapter-specific telemetry.

Must not own:

- process artifact paths,
- process status semantics,
- product authorization,
- current UI registry,
- product module services,
- canonical project/workflow/process state.

### `CanDoItAll.AgentFramework.Workflows.MafAdapter`

Own compilation and execution mapping for stored CanDoItAll workflow definitions.

Ordinary LLM nodes depend on `ILlmInvocationPort`, not `IAgentRuntime`.

The common MAF runtime must not reference this project merely to construct handoffs. Move MAF-native handoff construction into the MAF adapter or introduce a narrow correctly directed seam.

### `CanDoItAll.Modules.Workbench`

Own:

- Project Structure UI observation contributors,
- Canvas/Gantt/Manager Summary fact extraction,
- Project Structure canonical context hydrator if pinning is later supported,
- Project Structure runtime guidance contributor,
- Project Structure authority adapter backed by canonical access services,
- Project Structure agent tools.

It must not expose Gantt projection as canonical truth.

### `CanDoItAll.Modules.Processes`

Own:

- process execution policy contribution,
- process provider-selection policy,
- process outcome recovery policy,
- process artifact recovery,
- process completion and evidence gates,
- process-specific mapping from generic runtime evidence.

The MAF project must not reference this module.

### `CanDoItAll.Security.Abstractions` or equivalent narrow existing project

Create or select a stable SDK-free project for:

- `ISecretRuntimeResolver`,
- secret runtime request,
- purpose/consumer identifiers needed by runtime adapters.

`CanDoItAll.Modules.Security` implements it.

Do not let MAF reference `CanDoItAll.Modules.Security`.

### `CanDoItAll.AgentFramework.Hosting` and application module composition

Own registration of:

- Core services,
- MAF adapter implementations,
- workflow adapter,
- product contributors,
- security implementation,
- runtime factories.

Registration methods remain declarative. They do not execute business behavior.

## Target top-level types

| Concern | Target type |
|---|---|
| Live observation | `IAgentUiObservationRegistry` or documented observation-only successor to `IAgentChatContextRegistry` |
| Conversation binding | `IAgentConversationContextService`, `IAgentConversationContextStore` |
| Transition | `IAgentContextTransitionClassifier` |
| Turn capture | `IAgentTurnContextCaptureService` |
| Model context composition | `IAgentModelContextComposer` |
| Authority | `IAgentExecutionAuthorityResolver` |
| Workspace service bundle | `IWorkspaceRuntimeServicesFactory` |
| Agent execution | `IAgentExecutionRuntime` |
| Approval continuation | `IAgentContinuationRuntime` |
| Provider diagnostics | `IProviderDiagnosticsRuntime` |
| Provider administration | `IProviderModelAdministrationRuntime` |
| Generic recovery | `IAgentExecutionOutcomeRecoveryPolicy` |
| Direct LLM call | `ILlmInvocationPort` |
| Streaming direct LLM call | `IStreamingLlmInvocationPort` when supported |
| Provider-backed LLM adapter | `ProviderBackedLlmInvocationAdapter` or convention-aligned equivalent |
| Future ordinary LLM conversation | `ILlmConversationService`, `ILlmConversationStore` in an application boundary |
| MAF state compatibility | `IMafRuntimeStateCompatibilityPolicy` |

Names may be adjusted to match existing conventions, but responsibility boundaries are normative. Agent execution/continuation/diagnostic ports belong in `Runtime.Abstractions`; direct/streaming LLM ports belong in `Llm.Abstractions`. Do not merge them merely because both invoke a model.

## Transitional facades

Allowed temporarily:

- `IAgentRuntime` delegates to narrow ports.
- `MafAgentRuntime` delegates to MAF adapter components.
- `AgentRuntimeTransientContext` maps to separate model context and authority.

Every facade must have:

- an owner subbundle,
- a production caller inventory,
- a removal subbundle,
- a source assertion preventing new callers,
- no new behavior.

## Forbidden ownership

- `MafAgentRuntime.Processes.cs`
- `RuntimeContextManager`
- `AgentEverythingFactory`
- `CommonAgentContext`
- process recovery in MAF
- UI fragments as tool authorization policy
- a new partial file for any extracted responsibility
- lightweight LLM contracts inside the MAF project
- provider SDK types in `Llm.Abstractions`
- ordinary LLM chat implemented as a tool-disabled `AgentDefinition`
- a second provider credential/HTTP/retry/usage stack for lightweight inference
