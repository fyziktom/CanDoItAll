# Source Artifacts

No external files, screenshots, or docx artifacts were provided. The source of truth is the repository state under `C:\repositories\CanDoItAll`.

Primary repo references used during preparation:

| Artifact | Purpose |
| --- | --- |
| `C:\repositories\CanDoItAll\README.md` | Existing root README to repair and expand. |
| `C:\repositories\CanDoItAll\CanDoItAll.slnx` | Solution inventory and project grouping. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs` | Actual web host startup, module composition, readiness endpoints, and development diagnostics. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs` | Runtime module registration and database bootstrap behavior. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\ModuleAssemblies.cs` | Runtime Razor/component assembly list. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\DependencyInjection\InfrastructureServiceCollectionExtensions.cs` | Infrastructure, control-plane, persistence, storage, readiness, and health registration. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesModuleServiceCollectionExtensions.cs` | Process module services and hosted worker registration. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.RunStart.cs` | Process run start and step-run materialization flow. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs` | Step transition, artifact completion gate, dependency progression, outbox enqueue, and project-structure sync. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs` | AI-agent process-step dispatch loop, prompt construction, tool evidence rules, recovery, provider fallback, and artifact projection. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkModuleServiceCollectionExtensions.cs` | AgentFramework module services and background workers. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkAiTechnicalAgentBridge.cs` | CRM/HR AI party to technical AgentFramework catalog projection. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | Agent run persistence, chat sessions, tool approvals, and execution result records. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\MafAgentRuntime.Capabilities.cs` | Workspace, project-structure, process, MCP, skill, and tool capability attachment. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\MafAgentRuntime.Capabilities.Mcp.cs` | Local and hosted MCP capability attachment and secret binding rules. |
| `C:\repositories\CanDoItAll\docs\ui-shared-components\README.md` | Existing shared-components documentation that references the old single-project component shape. |
| `C:\repositories\CanDoItAll\docs\ui-shared-components\architecture\stack-and-architecture.md` | Existing shared-components architecture page requiring repair for the split component libraries. |
