# Reviewed Source Observations

## Branch Resolution

The user wrote `process-hardening`. GitHub branch search did not find that exact branch. The available matching branch is `processes-hardening`.

Reviewed commit:

- `processes-hardening` head: `df62c356f9192d632a3a3a0f20244e641ec9e969`
- Commit message: `phase3`

## Reviewed Files

- `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Audit/WorkspaceExecutionAuditContext.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeProgressionPlanner.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Publication.cs`
- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessDefinitionForm.razor`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs`

## Positive Fixes Confirmed

- `ProcessStepOperation`, `ProcessStepTargetScope`, and `ProcessStepOperationContract` now exist in process dispatch metadata.
- Execution metadata now emits `agentProcessStepAllowedOperations`, `agentProcessStepTargetScope`, and `agentProcessStepAllowsProductMutation`.
- `ExecutionInvocationMetadata.GroundPromptExternalTargetAliases` now avoids promoting prompt-grounded aliases to writable when process metadata disallows product mutation.
- Tool policy now denies product writes to external-target paths and managed output product paths when `ProcessAllowsProductMutation` is false.
- Manager recovery projection lineage exists through `ArtifactProjectionLineage`.
- Workflow run artifacts are projected into process artifact records.
- Subprocess parent completion now routes through process-owned finalization and source-less projection becomes diagnostic journal entries.
- Missing upstream artifact materialization is journaled and downstream reactivation code exists.
- Definition linter exists and is shown in the editor.

## Remaining Concern

The runtime is better, but many safety-critical decisions are still represented as strings, heuristics, and bounded text fields. The next hardening pass should introduce durable typed state and transaction-safe lifecycle mechanics.
