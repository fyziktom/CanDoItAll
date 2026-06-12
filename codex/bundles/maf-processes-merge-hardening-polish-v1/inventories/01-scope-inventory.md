# Scope Inventory

## Must inspect before editing

### Repo/transient artifact surfaces

- `.gitignore`
- `01-execution-report.md`
- `codex/bundles/**`
- `codex/bundle-exports/**`
- `codex/skills/bundles/**` (must remain)

### Process / MAF boundaries

- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.AgentFramework.Maf/**`
- `src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj`
- `src/CanDoItAll.Processes.Contracts/CanDoItAll.Processes.Contracts.csproj`

### Current driver packages

- `src/CanDoItAll.Processes.Drivers.Abstractions/**`
- `src/CanDoItAll.Processes.Drivers.ArtifactEvidence/**`
- `src/CanDoItAll.Processes.Drivers.BusinessAnalysis/**`
- `src/CanDoItAll.Processes.Drivers.ObservationAggregation/**`
- `src/CanDoItAll.Processes.Drivers.OfficeEvidence/**`
- `src/CanDoItAll.Processes.Drivers.RuntimeEvidence/**`
- `src/CanDoItAll.Processes.Drivers.TranscriptVerification/**`
- `src/CanDoItAll.Processes.Drivers.VerificationGateway/**`

### Dispatcher files with observed domain-specific logic

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OutputValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DomainRecoveryGuidance.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessImplementationStackRules.cs`
- Related files discovered by `rg -n "ProcessDotNetHostEvidenceRules|ProcessConcreteProductPathRules|ProcessImplementationReceiptTimeline|ProcessCarriedImplementationProofRules|TryBuildProjectStructureGroundingAsync|TryBuildArtifactInspectionGroundingAsync" src/CanDoItAll.Modules.Processes`.

### Tests to inspect

- `tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationTestHarness.cs`
- `tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs`
- `tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs`
- Any test discovered by `rg -n "SB[0-9]{2,3}|INV_[0-9]{3}|subbundle|bundle" tests`.

## Observed concrete findings

- `ProcessDriverVerificationGatewayTests.cs` has test method names that encode subbundle IDs.
- `SecretScanningTests.cs` skips `codex/bundles`; add a separate tracked-file artifact guard instead of relying on broad skip behavior.
- `ProcessImplementationStackRules.cs` is domain-heavy and resides under `CanDoItAll.Modules.Processes`.
- The current gateway and driver packages are intentionally explicit/read-only and should not be converted into dynamic runtime infrastructure.
