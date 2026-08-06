# Static caller and registration snapshot

Baseline: `51d9a2f071e9a5f295abac884c8c667328462cc4`.

This is a preparation-time map from GitHub search. SB00 must replace it with current-branch CodeAnalytics and direct repository evidence.

## Direct `MafAgentRuntime` construction candidates

- `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `tools/Diagnostics/CanDoItAll.OpenAiContextProbe/Program.cs`
- provider-health tests and runtime tests

## Broad runtime execution callers

- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowLlmComponentInvoker.cs`
- diagnostic probe tooling
- scenario harness tests

## Approval/continuation caller families

- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/FloatingAgentChatContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.ExecutionFacade.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionOrchestrator.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Hosting/ScenarioHarnessAgentRuntime.cs`
- contextual workspace components and related tests

## MAF registration/runtime graph

- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeServiceCollectionExtensions.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs`
- Hosting and AgentFramework module registration extensions
- workspace factory manual graph

## Ambient execution audit consumers

`WorkspaceExecutionAuditContext.BeginScope` participates in execution, process-lease cleanup/recovery, Processes cancellation integration, project/workspace tools and tests. It must not be removed or redefined as telemetry-only without mapping every authorization consumer to an explicit policy object first.

## Required SB00 output

Produce exact symbol references, implementation lists, project/module dependencies, DI registrations, lifetime ownership, source-based tests, and public API callers. Mark every item as:

- migrate in named subbundle;
- compatibility-only until SB18;
- test/diagnostic adaptation;
- unrelated false positive.

## Provider-neutral lightweight inference candidates

- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Contracts/ProviderCapabilityContracts.cs`
  - `IProviderChatCompletionDriver`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Contracts/ProviderRequestContracts.cs`
  - provider-neutral completion request/result/message/attachment models
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs`
  - provider runtime pool/handle dispatch for health, test chat, image chat, and model administration
- provider driver implementations for OpenAI, Azure OpenAI, and Ollama
- provider runtime lifecycle, dispatch-lane, batch-balancer, and concrete-driver tests

SB00 must determine which pieces are genuinely MAF-specific and which belong to a provider application/runtime boundary reusable by `ILlmInvocationPort`. The target must not create a second credential, HTTP client, dispatch, retry, model-normalization, or usage-accounting stack.
