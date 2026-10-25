# Finite recovery coordination and transfer safeguards — 2026-10-22

Behavioral gate: **Pass for this P16 integration checkpoint**. All **58 distinct cases**
qualify, including **25 new cases**. This continues the signed queue substrate
`13de74201`; automatic recovery is not enabled and the full E2E outcome remains open.

## Behavior and ownership

Process Application now owns a finite coordinator and a stage-operation interface. It
checkpoints a prepared intent before allowing an owner invocation, limits each slice by
operations and cooperative elapsed time, and releases unchanged progress when yielding.
Lost ownership ends the slice. Cancellation propagates without clearing uncertain work.
Unknown owner outcomes preserve the original intent and defer it; unknown checkpoint
acknowledgements propagate so a successor replays the saved action. Invalid owner results
cannot replace or silently clear retained evidence. Defaults are eight operations, two
seconds, a 30-second lease, 25 ms yield and two-second failure retry.

Process transfer inspection includes the queue in its exclusion locks. Pending rows and
unseeded stopped roots block source export and target adoption, including roots already
marked Cancelled. Completed markers still make a target nonempty, but do not themselves
block source export. Inspection uses metadata and preserves malformed payloads. It does
not lock the seed singleton. Independent PostgreSQL connections prove that ACCESS
EXCLUSIVE NOWAIT refuses an active runtime writer, releases import's acquired locks,
and allows that writer to stage and commit the original recovery row.

No project references, runtime partials or hosted registrations changed. Queue persistence
and business transitions retain their existing owners. The coordinator has a pure test
seam and does not depend on the Agent runtime, filesystem or database. Actual stage
composition and worker activation still require the remaining P16 proof.

## Qualification

| Owning class | Qualified cases |
| --- | ---: |
| Unit ProcessCancellationRecoveryCoordinatorTests | 19 |
| Unit ProcessCancellationRecoveryContractTests | 9 |
| Unit ProcessCancellationRecoveryCodecTests | 7 |
| Integration ProjectTransferOwnerInspectionTests | 23 |

Build-backed discovery confirmed each exact namespace/class prefix and every declared
source method. Tests used the owning csproj with `--configuration Release --no-build
--no-restore -p:UseLocalCanDoItAllLibraries=true /m:1 --filter <exact class prefix>`.
The retained proof records the full commands, discovered cases and individual results.

All six direct Release builds pass: Application, Process Module, Workbench, Web, Unit and
Integration. Integration was rebuilt after one test-only correction. The first run was
57 passed / one failed; the corrected transfer class passed all 23 cases. Its old model
assertion expected seven Projects entities. Source inspection confirms nine explicit
configurations, including the existing lifecycle receipt and deletion completion owners.
The assertion now requires nine; isolation and transaction checks remain intact. The
earlier Process model assertion was also updated from its historical 22 to the current
34 entities. This is not a suppression of owner-boundary checks.

Exact comparison of nine pinned source files and twenty owning/copied binaries confirms
that the repair changed only the transfer test and Integration DLL/PDB. The 35 Unit
cases are retained under that unchanged-production/Unit bridge. Both candidates were
verified again after execution. Across both attempts, 81 cases executed: 80 passed and
one historical assertion failure is retained; the final qualified set has 58 distinct
passing cases.

Final portability enforcement passes with **15669 reviewed findings unchanged**, without
rewriting the baseline. Rendering register: 1212 inputs; documentation: 434 maintained
files at execution; whitespace passes. The portability enforcer, secret scanner and
rendering validator tests also pass (6, 4 and 7 cases).

Application architecture snapshot `snap-20261022122403-1f7ee93a` has 401 types, 4174
members, no diagnostics or cycles, and no coordinator findings. Direct project-file
inspection confirms unchanged references. The module snapshot is recorded with its
limitation: 123 package-advisory workspace diagnostics and one DI warning prevent
claiming a clean full-module analysis; direct builds pass. The changed transfer queries
introduce no dependency or new owner. This gate passes for coordinator/transfer work,
with stage composition and operational activation still requiring review.

## Proof and resources

Proof: [2026-10-22-recovery-coordination.json](../proof/2026-10-22-recovery-coordination.json),
123957 bytes, SHA256 `CA28C96D313575E4A26F13570D1DEE070C7999065DD36A7CC85DCC71DC8CB86C`.
It passes secret-pattern and private PostgreSQL-sentinel checks.

The two candidates reuse standard Release outputs and the existing isolated PostgreSQL
18 server. All five execution commands are observer-covered. Both attempts reconcile
with zero remaining observed processes, database-name deltas or temporary-path deltas.
The fixture owns provider-history database leases and transfer-dataset-activation
temporary directories. No new application host or container was created. Historical
automatic cleanup denials remain recorded; none was retried. Ordinary 5032, installed
tools, unrelated resources and SPA's separate branch remain preserved.

## Continuing P16

The actual stage adapter must retain runtime/notification cursors, keep unresolved-work
observations across a full cycle, and check persisted root-local work before completion.
Ordinary Agent selection must bound journal hydration and qualify cold metadata cost;
stale indexes cannot trigger an implicit full-journal rebuild in a recovery slice. Exact
Agent stop owners must retain unknown effects and current source/profile fences. Fair
host scheduling, restart/takeover, HTTP/status, snapshot refresh, Workflow/settings,
remaining writers, sustained capacity and the final integrated gate remain required.
SPA stays on `codex/concurrent-mutations`; no SPA main merge is authorized.
