# Source Artifacts

## Raw And Related Inputs

- `bundle://inputs/00-original-request.md`
- `repo://codex/bundles/process-escalation-root-cause-architecture/README.md`
- `repo://codex/bundles/process-runtime-dispatch-flexibility-hardening/README.md`
- `repo://codex/bundles/process-tool-proof-readiness-refactor/README.md`
- `repo://codex/bundles/multiteam-development-escalation-repair/README.md`

## CodeAnalytics Evidence

- Snapshot id: `snap-20260707213600-f58ac646`
- Scope: `CanDoItAll.slnx` with Processes projects, `CanDoItAll.Modules.Processes`, `CanDoItAll.Tests.Unit`, and `CanDoItAll.Tests.Integration`
- Result: 14 projects, 483 documents, no blocking errors, no project cycles reported by `code_analytics_dependencies_get`
- Dashboard limitations: DI analysis reported partially interpreted factory registrations in integration tests; several class diagrams were truncated at 80 types; unrelated `Microsoft.OpenApi` known-vulnerability warnings appeared during workspace processing.

## Primary Source References

- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Rework.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeScheduler.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessTemplateKernelBuilder.cs`
- `repo://src/Processes/CanDoItAll.Processes.Builder/ProcessInstancePlan.cs`
- `repo://src/Processes/CanDoItAll.Processes.Builder/ProcessInstancePlanCompiler.Builders.cs`
- `repo://src/Processes/CanDoItAll.Processes.Core/ProcessArtifactModels.cs`
- `repo://src/Processes/CanDoItAll.Processes.Core/ProcessGraphKernel.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionRetryPolicy.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs`
