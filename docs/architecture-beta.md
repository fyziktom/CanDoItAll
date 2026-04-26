# CanDoItAll Architecture Beta

This page describes the current CanDoItAll architecture as of 2026-04-26. It is source-grounded in the current `CanDoItAll.slnx`, `CanDoItAll.Web`, `CanDoItAll.Composition`, `CanDoItAll.Infrastructure`, `CanDoItAll.Modules.Processes`, `CanDoItAll.Modules.AgentFramework`, and `CanDoItAll.AgentFramework.*` projects.

## Current Shape

CanDoItAll is a local-first .NET 10 Blazor Web App. The web host composes product modules, infrastructure, shared components, control-plane database profiles, MCP-facing developer services, and an AgentFramework-backed AI execution runtime.

The important architecture rule is simple: product semantics live in modules and shared services, while MCP servers and tools expose those semantics to agents. MCP projects are adapters, not competing implementations.

Primary source references:

- [`src/CanDoItAll.Web/Program.cs`](../src/CanDoItAll.Web/Program.cs)
- [`src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`](../src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs)
- [`src/CanDoItAll.Composition/ModuleAssemblies.cs`](../src/CanDoItAll.Composition/ModuleAssemblies.cs)
- [`src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`](../src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs)
- [`src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs`](../src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs)
- [`src/CanDoItAll.Modules.Processes/ProcessRunAutomationDispatchService.cs`](../src/CanDoItAll.Modules.Processes/ProcessRunAutomationDispatchService.cs)
- [`src/CanDoItAll.Modules.AgentFramework/AgentFrameworkModuleServiceCollectionExtensions.cs`](../src/CanDoItAll.Modules.AgentFramework/AgentFrameworkModuleServiceCollectionExtensions.cs)
- [`src/CanDoItAll.AgentFramework.Maf/MafAgentRuntime.Capabilities.cs`](../src/CanDoItAll.AgentFramework.Maf/MafAgentRuntime.Capabilities.cs)

## Architecture Beta Overview

```mermaid
architecture-beta
    group clients(cloud)[Clients and Agents]
    group host(server)[CanDoItAll Runtime]
    group modules(server)[Runtime Modules]
    group agent(server)[AgentFramework]
    group data(database)[Data and Files]
    group mcp(server)[MCP Sidecars]

    service browser(internet)[Blazor Browser] in clients
    service codex(internet)[Codex or MCP Client] in clients
    service web(server)[CanDoItAll.Web] in host
    service composition(server)[Composition Root] in host
    service infra(server)[Infrastructure] in host
    service processModule(server)[Processes Module] in modules
    service projectModule(server)[Projects and Workbench Modules] in modules
    service crmhr(server)[CRM HR Module] in modules
    service workspace(server)[Workspace and Prompt Modules] in modules
    service agentModule(server)[AgentFramework Module] in modules
    service agentCore(server)[AgentFramework Core] in agent
    service maf(server)[MAF Runtime Adapter] in agent
    service workspaceStore(disk)[File Sandbox Workspace] in data
    service db(database)[AppDbContext Profile] in data
    service managedFiles(disk)[Managed Artifact Store] in data
    service watch(server)[DotNetWatch MCP] in mcp
    service processesMcp(server)[Processes MCP] in mcp
    service projectStructureMcp(server)[ProjectStructure MCP] in mcp

    browser:R -- L:web
    codex:R -- L:processesMcp
    codex:R -- L:projectStructureMcp
    codex:R -- L:watch
    web:R -- L:composition
    composition:B -- T:infra
    composition:R -- L:processModule
    processModule:R -- L:agentModule
    agentModule:R -- L:agentCore
    agentCore:R -- L:maf
    infra:B -- T:db
    infra:B -- T:managedFiles
    agentCore:B -- T:workspaceStore
    processModule:B -- T:db
    processModule:B -- T:managedFiles
    projectModule:B -- T:db
    crmhr:B -- T:db
    workspace:B -- T:db
```

## C4 Context

```mermaid
C4Context
title CanDoItAll System Context
Person(user, "Local user", "Uses the Blazor app to manage projects, process runs, prompts, validation, resources, agents, and workbench surfaces.")
Person(agentClient, "AI coding agent", "Uses MCP servers and workspace tools to inspect, change, validate, and operate local work.")
System(canDoItAll, "CanDoItAll", ".NET 10 Blazor Web App with modular runtime, process automation, AgentFramework execution, local control plane, and MCP adapters.")
System_Ext(provider, "AI providers", "OpenAI, Azure OpenAI, Ollama, or compatible provider profiles selected by AgentFramework.")
System_Ext(browserMcp, "Browser and local MCP tools", "Optional local tools attached to agents for browser, workspace, process, and project-structure operations.")
System_Ext(fileSystem, "Local filesystem", "Control plane, managed SQLite profiles, file sandbox workspaces, managed artifacts, and MCP install artifacts.")

Rel(user, canDoItAll, "Uses through Blazor Server UI")
Rel(agentClient, canDoItAll, "Uses MCP adapters and runtime endpoints")
Rel(canDoItAll, provider, "Runs model requests through provider profiles")
Rel(canDoItAll, browserMcp, "Attaches tools to AgentFramework runs when configured")
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
    Container(agentFramework, "CanDoItAll.AgentFramework.*", ".NET libraries", "Agent catalog, file sandbox workspace, provider registry, MAF runtime integration, tools, MCP capabilities, execution runs, and artifacts.")
    Container(components, "CanDoItAll.Components.*", "Razor class libraries", "Base UI primitives, canvas controls, overlay windows, WebGL workbench experiments, and facade/sandbox projects.")
    Container(mcpServers, "CanDoItAll.Mcp.*", ".NET console MCP servers", "Agent-facing adapters for components, code analytics, dotnet watch, process runtime, project structure, SSH ops, and local runtime helpers.")
    ContainerDb(appDb, "AppDbContext profile", "SQLite, PostgreSQL, or in-memory", "Application state for modules and runtime records.")
    ContainerDb(controlPlane, "Control-plane files", "JSON and protected local files", "Database profiles, active profile metadata, DataProtection keys, and profile storage roots.")
    ContainerDb(workspaceFiles, "Agent workspace files", "JSON and artifacts", "Organization-scoped AgentFramework catalog, chats, execution slices, outputs, receipts, and artifacts.")
}
System_Ext(aiProvider, "AI provider")

Rel(user, web, "Uses")
Rel(agentClient, mcpServers, "Calls MCP tools")
Rel(mcpServers, modules, "Delegates to module services")
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
    Component(mafRuntime, "MafAgentRuntime", "Runtime adapter", "Builds Microsoft Agent Framework agents and attaches workspace, process, project-structure, skill, MCP, and provider-native tools.")
    Component(fileStore, "FileSandboxWorkspaceStore", "File store", "Persists organization-scoped catalog, chats, execution slices, and artifacts.")
}
ContainerDb(appDb, "AppDbContext profile", "EF Core")
System_Ext(provider, "AI provider")
System_Ext(localTools, "Workspace, process, project-structure, browser, and MCP tools")

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
    participant UI as Processes UI or MCP
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
    MAF->>MAF: Execute permitted workspace, process, project-structure, skill, MCP, or provider-native tools
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
    participant Tools as Workspace and MCP Tools
    participant Provider as AI Provider

    Request->>Service: ExecuteRunAsync(agent id, prompt, chat session, context)
    Service->>Store: Load catalog, chat session, execution state
    Service->>Store: Create or update execution run and user message
    Service->>Runtime: Execute with agent definition and runtime session
    Runtime->>Runtime: Attach workspace memory, project-structure tools, process tools, built-in tools, skills, MCP, and provider-native tools
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

- Application database profiles store module data through `AppDbContext`. The active provider can be SQLite, PostgreSQL, or in-memory for tests. Runtime database switching is mediated by profile services, a switchable DbContext factory, and `DatabaseSwitchCoordinator`.
- Control-plane and workspace files live outside the selected application database. The control plane stores profile metadata and DataProtection keys. AgentFramework file sandbox stores organization-scoped catalog, chats, execution runs, artifacts, receipts, and output files under the active profile workspace root.

This separation lets the selected app database change without losing machine-level profile metadata or AgentFramework file workspace shape.

## Module Boundaries

| Area | Responsibility |
| --- | --- |
| `CanDoItAll.Web` | Host, routes, Razor component bootstrapping, development endpoints, runtime readiness, health checks. |
| `CanDoItAll.Composition` | Runtime module registration, module assembly list, database profile bootstrapping and switching. |
| `CanDoItAll.Infrastructure` | AppDbContext factory, control plane, storage, search, readiness, managed artifacts, DataProtection, health. |
| `CanDoItAll.Modules.Processes` | Process definitions, template pack import/projection, process canvas, run state, transitions, outbox, recovery, agent dispatch, artifact records. |
| `CanDoItAll.Modules.AgentFramework` | Current-profile AgentFramework facade, CRM/HR AI-party bridge, provider runtime gateway, catalog warmup and repair. |
| `CanDoItAll.AgentFramework.Core` | Agent catalog services, execution service, workspace tools, command/file/artifact policies, execution audit trail. |
| `CanDoItAll.AgentFramework.Maf` | Microsoft Agent Framework integration, provider transport, capability composition, MCP integration, tool execution wrappers. |
| `CanDoItAll.Components.*` | Shared UI primitives, canvas, overlays, WebGL workbench experiments, facade/sandbox projects. |
| `CanDoItAll.Mcp.*` | Local or remote MCP adapters that expose canonical module/runtime behavior to agents. |

## MCP Boundaries

MCP projects are adapters:

- `CanDoItAll.Mcp.Processes` uses the same process module and active database profile as the web host.
- `CanDoItAll.Mcp.ProjectStructure` is a thin client against the web instance project-structure API.
- `CanDoItAll.Mcp.DotNetWatch` supervises development watch sessions and exposes readiness/log/status operations.
- `CanDoItAll.Mcp.CodeAnalytics` uses the sibling CodeAnalytics libraries to inspect solution and dependency facts.
- `CanDoItAll.Mcp.Components` exposes component-library discovery data to agents.
- `CanDoItAll.Mcp.SshOps` exposes SSH operations through explicit settings and policy.

The canonical rule is that MCP tools should call application services or specialized libraries. They should not fork process, project, storage, or AgentFramework behavior.

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
