# SB02 Semantic Invariants

## Invariants

### SB02-I1 Retired profile states cannot become runtime profiles

Raw note: "No production `src/`, `tests/`, `CanDoItAll.slnx`, build, or runtime path may contain retired SQLite provider support."

Expected behavior: persisted runtime profile operations accept PostgreSQL runtime profiles only. InMemory remains explicit override/test-only and retired legacy profile values are quarantined.

Shallow-pass trap: remove visible UI options while keeping legacy enum/storage values or allowing persisted InMemory profiles.

Adversarial negative proof: `bundle://proof/SB02-legacy-profile-quarantine-hardening/transcripts/dotnet-test-unit-control-plane.txt` and `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/audit-residue-and-bottlenecks.txt`.

Semantic positive proof: `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs` filters persisted runtime profiles and `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs` removes retired modes.

Production assertions: `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt`.

Changed source files: see `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv`.

Downstream dependency check: SB03 canonical runtime resolves only eligible persisted PostgreSQL profiles.

### SB02-I2 Retired-provider audit is explicit, not hidden

Raw note: "Do not hide retired-provider words via string concatenation."

Expected behavior: retired tokens exist only in explicit quarantine constants and an audit allowlist.

Shallow-pass trap: hide retired strings as concatenations to satisfy `rg` scans.

Adversarial negative proof: residue audit rejects hidden string concatenation and unexpected retired tokens.

Semantic positive proof: `repo://src/CanDoItAll.Infrastructure/ControlPlane/LegacyDatabaseProfileCatalogQuarantine.cs` names retired constants directly; `bundle://scripts/audit_residue_and_bottlenecks.ps1` allowlists only that file.

Production assertions: `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/audit-residue-and-bottlenecks.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Explicit quarantine boundary | `repo://src/CanDoItAll.Infrastructure/ControlPlane/LegacyDatabaseProfileCatalogQuarantine.cs` | `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs` | `bundle://proof/SB02-legacy-profile-quarantine-hardening/transcripts/dotnet-test-unit-control-plane.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/audit-residue-and-bottlenecks.txt` |
