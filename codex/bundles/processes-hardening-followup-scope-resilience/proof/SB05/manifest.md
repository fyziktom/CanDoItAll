# SB05 Proof Manifest

## Status

- Completed

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs line 696 detects runtime-log signals conservatively.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs line 893 checks subprocess lineage by subprocess run id.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs line 926 requires valid JSON when JSON format is declared.
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs line 12994 starts the validation tuning test block.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| ProcessArtifactExpectationValidationResult | ValidateArtifactCandidate; source: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | FinalizeStepCompletionAsync; proof: bundle://proof/SB05/transcripts/source-assertions.txt | Created per required artifact before completion, branch routing, or block decision; passing command: bundle://proof/SB05/transcripts/passing.txt | ArtifactContractValidation_rejects_workspace_artifact_from_wrong_execution_run proves stale lineage rejection.; negative transcript: bundle://proof/SB05/transcripts/failing-first.txt |

## Failing-First Proof

- Transcript: bundle://proof/SB05/transcripts/failing-first.txt
- Summary: Pre-change heuristics treated any log as runtime proof, allowed broad lineage, and used placeholder detection that could reject real TODO registers.

## Passing Proof

- Transcript: bundle://proof/SB05/transcripts/passing.txt
- Tests: ArtifactContractValidation_does_not_treat_decision_log_as_runtime_proof; ArtifactContractValidation_accepts_todo_register_as_legitimate_deliverable; ArtifactContractValidation_rejects_malformed_json_file_when_json_is_required; ArtifactContractValidation_rejects_workspace_artifact_from_wrong_execution_run; ArtifactContractValidation_accepts_subprocess_artifact_with_current_child_lineage

## Semantic Invariants

- Contract: bundle://proof/SB05/semantic-invariants.md
- Invariant: SB05-INV-001

## Anti-Stub Audit

- Transcript: bundle://proof/SB05/transcripts/anti-stub-audit.txt
- Result: No production stubs, no NotImplementedException placeholders, and no fake artifact satisfiers were introduced.

## Changed-File Hashes

- Transcript: bundle://proof/SB05/transcripts/changed-file-hashes.txt
- 5B7219D5142FBE47BD91987F46BEEA07D78DDEC12C81BBFD59C99A642551F0DD  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- DDE0D4670E819160395A994D3A89A853021544520931DE246582ABB32385FAEE  repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Validation

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests" exited 0 with 409 passed tests.
- dotnet build CanDoItAll.slnx --no-restore exited 0 with existing EF Core assembly-version warnings and zero errors.

## Blockers

- None.

