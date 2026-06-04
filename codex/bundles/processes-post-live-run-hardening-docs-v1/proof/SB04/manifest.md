# SB04 Manifest

## Summary

SB04 hardened process artifact storage identity by serializing PostgreSQL artifact writes on a run-scoped projection/external-reference advisory lock and by resolving unique-index conflicts back into the same idempotent dedupe semantics used by the pre-insert lookup. It also documented retention guidance for artifact cleanup so retention remains explicit, auditable, and lineage-preserving.

## Changed File Hashes

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs SHA-256 A7BBC03F4B0799D748AE5AE318D0F98E00790BD3CEF15FAD75AAF7007AC25049
- repo://tests/CanDoItAll.Tests.Integration/ProcessDatabaseRedTeamSourceInvariantTests.cs SHA-256 9D5DB5B4FF54568CC2CC59A90F597E391BB4D33AD783820CBEAE135EA196EB7D
- repo://src/CanDoItAll.Modules.Processes/README.md SHA-256 CECE3445E09DDBBCECA88CF2B44D93E4FDED7C2E4A44C9A527FE4C2C57CB9C58

## Artifact References

- Runtime artifact recording: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs
- Projection identity service: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactIdentityService.cs
- Artifact record unique index: repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessRuntimeEntityConfigurations.cs
- Red-team source invariant: repo://tests/CanDoItAll.Tests.Integration/ProcessDatabaseRedTeamSourceInvariantTests.cs
- Retention guidance: repo://src/CanDoItAll.Modules.Processes/README.md
- Semantic invariant contract: bundle://proof/SB04/semantic-invariants.md
- Source assertions transcript: bundle://proof/SB04/transcripts/source-assertions.txt
- Failing-first transcript: bundle://proof/SB04/transcripts/failing-first.txt
- Passing transcript: bundle://proof/SB04/transcripts/passing.txt
- Anti-stub audit transcript: bundle://proof/SB04/transcripts/anti-stub-audit.txt
- Changed-file hash transcript: bundle://proof/SB04/transcripts/changed-file-hashes.txt

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Artifact projection identity lock and unique-conflict recovery in repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs `RecordArtifactAsync` | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs dispatch/recovery callers | bundle://proof/SB04/transcripts/source-assertions.txt proves normalize, lock, lookup, insert, commit, or conflict resolution lifecycle | bundle://proof/SB04/transcripts/failing-first.txt proves the old unguarded save/notify/success path is absent |
| Projection identity hash in repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactIdentityService.cs | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactIdentityService.cs `ComputeProjectionIdentityHash` | repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs `ProcessArtifactRecord` | bundle://proof/SB04/transcripts/passing.txt proves content hash, lineage normalization, and persisted `ProjectionIdentityHash` lifecycle | bundle://proof/SB04/transcripts/passing.txt proves duplicate identities collapse |
| Content-hash mismatch classification in repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs validator | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs read model | bundle://proof/SB04/transcripts/passing.txt proves read content, compare lineage hash, classify mismatch lifecycle | bundle://proof/SB04/transcripts/passing.txt proves stale content is not accepted |
| Retention guidance in repo://src/CanDoItAll.Modules.Processes/README.md | repo://src/CanDoItAll.Modules.Processes/README.md Processes architecture docs | repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs `RetentionDays` policy field | bundle://proof/SB04/transcripts/source-assertions.txt proves explicit dry-run cleanup and lineage-preservation lifecycle guidance | bundle://proof/SB04/transcripts/source-assertions.txt proves guidance is present |

## Semantic Evidence

- Raw note owned: RN04
- Shipped behavior: `RecordArtifactAsync` now computes normalized lineage identity, acquires a run-scoped PostgreSQL advisory lock for projection/external artifact keys, performs the existing duplicate lookup under that lock, catches unique-index races, and resolves the conflicting row into the same idempotent success or scope-conflict error.
- Source proof: bundle://proof/SB04/transcripts/source-assertions.txt records SB04-INV-001 for advisory locking, unique-conflict recovery, projection identity hashing, DB uniqueness, and retention guidance.
- Test proof: bundle://proof/SB04/transcripts/passing.txt records the red-team source invariant, seven `RecordArtifactAsync` identity/dedupe/content-hash tests, and the stale-content hash-mismatch validator test.
- Shallow-pass trap: relying only on a pre-insert duplicate query, which still races under concurrent workers.
- Failing-first test: bundle://proof/SB04/transcripts/failing-first.txt records SB04-INV-002 and exits 1 because the old unguarded artifact save/notify/success sequence is no longer present.
- Semantic positive proof: bundle://proof/SB04/transcripts/passing.txt
- Anti-stub audit: bundle://proof/SB04/transcripts/anti-stub-audit.txt records no TODO, `NotImplementedException`, or pending marker in SB04 changed files.
- Browser validation: N/A - SB04 changed runtime storage/identity behavior and README retention guidance, not operator UI layout.
