# Process Runtime Restoration Run Instructions

## Validation Commands
Run from the repository root.

```powershell
dotnet build CanDoItAll.slnx --configuration Debug
```

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build
```

```powershell
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessOutboxIntegrationTests|FullyQualifiedName~ProcessWorkflowExecutorIntegrationTests|FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests|FullyQualifiedName~SchedulerPlannerIntegrationTests|FullyQualifiedName~ProjectStructureAgentApi_start_process_node_SB011_INV_001|FullyQualifiedName~ProjectStructureAgentApi_execute_process_node_SB012_INV_001|FullyQualifiedName~ProcessObservationIntentResolverTests|FullyQualifiedName~ProcessTranscriptVerificationReadOnlyAdapterTests|FullyQualifiedName~ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests|FullyQualifiedName~ProcessDomainEvidenceReadOnlyAdapterTests|FullyQualifiedName~RuntimeEvidenceSourceIntegrationTests|FullyQualifiedName~RuntimeHostedWorkerPolicyIntegrationTests|FullyQualifiedName~AgentRecoveryModelsTests|FullyQualifiedName~ProcessRuntimeOperatorReadModelTests|FullyQualifiedName~Api_nested_process_runtime_routes_preserve_typed_contract_state|FullyQualifiedName~Api_process_run_detail_SB12_INV_001_exposes_upstream_missing_artifact_recovery_health"
```

```powershell
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-build --filter "FullyQualifiedName~Process_start_SB015_INV_001_large_screen_imports_template_and_executes_ready_launch_from_ui|FullyQualifiedName~Process_run_detail_recovery_SB030_large_screen_displays_blocked_recovery_and_artifact_readback|FullyQualifiedName~Project_structure_process_run_output_SB012_INV_002_opens_project_processes_from_output_folder_node"
```

```powershell
python codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py --profile initiative --stage completed --repo-root C:\repositories\CanDoItAll codex\bundles\process-runtime-execution-restoration-live-openai-completion-v1
```

## Source Scans
```powershell
rg -n "codex[/\\]bundles|C:\\repositories\\CanDoItAll\\codex|process-runtime-execution-restoration-live-openai-completion-v1" src tests -g "*.cs" -g "*.razor"
```

```powershell
rg -n "IProcessDriverHost|ProcessDriverHost|ProcessDriverRuntimeHost|GenericProcessDriverHost|IProcessDriverRegistry|ProcessDriverRegistry|ProcessDriverRuntimeSelector|ProcessDriverManagerCommand|ProcessDriverServiceCollectionExtensions|AddProcessDriver|MapProcessDriver" src\CanDoItAll.Modules.Processes src\CanDoItAll.Processes.Drivers.Abstractions src\CanDoItAll.Processes.Drivers.ArtifactEvidence src\CanDoItAll.Processes.Drivers.BusinessAnalysis src\CanDoItAll.Processes.Drivers.OfficeEvidence src\CanDoItAll.Processes.Drivers.RuntimeEvidence src\CanDoItAll.Processes.Drivers.TranscriptVerification src\CanDoItAll.Processes.Drivers.VerificationGateway src\CanDoItAll.Processes.Drivers.ObservationAggregation src\CanDoItAll.Composition src\CanDoItAll.Web -g "*.cs" -g "*.csproj"
```

Both scans should return no matches for the active source/test surfaces.

## Live OpenAI Smoke
The live OpenAI smoke is not part of deterministic final closure. It must remain opt-in, budgeted, bounded by timeout, and secret-redacted. Do not count deterministic fake-provider tests as live provider proof.

## Package
The final bundle zip is written next to the bundle directory as:

`codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1.final.zip`

The package hash is written as the adjacent `.sha256` sidecar after packaging.
