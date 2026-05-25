# SB04 Proof Manifest

## Status

- Completed

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs line 14 defines missing-upstream-artifact-materialization-requested.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs line 126 checks missing upstream materialization before normal dispatch.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs line 1370 records the durable fingerprint event.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs line 1428 creates the fingerprint.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| MissingUpstreamArtifactMaterializationRequested event | RecordMissingUpstreamArtifactMaterializationAsync; source: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs | TryRequestMissingUpstreamArtifactMaterializationAsync; proof: bundle://proof/SB04/transcripts/source-assertions.txt | Created with a deterministic fingerprint before upstream rerun is requested; passing command: bundle://proof/SB04/transcripts/passing.txt | CreateMissingUpstreamArtifactMaterializationFingerprint deduplicates repeated missing-input requests.; negative transcript: bundle://proof/SB04/transcripts/failing-first.txt |

## Failing-First Proof

- Transcript: bundle://proof/SB04/transcripts/failing-first.txt
- Summary: Pre-change retry behavior could repeatedly ask the same source step to materialize missing artifacts without a durable duplicate guard.

## Passing Proof

- Transcript: bundle://proof/SB04/transcripts/passing.txt
- Tests: ProcessRunAutomationDispatchServiceTests focused suite source assertions plus build

## Semantic Invariants

- Contract: bundle://proof/SB04/semantic-invariants.md
- Invariant: SB04-INV-001

## Anti-Stub Audit

- Transcript: bundle://proof/SB04/transcripts/anti-stub-audit.txt
- Result: No production stubs, no NotImplementedException placeholders, and no fake artifact satisfiers were introduced.

## Changed-File Hashes

- Transcript: bundle://proof/SB04/transcripts/changed-file-hashes.txt
- 31A7C47419DD9026B351ABBD5621FF40A0B4BCDAE624928CF8EF916BB9137319  repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs
- 2E4451B605ED202E0100D084993A623EF177FC20E81B79D35623074C97EA7385  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- DDE0D4670E819160395A994D3A89A853021544520931DE246582ABB32385FAEE  repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Validation

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests" exited 0 with 409 passed tests.
- dotnet build CanDoItAll.slnx --no-restore exited 0 with existing EF Core assembly-version warnings and zero errors.

## Blockers

- None.

