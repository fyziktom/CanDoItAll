# SB021 Proof Manifest

## Scope
- Subbundle: `SB021 - Gate G - projection/validation descriptor proof`
- Objective: prove projection and validation descriptors are additive, pure, and behavior-preserving.

## Changed Sources
- `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactProjectionValidationDescriptors.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationDescriptorAdapter.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactEvidenceValidationRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts
- Build: `bundle://proof/SB021/transcripts/build.txt`
- Architecture/API boundary tests: `bundle://proof/SB021/transcripts/architecture-projection-validation-descriptor-tests.txt`
- Focused dispatch integration tests: `bundle://proof/SB021/transcripts/process-dispatch-projection-validation-descriptor-integration-tests.txt`
- Changed-file hashes: `bundle://proof/SB021/transcripts/changed-file-hashes.txt`
- Source assertions: `bundle://proof/SB021/transcripts/source-assertions.txt`
- Core descriptor forbidden-token scan: `bundle://proof/SB021/transcripts/core-descriptor-forbidden-token-scan.txt`
- Anti-stub audit: `bundle://proof/SB021/transcripts/anti-stub-audit.txt`

## Results
- `dotnet build CanDoItAll.slnx --no-incremental -v:minimal` passed with three unrelated pre-existing warnings.
- `ProcessAgentExecutionBoundaryArchitectureTests` passed: 88 tests.
- `ProcessRunAutomationDispatchServiceTests` passed: 539 tests.
- Focused SB019 and SB020 descriptor tests passed.
- Failing-first: N/A for behavior-preserving descriptor extraction; adversarial negative coverage is recorded in `semantic-invariants.md`.
