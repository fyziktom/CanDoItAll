# SB03 proof manifest

## Status

Completed.

## Owned requirements

Remove dead hot-switching and drain state. Keep database activation restart-first.

## Semantic invariant contract

`bundle://proof/SB03/semantic-invariants.md`

## Changed files

- `repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`
- `repo://src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs`
- `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://tests/CanDoItAll.Tests.Unit/DatabaseRuntimeSwitchingTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/DatabaseRuntimeSwitchingIntegrationTests.cs`
- Hash proof: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`

## Command transcripts

- `bundle://proof/SB03/transcripts/residue-and-switch-audit-final.txt`
- `bundle://proof/SB02/transcripts/unit-runtime-switch-tests.txt`
- `bundle://proof/SB08/transcripts/full-solution-build-final-clean.txt`

## Source assertions

- `DatabaseRuntimeSwitching.cs` now holds runtime profile metadata and generation only.
- `AcquireContextLeaseAsync`, `BeginSwitchAsync`, `DatabaseContextLease`, `DatabaseSwitchSession`, `WaitForDrainAsync`, and `EnableMaintenanceHotSwitch` have no final source matches.
- `DatabaseSwitchCoordinator` records pending activation and returns runtime/pending ids instead of pretending to live switch the process.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Runtime database status | `repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs` | `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | `bundle://proof/SB02/transcripts/unit-runtime-switch-tests.txt` | `bundle://proof/SB03/transcripts/residue-and-switch-audit-final.txt` |

## Semantic positive proof

The full solution builds with the simplified runtime state. Runtime switching tests prove context creation remains canonical and activation remains restart-first.

## Adversarial negative proof

The final residue audit reports `(no matches)` for hot-switching/drain terms and for retired provider residue.

## Residual risks

None for dead hot-switch/drain removal. Existing EF assembly conflict warnings remain outside this subbundle.
