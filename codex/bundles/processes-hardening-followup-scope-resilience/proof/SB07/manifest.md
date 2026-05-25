# SB07 Proof Manifest

## Status

- Completed

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs line 35 defines ProcessDefinitionLinter.
- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs line 21 builds dry-run summaries.
- repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs line 5 defines the linter test suite.
- repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs line 137 covers legal decision log false-positive avoidance.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| ProcessDefinitionLintIssue | ProcessDefinitionLinter.Analyze; source: repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs | Definition authoring dry-run review; proof: bundle://proof/SB07/transcripts/source-assertions.txt | Created on demand from ProcessDefinitionEditorModel before runtime execution; passing command: bundle://proof/SB07/transcripts/passing.txt | Does_not_warn_legal_approval_decision_log_as_runtime_conflict covers the non-software false-positive guard.; negative transcript: bundle://proof/SB07/transcripts/failing-first.txt |

## Failing-First Proof

- Transcript: bundle://proof/SB07/transcripts/failing-first.txt
- Summary: Pre-change repository had no ProcessDefinitionLinter source file, so definitions could not be dry-run linted for these generic process risks.

## Passing Proof

- Transcript: bundle://proof/SB07/transcripts/passing.txt
- Tests: ProcessDefinitionLinterTests

## Semantic Invariants

- Contract: bundle://proof/SB07/semantic-invariants.md
- Invariant: SB07-INV-001

## Anti-Stub Audit

- Transcript: bundle://proof/SB07/transcripts/anti-stub-audit.txt
- Result: No production stubs, no NotImplementedException placeholders, and no fake artifact satisfiers were introduced.

## Changed-File Hashes

- Transcript: bundle://proof/SB07/transcripts/changed-file-hashes.txt
- 7304F6B3CE8819AFFFC222B96E3C948D665CA731B11B85132344842AB9E394A1  repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs
- 6C2E7DA776C43596BF604891A79C6CD0F08BA57503FFAD1D0B29565A5F35A14E  repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs

## Validation

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests" exited 0 with 409 passed tests.
- dotnet build CanDoItAll.slnx --no-restore exited 0 with existing EF Core assembly-version warnings and zero errors.

## Blockers

- None.

