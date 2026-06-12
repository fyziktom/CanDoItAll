# Source Artifacts

Branch and merge context:

- Branch under review: `maf-processes-refactor`.
- Merge target: `development`.
- Compare result observed during bundle preparation: branch is ahead of `development` and not behind. The compare includes broad deletion of old `codex/bundles/*` content and an added `codex/bundle-exports/process-runtime-live-openai-verification-host-alpha-v1.zip` artifact.

Repository files inspected during bundle preparation:

- `CanDoItAll.slnx`
- `.gitignore`
- `01-execution-report.md`
- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj`
- `src/CanDoItAll.Processes.Contracts/CanDoItAll.Processes.Contracts.csproj`
- `src/CanDoItAll.Processes.Drivers.Abstractions/CanDoItAll.Processes.Drivers.Abstractions.csproj`
- `src/CanDoItAll.Processes.Drivers.Abstractions/Gateway/ProcessDriverVerificationGatewayLane.cs`
- `src/CanDoItAll.Processes.Drivers.Abstractions/Gateway/ProcessDriverVerificationGatewayLaneRules.cs`
- `src/CanDoItAll.Processes.Drivers.Abstractions/Gateway/ProcessDriverVerificationGatewayLaneDescriptor.cs`
- `src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs`
- `src/CanDoItAll.Processes.Drivers.ArtifactEvidence/ArtifactEvidenceAlphaVerifier.cs`
- `src/CanDoItAll.Processes.Drivers.ArtifactEvidence/ArtifactEvidenceVerificationRequest.cs`
- `src/CanDoItAll.Processes.Drivers.OfficeEvidence/OfficeEvidenceAlphaVerifier.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionInvocationRequestBuilder.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OutputValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DomainRecoveryGuidance.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessImplementationStackRules.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionEvidenceDescriptorAdapter.cs`
- `tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs`
- `tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationTestHarness.cs`

Important observed source signals:

- `CanDoItAll.AgentFramework.Maf.csproj` currently references AgentFramework, Tools, Security, and Workspace projects, but not `CanDoItAll.Modules.Processes`.
- `CanDoItAll.Modules.Processes.csproj` currently references Process Core, Contracts, and all current Process Driver packages.
- `ProcessDriverVerificationGateway` is an explicit read-only gateway with typed methods and no observed generic `Verify(lane, object)` dispatch.
- `ProcessDriverVerificationGatewayTests` contains test method names with work-package identifiers such as `SB018_INV_001`, `SB012_INV_001`, `SB033_INV_001`, and `SB024_INV_001`.
- `SecretScanningTests` explicitly skips any path under `codex/bundles`.
- `.gitignore` only ignores `codex/bundles/input/` and one concrete historical bundle log path; it does not block future tracked `codex/bundle-exports` or arbitrary transient work-package directories.
- `ProcessImplementationStackRules` and `ProcessRunAutomationDispatchService.ImplementationProof.cs` still contain software-delivery / runnable-app / .NET / JavaScript / Blazor / project-file heuristics inside `CanDoItAll.Modules.Processes`, not inside a domain driver.
- `ProcessRunAutomationDispatchService.DomainRecoveryGuidance.cs` currently provides empty domain hooks, while concrete software-delivery proof policy remains in generic dispatcher partials.
