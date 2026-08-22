# Agent Runtime Tool Surface

This page defines the boundary between tools attached to an agent execution and operations exposed only through the HTTP control plane.

## What Makes A Tool Executable

A capability template or API route does not create an agent tool. A direct runtime tool must come from one of:

- a MAF built-in or workspace capability
- a configured MCP or A2A descriptor
- a provider-native tool
- a registered [`IAgentRuntimeToolProvider`](../src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs)

[`RuntimeToolProviderComposer`](../src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeToolProviderComposer.cs) evaluates provider descriptors and asks eligible providers to create typed `AITool` instances for the current invocation.

Registration is only the first gate. Actual attachment depends on:

- execution purpose, such as interactive chat or governed process automation
- agent status, permissions, and capability assignments
- provider descriptor supported purposes
- project, process, HR, scheduler, memory, or curator authorization scope
- tool invocation policy and approval requirements

## Tool Call Scheduling Policy

CanDoItAll permits a provider to return multiple tool calls in one model response, but the
MAF invocation layer executes those calls serially in provider order. The central agent
options factory explicitly sets `AllowConcurrentInvocation` to `false`; approval-required
calls remain dependency barriers and are not bypassed by later calls in the same response.

Concurrent tool execution is not a configurable runtime capability. Enabling it requires a
separate design for ordering, authorization, cancellation, side-effect isolation, receipts,
and replay safety. Declaration-only function-call storage is likewise not enabled by the
current runtime policy.

## Current First-Party Providers

| Provider | Source | Responsibility |
| --- | --- | --- |
| Memory | [`MemoryAgentRuntimeToolProvider.cs`](../src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Tools/MemoryAgentRuntimeToolProvider.cs) | Generic provider-backed context query and operation status. |
| Project Structure | [`ProjectStructureAgentRuntimeToolProvider.cs`](../src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs) | Projects, hierarchy, tasks, nodes, assets, links, leases, analytics, and process/workflow bridges. |
| Image Generation | [`ImageGenerationAgentRuntimeToolProvider.cs`](../src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs) | Provider-backed image generation or editing to a managed workspace path. |
| Workflow | [`WorkflowAgentRuntimeToolProvider.cs`](../src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/WorkflowAgentRuntimeToolProvider.cs) | Workflow discovery, launch, status, cancellation, and external-response operations. |
| Prompt Gallery | [`PromptGalleryAgentRuntimeToolProvider.cs`](../src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/PromptGalleryAgentRuntimeToolProvider.cs) | Compatible final Prompt Gallery discovery and retrieval. |
| Prompts Curator | [`PromptsCuratorAgentRuntimeToolProvider.cs`](../src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/PromptCurator/PromptsCuratorAgentRuntimeToolProvider.cs) | Authorized Prompt Gallery curation. |
| Workflow Curator | [`WorkflowCuratorAgentRuntimeToolProvider.cs`](../src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/WorkflowCurator/WorkflowCuratorAgentRuntimeToolProvider.cs) | Authorized workflow definition and component curation. |
| Capability Curator | [`CapabilityCuratorAgentRuntimeToolProvider.cs`](../src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/CapabilityCurator/CapabilityCuratorAgentRuntimeToolProvider.cs) | Authorized capability catalog curation and validation. |
| HR | [`HrAgentRuntimeToolProvider.cs`](../src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/Hr/HrAgentRuntimeToolProvider.cs) | Identity-bound agent governance, usage analysis, process review, avatar generation, and privacy-safe CRM/HR queries. |
| Scheduler | [`SchedulerAgentRuntimeToolProvider.cs`](../src/Modules/CanDoItAll.Modules.SchedulerPlanner/AgentTools/SchedulerAgentRuntimeToolProvider.cs) | Identity-bound workflow target/schedule discovery and workflow schedule creation. |

Do not publish a copied count or complete tool-name inventory here. Provider code and runtime metadata are the source of truth, and attachment varies by invocation.

## Process Boundary

There is no general first-party `ProcessAgentRuntimeToolProvider`. Current process operations use:

- the `/api/processes` HTTP family
- governed execution through `AgentFrameworkProcessExecutionAdapter`
- Project Structure bridge tools for definition linking, process start, and governed subprocess launch
- the Blazor process workspace

Process run-record search, analytics, summary, graph, detail, and history are HTTP operations. Do not represent them to an agent as direct tools unless a concrete provider is implemented, registered, policy-classified, and tested.

## Project Structure Read Sources

`project_structure_read` uses the typed `ProjectStructureReadSource` contract:

- `ContextDefault` selects an invocation snapshot only for an eligible interactive Project Structure invocation; otherwise it selects canonical current state.
- `InvocationSnapshot` requires the exact bounded snapshot attached to the invocation and fails closed when scope, profile generation, freshness, fingerprint, or requested coverage does not match.
- `CanonicalCurrent` reads through the canonical Project Structure application service.

There is no silent snapshot-to-database fallback. Writes always go through canonical services and authorization gates. The HTTP Project Structure read endpoint has no in-process invocation attachment, so `ContextDefault` is normalized to canonical state and `InvocationSnapshot` is rejected.

See [Internal communication](architecture/internal-communication.md) for the execution
and live-event communication contract.

## HTTP And Runtime Authorization Are Different

Bearer authorization to an HTTP endpoint is not an agent capability grant. Conversely, an agent capability assignment is not a bearer token or permission to call an external API route.

Integrations that require agent-level controls must use a governed runtime execution. External automation must use the API authorization and route policy. Never infer one authority model from the other.

## Adding A Provider Or Tool

Adding a direct tool is a runtime and security change. The minimum implementation is:

1. A typed application/domain operation in the owning module.
2. A provider registration or existing provider extension.
3. Strongly typed request and result models.
4. Accurate descriptor metadata and supported purposes.
5. Explicit scope and authorization checks.
6. `IAgentToolInvocationPolicy` evaluation, `AgentToolInvocationPolicyMetadata` classification, and approval review for side effects.
7. Tests proving conditional availability, denial, invocation, and receipts.
8. OpenAPI or operator documentation only when an HTTP transport also exists.

Without that set, keep the operation on its existing UI or HTTP boundary.

## Validation

Runtime-tool changes should include the owning provider tests and:

```powershell
dotnet build src\MAF\Common\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj --configuration Release
dotnet test tests\Solutions\CanDoItAll.Tests.Unit.slnx --configuration Release --list-tests --filter "FullyQualifiedName~MafAgentRuntimeToolProviderCompositionTests"
dotnet test tests\Solutions\CanDoItAll.Tests.Unit.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~MafAgentRuntimeToolProviderCompositionTests"
```

State the expected discovery count before the first command and reject zero or drifted
discovery. Run the [broad stable gate](testing.md#broad-stable-gate) only for CI,
release/merge closure, a frozen checkpoint, or a named invalidation trigger.
