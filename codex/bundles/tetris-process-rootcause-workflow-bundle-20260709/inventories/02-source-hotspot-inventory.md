# Source Hotspot Inventory

## Runtime And Adapter

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionGates.cs`
  - Gate evaluation lacks branch-aware context.
  - Ordering treats missing receipts before product content defect routeability.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs`
  - Product completion tool receipts are string-based and unconditional.
  - Process/capability receipts are enforced without branch outcome applicability.
  - Active launch context tool name extraction depends on string-only product receipt parsing.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`
  - Counts matching receipts by selector/current-run/success, but has no branch/purpose filtering.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
  - Unsatisfied completion gates return manager-needed result before branch-routed completion issues can emit branch signals.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Types.cs`
  - `ProcessCompletionIssue` has no route kind, target branch, issue purpose, skipped rule, or runtime gate findings reference.

## Contracts And Launch Variables

- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
  - `ProcessRequiredToolReceipt` has no branch outcome or purpose metadata.
  - `FromProductCompletionRequiredToolReceipts(JsonElement)` ignores JSON object arrays.
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
  - Step-scoped launch variable enrichment preserves selected step values but `FormatProductCompletionRequiredStringList` drops object rules.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessLaunchExecutorResolver.cs`
  - Required runtime tool extraction must continue to discover tool names from structured object rules.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRuntimeStepAssignmentRepairService.cs`
  - Assignment repair required tool names must read object rules from by-step maps.

## Domain Boundary Leaks

- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepRecoveryInstructionBuilder.cs`
  - Contains `workspace_pwsh_run_script`, `workspace_dotnet_new`, QA step keys, and QA branch keys.
  - Builds .NET/browser receipt guidance from generic application layer.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs`
  - Contains .NET tool-name heuristics in adapter evidence handling; execution must classify whether this remains a justified adapter-specific temporary bridge or moves into provider metadata.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetSolutionSetupRuntimeExecutor.cs`
  - .NET-specific runtime executor; allowed domain-specific location.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetSolutionSetupToolPlanGuard.cs`
  - .NET-specific guard; allowed domain-specific location if not used as generic branch-routing logic.

## Workbench Domain Sources

- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`
  - Correct owner for .NET/Blazor scaffold checks and tool names.
  - Must emit structured branch-aware rules and route metadata rather than plain string arrays.
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Services/WorkbenchModuleServiceCollectionExtensions.cs`
  - Existing registration point for project-structure launch variable contributors and likely domain recovery provider registration.

## Tests

- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
  - Existing adapter tests and likely home for incident regression until extracted services have focused tests.
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessCapabilityScopeContractTests.cs`
  - Contract compatibility tests for receipt scope.
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRequiredRuntimeToolNamesTests.cs`
  - Tool-name extraction tests for string and object receipt formats.
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessStepRecoveryInstructionBuilderTests.cs`
  - Must be rewritten around provider-based advice.
