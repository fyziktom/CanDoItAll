# SB01 Semantic Invariants

## Invariant 1

- Invariant ID: `SB01_INV_001`
- Source raw note: `bundle://inputs/00-original-request.md`
- Expected behavior: A process-owned completion transition must validate required artifacts with the executor lineage that produced or projected the artifact, so a current direct-agent workspace-written artifact can complete the step.
- Disallowed shallow implementation: Do not disable transition-time required-artifact validation, do not treat all manual callers as current, and do not rely on read-model satisfaction alone.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB01/transcripts/passing.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Red-team negative case: `bundle://proof/SB01/transcripts/passing.txt` includes the manual stale-lineage rejection test.
- Downstream dependency check: `bundle://proof/SB02/transcripts/blazor-template-validation.txt`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessStepTransitionRequest.ArtifactValidation*` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` and `bundle://proof/SB01/transcripts/source-assertions.txt` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs` and `bundle://proof/SB01/transcripts/source-assertions.txt` | `bundle://proof/SB01/transcripts/passing.txt` | `bundle://proof/SB01/transcripts/failing-first.txt` and `bundle://proof/SB01/transcripts/passing.txt` |

