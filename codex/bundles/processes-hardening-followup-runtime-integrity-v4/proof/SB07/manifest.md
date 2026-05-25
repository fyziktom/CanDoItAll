# SB07 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` adds typed `ProcessArtifactFailureOwnership` and persists the ownership on validation results and diagnostic payloads.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` requires `CanRouteArtifactContractDispositionFailures` before negative branch routing; the gate requires a satisfied required decision artifact and all failures to resolve to `ReviewDisposition`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` maps `Missing`, `InvalidFormat`, `PlaceholderOnly`, and `StaleOrWrongRun` to `OwnOutput`, preventing missing or malformed current-step artifacts from routing to no-go/repair branches.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` covers missing-own-output blocking, validation ownership production, and allowed review-disposition routing with an already recorded decision artifact.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Artifact failure ownership | `ResolveArtifactFailureOwnership` during required artifact validation | `ProcessArtifactExpectationValidationResult`, artifact validation diagnostic payload, disposition router | Transient per validation run and durable in existing diagnostic journal payloads | SB07 validation test proves missing required evidence is classified `OwnOutput` |
| Required decision artifact gate | `RefreshCandidateArtifactSatisfaction` records satisfied required artifact ids | `HasSatisfiedRequiredDecisionArtifact` in the disposition router | Per finalizer pass, derived from validated current-step artifacts | SB07 router test proves a missing own artifact cannot route even when a no-go branch exists |
| Review disposition route allowance | Explicit `ReviewDisposition` ownership plus required decision artifact | `CanRouteArtifactContractDispositionFailures` | Per finalizer route decision after artifact validation/recovery | Existing router regression proves review disposition failures still route to repair when the decision artifact exists |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB07/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB07/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB07/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB07/transcripts/changed-file-hashes.txt`

## Validation

Passed:

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~SB07_INV_001" --no-restore -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ArtifactDispositionRouter" --no-restore --no-build -v minimal`

The first SB07 focused run compiled the integration test assembly and passed with the known unrelated MSB3277 EntityFrameworkCore.Relational 10.0.0/10.0.4 warnings.

## Blockers

None.
