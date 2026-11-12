# Durable recovery cursors and pending cycles

Status: Behavioral gate passes, 54/54 selected cases. This qualifies the retained
position/intent contract; automatic recovery and final E2E remain open.

Runtime positions now retain the selected Process run and last inspected Agent execution.
Both are typed, stage constrained and preserved across queue release and lease takeover.
A cycle that advances past unresolved work must retain CycleHadPending; later success
cannot erase it or complete recovery. Only the explicit pending Quiescence-to-Initial
restart clears it. An unresolved mutation retains its exact selection, original command
context and action; adding pending evidence cannot move its cursor or replace its intent.

The codec omits nullable additions and the false pending flag. Existing canonical v1
positions remain byte-identical, with the same 1024-byte bound and no schema migration.
Lost mutation acknowledgement is distinct from known pending work: the coordinator
keeps the original position/intent, so a lost final-root acknowledgement can complete
after exact receipt recovery. No blanket exception-to-pending-bit conversion was added.

| Class | Project | Expected / discovered / passed |
| --- | --- | --- |
| ProcessCancellationRecoveryContractTests | Unit | 12 / 12 / 12 |
| ProcessCancellationRecoveryCodecTests | Unit | 8 / 8 / 8 |
| ProcessCancellationRecoveryCoordinatorTests | Unit | 22 / 22 / 22 |
| ProcessCancellationRecoveryQueueTests | Integration | 8 / 8 / 8 |
| ProcessCancellationRecoveryAtomicTests | Integration | 4 / 4 / 4 |

Exact class-prefix filters (including the trailing dot) were declared before execution.
Real PostgreSQL tests prove selected cursors, sticky evidence, original intent and stale
owner refusal through release and takeover. Existing atomic barrier/receipt cases pass.
There were no build or test failures in this candidate.

Application, Persistence, Processes module, Composition, Web, Unit and Integration build
in dependency order in standard Release with `--no-restore --no-dependencies
-p:UseLocalCanDoItAllLibraries=true /m:1`. All 24 owning/copied DLL/PDB identities and
source pins match before/after testing. The direct architecture review passes: existing
Application and Persistence contracts only, with no new production type, project
reference, partial or dependency direction. Another design-time graph snapshot is not
needed for these record/codec edits.

Full portability enforcement passes without baseline changes (15670 reviewed findings),
rendering review passes 1212 inputs, and documentation/whitespace checks pass. Unchanged
validation-tool checks retain the preceding 6/4/7 qualification. All five execution
commands were observed, with zero new process, database-name or temporary-path residue.
The isolated PostgreSQL server and existing Release outputs were reused.

Compact proof: `proof/2026-10-22-recovery-cursors.json`, 93705 bytes, SHA256
`C3C77797B12265553599B0658C1BECF48BFC3D715019BD31EE1DBF317571F0A3`.
Secret-pattern and private fixture-password sentinel checks pass. Required command,
TRX and identity records remain under `c4-recovery-cursors`. Historical policy-denied
cleanup remains unresolved; no denied resource was retried. SPA remains clean on
`codex/concurrent-mutations` and has not been merged into main.

Progression: implement bounded runtime/decision/shared-scope/quiescence stages and exact
owner-intent recovery. Fix the inherited adapter's mutable-profile comparison before
using it. Include persisted root-local work and legacy Cancelled-root confirmation in
quiescence. Actual admission/publication races, cancellation-aware waits, fairness,
capacity, HTTP/status, refresh, remaining writers and final Stable are still required.
