# C# current-state inventory

## Scope

This inventory covers the floating chat context path, execution orchestration, MAF adapter, workflow LLM adapter, process integration, workspace service construction, and affected project references.

## Responsibility inventory

| Current owner | Responsibilities currently concentrated there | Architectural concern |
|---|---|---|
| `AgentChatContextRegistry` | active workspace position, active surface, access visibility, fragments, opaque attachments, versioning, strict capture, publication lifecycle | Correctly owns live observation, but the name and models imply broader chat authority |
| `AgentChatContextInvocationFactory` | model-context composition, digest, metadata schema, workspace scope propagation, external target propagation, completion notification | Mixes observation mapping with authority-adjacent request construction |
| `AgentChatExecutionOrchestrator` | activity admission, strict context capture, database-generation validation, invocation creation, send/continue dispatch | Knows too much about UI context mechanics |
| `FloatingAgentChatCoordinator` | active handles, agent preparation, session association, visibility, run state, cleanup | Does not model what each conversation follows |
| `AgentRuntimeTransientContext` | prompt content, workspace scope, typed opaque attachments | Combines observation payload and execution scope |
| `AgentRunTransientContextRegistry` | original context lease for a run and approval continuation | Correct fail-closed behavior, but purely in-memory and not adapter-versioned |
| `AgentFrameworkWorkspaceExecutionService` | run admission, execution, continuation, provider selection, process criticality, transient context, validation, persistence, receipts, cleanup | Distributed orchestration god object; process-specific branches remain in generic Core |
| `MafAgentRuntime` | execute, resume, streaming, background polling, finalizer short-circuit, repair, response assembly, session persistence, diagnostics | Broad runtime facade |
| `MafRuntimeAgentFactory` | provider/model selection, runtime build, capability composition, handoff participants, instrumentation, policy middleware, logging | Construction and execution policy are mixed |
| `RuntimeCapabilityComposer` | memory, context contributors, skills, workspace tools, runtime providers, A2A, MCP, compaction, storage, path resolution | Extension hotspot and mixed scope sources |
| `MafRuntimeDependencyResolver` | provider graph lookup, workspace service lookup, fallback construction | Service locator and hidden fallback behavior |
| `ProjectStructureAgentChatContextBuilder` | base project facts, current-view facts, selection facts, extensive agent operational guidance | UI observation and runtime guidance are mixed |
| `ProjectStructureAgentChatContextProvider` | access projection, active view, project invocation snapshots, external-target attachments, freshness timer, completion refresh | Large module-level context publisher |
| `ProjectStructureGanttPanel` | projection, warnings, assignments, view state, row order, task/dependency/schedule mutations, dialogs | Rich Gantt facts are not exposed to the floating agent observation |
| `ProcessArtifactRecoveryService` inside MAF | process artifact path, status parsing, blocked semantics, current-execution write evidence, process outcome construction | Product/process semantics leak into framework adapter |
| `MafWorkflowLlmComponentInvoker` | provider lookup, temporary agent/session creation, runtime call, JSON validation, project-scope inference from payload | Ordinary LLM transform uses full agent runtime and parses authority from payload |

## Current end-to-end floating send path

```text
Active Razor surface
  -> AgentChatContextSurfaceProvider
  -> AgentChatContextRegistry current publication
  -> user presses Send
  -> AgentChatExecutionOrchestrator captures strict snapshot
  -> AgentChatContextInvocationFactory composes transient content + scope + attachments
  -> ExecutionRun metadata receives digest and context metadata
  -> AgentRunTransientContextRegistry leases exact payload by run id
  -> AgentFrameworkWorkspaceExecutionService builds runtime options
  -> MafAgentRuntime executes
```

## Current view-switch behavior

Project Structure already maps `structureViewIndex` to a distinct `ProjectStructureAgentChatView`. The provider republishes context when `ActiveView` changes, so the next send sees Gantt rather than Canvas.

The current implementation does **not** provide:

- a durable or session-owned record that the conversation changed view,
- a trusted transition classification,
- a visible chat-level `following current context` state,
- detailed Gantt projection facts,
- a clean distinction between a view observation and authority to mutate the project.

## Current continuation behavior

The execution run records a transient-context digest. The original transient object is held in `AgentRunTransientContextRegistry`. Approval continuation resolves that exact object and fails if it has been lost. This behavior is a required invariant.

## Existing provider-neutral lightweight-inference foundation

The repository already has a lower-level provider architecture that should be reused rather than bypassed:

- `IProviderChatCompletionDriver` is an SDK-neutral provider contract.
- OpenAI, Azure OpenAI, and Ollama implement that contract behind the provider driver registry.
- `IProviderRuntimePool` and runtime handles own provider lifecycle and dispatch.
- dispatch lanes, credentials, model normalization, and provider failure behavior already exist below the agent runtime.
- `MafProviderRuntimeGateway` currently exposes test-chat behavior over that infrastructure, but it is too broad and MAF-named to become the application LLM port.

This means the target lightweight LLM architecture is **not** a reduced `MafAgentRuntime`. It is an application-facing, SDK-free invocation port implemented over the existing provider runtime/driver layer. The workflow adapter and a future ordinary-chat application service should depend on that port, while provider protocol behavior remains in provider drivers.

SB00 must confirm whether ordered messages, streaming, response-format/schema mapping, attachments, thinking/reasoning controls, usage details, and blank-response retry are already complete in the provider contracts. Extend the provider boundary additively only where evidence shows a real gap.

## Production caller and adaptation breadth

The runtime split affects more than the direct `RunAsync` call sites. Characterization and migration must include:

- `AgentFrameworkWorkspaceExecutionService` send and approval continuation paths;
- workspace facade/service layers and current-profile wrappers;
- Hosting and module registration;
- `CanDoItAllAgentWorkspaceFactory` manual construction;
- `ProcessMockAgentRuntime` and `ScenarioHarnessAgentRuntime`;
- provider health, provider test chat, image chat, and model administration;
- hosted/A2A and handoff construction;
- `MafWorkflowLlmComponentInvoker`;
- SchedulerPlanner and every module that registers runtime tool providers;
- `CanDoItAll.OpenAiContextProbe`;
- API test hosts and other tests that manually assemble the runtime graph;
- source-based architecture tests that encode the current type names or file locations.

A successful compile after changing `IAgentRuntime` is not sufficient proof. Each family must be assigned to one target port or explicitly remain behind a temporary facade with a deletion owner.

## Constructor and service-location audit targets

Claude Code must measure and record:

- constructor parameter count,
- retained fields,
- `IServiceProvider` use,
- direct `new` fallback paths,
- concrete SDK/product type references,
- call-site count

for:

- `MafAgentRuntime`
- `MafRuntimeAgentFactory`
- `RuntimeCapabilityComposer`
- `MafRuntimeDependencyResolver`
- `AgentFrameworkWorkspaceExecutionService`
- `CanDoItAllAgentWorkspaceFactory`
- `AgentChatContextInvocationFactory`
- `AgentChatExecutionOrchestrator`
- `ProjectStructureAgentChatContextProvider`
- `ProjectStructureAgentChatContextBuilder`

## Current compile-time concerns

The MAF project currently references product/outer projects, including:

- `CanDoItAll.Modules.Security`
- `CanDoItAll.Modules.Workspace`
- `CanDoItAll.AgentFramework.Workflows.MafAdapter`

The first two are forbidden in the target architecture. The workflow adapter reference must be removed or justified by moving the MAF-native handoff implementation to the correct adapter owner.

## Existing tests to preserve and extend

- `FloatingAgentChatArchitectureTests`
- `AgentChatPanelResponsivenessTests`
- `AgentChatExecutionActivityOrchestratorTests`
- `CurrentProfileAgentExecutionActivityAdmissionTests`
- `MafRuntimeArchitectureServicesTests`
- `MafApprovalSessionRoundTripTests`
- `MafAgentRuntimeHandoffTests`
- `MafWorkflowAdapterIsolationTests`
- `AgentFinalizerPolicyTests`
- process runtime metadata, dispatch, recovery, and completion tests

## Missing proof at baseline

- Same-thread Canvas -> Gantt transition is represented explicitly.
- Current run remains Canvas while the UI switches to Gantt.
- Next turn receives a transition and Gantt facts.
- Cross-project transition re-resolves authority.
- UI context cannot grant mutation authority.
- Every scope-bound workspace service shares one identity.
- MAF project contains no `Modules.*` reference.
- MAF project contains no process-specific type or source-kind branch.
- Narrow runtime ports are used by all production callers.
- Workflow LLM payload cannot select workspace authority.
- Provider-backed lightweight invocation does not construct an agent, session, capability graph, or workspace scope.
- Every production broad-runtime caller family is mapped to a narrow port or named transitional facade.
- Mock, scenario, diagnostic, API-test-host, and manual-composition paths use the same production seams.
