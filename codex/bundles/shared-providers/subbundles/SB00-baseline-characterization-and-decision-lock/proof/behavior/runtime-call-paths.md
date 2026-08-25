# SB00 provider runtime call-path characterization

Captured: 2026-08-24  
Evidence mode: static source characterization  
Product behavior changed by this artifact: **No**

## Decision

A shared provider is an outer Workspace origin that projects to an existing effective runtime
profile. Ordinary execution must continue through the existing canonical provider snapshot,
runtime pool, and typed provider drivers. It must not add `ProviderKind.Shared`, construct a
second runtime stack, or route Agent, Simple Chat, Workflow, health, and image operations through
one misleading generic adapter.

## Ordinary Agent path

1. `AgentChatExecutionOrchestrator.SendMessageCoreAsync` starts the UI chat orchestration at
   `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionOrchestrator.cs:140`.
2. `AgentFrameworkWorkspaceExecutionService` calls
   `IAgentExecutionRuntime.ExecuteAsync` at
   `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:1388`.
3. `MafAgentExecutionAdapter.ExecuteCoreAsync` builds the runtime through
   `MafRuntimeAgentFactory.CreateRuntimeBuildAsync` at
   `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Execution/MafAgentExecutionAdapter.cs:80,121`.
4. `MafRuntimeAgentFactory.CreateRuntimeBuildAsync` delegates provider-agent construction to
   `MafProviderAgentFactory.CreateFrameworkAgent` at
   `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs:84,242`.
5. The provider-kind switch is in
   `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderAgentFactory.cs:219`.

The shared Workspace connector must be mapped before step 2 to its effective OpenAI-compatible
kind, transport, endpoint, model, and secret reference. Agent execution then remains an ordinary
provider run and retains the current agent/session/tool/memory/approval semantics.

## Simple Chat path

Simple Chat deliberately does **not** construct an agent:

1. `LlmChatConversationEngineFactory.Create` wraps the provider invocation ports with audited and
   persistence-fenced ports at
   `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime/LlmChatConversationEngineFactory.cs:22`.
2. `LlmConversationService` calls `ILlmInvocationPort.InvokeAsync` at
   `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationService.cs:140`.
3. `ProviderBackedLlmInvocationAdapter.InvokeAsync` registers the descriptor, leases
   `IProviderRuntimePool`, and resolves `IProviderChatCompletionDriver` at
   `src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs:21,133,158`.
4. Streaming resolves `IProviderStreamingChatCompletionDriver`, with the completed chat driver as
   the supported fallback, at
   `src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmStreamingInvocationAdapter.cs:332,341,352`.

The shared profile must therefore work through the provider runtime descriptor/pool contracts;
an Agent-only integration would leave Simple Chat unsupported.

## Workflow LLM path

`WorkflowLlmComponentInvoker.ExecuteAsync` resolves a profile from
`IProviderRuntimeProfileSource`, validates that it is enabled and chat-purpose, and invokes
`ILlmInvocationPort` at
`src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowLlmComponentInvoker.cs:20,70,119`.
`WorkflowLlmServiceCollectionExtensions.AddWorkflowLlmInvocation` registers the same
provider-backed stateless invocation port at
`src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowLlmServiceCollectionExtensions.cs:18`.

This path constructs no agent, session, tools, memory, or workspace authority. Shared-provider
compatibility belongs in the profile projection and typed chat driver path, not in the workflow
invoker.

## Health path

The active Agent UI health button calls
`AgentProviderProfilesPanel.TestProviderAsync` at
`src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs:145`.
The call then flows through:

- `CurrentProfileAgentFrameworkWorkspaceService.TestProviderAsync`,
  `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs:315`;
- `AgentFrameworkWorkspaceCatalogService.TestProviderAsync`,
  `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.ProvidersAndCapabilities.cs:44`;
- `ProviderDiagnosticsService.TestProviderAsync`,
  `src/MAF/Common/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs:726`;
- `MafProviderDiagnosticsAdapter`,
  `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Diagnostics/MafProviderDiagnosticsAdapter.cs:11`;
- `MafProviderRuntimeGateway.TestProviderAsync`, which leases the runtime pool and resolves
  `IProviderHealthDriver`,
  `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs:83,100`.

The Workspace-facing `IProviderRuntimeGateway` is replaced by
`AgentFrameworkProviderRuntimeGateway` in the full application at
`src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs:345`.
Its `CheckHealthAsync` delegates to the organization AgentFramework workspace at
`src/Modules/CanDoItAll.Modules.AgentFramework/Providers/AgentFrameworkProviderRuntimeGateway.cs:16`.

## Image paths

Image generation is a dedicated driver path:

- Agent image tools call `ProviderRuntimeImageGenerationService.GenerateAsync` through
  `ImageGenerationAgentRuntimeToolProvider` at
  `src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs:122`.
- `ProviderRuntimeImageGenerationService` registers the runtime descriptor, leases
  `IProviderRuntimePool`, and resolves `IProviderImageGenerationDriver` at
  `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageGenerationService.cs:13,66,110`.

`MafProviderRuntimeGateway.RunProviderImageChatAsync` at
`src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs:155`
is image **analysis/chat**, not generation. The prepared assumption that this gateway method was
the generation path is amended. Shared catalog capability projection must distinguish chat with
image input from image generation.

## Legacy Workspace-only path

`LegacyProviderRuntimeGateway` at
`src/Modules/CanDoItAll.Modules.Workspace/Providers/ProviderRuntimeGateway.cs:15` directly loads
the Workspace EF row, resolves `ProviderRegistry`/`IProviderAdapter`, resolves the secret, calls
the adapter, and persists health state. Workspace registers it as the fallback
`IProviderRuntimeGateway` at
`src/Modules/CanDoItAll.Modules.Workspace/Services/WorkspaceModuleServiceCollectionExtensions.cs:27`.

In the normal host, Workspace is registered before AgentFramework and the scoped registration at
`AgentFrameworkModuleServiceCollectionExtensions.cs:345` is effective. The legacy gateway still
matters for Workspace-only hosts and direct/concrete use, so a new shared connector cannot claim
legacy compatibility unless its Workspace adapter supports the requested operation. Legacy
support must not become a silent fallback when the canonical AgentFramework runtime fails.

## Runtime risks locked for implementation

- A connector-origin branch inside `MafProviderAgentFactory` would couple central-publication
  metadata to the inner runtime and still miss Simple Chat and Workflow.
- A generic `IProviderAdapter.SendAsync` implementation alone would cover only the legacy path;
  it would not prove ordinary Agent, Simple Chat, Workflow, or image support.
- Treating image analysis and generation as one capability would advertise unsupported calls.
- Resolving source credentials before the prepared dispatch scope would extend secret lifetime
  and permit inconsistent credential reads during a single invocation.
- Returning stale runtime profiles after a source/import commit would violate the existing
  fail-closed snapshot contract.

## Minimal characterization tests

1. Persist one profile at a known canonical snapshot revision and assert Agent, Simple Chat, and
   Workflow each resolve that revision while reaching their existing typed execution port.
2. Use fakes around `MafProviderAgentFactory` and `IProviderRuntimePool` to prove Agent constructs
   an agent, while Simple Chat and Workflow never do and instead resolve chat drivers.
3. In the full module host, assert the Workspace health surface resolves
   `AgentFrameworkProviderRuntimeGateway` and reaches `IProviderHealthDriver`; in a Workspace-only
   host, assert `LegacyProviderRuntimeGateway` is selected explicitly.
4. Advertise image-analysis-only and image-generation-only profiles and assert each operation
   rejects the other capability instead of falling back.
5. Mutate or delete the canonical profile after a warmed runtime snapshot and assert subsequent
   Agent, Simple Chat, Workflow, health, and image acquisition observe the new revision or fail
   closed; none may use stale endpoint/credential metadata.

