# Source Artifacts

## Live Run Evidence

- Process run: `6f0d229f-7c7e-4322-8b73-614ba5910cc4`
- Process: `Multi-team software delivery and release governance`
- Observed status: `NeedsAttention`
- Current step: `qa-recheck`
- Current step instance: `d977da1b-ec59-4188-b45b-e6e6ef6173af`
- Role: `qa-lead`
- Executor: `Delivery QA Observer`
- Attempt number: `5`
- Blocked timestamp: `2026-07-07T11:40:24.537702-04:00`

## Execution Run Findings

- Final QA recheck execution run `d194753a-5df9-48fd-8e69-3597ef2c531c` attached Playwright Local MCP and runtime tool providers but did not invoke required runtime/browser/image proof tools.
- The result summary reported `Completed` with branch `repair-escalation`, while noting missing current-run receipts for `workspace_dotnet_run`, `browser_navigate`, `browser_snapshot`, `browser_take_screenshot`, `browser_console_messages`, `workspace_dotnet_stop`, `workspace_inspect_image`, `workspace_analyze_image`, and `workspace_analyze_images`.
- Earlier QA validation execution run `a16a78a9-df6e-4dc7-8b71-577a76d72dc2` successfully invoked runtime, Playwright, screenshot, console, and image analysis tools. That proves the environment and agent can use the tools in at least one process step.
- Repeated recheck attempts read upstream artifacts and wrote process artifacts but did not enforce the required proof receipts before outcome acceptance.

## Database And Assignment Findings

- `process_runtime_step_assignments` rows for the run had `CapabilityScopeJson = {}`.
- `qa-validation` and `qa-recheck` allowed operations included `CaptureRuntimeProof`, `LaunchRuntime`, `ReadProcessContext`, `ReadProjectStructure`, `ReadUpstreamArtifacts`, `RunValidation`, and `WriteManagedProcessArtifacts`.
- Empty capability scope means the process template did not provide a typed step-specific allow, deny, suppress, or require contract for the proof tools.

## Source References

- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessCapabilityScopeTranslator.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Metadata.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Capabilities/ProcessAllowedOperationsCapabilityPolicyCompiler.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.RuntimeToolReceipts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverPackage.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterStrategyFactory.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRuntimeStepAssignmentRepairService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Capabilities/mcps.json`
- `repo://Templates/Capabilities/tools.json`
- `repo://Templates/Capabilities/skills.json`
