# Source Hotspot Inventory

## Runtime Integration

| Hotspot | Source reference | Bundle concern |
| --- | --- | --- |
| Result conversion | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` | Completion validation order, aggregation, adapter result shaping. |
| Product content gates | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs` | Failed solution membership readback and safe/idempotent file-content diagnostics. |
| Required receipt gates | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs` | Missing `workspace_pwsh_run_script` receipt. |
| Completion issue mapping | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs` | Current manager-required mapping for completion issues. |
| Managed artifact staging | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs` | Runtime wording and acceptance order. |
| Adapter shell | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs` | Current materialization order and service extraction seam. |

## Runtime And Application

| Hotspot | Source reference | Bundle concern |
| --- | --- | --- |
| Recovery decisions | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs` | Blocked-to-manager mapping and substring failure classification. |
| Runtime state | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs` | Existing `SafeRetry` and `CurrentStepRetry` states. |
| Launch enrichment | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs` | Central place to apply resolved launch variables. |
| Subprocess bridge | `repo://src/Processes/CanDoItAll.Processes.Runtime/ParentSubprocessArtifactBridge.cs` | Child diagnostic propagation and ledger-first artifact transfer. |
| Subprocess contract resolver | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessSubprocessContractResolver.cs` | Hardcoded subprocess mapping should move toward typed template metadata. |
| Tool preflight | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeToolPreflightService.cs` | Tool name checks need exact argument/path/scope validation. |

## Workbench And MAF

| Hotspot | Source reference | Bundle concern |
| --- | --- | --- |
| .NET launch variables | `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs` | Emits script refs containing unresolved `{CurrentProcessRunId}`. |
| MAF finalizer | `repo://src/AgentFramework/CanDoItAll.AgentFramework/MafAgentRuntime.cs` | Structured finalizer validation is not process semantic acceptance. |

## Test Hotspots

| Hotspot | Source reference | Bundle concern |
| --- | --- | --- |
| Launch variable old expectations | `repo://tests/Unit/CanDoItAll.Tests.Unit/DotNetProcessLaunchVariableContributorTests.cs` | Existing expectations must change from unresolved placeholders to resolved values or pre-resolution contributor contract. |
| Project structure integration | `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs` | Integration expectations around script refs must be updated. |
| Runtime recovery | `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs` | Current safe/idempotent diagnostic mapping must become retry-oriented. |
| Runtime integration adapter | `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs` | Add aggregate diagnostics and managed artifact acceptance tests. |
| Tool preflight | `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeToolPreflightServiceTests.cs` | Extend beyond tool names. |
