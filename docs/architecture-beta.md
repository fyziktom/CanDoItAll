# CanDoItAll Architecture Beta

Last source review: 2026-06-25.

This page is the broad current architecture overview. For source-level details around process runtime, Microsoft Agent Framework, providers, and the next hardening roadmap, read [Processes, MAF, and providers implementation map](processes-maf-providers-implementation-map.md).

## Current Shape

CanDoItAll is a local-first .NET 10 Blazor Web App. The web host composes product modules, infrastructure, shared components, database profile control, HTTP APIs, selected development MCP sidecars, and an AgentFramework-backed AI execution runtime.

The important architecture rule is still simple: product semantics live in modules and application services. HTTP APIs, Blazor components, runtime tools, and MCP sidecars expose those semantics; they must not become competing implementations.

Primary source references:

- [`src/CanDoItAll.Web/Program.cs`](../src/CanDoItAll.Web/Program.cs)
- [`src/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs`](../src/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs)
- [`src/CanDoItAll.Web/Api/ProcessesApi.cs`](../src/CanDoItAll.Web/Api/ProcessesApi.cs)
- [`src/CanDoItAll.Web/ProjectStructureAgentApi.cs`](../src/CanDoItAll.Web/ProjectStructureAgentApi.cs)
- [`src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`](../src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs)
- [`src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`](../src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs)
- [`src/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`](../src/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs)
- [`src/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`](../src/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs)
- [`src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`](../src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs)
- [`src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`](../src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs)
- [`src/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs`](../src/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs)
- [`src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`](../src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs)
- [`src/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs`](../src/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs)
- [`src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`](../src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs)

## Architecture Overview

```mermaid
flowchart LR
    Browser["Blazor browser"] --> Web["CanDoItAll.Web"]
    Agent["AI or automation client"] --> Api["HTTP API control plane"]
    Agent --> Mcp["Selected local MCP sidecars"]

    Api --> Web
    Web --> Composition["Composition root"]
    Composition --> Infrastructure["Infrastructure"]
    Composition --> Modules["Runtime modules"]

    Modules --> Processes["Processes module"]
    Modules --> Workbench["Projects and workbench"]
    Modules --> AgentModule["AgentFramework module"]
    Modules --> Memory["Cognitive Memory"]
    Modules --> OtherModules["Security, workspace, plugins, prompts, scheduler, CRM/HR"]

    Processes --> ProcessLibraries["CanDoItAll.Processes.*"]
    Processes --> AgentModule
    Workbench --> Processes
    AgentModule --> AgentCore["AgentFramework core"]
    AgentCore --> Maf["MAF adapter"]
    Maf --> ProviderRuntime["Provider runtime"]
    Maf --> RuntimeTools["Built-in, project-structure, image, skill, MCP, and provider-native tools"]

    Infrastructure --> AppDb[("Active AppDbContext profile")]
    Infrastructure --> ControlPlane[("Control-plane files")]
    AgentCore --> WorkspaceFiles[("Agent workspace files")]
    Processes --> ProcessDb[("Process EF stores and projections")]
```

## Module Boundaries

| Area | Responsibility |
| --- | --- |
| `CanDoItAll.Web` | Blazor host, minimal APIs, route mapping, OpenAPI, development endpoints, managed file routes, health/readiness. |
| `CanDoItAll.Composition` | Runtime module registration, OpenAI credential promotion, Qdrant RAG wiring, database bootstrap/switching. |
| `CanDoItAll.Infrastructure` | AppDbContext factory, control plane, profile runtime, storage, search, managed files, DataProtection, readiness. |
| `CanDoItAll.Processes.*` | Generic process ids, graph/kernel contracts, builder/compiler, runtime engine/scheduler/dispatcher, EF stores, projections, templates, application services, driver abstractions. |
| `CanDoItAll.Modules.Processes` | Process DI, Blazor process workspace, AgentFramework process execution adapter, launch/dispatch queue workers, process shell navigation. |
| `CanDoItAll.Modules.Workbench` | Project structure UI/API/services and current project-structure runtime tools, including process definition link/start and subprocess launch tools. |
| `CanDoItAll.Modules.AgentFramework` | Current-profile AgentFramework facade, provider runtime gateway for workspace APIs, catalog repair/warmup, image-generation runtime tools. |
| `CanDoItAll.AgentFramework.Core` | Provider-neutral catalog, execution service, workspace file/command/artifact services, tool policy, output contracts, telemetry. |
| `CanDoItAll.AgentFramework.Maf` | Microsoft Agent Framework runtime adapter, capability composition, provider dispatch, MCP/A2A/workflow integration, structured output/finalizer handling. |
| `CanDoItAll.AgentFramework.Providers` | Provider driver contracts, runtime handles, dispatch lane gates, batching, concrete provider driver registry support. |
| `CanDoItAll.AppComponents` and `CanDoItAll.Components.*` | Shared Blazor shell, UI primitives, canvas, overlays, charts, Git/WebGL components. |
| Sibling `CanDoItAll.Mcp` repo | Development sidecars such as code analytics, components, dotnet watch, Mermaid, SSH, and local runtime helpers. |

## Runtime Startup

```mermaid
sequenceDiagram
    autonumber
    participant Host as Web host
    participant Infra as Infrastructure
    participant Composition as Composition root
    participant Db as Runtime database profile
    participant Readiness as Runtime readiness

    Host->>Host: Build Blazor web app and API services
    Host->>Infra: Register infrastructure and profile-aware DbContext services
    Host->>Composition: Register runtime database switching
    Host->>Composition: Register product modules
    Composition->>Composition: Promote configured OPENAI_API_KEY when present
    Composition->>Db: Ensure active profile schema and provider bootstrap
    Host->>Host: Map APIs, Razor components, managed files, health, and dev endpoints
    Host->>Readiness: Mark runtime ready after profile bootstrap
```

`/_dev/runtime` is the local readiness and diagnostics endpoint. API status starts at `GET /api/access/status`.

## Process Runtime

The current process implementation uses the rebuilt `CanDoItAll.Processes.*` libraries and module adapter, not the older `ProcessesService`/outbox-dispatch architecture from historical docs.

Current process flow:

```mermaid
sequenceDiagram
    autonumber
    participant Caller as UI/API/project-structure tool
    participant Launch as ProcessLaunchApplicationService
    participant Stores as Process EF stores
    participant Queue as ProcessRuntimeDispatchQueue
    participant Dispatch as ProcessRuntimeDispatchApplicationService
    participant Runtime as ProcessRuntimeEngine
    participant Adapter as AgentFrameworkProcessExecutionAdapter
    participant MAF as MafAgentRuntime
    participant Provider as Provider runtime
    participant Projection as Process projections

    Caller->>Launch: LaunchAsync(request)
    Launch->>Launch: Load template, build kernel, compile plan
    Launch->>Stores: Persist plan, run state, assignments, artifact root
    Launch->>Runtime: Activate and schedule ready steps
    Launch->>Projection: Catch up projections
    Launch->>Queue: Enqueue when Execute=true
    Queue->>Dispatch: ExecuteReadyAsync(run id)
    Dispatch->>Runtime: Claim ready work and submit strategy results
    Dispatch->>Adapter: Execute process strategy
    Adapter->>MAF: Run assigned AgentFramework agent
    MAF->>Provider: Dispatch model/tool work
    Provider-->>MAF: Response, usage, provider failures when any
    MAF-->>Adapter: Agent runtime result
    Adapter-->>Dispatch: Strategy result
    Dispatch->>Runtime: Commit result and schedule next work
    Dispatch->>Projection: Catch up projections
```

The active `/api/processes` route set is contract, launch, dispatch, cancel, rework, live, run detail, and run history. See [API control plane](api-control-plane.md).

## MAF And Runtime Tools

MAF composes runtime capabilities through `MafAgentRuntime`. It attaches built-in tools, skills, MCP/A2A capabilities, context contributions, provider-native capabilities, and registered `IAgentRuntimeToolProvider` implementations.

Current first-party runtime tool providers:

- `ProjectStructureAgentRuntimeToolProvider` from Workbench.
- `ImageGenerationAgentRuntimeToolProvider` from AgentFramework module.

There is no current concrete process runtime tool provider in `CanDoItAll.Modules.Processes`. Process operations currently flow through `/api/processes` and project-structure bridge tools. If direct process tools are reintroduced, they need an explicit provider, typed models, tool policy classification, approval behavior, and tests.

## Provider Runtime

Provider runtime services are registered by `AddMafProviderRuntimeServices`. Current provider execution uses:

- provider descriptor store/source
- credential resolver
- concrete driver registry for OpenAI, Azure OpenAI, Ollama, and ComfyUI
- HTTP client pool
- runtime handle factory and pool
- dispatch lane gate and streaming gate
- batch balancer
- provider image-generation service
- provider health/test-chat/image-chat/model-maintenance gateway

Provider failures are classified into quota/billing, rate limit, or provider error with redacted details before they reach users and execution records.

## Persistence And Control Plane

CanDoItAll uses two persistence concepts:

- Active application database profile: module data, process runtime state, process projections, provider records, workspace records, and other EF-managed runtime state.
- Control-plane and workspace files: profile metadata, DataProtection keys, file-backed AgentFramework workspace slices, artifacts, receipts, and selected local tool artifacts.

The selected database profile can change, but control-plane metadata and local workspace files remain machine-local.

## API And MCP Boundaries

The current automation boundary is split deliberately:

- HTTP API: `/api/projects`, `/api/project-structure`, `/api/processes`, `/api/agents`, `/api/workflows`, `/api/cognitive-memory`, `/api/plugins`, and `/api/access`.
- Codex/operator API skills: `candoitall-api-project-structure`, `candoitall-api-processes`, `candoitall-api-agents`, `candoitall-api-workflows`, and `candoitall-api-cognitive-memory`.
- Internal app capability templates: skills, tools, MCP servers, and access policies under `Templates/Capabilities`.
- Selected MCP sidecars: development and diagnostics helpers from the sibling `CanDoItAll.Mcp` repo.
- Suppressed MCPs: old Processes and ProjectStructure MCP servers are not current. Use the HTTP APIs plus Codex/operator API skills for external operation, and use template-backed app capabilities for internal agents.

## Validation Guidance

For documentation-only changes:

```powershell
git diff --check
```

For process, MAF, or provider behavior changes, start with focused tests around the owning service or adapter before broad solution runs. The current implementation map lists the recommended next hardening gates.
