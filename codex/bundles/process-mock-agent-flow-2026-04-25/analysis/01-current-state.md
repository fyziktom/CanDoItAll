# Current State

## Process Runtime

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.RunStart.cs` creates `ProcessRun`, assignments, step runs, work briefs, and queues `dispatch-run-automation` records when a run starts.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessOutbox.cs` owns asynchronous outbox dispatch and calls `IProcessRunAutomationDispatchService.DispatchAsync`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs` validates step transitions, enforces required artifacts when completing a step, records runtime decisions, and queues automation dispatch unless suppressed.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeProgressionPlanner.cs` activates dependent steps after prerequisite completion and supports branch-gated dependencies.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessStepTransitionGuard.cs` requires a selected branch outcome when a completed step has conditional dependents.

## Agent Dispatch Boundary

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs` is the bridge from process steps to technical agents. It starts assigned steps, invokes `IAgentFrameworkWorkspaceService.ExecuteRunAsync`, projects artifacts, and transitions the step to the declared final status.
- The dispatcher already requires a governed outcome marker: `PROCESS_STEP_OUTCOME` with status and optional branch outcome key.
- This is the wrong place for deterministic mock behavior. The dispatcher should keep exercising the same production path.

## AgentFramework Runtime Boundary

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts.cs` defines `IAgentRuntime` and `AgentRuntimeResponse`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` loads the agent/provider/session and calls `IAgentRuntime.ExecuteAsync`.
- `WorkspaceExecutionAuditContext` carries `ProcessRunId`, `ProcessStepId`, `SourceKind`, and execution run metadata while the runtime executes.
- Workspace file writes performed through `IWorkspaceFileService` are audited and can be projected as execution artifacts.

## Existing Harness And Gap

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkWorkspaceFactory.cs` currently wraps `MafAgentRuntime` with `ScenarioHarnessAgentRuntime`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ScenarioHarnessAgentRuntime.cs` provides deterministic scenario responses for `scenario://harness`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ScenarioHarnessSupport.cs` has scenario catalog definitions, including calculator-oriented scenarios, but they are guided harness scenarios, not role-specific process mock agents.
- The existing harness is always wired and centered around one scenario operator. The requested capability needs a separate settings-gated provider and multiple role-specific agents.

## Role And Agent Projection

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\AiTechnicalAgentBridge.cs` defines the technical agent bridge contract used by process automation.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkAiTechnicalAgentBridge.cs` projects AgentFramework agents into the CRM-HR AI party directory and binds party IDs to technical agent IDs.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkCatalogWarmupService.cs` is the right place to seed the optional mock provider and agents before synchronizing the AI party directory.
