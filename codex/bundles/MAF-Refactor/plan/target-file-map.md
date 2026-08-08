# Target file and project map

This is a target ownership map, not permission to create broad empty projects. SB00 must confirm paths and dependency direction before creation.

## Proposed new project

### `CanDoItAll.AgentFramework.Runtime.Abstractions`

Suggested path:

`src/MAF/Common/CanDoItAll.AgentFramework.Runtime.Abstractions/`

May reference:

- `CanDoItAll.AgentFramework.Models`
- narrowly justified SDK-free shared kernel contracts

Must not reference:

- MAF or `Microsoft.Extensions.AI`
- provider SDKs
- UI/components
- product modules
- persistence implementations

Suggested contracts:

- `IAgentExecutionRuntime`
- `IAgentContinuationRuntime`
- `IHostedAgentFactory`
- `IProviderDiagnosticsRuntime`
- `IProviderModelAdministrationRuntime`
- runtime-neutral agent execution/continuation request, result, failure, and envelope descriptors


## Proposed lightweight LLM projects

### `CanDoItAll.AgentFramework.Llm.Abstractions`

Suggested path:

`src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/`

Suggested contracts:

- `ILlmInvocationPort`
- `IStreamingLlmInvocationPort` when justified by provider evidence
- repository-owned messages, attachments, model settings, response format, usage, finish, failure, and streaming-update records

Must not reference MAF, agent/runtime session contracts, product modules, UI, workspace authority, process contracts, or provider SDK packages.

### `CanDoItAll.AgentFramework.Llm.ProviderRuntime` or equivalent focused implementation owner

Suggested path:

`src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/`

May reference:

- `CanDoItAll.AgentFramework.Llm.Abstractions`
- `CanDoItAll.AgentFramework.Providers` and the existing provider runtime/pipeline abstractions
- narrowly required provider profile/model contracts

Owns only mapping and dispatch through the existing provider runtime/driver stack. It must not create its own credentials, HTTP clients, dispatch lane, retry pipeline, usage parser, agent/session, capability graph, or workspace scope.

A future ordinary-chat application project/service depends on `Llm.Abstractions` and owns transcript persistence above the stateless port. It is not implemented as part of the provider adapter.

## Models additions

Suggested files under `CanDoItAll.AgentFramework.Models`:

```text
Conversations/AgentConversationContextModels.cs
Context/AgentUiObservationModels.cs
Context/AgentContextTransitionModels.cs
Context/AgentTurnContextModels.cs
Execution/AgentExecutionAuthorityModels.cs
Runtime/RuntimeStateEnvelopeModels.cs
```

Keep these records SDK-free. Avoid adding more unrelated types to `ConversationModels.cs` or `FloatingAgentChatModels.cs`.

## Core/application additions

Suggested files under `CanDoItAll.AgentFramework.Core`:

```text
Context/AgentTurnContextCaptureService.cs
Context/AgentConversationContextService.cs
Context/AgentContextTransitionClassifier.cs
Context/AgentModelContextComposer.cs
Execution/AgentExecutionAuthorityResolver.cs
Execution/AgentExecutionRecoveryCoordinator.cs
Workspace/Runtime/WorkspaceExecutionScope.cs
Workspace/Runtime/IWorkspaceRuntimeServicesFactory.cs
Workspace/Runtime/WorkspaceRuntimeServices.cs
```

The exact authority implementation may live in an outer module when it requires product services. Core owns the orchestration contract and invariant validation, not product authorization rules.

## AgentFramework module additions

Suggested owners:

```text
Services/FloatingAgentConversationContextCoordinator.cs
Workspace/WorkspaceRuntimeScopeFactory.cs
Services/AgentExecutionAuthorityComposition.cs
```

The module/composition layer may use `IServiceScopeFactory` internally to create an owned scope. It must expose typed results and must not leak the provider to runtime behavior.

## Workbench additions

Suggested files:

```text
AgentContext/ProjectStructureBaseObservationContributor.cs
AgentContext/ProjectStructureCanvasObservationContributor.cs
AgentContext/ProjectStructureGanttObservationContributor.cs
AgentContext/ProjectStructureSelectionObservationContributor.cs
AgentContext/ProjectStructureRuntimeGuidanceContributor.cs
AgentContext/ProjectStructureGanttObservationModels.cs
```

The Gantt contributor owns visible projection facts. Product mutation/query tools remain the authority for exact project state.

## MAF adapter additions

Suggested files:

```text
Runtime/Execution/MafAgentExecutionAdapter.cs
Runtime/Execution/MafStreamingTurnExecutor.cs
Runtime/Continuation/MafAgentContinuationAdapter.cs
Runtime/Diagnostics/MafProviderDiagnosticsAdapter.cs
Runtime/Administration/MafProviderModelAdministrationAdapter.cs
Runtime/Hosting/MafHostedAgentFactory.cs
Runtime/State/MafRuntimeStateAdapter.cs
Runtime/State/MafRuntimeStateCompatibilityPolicy.cs
Runtime/Handoffs/MafHandoffWorkflowFactory.cs
Runtime/Mapping/MafRuntimeResponseMapper.cs
```

`MafAgentRuntime.cs` is temporary delegation only and is removed in SB18 after SB17 cleanup readiness.

## Security abstraction

Suggested project:

`CanDoItAll.Security.Abstractions`

Suggested contracts moved from `Modules.Security`:

- `ISecretRuntimeResolver`
- `SecretRuntimeRequest`
- runtime purpose and consumer identity value objects/constants required by adapters

`Modules.Security` implements the abstraction. MAF references only the abstraction.

## Processes additions

Suggested files:

```text
Services/RuntimeIntegration/Policies/ProcessExecutionProviderSelectionPolicy.cs
Services/RuntimeIntegration/Recovery/ProcessAgentExecutionOutcomeRecoveryPolicy.cs
Services/RuntimeIntegration/Policies/ProcessExecutionCriticalityPolicy.cs
```

Move or replace:

- `ProcessArtifactRecoveryService` from MAF
- process-specific provider override in generic AgentFramework execution
- process source-string criticality checks where a typed policy can own them

## Workflow adapter additions

Suggested files:

```text
MafWorkflowLlmComponentInvoker.cs            # rewritten to use ILlmInvocationPort
```

The provider-backed adapter belongs in the focused LLM/provider-runtime implementation owner described above, not in the workflow project and not in MAF agent execution.

The workflow adapter must not construct a temporary product agent or chat session.

## Files expected to shrink or disappear

- `MafAgentRuntime.cs`
- `MafRuntimeAgentFactory.cs`
- `RuntimeCapabilityComposer.cs`
- `MafRuntimeDependencyResolver.cs` (prefer deletion)
- `AgentChatContextInvocationFactory.cs`
- `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `ProjectStructureAgentChatContextBuilder.cs`
- `ProjectStructureAgentChatContextProvider.razor`
- `ProcessArtifactRecoveryService.cs` in MAF (delete)

A source file merely becoming shorter is insufficient. Moved behavior must have a cohesive new owner and direct tests.

## Revision 2 additional target and caller groups

| Concern | Inspect/modify candidates |
|---|---|
| Lightweight LLM abstractions | new focused LLM abstraction project or equivalent existing SDK-free owner |
| Provider-backed LLM adapter | provider runtime pool/handle/gateway and provider driver contracts/implementations |
| Workflow migration | `MafWorkflowLlmComponentInvoker.cs`, workflow DI and usage tests |
| Future ordinary-chat boundary | application contract/project only; no UI in this bundle |
| Broad runtime callers | Core execution/workspace, Hosting, module DI, workspace factory, scheduler, mocks, harness, A2A, API test host |
| Cutover telemetry | activity/diagnostic models near application/runtime boundaries |
| Public projections | agent API response contracts and integration tests |
| Stabilization | new focused tests, source guards, fault fixtures; no production dumping-ground service |
