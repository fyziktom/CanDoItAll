# Scope Inventory

## Runtime And Application

- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`

## Contracts And Drivers

- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard`

## Process Module And MAF Adapter

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionState.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchQueueServices.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceImageAnalysisPromptNormalizer.cs`

## Templates And Agents

- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`
- `repo://Templates/Agents/teams/dotnet-delivery`
- `repo://Templates/Agents/teams/visual-automation-templates`

## Tests To Extend

- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`
- `repo://tests/Components/CanDoItAll.Tests.Components`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright`

## Out Of Scope For This Bundle

- Blazor UI redesign.
- Changing product output artifacts for a specific Calculator or Tetris run.
- Rebuilding process templates before diagnostics/readiness foundations are in place.
- Adding live provider dependencies to unit tests.
