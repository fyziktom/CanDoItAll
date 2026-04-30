# Evidence Map

This map references the actual uploaded snapshot inspected for this bundle.

## Security/report integrity

- `src/CanDoItAll.Web/appsettings.json:33` — provider key pattern still present. Do not copy value.
- Missing expected files from pasted report: `01-execution-report.md`, `SecretScanningTests.cs`, `AgentRecoveryModels.cs`, `AgentRecoveryModelsTests.cs`.

## Structured output and continuation

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs:247-261` — applies `ChatResponseFormat.ForJsonSchema(...)` when structured output contract is supplied.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:102-128` — process automation calls `ExecuteRunAsync(...)` with `ProcessStepOutcomeStructuredOutputContract`.
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:131-142` — pending approval continuation passes `structuredOutput: null`.
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:150-160` — auto-approved continuation passes `structuredOutput: null`.
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:606-696` — assistant message is built/persisted from raw `runtimeResponse.ResponseText` and run is completed.

## Tool governance

- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs:214` — `IsBuiltInToolEnabled(...)` returns true.
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs:612-620` — mutation classification covers workspace tools only.
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs:57-137` — process mutation tools are exposed as function tools.
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:270-325` — policy middleware can classify `InvalidOperationException`/`NotSupportedException` as policy exceptions.

## Recovery/rework

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:41-44` — carries successful tool names across attempts.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:276-278` and `:331-333` — recovery clears chat session and builds text directive.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs:25` — recovery directive builder is text-based.
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Rerun.cs:158-215` — manual rerun directive is text-based.

## UI/operations

- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsLifecycleSection.razor:117-183` — shows health and manual rerun button.
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RuntimeOperations.cs:80-105` — manual rerun uses a fixed operator reason.
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsExecutionSection.razor:1-275` — displays outbox/execution/approvals/checkpoints/tool receipts.

## Escalations

- `src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs:75-91` — blocked transitions map to escalation decision/outcome.
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs:124-144` — blocked/refused/failed add conformance observation/improvement candidate.
