# CanDoItAll Architecture Beta

This page describes the current CanDoItAll architecture as of 2026-05-09. It is source-grounded in the current `CanDoItAll.slnx`, `CanDoItAll.Web`, `CanDoItAll.Composition`, `CanDoItAll.Infrastructure`, `CanDoItAll.Modules.Processes`, `CanDoItAll.Modules.AgentFramework`, and `CanDoItAll.AgentFramework.*` projects.

## Current Shape

CanDoItAll is a local-first .NET 10 Blazor Web App. The web host composes product modules, infrastructure, shared components, control-plane database profiles, HTTP APIs, selected MCP-facing developer sidecars, and an AgentFramework-backed AI execution runtime.

The important architecture rule is simple: product semantics live in modules and shared services. HTTP APIs and MCP tools expose those semantics to agents and external automation; they must not become competing implementations. Process, project-structure, and agent automation now uses the web-hosted API control plane. The old Processes and ProjectStructure MCP servers are suppressed in the current repo state.

Primary source references:

- [`src/CanDoItAll.Web/Program.cs`](../src/CanDoItAll.Web/Program.cs)
- [`src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`](../src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs)
- [`src/CanDoItAll.Composition/ModuleAssemblies.cs`](../src/CanDoItAll.Composition/ModuleAssemblies.cs)
- [`src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`](../src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs)
- [`src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs`](../src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs)
- [`src/CanDoItAll.Modules.Processes/ProcessRunAutomationDispatchService.cs`](../src/CanDoItAll.Modules.Processes/ProcessRunAutomationDispatchService.cs)
- [`src/CanDoItAll.Modules.AgentFramework/AgentFrameworkModuleServiceCollectionExtensions.cs`](../src/CanDoItAll.Modules.AgentFramework/AgentFrameworkModuleServiceCollectionExtensions.cs)
- [`src/CanDoItAll.AgentFramework.Maf/MafAgentRuntime.Capabilities.cs`](../src/CanDoItAll.AgentFramework.Maf/MafAgentRuntime.Capabilities.cs)

## Architecture Overview

```mermaid
flowchart LR
    Browser["Blazor browser"] --> Web["CanDoItAll Web host"]
    Codex["AI coding agent"] --> Api["HTTP API control plane"]
    Codex --> Mcp["Local MCP sidecars"]

    Api --> Web
    Web --> Composition["Composition root"]
    Composition --> Infrastructure["Infrastructure"]
    Composition --> Modules["Runtime modules"]

    Modules --> Processes["Processes module"]
    Modules --> Projects["Projects and workbench"]
    Modules --> CrmHr["CRM and HR"]
    Modules --> Workspace["Workspace and prompts"]
    Modules --> AgentModule["AgentFramework module"]

    Processes --> AgentModule
    AgentModule --> AgentCore["AgentFramework core"]
    AgentCore --> Maf["MAF runtime adapter"]
    Maf --> Providers["AI providers"]
    Maf --> Tools["Workspace, API, skill, and MCP tools"]

    Infrastructure --> AppDb[("AppDbContext profile")]
    Infrastructure --> ControlPlane[("Control-plane files")]
    AgentCore --> WorkspaceStore[("File sandbox workspace")]
    Processes --> ManagedFiles[("Managed artifact store")]

    Mcp --> DevSidecars["Code analytics, components, dotnet watch, Mermaid, SSH"]
    DevSidecars --> Infrastructure
```

## C4 Context

```mermaid
C4Context
title CanDoItAll System Context
Person(user, "Local user", "Uses the Blazor app to manage projects, process runs, prompts, validation, resources, agents, and workbench surfaces.")
Person(agentClient, "AI coding agent", "Uses HTTP APIs, repo skills, selected MCP sidecars, and workspace tools to inspect, change, validate, and operate local work.")
System(canDoItAll, "CanDoItAll", ".NET 10 Blazor Web App with modular runtime, process automation, AgentFramework execution, local control plane, HTTP APIs, and selected MCP adapters.")
System_Ext(provider, "AI providers", "OpenAI, Azure OpenAI, Ollama, or compatible provider profiles selected by AgentFramework.")
System_Ext(localTools, "Browser, API, and local MCP tools", "Optional local tools attached to agents for browser, workspace, API, and sidecar operations.")
System_Ext(fileSystem, "Local filesystem", "Control plane, managed SQLite profiles, file sandbox workspaces, managed artifacts, and selected MCP install artifacts.")

Rel(user, canDoItAll, "Uses through Blazor Server UI")
Rel(agentClient, canDoItAll, "Uses HTTP APIs, repo skills, and selected MCP adapters")
Rel(canDoItAll, provider, "Runs model requests through provider profiles")
Rel(canDoItAll, localTools, "Attaches tools to AgentFramework runs when configured")
Rel(canDoItAll, fileSystem, "Stores control-plane state, managed artifacts, and file-backed agent workspace data")
```

## C4 Containers

```mermaid
C4Container
title CanDoItAll Container Model
Person(user, "Local user")
Person(agentClient, "AI coding agent")
System_Boundary(system, "CanDoItAll") {
    Container(web, "CanDoItAll.Web", "ASP.NET Core Blazor Web App", "Interactive Server host, runtime endpoints, health checks, module assembly loading, and development diagnostics.")
    Container(composition, "CanDoItAll.Composition", ".NET library", "Registers runtime modules and database switching/bootstrap services.")
    Container(infrastructure, "CanDoItAll.Infrastructure", ".NET library", "Control plane, profile-aware AppDbContext factory, storage routing, search, managed files, readiness, and DataProtection.")
    Container(modules, "CanDoItAll.Modules.*", ".NET libraries", "Feature modules for projects, processes, workbench, workspace, prompts, resources, validation, automation, CRM/HR, and AgentFramework.")
    Container(api, "CanDoItAll HTTP APIs", "Minimal APIs", "Projects, project-structure, processes, agents, and API-access endpoints hosted by CanDoItAll.Web.")
    Container(agentFramework, "CanDoItAll.AgentFramework.*", ".NET libraries", "Agent catalog, file sandbox workspace, provider registry, MAF runtime integration, tools, MCP capabilities, execution runs, and artifacts.")
    Container(components, "CanDoItAll.Components.*", "Razor class libraries", "Base UI primitives, canvas controls, overlay windows, WebGL workbench experiments, and facade/sandbox projects.")
    Container(mcpServers, "CanDoItAll.Mcp.*", ".NET console MCP servers", "Selected agent-facing sidecars for components, code analytics, dotnet watch, Mermaid, SSH ops, and local runtime helpers.")
    ContainerDb(appDb, "AppDbContext profile", "SQLite, PostgreSQL, or in-memory", "Application state for modules and runtime records.")
    ContainerDb(controlPlane, "Control-plane files", "JSON and protected local files", "Database profiles, active profile metadata, DataProtection keys, and profile storage roots.")
    ContainerDb(workspaceFiles, "Agent workspace files", "JSON and artifacts", "Organization-scoped AgentFramework catalog, chats, execution slices, outputs, receipts, and artifacts.")
}
System_Ext(aiProvider, "AI provider")

Rel(user, web, "Uses")
Rel(agentClient, api, "Calls HTTP APIs")
Rel(agentClient, mcpServers, "Calls selected MCP sidecars")
Rel(api, modules, "Delegates to module services")
Rel(mcpServers, infrastructure, "Uses diagnostics, workspace, or sidecar services")
Rel(web, composition, "Registers")
Rel(composition, infrastructure, "Adds")
Rel(composition, modules, "Adds")
Rel(web, components, "Renders")
Rel(modules, infrastructure, "Use AppDbContext, storage, search, readiness")
Rel(modules, agentFramework, "Run technical agents and bridge AI parties")
Rel(agentFramework, aiProvider, "Runs model calls")
Rel(infrastructure, appDb, "Reads and writes")
Rel(infrastructure, controlPlane, "Reads and writes")
Rel(agentFramework, workspaceFiles, "Reads and writes")
```

## C4 Process And Agent Components

```mermaid
C4Component
title Process Runtime and AI Agent Components
Container_Boundary(processes, "CanDoItAll.Modules.Processes") {
    Component(processesService, "ProcessesService", "Application service", "Creates definitions and runs, starts runs, transitions steps, records artifacts, and reads runtime details.")
    Component(transitionGuard, "ProcessStepTransitionGuard", "Domain policy", "Validates legal step transitions and branch outcome choices.")
    Component(progressionPlanner, "ProcessRuntimeProgressionPlanner", "Domain service", "Unlocks dependent steps after transitions.")
    Component(outbox, "ProcessOutboxService", "Durable outbox", "Queues process automation dispatch work.")
    Component(dispatcher, "ProcessRunAutomationDispatchService", "Automation dispatcher", "Claims ready steps, builds prompts, runs technical agents, audits tools, projects artifacts, and settles steps.")
    Component(recovery, "ProcessRunRecoveryWorker", "Hosted worker", "Recovers stranded process automation runs.")
}
Container_Boundary(agentModule, "CanDoItAll.Modules.AgentFramework") {
    Component(aiBridge, "AgentFrameworkAiTechnicalAgentBridge", "Bridge", "Projects AgentFramework catalog agents into CRM/HR AI parties and bindings.")
    Component(workspaceFacade, "CurrentProfileAgentFrameworkWorkspaceService", "Facade", "Runs organization-scoped AgentFramework operations for the active database profile.")
    Component(catalogRepair, "AgentFrameworkOrganizationCatalogRepairService", "Repair service", "Keeps organization agent catalog shape current.")
}
Container_Boundary(agentCore, "CanDoItAll.AgentFramework.*") {
    Component(executionService, "AgentFrameworkWorkspaceExecutionService", "Execution service", "Persists chat sessions, execution runs, tool receipts, approvals, artifacts, and metrics.")
    Component(mafRuntime, "MafAgentRuntime", "Runtime adapter", "Builds Microsoft Agent Framework agents and attaches workspace, process, project-structure, API, skill, MCP, and provider-native tools.")
    Component(fileStore, "FileSandboxWorkspaceStore", "File store", "Persists organization-scoped catalog, chats, execution slices, and artifacts.")
}
ContainerDb(appDb, "AppDbContext profile", "EF Core")
System_Ext(provider, "AI provider")
System_Ext(localTools, "Workspace, API, browser, and MCP tools")

Rel(processesService, transitionGuard, "Uses")
Rel(processesService, progressionPlanner, "Uses")
Rel(processesService, outbox, "Enqueues")
Rel(outbox, dispatcher, "Dispatches")
Rel(recovery, dispatcher, "Recovers")
Rel(dispatcher, aiBridge, "Resolves technical agent")
Rel(dispatcher, workspaceFacade, "Executes agent run")
Rel(workspaceFacade, executionService, "Delegates")
Rel(executionService, mafRuntime, "Runs")
Rel(mafRuntime, provider, "Sends model requests")
Rel(mafRuntime, localTools, "Calls tools")
Rel(executionService, fileStore, "Persists")
Rel(dispatcher, appDb, "Reads run state and records artifacts")
Rel(processesService, appDb, "Reads and writes runtime state")
```

## Key Runtime Classes

```mermaid
classDiagram
    class ProcessesService
    class ProcessStepTransitionGuard
    class ProcessRuntimeProgressionPlanner
    class ProcessOutboxService
    class ProcessRunAutomationDispatchService
    class ProcessRunRecoveryWorker
    class AgentFrameworkAiTechnicalAgentBridge
    class CurrentProfileAgentFrameworkWorkspaceService
    class AgentFrameworkWorkspaceExecutionService
    class MafAgentRuntime
    class FileSandboxWorkspaceStore

    ProcessesService --> ProcessStepTransitionGuard : validates
    ProcessesService --> ProcessRuntimeProgressionPlanner : unlocks
    ProcessesService --> ProcessOutboxService : queues
    ProcessOutboxService --> ProcessRunAutomationDispatchService : dispatches
    ProcessRunRecoveryWorker --> ProcessRunAutomationDispatchService : recovers
    ProcessRunAutomationDispatchService --> AgentFrameworkAiTechnicalAgentBridge : resolves agent
    ProcessRunAutomationDispatchService --> CurrentProfileAgentFrameworkWorkspaceService : executes run
    CurrentProfileAgentFrameworkWorkspaceService --> AgentFrameworkWorkspaceExecutionService : delegates
    AgentFrameworkWorkspaceExecutionService --> MafAgentRuntime : invokes
    AgentFrameworkWorkspaceExecutionService --> FileSandboxWorkspaceStore : persists
```

## Runtime Startup Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Host as CanDoItAll.Web Program
    participant Infra as Infrastructure services
    participant Composition as Runtime modules
    participant Db as AppDbContext profile
    participant Readiness as RuntimeReadinessService

    Host->>Host: Create WebApplicationBuilder
    Host->>Host: Add Razor components and Interactive Server rendering
    Host->>Infra: AddCanDoItAllInfrastructure(configuration, environment, ModuleAssemblies.All)
    Infra->>Infra: Configure database, storage, workbench, manager, and control-plane options
    Infra->>Infra: Register profile resolver, switchable DbContext factory, storage, search, readiness, and health checks
    Host->>Composition: AddCanDoItAllRuntimeDatabaseSwitching()
    Host->>Composition: AddCanDoItAllRuntimeModules(configuration)
    Composition->>Composition: Promote configured OPENAI_API_KEY when present
    Composition->>Composition: Register Security, Workspace, Projects, Workbench, Resources, Prompts, Factory, Processes, Validation, TestLab, Activity, AgentFramework, Automation, Collaboration, CRM/HR
    Host->>Host: Map managed files, development endpoints, Razor components, and health checks
    Host->>Readiness: MarkStarting(environment, urls)
    Host->>Db: EnsureCurrentProfileReadyAsync()
    Db->>Db: Ensure database exists or apply EF migrations and managed SQLite bootstrap data
    Host->>Readiness: MarkReady(environment, urls)
```

The startup sequence is important because module services assume the active database profile and file storage roots are ready before the web app is considered healthy. In development, `/_dev/runtime` exposes readiness, watch iteration, runtime PID, owner metadata, hot reload generation, and active URLs.

## Process Execution With AI Agents

The process runtime is durable. It does not simply call an agent and hope the session finishes. It materializes process state, records assignments and artifacts, uses transition guards, writes outbox records, detects stranded execution runs, and projects AgentFramework artifacts back into process evidence.

### Process Run Sequence

```mermaid
sequenceDiagram
    autonumber
    participant UI as Processes UI or API
    participant Service as ProcessesService
    participant Db as AppDbContext
    participant Outbox as ProcessOutboxService
    participant Dispatcher as ProcessRunAutomationDispatchService
    participant Bridge as AgentFrameworkAiTechnicalAgentBridge
    participant Workspace as AgentFrameworkWorkspaceService
    participant MAF as MafAgentRuntime
    participant Provider as AI Provider
    participant Storage as Managed Artifact Store

    UI->>Service: StartRunAsync(definition or launch plan)
    Service->>Db: Load published definition, roles, steps, dependencies, artifact expectations
    Service->>Db: Create ProcessRun, assignments, step runs, work briefs, decisions, journal
    Service->>Outbox: Enqueue automation dispatch
    Outbox-->>Dispatcher: DispatchAsync(run id, trigger)
    Dispatcher->>Db: Load next ready, waiting approval, or in-progress step
    Dispatcher->>Workspace: ListExecutionRunsAsync(processRunId, processStepId)
    Dispatcher->>Bridge: GetDirectorySummariesAsync(current executor party)
    Bridge->>Workspace: Resolve organization agent catalog
    Bridge-->>Dispatcher: Bound technical agent id, provider, model, capabilities
    Dispatcher->>Dispatcher: Build governed process-step prompt
    Dispatcher->>Workspace: GetOrCreateChatSessionAsync(technical agent)
    Dispatcher->>Workspace: ExecuteRunAsync(ExecutionRunRequest, AutoApprovePendingToolCalls=true)
    Workspace->>MAF: Build runtime agent and attach allowed tools
    MAF->>Provider: Send chat/model request
    Provider-->>MAF: Assistant response and tool requests
    MAF->>MAF: Execute permitted workspace, API, skill, MCP, or provider-native tools
    MAF-->>Workspace: Response, tool receipts, approvals, artifacts, metrics
    Workspace-->>Dispatcher: ExecutionRunResult
    Dispatcher->>Dispatcher: Validate required tools, branch outcome, browser or workspace evidence when required
    Dispatcher->>Storage: Place execution artifacts
    Dispatcher->>Service: RecordArtifactAsync(process artifact records)
    Dispatcher->>Service: TransitionStepAsync(completed, blocked, failed, or refused)
    Service->>Db: Apply transition, decisions, journal, conformance, run status, dependency progression
    Service->>Outbox: Enqueue next automation dispatch when allowed
```

### Step Selection And Dispatch

`ProcessRunAutomationDispatchService` only considers active runs and steps in `Ready`, `WaitingApproval`, or `InProgress`. For each candidate step it:

- skips steps without a current executor party
- checks for active or recoverable execution runs for the same process run and step
- resolves manual recovery directives from process journal entries
- resolves the executor party to an AgentFramework technical agent through `IAiTechnicalAgentBridge`
- prepares upstream durable artifact inputs for the prompt
- loads expected artifacts and branch outcomes
- detects when a step must explicitly select a branch outcome because downstream dependencies are conditional

If the bridge cannot resolve a bound technical agent, the dispatcher logs the diagnostic and moves to another eligible step instead of fabricating an executor.

### Prompt Contract

The dispatcher prompt starts with `You are executing a CanDoItAll process step.` It includes:

- process name, run name, step title, and executor name
- run objective and project-structure context when present
- work brief, handoff summary, expected outcome, and evidence expectation
- required output artifacts and response contract
- upstream artifacts and governed artifact inspection rules
- available branch outcomes
- recovery directive when a previous run is being repaired
- governed evidence rules for `workspace_stat_path`, `workspace_read_file`, browser proof, build/test proof, and concrete product write tools when the step requires them

This prompt is intentionally stricter than a generic chat message. It makes tool and evidence use part of the step contract, not optional assistant behavior.

### Completion And Recovery

The dispatcher executes a step until it reaches a settled outcome or exhausts the step-specific attempt limit. It can:

- recover an existing AgentFramework execution run for a stranded step
- adopt a concurrently-started run when another dispatcher instance already claimed the chat session
- inspect failed execution detail after `AgentChatRunFailedException`
- detect missing governed tools or evidence
- retry recoverable gaps
- switch affected agents to a healthy fallback provider when provider failure is recoverable
- project non-transient execution artifacts into managed storage
- record process artifacts against expected artifact definitions when possible
- transition the process step with an explicit completion reason

Required artifacts gate completion in `ProcessesService.Runtime.StepTransitions.cs`. A step cannot complete until required artifacts are recorded.

## AgentFramework Tool And Artifact Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Request as ExecutionRunRequest
    participant Service as AgentFrameworkWorkspaceExecutionService
    participant Store as FileSandboxWorkspaceStore
    participant Runtime as MafAgentRuntime
    participant Tools as Workspace, API, and MCP Tools
    participant Provider as AI Provider

    Request->>Service: ExecuteRunAsync(agent id, prompt, chat session, context)
    Service->>Store: Load catalog, chat session, execution state
    Service->>Store: Create or update execution run and user message
    Service->>Runtime: Execute with agent definition and runtime session
    Runtime->>Runtime: Attach workspace memory, process and project APIs, built-in tools, skills, MCP, and provider-native tools
    Runtime->>Provider: Send prompt and available tool definitions
    Provider-->>Runtime: Response and tool calls
    Runtime->>Tools: Execute permitted tool calls
    Tools-->>Runtime: Tool results, receipts, files, artifacts
    Runtime-->>Service: Runtime response with text, pending approvals, metrics, artifacts
    Service->>Store: Persist assistant message, run state, execution log, approvals, tool receipts, checkpoints, artifacts
    Service-->>Request: ExecutionRunResult
```

Tool approval is modeled in execution state. Process automation passes `AutoApprovePendingToolCalls=true`, and `AgentFrameworkWorkspaceExecutionService` can continue approved pending tool calls when the agent/session policy allows it. For normal interactive agent use, approvals can remain explicit.

## Persistence And Control Plane

CanDoItAll uses two different persistence concepts:

- Application database profiles store module data through `AppDbContext`. The active provider can be SQLite, PostgreSQL, or in-memory for tests. Runtime database switching is mediated by profile services, a switchable DbContext factory, and `DatabaseSwitchCoordinator`. Governed process-agent automation is expected to run on PostgreSQL when `Processes:Runtime:RequirePostgreSqlForAgentAutomation` is enabled; the guard prevents slow SQLite-backed multi-agent runs.
- Control-plane and workspace files live outside the selected application database. The control plane stores profile metadata and DataProtection keys. AgentFramework file sandbox stores organization-scoped catalog, chats, execution runs, artifacts, receipts, and output files under the active profile workspace root.

This separation lets the selected app database change without losing machine-level profile metadata or AgentFramework file workspace shape.

## Module Boundaries

| Area | Responsibility |
| --- | --- |
| `CanDoItAll.Web` | Host, HTTP APIs, routes, Razor component bootstrapping, development endpoints, runtime readiness, health checks. |
| `CanDoItAll.Composition` | Runtime module registration, module assembly list, database profile bootstrapping and switching. |
| `CanDoItAll.Infrastructure` | AppDbContext factory, control plane, storage, search, readiness, managed artifacts, DataProtection, health. |
| `CanDoItAll.Modules.Processes` | Process definitions, template pack import/projection, process canvas, run state, transitions, outbox, recovery, agent dispatch, artifact records. |
| `CanDoItAll.Modules.AgentFramework` | Current-profile AgentFramework facade, CRM/HR AI-party bridge, provider runtime gateway, catalog warmup and repair. |
| `CanDoItAll.AgentFramework.Core` | Agent catalog services, execution service, workspace tools, command/file/artifact policies, execution audit trail. |
| `CanDoItAll.AgentFramework.Maf` | Microsoft Agent Framework integration, provider transport, capability composition, MCP integration, tool execution wrappers. |
| `CanDoItAll.Components.*` | Shared UI primitives, canvas, overlays, WebGL workbench experiments, facade/sandbox projects. |
| `CanDoItAll.Mcp.*` | Selected local sidecars for development diagnostics, code analytics, components, Mermaid, SSH, and local runtime helpers. |

## API And MCP Boundaries

The current automation boundary is split deliberately:

- HTTP API: `/api/projects`, `/api/project-structure`, `/api/processes`, `/api/agents`, and `/api/access` are the current process, project-structure, project, and agent control surfaces.
- Repo-managed skills: `candoitall-api-project-structure`, `candoitall-api-processes`, and `candoitall-api-agents` explain how agents should use those APIs.
- Selected MCP sidecars: `CanDoItAll.Mcp.DotNetWatch`, `CanDoItAll.Mcp.CodeAnalytics`, `CanDoItAll.Mcp.Components`, `CanDoItAll.Mcp.Mermaid`, `CanDoItAll.Mcp.SshOps`, and local runtime helpers remain thin adapters for development and inspection.
- Suppressed MCPs: `CanDoItAll.Mcp.Processes` and `CanDoItAll.Mcp.ProjectStructure` are not active in the current repo state. Do not reinstall or call `candoitall_processes` or `candoitall_projectstructure`; use the HTTP API replacements.

The canonical rule is that API endpoints and MCP tools call application services or specialized libraries. They should not fork process, project, storage, or AgentFramework behavior.

## Validation Guidance

For documentation changes:

- Check that project README coverage is complete for tracked `.csproj` directories.
- Check for required diagram block types when the architecture doc is changed.
- Run `git diff --check`.

For process or AgentFramework behavior changes:

- Add or update tests around `ProcessesService`, `ProcessRunAutomationDispatchService`, and AgentFramework execution records.
- Validate required artifact gates and branch outcome selection.
- Validate recovery and provider fallback only with explicit test data.
- Use browser proof only when UI behavior changes.
