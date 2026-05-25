# SB06 Proof Manifest

## Status

- Completed

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs line 371 defines ShouldCompressNoProgressRetry.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs line 396 detects current-attempt evidence.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs line 405 identifies no-progress reasons.
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs line 6834 covers repeated no-progress compression.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| No-progress retry compression decision | ShouldCompressNoProgressRetry; source: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs | ShouldRetryIncompleteSuccessfulRun; proof: bundle://proof/SB06/transcripts/source-assertions.txt | Evaluated before scheduling another dispatch retry; passing command: bundle://proof/SB06/transcripts/passing.txt | ShouldRetryIncompleteSuccessfulRun_compresses_repeated_no_progress_missing_tool_attempt proves repeated no-progress attempts stop.; negative transcript: bundle://proof/SB06/transcripts/failing-first.txt |

## Failing-First Proof

- Transcript: bundle://proof/SB06/transcripts/failing-first.txt
- Summary: Pre-change retry logic kept scheduling identical no-progress attempts after attempt one.

## Passing Proof

- Transcript: bundle://proof/SB06/transcripts/passing.txt
- Tests: ShouldRetryIncompleteSuccessfulRun_compresses_repeated_no_progress_missing_tool_attempt

## Semantic Invariants

- Contract: bundle://proof/SB06/semantic-invariants.md
- Invariant: SB06-INV-001

## Anti-Stub Audit

- Transcript: bundle://proof/SB06/transcripts/anti-stub-audit.txt
- Result: No production stubs, no NotImplementedException placeholders, and no fake artifact satisfiers were introduced.

## Changed-File Hashes

- Transcript: bundle://proof/SB06/transcripts/changed-file-hashes.txt
- BE96AE9C810CE82F0024598B114E3E5418105C81B6E7CB481C44404C566F4913  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs
- DDE0D4670E819160395A994D3A89A853021544520931DE246582ABB32385FAEE  repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Validation

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests" exited 0 with 409 passed tests.
- dotnet build CanDoItAll.slnx --no-restore exited 0 with existing EF Core assembly-version warnings and zero errors.

## Blockers

- None.

