# SB020 Proof Manifest

## Scope
- Subbundle: `SB020 - Validation descriptor convergence`
- Objective: move only safe validation requirement descriptors and producer policy rules into Core.

## Changed Sources
- `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactProjectionValidationDescriptors.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationDescriptorAdapter.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactEvidenceValidationRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Proof
- Focused validation descriptor test: `bundle://proof/SB020/transcripts/validation-requirement-descriptor-test.txt`
- Critical gate integration proof: `bundle://proof/SB021/transcripts/process-dispatch-projection-validation-descriptor-integration-tests.txt`
- Source assertions: `bundle://proof/SB021/transcripts/source-assertions.txt`
- Core descriptor forbidden-token scan: `bundle://proof/SB021/transcripts/core-descriptor-forbidden-token-scan.txt`

## Result
- Core owns pure expectation mode classification and producer policy facts.
- Validation orchestration, content reads, lineage checks, and diagnostics persistence remain in the module.
- Existing artifact validation behavior is preserved through the module adapter.
