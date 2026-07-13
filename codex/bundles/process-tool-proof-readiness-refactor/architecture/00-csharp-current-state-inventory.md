# C# Current State Inventory

## Responsibility Hotspots

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`: large execution-run orchestration surface. Do not add process-specific proof/fallback branching here.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`: large static policy surface. Do not grow it with template-specific tool rules.
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs`: launch/readiness/UI flow currently has broad process matching logic. Extract reusable readiness services rather than embedding contract logic in the component partial.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs`: prompt assembly already carries scoped instruction fragments. Keep prompt text here, but do not make it the enforcement mechanism.

## Existing Useful Seams

- `ProcessCapabilityScope` already provides a process-owned typed directive model.
- `AgentRuntimeCapabilityScopeOverride` already provides a trusted MAF metadata channel for governed process runs.
- `ProcessAllowedOperationsCapabilityPolicyCompiler` already maps process operations to generic capability classifications.
- `AgentFrameworkWorkspaceExecutionService.RuntimeToolReceipts.cs` already records runtime tool receipts.
- Driver abstractions under `src/Processes/Drivers` already provide a place for domain-specific recovery providers and strategies.

## Observed Gaps

- Required runtime/MCP receipts are not a first-class process contract.
- `Require` directives are not sufficient for receipt enforcement because only capability identity requirements become required capabilities.
- HR readiness can evaluate broad role fit but cannot guarantee required current-run proof.
- Finalizer recovery can accept artifact-only outcomes even when the actual requirement is current-run tool proof.
- Process templates contain important proof requirements in prose, which cannot drive suppression, readiness, or gating.

## Non-Goals

- No new generic "development QA" project is required for this bundle. The immediate need is a process-owned contract and driver architecture.
- No broad rewrite of MAF execution service or tool policy is justified.
- No migration away from existing process driver abstractions is justified.
