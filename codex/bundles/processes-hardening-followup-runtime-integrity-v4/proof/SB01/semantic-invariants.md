# SB01 Semantic Invariants

## SB01-INV-001

Source raw note: RQ01 / VF01 requires upstream artifact materialization reactivation to include the artifact recorded in the same call so dependent steps can unblock.

Expected behavior: `RecordArtifactAsync` records a required upstream artifact and reopens a blocked downstream step in the same production lifecycle when the downstream dependency and artifact input are satisfied.

Disallowed shallow implementation:

- prompt-only change
- source-assertion-only proof
- tests that manually seed final state instead of exercising producer/consumer lifecycle
- branch-specific hardcoding
- software-only behavior for generic process runtime

Failing-first or red-team proof:

- `bundle://proof/SB01/transcripts/failing-first.txt` shows the old `HEAD` source called reactivation without the tracked artifact and queried persisted records before `SaveChanges`.

Passing proof:

- `bundle://proof/SB01/transcripts/passing.txt` runs `RecordArtifactAsync_SB01_INV_001_reactivates_blocked_downstream_with_tracked_materialized_artifact` and passes.

Changed source files and hashes:

- `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

Production assertions:

- `bundle://proof/SB01/transcripts/source-assertions.txt`

Red-team negative case:

- The old query-only implementation cannot see the newly tracked artifact; the test records the artifact through production `RecordArtifactAsync` and asserts the downstream blocked step is reopened without manually seeding the final state.

Downstream dependency check:

- SB02 may proceed because the materialized artifact lifecycle now has source and test proof that the just-recorded artifact participates in downstream satisfaction.

Anti-stub audit:

- `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessArtifactRecord` tracked during materialization | `bundle://proof/SB01/transcripts/source-assertions.txt` | `bundle://proof/SB01/transcripts/source-assertions.txt` | `bundle://proof/SB01/transcripts/passing.txt` | `bundle://proof/SB01/transcripts/failing-first.txt` |
| `missing-upstream-artifact-materialization-resolved` journal event | `bundle://proof/SB01/transcripts/source-assertions.txt` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs` journal read model consumers | `bundle://proof/SB01/transcripts/passing.txt` | `bundle://proof/SB01/transcripts/failing-first.txt` |
