# SB06 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs` introduces workflow output mapping metadata parsed from artifact expectations and projects workflow artifacts by explicit output id/node id/artifact id/name only when the mapping is unambiguous.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs` keeps heuristic mapping only for the high-confidence single-artifact/single-expectation case and marks ambiguous unmapped workflow artifacts as non-satisfying `Other` records with diagnostics.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` introduces subprocess child-expectation-to-parent-expectation mappings and blocks ambiguous same-kind projection without explicit mapping.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` covers workflow same-kind/name-conflict mapping and subprocess same-kind/title-conflict mapping.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Workflow output artifact mapping | `ResolveWorkflowOutputArtifactMappings` from process artifact expectation metadata | `ProjectWorkflowArtifactsAsync` and workflow start input JSON | Durable in existing process artifact expectation text metadata; projected per workflow run observation | SB06 workflow tests prove same-kind title/name heuristic is blocked without explicit output id |
| Workflow ambiguous mapping diagnostic | `ResolveWorkflowArtifactExpectation` | Workflow projection logging and non-satisfying `Other` process artifact record | Per projected workflow artifact | Ambiguous unmapped artifacts do not receive `ArtifactExpectationId` or expected kind/title |
| Subprocess child expectation mapping | `ResolveSubprocessOutputArtifactMappings` from parent artifact expectation metadata | `ProjectCompletedSubprocessArtifactsAsync` | Durable in existing parent expectation text metadata; consumed when child run completes | SB06 subprocess tests prove wrong same-kind/title child artifact is not selected without explicit child mapping |
| Subprocess projection diagnostic | `RecordSubprocessProjectionGapAsync` | Existing artifact validation diagnostic journal path | Durable process journal entry for missing/ambiguous projection gaps | Ambiguous child artifact projection records a readable diagnostic instead of silently selecting a source |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB06/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB06/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB06/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB06/transcripts/changed-file-hashes.txt`

## Validation

Passed:

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~SB06_INV_001" --no-restore --no-build -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ArtifactContractValidation" --no-restore --no-build -v minimal`

The first SB06 run included a build and passed with the known unrelated MSB3277 EntityFrameworkCore.Relational 10.0.0/10.0.4 warnings.

## Blockers

None.
