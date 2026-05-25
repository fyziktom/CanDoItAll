# SB08 Proof Manifest

## Status

- Completed

## Source Assertions

- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs line 4037 covers tool policy rejection.
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs line 12994 covers artifact validation tuning.
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs line 13201 covers disposition routing.
- repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs line 5 covers definition linter red-team scenarios.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Red-team integration test suite | xUnit filtered test run; source: repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs and repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs | Bundle closure gate; proof: bundle://proof/SB08/transcripts/source-assertions.txt | Run after SB01-SB07 implementation and before completed validator; passing command: bundle://proof/SB08/transcripts/passing.txt | The focused dotnet test filter includes adversarial cases and passed with 409 process/linter tests.; negative transcript: bundle://proof/SB08/transcripts/failing-first.txt |

## Failing-First Proof

- Transcript: bundle://proof/SB08/transcripts/failing-first.txt
- Summary: The initial focused red-team run failed with ExitCode: 1 before the boundary/destination gate was corrected; the final run passed after implementation.

## Passing Proof

- Transcript: bundle://proof/SB08/transcripts/passing.txt
- Tests: ProcessRunAutomationDispatchServiceTests; ProcessDefinitionLinterTests

## Semantic Invariants

- Contract: bundle://proof/SB08/semantic-invariants.md
- Invariant: SB08-INV-001

## Anti-Stub Audit

- Transcript: bundle://proof/SB08/transcripts/anti-stub-audit.txt
- Result: No production stubs, no NotImplementedException placeholders, and no fake artifact satisfiers were introduced.

## Changed-File Hashes

- Transcript: bundle://proof/SB08/transcripts/changed-file-hashes.txt
- DDE0D4670E819160395A994D3A89A853021544520931DE246582ABB32385FAEE  repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- 6C2E7DA776C43596BF604891A79C6CD0F08BA57503FFAD1D0B29565A5F35A14E  repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs

## Validation

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests" exited 0 with 409 passed tests.
- dotnet build CanDoItAll.slnx --no-restore exited 0 with existing EF Core assembly-version warnings and zero errors.

## Blockers

- None.

