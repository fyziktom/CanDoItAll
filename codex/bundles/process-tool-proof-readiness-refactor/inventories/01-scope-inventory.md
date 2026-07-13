# Scope Inventory

## Process Runtime Surfaces

- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`: existing process scope model with directives and instruction fragments.
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs`: persisted assignment carries allowed operations and `CapabilityScope`.
- `repo://src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeStepAssignmentStore.cs`: assignment persistence and deserialization path.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Metadata.cs`: builds execution metadata for MAF.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessCapabilityScopeTranslator.cs`: translates process scope into MAF runtime overrides.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs`: adds process-scoped instruction fragments to step brief.

## MAF Runtime Surfaces

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`: trusted governed-process metadata channel.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeCapabilityScopeModels.cs`: runtime capability override model.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Capabilities/ProcessAllowedOperationsCapabilityPolicyCompiler.cs`: maps allowed operations to classifications.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs`: composes effective runtime policies.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.RuntimeToolReceipts.cs`: receipt capture surface.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`: large policy surface that should not absorb more process-specific rules.

## HR And Project Structure Surfaces

- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs`: project-structure launch, readiness, and role matching flow.
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureProcessAssignmentDialog.razor`: HR matching dialog surface.
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/ProjectPartyAssignmentPanel.razor`: assignment UI context that may surface readiness gaps.

## Driver And Recovery Surfaces

- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverPackage.cs`: driver package and recovery provider boundary.
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`: strategy contract boundary.
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterStrategyFactory.cs`: current standard strategy composition.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRuntimeStepAssignmentRepairService.cs`: repair flow surface.

## Template Surfaces

- `repo://Templates/Processes/processes/software-delivery/definition.json`: QA and QA recheck proof instructions currently in prose.
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`: screenshot and image-analysis process that needs typed proof contracts.
- `repo://Templates/Capabilities/mcps.json`: Playwright MCP template capability.
- `repo://Templates/Capabilities/tools.json`: workspace/browser/image runtime tool templates.
- `repo://Templates/Capabilities/skills.json`: skill template registry, including process-owned image analysis guidance.
