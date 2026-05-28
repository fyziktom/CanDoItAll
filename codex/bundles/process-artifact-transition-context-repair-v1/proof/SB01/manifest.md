# SB01 Proof Manifest

## Status

- Subbundle: `SB01 Runtime Artifact Transition Context`
- Status: `Completed`
- Owned requirements: `R-001`, `R-002`, `R-003`, `R-004`
- Raw notes owned: process failed on artifacts; process must be able to proceed to generic Blazor WASM PWA build work.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Hashes

| File | After SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | `3880795eafd95aa7ff524e04b00c9c2be79f40e459e594533a91a42c79915f71` |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs` | `93010ebe0db08b57091c870e93b92b3e8f19bd6499b780a6430f55dc2c0e585c` |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs` | `3a21ec7a087ab90ec5880820521c1328efd1d9bdbec586e67289d262e8116428` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` | `b81e7f56ad7e56b8c64d1cca7ffb2fe814d7583496019d9c43d7b3f0cc6f3fb7` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing.txt`
- Broader regression transcript: `bundle://proof/SB01/transcripts/artifact-validation-regression.txt`
- Source assertion transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Changed-file hash transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

## Test Names

- Test name: `TransitionStepAsync_SB01_INV_001_allows_automation_completion_with_matching_execution_lineage_required_artifact`
- Test name: `TransitionStepAsync_SB10_INV_001_rejects_stale_execution_lineage_required_artifact_on_manual_completion`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessStepTransitionRequest.ArtifactValidation*` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` and `bundle://proof/SB01/transcripts/source-assertions.txt` prove the process-owned finalizer forwards executor lineage. | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs` and `bundle://proof/SB01/transcripts/source-assertions.txt` prove transition validation consumes the lineage. | `bundle://proof/SB01/transcripts/passing.txt` proves the transition lifecycle completes with current direct-agent workspace lineage. | `bundle://proof/SB01/transcripts/failing-first.txt` and `bundle://proof/SB01/transcripts/passing.txt` prove the old transition rejected matching automation lineage and manual stale lineage still rejects. |

## Closure

- Source proof: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Semantic positive proof: `bundle://proof/SB01/transcripts/passing.txt`
- Adversarial negative proof: `bundle://proof/SB01/transcripts/failing-first.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` reports no stub markers.
- Downstream smoke proof: `bundle://proof/SB02/transcripts/blazor-template-validation.txt`

