# SB04 Semantic Invariants

## Invariants

- Invariant ID: SB04-INV-001
- Source raw note: RN04 - Harden artifact storage, lineage, dedupe, content hash, retention, and stale artifact handling.
- Expected behavior: Artifact recording must normalize projection lineage, compute content hash when required, dedupe by projection identity before display key, preserve scope boundaries, serialize concurrent PostgreSQL writes for the same run-scoped artifact identity, and recover unique-index races as explicit idempotent success or scope-conflict failures.
- Disallowed shallow implementation: A pre-insert lookup without a transactional lock or unique-conflict recovery, because concurrent dispatch/recovery workers can still insert duplicate logical artifacts or throw raw provider exceptions.
- Failing-first test: bundle://proof/SB04/transcripts/failing-first.txt proves the old unguarded artifact save/notify/success sequence is absent.
- Passing test: bundle://proof/SB04/transcripts/passing.txt proves the race source invariant, artifact dedupe/hash runtime tests, and stale-content hash-mismatch validator test pass.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs; repo://tests/CanDoItAll.Tests.Integration/ProcessDatabaseRedTeamSourceInvariantTests.cs; repo://src/CanDoItAll.Modules.Processes/README.md.
- Production assertions: The runtime uses strongly typed lineage and existing `ProcessArtifactRecord` identity fields; no hardcoded project, run, UI, Blazor, or Tetris special cases were introduced.
- Red-team negative case: A duplicate projection identity that arrives after the pre-insert query is protected by advisory locking on PostgreSQL and by unique-conflict recovery on providers that surface a unique violation.
- Retention guidance: Artifact cleanup remains explicit and dry-run-first; it must preserve enough lineage metadata to explain stale, duplicate, or hash-mismatched evidence and must not delete the only required-artifact lineage record before retention policy expiry.
- Downstream dependency check: SB08 and SB13 can rely on artifact storage identity and stale/hash proof without adding their own dedupe rules.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Artifact projection identity lock and unique-conflict recovery in repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs `RecordArtifactAsync` | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs dispatch/recovery callers | bundle://proof/SB04/transcripts/source-assertions.txt proves normalize, lock, lookup, insert, commit, or conflict resolution lifecycle | bundle://proof/SB04/transcripts/failing-first.txt proves the old unguarded save/notify/success path is absent |
| Projection identity hash in repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactIdentityService.cs | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactIdentityService.cs `ComputeProjectionIdentityHash` | repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs `ProcessArtifactRecord` | bundle://proof/SB04/transcripts/passing.txt proves content hash, lineage normalization, and persisted `ProjectionIdentityHash` lifecycle | bundle://proof/SB04/transcripts/passing.txt proves duplicate identities collapse |
| Content-hash mismatch classification in repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs validator | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs read model | bundle://proof/SB04/transcripts/passing.txt proves read content, compare lineage hash, classify mismatch lifecycle | bundle://proof/SB04/transcripts/passing.txt proves stale content is not accepted |
| Retention guidance in repo://src/CanDoItAll.Modules.Processes/README.md | repo://src/CanDoItAll.Modules.Processes/README.md Processes architecture docs | repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs `RetentionDays` policy field | bundle://proof/SB04/transcripts/source-assertions.txt proves explicit dry-run cleanup and lineage-preservation lifecycle guidance | bundle://proof/SB04/transcripts/source-assertions.txt proves guidance is present |
