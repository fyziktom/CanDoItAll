# SB02 Proof Manifest

## Subbundle

SB02-legacy-profile-quarantine-hardening — Completed.

Owned requirements: R2, R3, R4.

Semantic invariant contract: `bundle://proof/SB02-legacy-profile-quarantine-hardening/semantic-invariants.md`.

## Changed Files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs` | See `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv` | See hash inventory | Removes obsolete storage/source values and adds canonical runtime contract. |
| `repo://src/CanDoItAll.Infrastructure/ControlPlane/LegacyDatabaseProfileCatalogQuarantine.cs` | See hash inventory | See hash inventory | Replaces hidden retired-provider concatenation with explicit constants and quarantine boundary. |
| `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs` | See hash inventory | See hash inventory | Filters persisted profile operations to PostgreSQL runtime profiles. |
| `repo://tests/CanDoItAll.Tests.Unit/DatabaseProfileControlPlaneTests.cs` | See hash inventory | See hash inventory | Covers explicit retired-provider quarantine behavior. |
| `bundle://scripts/audit_residue_and_bottlenecks.ps1` | See hash inventory | See hash inventory | Adds explicit allowlist for intentional retired-source tokens. |

## Commands

| Command | Transcript path | Result |
|---|---|---|
| Control-plane unit tests | `bundle://proof/SB02-legacy-profile-quarantine-hardening/transcripts/dotnet-test-unit-control-plane.txt` | Passed 7 tests. |
| Residue and bottleneck audit | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/audit-residue-and-bottlenecks.txt` | Passed. |
| Source assertions | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` | Passed. |

## Semantic Positive Proof

Production profile persistence now accepts persisted PostgreSQL profiles and rejects hidden managed/InMemory persistence paths. Legacy catalog quarantine still recognizes retired profile tokens explicitly so old catalogs are quarantined predictably instead of deserializing into runtime profiles.

## Adversarial Negative Proof

The residue audit fails hidden `"Sql" + "ite"` style matching and permits retired tokens only in `LegacyDatabaseProfileCatalogQuarantine.cs`. Control-plane tests cover legacy provider/source cleanup while preserving PostgreSQL profile behavior.

## Canonicality Proof

Removing `ManagedPerProfile` and `LegacyDiscovery` prevents retired profile modes from becoming canonical runtime candidates. `IsPersistedRuntimeProfile` keeps persisted Data Sources scoped to PostgreSQL.

## Anti-Stub Audit

`bundle://proof/SB08-final-validation-benchmark-gate/transcripts/anti-stub-audit.txt` found no `TODO`, `NotImplemented`, fixture-specific, test-only, or stub markers in changed production files.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Legacy profile quarantine record | `repo://src/CanDoItAll.Infrastructure/ControlPlane/LegacyDatabaseProfileCatalogQuarantine.cs` | `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs` | `bundle://proof/SB02-legacy-profile-quarantine-hardening/transcripts/dotnet-test-unit-control-plane.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/audit-residue-and-bottlenecks.txt` |

## Browser Validation Analytics

N/A. SB02 has no UI behavior.

## Remaining Risks

No implementation risk remains for SB02. Old catalogs with unknown future retired values will fail explicitly instead of silently becoming runtime profiles.
