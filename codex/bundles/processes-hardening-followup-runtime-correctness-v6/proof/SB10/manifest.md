# SB10 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs` selects deterministic executable recovery actions from typed block reason code, ownership, diagnostics, and prior attempts.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs` escalates repeated `NoProgress` recovery to `HumanEscalation` when the evidence fingerprint has not changed.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs` persists recovery options and next recovery action when typed block state is applied, and clears both when the step unblocks.
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs` persists `NextRecoveryAction` on `ProcessStepRun`.
- `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260526015652_ProcessRecoveryNextAction.cs` adds the PostgreSQL `NextRecoveryAction` column with `None` as the deterministic default.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs` records `recovery-routing-decision-recorded` journal events with next action, block code, ownership, available actions, evidence fingerprint, and no-progress guard state.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs` exposes next recovery action and recovery options in runtime health read models.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` covers wait-for-materialization, recover-artifacts-only, fresh-agent-session, human-escalation, repair-implementation, and repeated no-progress escalation.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` proves transition persistence of next action and lifecycle event state.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB10 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs | bundle://proof/SB10/manifest.md | bundle://proof/SB10/transcripts/passing.txt | bundle://proof/SB10/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB10/semantic-invariants.md`

## Failing-First or Red-Team Proof

Transcript: `bundle://proof/SB10/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB10/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB10/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB10/transcripts/changed-file-hashes.txt`

- `54AFC605C5B744560B144C6BC0B55B9A1D2D669CA546B97FED45E48EF02A0F27` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs`
## Validation

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRecoveryRouter_SB10_INV|FullyQualifiedName~TransitionStepAsync_SB10_INV_001_persists_recovery_router"` passed: 3 tests.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRecoveryRouter_SB10_INV|FullyQualifiedName~TransitionStepAsync_SB10_INV_001_persists_recovery_router|FullyQualifiedName~TransitionStepAsync_SB05_INV|FullyQualifiedName~TransitionStepAsync_SB09_INV_001_persists_typed_policy_denial_block_state"` passed.
- `dotnet build src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --no-restore` passed with existing EF Core relational version MSB3277 warnings.
- SQLite audit found no SB10 SQLite runtime or migration dependency.
- Migration XML-doc audit found no generated XML documentation comments in the SB10 migration files.

## Blockers

None.




