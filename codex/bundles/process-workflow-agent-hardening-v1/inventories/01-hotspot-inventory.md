# Hotspot Inventory

## Large Responsibility Centers

| Lines observed | Path | Primary concern |
| ---: | --- | --- |
| 3913 | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` | Artifact expectation evaluation, validation, lineage, diagnostics. |
| 3515 | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs` | Workflow UI state, node editing, executor mapping, validation, persistence. |
| 3170 | `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor` | Process dashboard, status display, observability, interactions. |
| 2496 | `repo://Templates/Processes/processes/software-delivery/definition.json` | Central process template, operation contract, sidecar references. |
| 2347 | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Finalization, structured output, step transition, recovery. |
| 2115 | `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs` | Tool allow/deny policy, capability matching, approval semantics. |
| 1994 | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Dispatch orchestration, prompt construction, agent launch, context. |
| 1897 | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs` | Tool validation, browser/runtime proof rules. |
| 1766 | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` | Runtime creation, provider/capability/tool composition. |
| 1699 | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Artifact projection and writeback. |
| 1692 | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | Process run startup, initial state, launch context. |
| 1653 | `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.Staffing.cs` | Staffing, agent selection, role mapping. |
| 1455 | `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | Execution lifecycle, metrics, structured output, persistence. |
| 1366 | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProjectStructureTools.cs` | Project structure tool integration. |
| 1331 | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | Async dispatch/outbox concurrency. |
| 1217 | `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs` | Runtime command planning and validation. |
| 1199 | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | Observation and telemetry. |
| 1120 | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOperatorControlPlane.cs` | Operator actions and runtime control. |

## Refactor Rule

For each hotspot, Codex must first add or identify characterization tests before extracting behavior. The target is not a line-count contest. A file may remain large if it is a thin composition root; it must not remain a mixed policy/IO/UI/domain implementation blob.

## Candidate Test Anchors

- Existing Tetris process run fixture/API captures.
- Existing `ProcessTemplateGovernanceTests`.
- Existing `ProcessRunAutomationDispatchServiceTests`.
- Existing `ProcessRunStatusResolverTests`.
- Existing `ProviderPricingTests`.
- Existing `ApiDocsSkillsParityTests`.
- New contract drift scanner.
- New provider usage ledger tests.
- New E2E process scenario harness.
