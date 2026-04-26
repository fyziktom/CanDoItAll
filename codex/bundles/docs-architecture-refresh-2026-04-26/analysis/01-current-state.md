# Current State

## Repository Shape

- `C:\repositories\CanDoItAll\CanDoItAll.slnx` lists the main product, test, and tool projects.
- A CodeAnalytics snapshot of `C:\repositories\CanDoItAll\CanDoItAll.slnx` reported `53` projects and `1190` documents for the loaded solution snapshot.
- `git ls-files` found tracked `.csproj` files under `src`, `tests`, and `tools`; none of their project directories had a root-level `README.md` before this bundle started.
- Existing docs include a root `README.md`, `docs\*.md`, UI shared-component docs, architecture ADRs/reviews, Codex skill docs, template docs, and several historical bundle folders.

## Actual Runtime Architecture

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs` is the runtime host. It adds Razor components with Interactive Server rendering, loads BaseLib, infrastructure, runtime database switching, runtime modules, MermaidJS, development manager client services, managed files, development diagnostics, module assemblies, and health checks.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs` composes runtime modules in this order: Security, Workspace, Projects, Workbench, Resources, Prompts, Factory, Processes, Validation, TestLab, Activity, AgentFramework, Automation, Collaboration, and CRM/HR. It also promotes configured `OPENAI_API_KEY` values into the AgentFramework credential environment.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\ModuleAssemblies.cs` defines the additional Razor component assemblies loaded by the web host.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\DependencyInjection\InfrastructureServiceCollectionExtensions.cs` owns control-plane options, database profile services, runtime database switching foundation, storage drivers, DataProtection keys, readiness, health, background job queue, search, and managed artifact storage.

## Process And AI-Agent Execution

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesModuleServiceCollectionExtensions.cs` registers process services, template pack loading/projection, canvas services, outbox, process automation dispatch, runtime reads, recovery, and hosted workers when the local runtime lane allows background workers.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.RunStart.cs` creates `ProcessRun`, `ProcessRunAssignment`, `ProcessStepRun`, `ProcessWorkBrief`, journal, and outbox records from a published definition or approved launch plan. It validates the published step graph and initializes root steps as `Ready` or `WaitingApproval`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs` validates step transitions, checks required artifacts before completion, applies dependency progression, updates run status, writes decision/journal/conformance records, syncs project structure, and enqueues automation dispatch unless suppressed.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs` loads the next dispatchable process step, resolves the current executor party to a technical AgentFramework agent, builds a governed process-step prompt, creates or reuses an AgentFramework chat session, executes the run with auto-approved pending tool calls, detects missing required tools/evidence, handles retries and recovery, projects execution artifacts into managed storage, and transitions the step to the settled status.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.GovernedOutcomes.cs` requires governed step outcomes and maps declared branch outcomes back to process branch definitions.

## AgentFramework Architecture

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkModuleServiceCollectionExtensions.cs` registers the profile-scoped file sandbox store, provider registry, workspace service, organization catalog repair, catalog warmup, execution recovery, scenario harness, provider runtime gateway, and AI technical-agent bridge.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkAiTechnicalAgentBridge.cs` synchronizes AgentFramework organization catalog agents into CRM/HR `Party` records of type `AiAgent` and `AiResourceBinding` records. It also resolves effective provider/model/capability summaries for staffing and process assignment.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` persists execution runs, chat sessions, assistant messages, metrics, tool receipts, artifacts, pending approvals, and continuation behavior.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\MafAgentRuntime.Capabilities.cs` attaches workspace memory, project-structure tools, process tools, built-in workspace tools, skill execution, MCP capabilities, and provider-native tool capabilities based on the agent catalog and provider support.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\MafAgentRuntime.Capabilities.Mcp.cs` supports logical MCP seams, provider-native hosted MCP, and local stdio MCP launch with explicit allowed tools, interpreter policy, approval mode, and secret bindings.

## Documentation Gaps Found

- The root README gives useful commands but does not expose the current architecture in enough detail.
- `docs\ui-shared-components\README.md` and `docs\ui-shared-components\architecture\stack-and-architecture.md` still describe the component library mostly as `CanDoItAll.Components`, while the actual architecture is split across `Components.Common`, `Components.BaseLib`, `Components.CanvasLib`, `Components.OverlayLib`, `Components.WebGlLib`, facade/sandbox projects, and module consumers.
- There is no docs landing page.
- There is no top-level architecture landing page.
- There is no single current architecture-beta document with overview, C4, and process AI-agent sequence diagrams.
- Project directories do not have README coverage.
