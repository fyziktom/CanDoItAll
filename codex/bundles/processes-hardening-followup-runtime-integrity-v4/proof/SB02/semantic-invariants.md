# SB02 Semantic Invariants

## SB02-INV-001

Source raw note: RQ02 / VF02 requires recovery, workflow, subprocess, and source artifact lineage to survive bounded external reference keys.

Expected behavior: process artifact projection stores full typed lineage in `ProjectionLineageJson`, uses compact hash keys for manager recovery dedupe, and validates producer/current-run identity from typed lineage before legacy key/provenance text.

Disallowed shallow implementation:

- prompt-only change
- source-assertion-only proof
- tests that manually seed final state instead of exercising producer/consumer lifecycle
- branch-specific hardcoding
- software-only behavior for generic process runtime

Failing-first or red-team proof:

- `bundle://proof/SB02/transcripts/failing-first.txt` shows old source encoded recovery lineage inside bounded `ExternalReferenceKey`, had no `ProjectionLineageJson`, and validated manager recovery identity from key/provenance GUID text.

Passing proof:

- `bundle://proof/SB02/transcripts/passing.txt` runs `ApplyArtifactProjectionLineage_SB02_INV_001_uses_compact_key_for_long_recovery_lineage` and `ArtifactContractValidation_SB02_INV_001_accepts_manager_recovery_with_compact_key_and_typed_lineage`.

Changed source files and hashes:

- `bundle://proof/SB02/transcripts/changed-file-hashes.txt`

Production assertions:

- `bundle://proof/SB02/transcripts/source-assertions.txt`

Red-team negative case:

- A compact manager recovery key with no embedded GUIDs and no recovery GUIDs in provenance validates only when typed lineage is present; old text-only validation cannot prove it belongs to the current recovery lifecycle.

Downstream dependency check:

- SB03 may proceed because artifact producer identity no longer depends on long, truncatable recovery strings.

Anti-stub audit:

- `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProjectionLineageJson` | `bundle://proof/SB02/transcripts/source-assertions.txt` | `bundle://proof/SB02/transcripts/source-assertions.txt` | `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260525140500_ProcessArtifactProjectionLineage.cs` | `bundle://proof/SB02/transcripts/failing-first.txt` |
| Compact manager recovery external key | `bundle://proof/SB02/transcripts/source-assertions.txt` | `bundle://proof/SB02/transcripts/passing.txt` | `bundle://proof/SB02/transcripts/passing.txt` | `bundle://proof/SB02/transcripts/failing-first.txt` |
