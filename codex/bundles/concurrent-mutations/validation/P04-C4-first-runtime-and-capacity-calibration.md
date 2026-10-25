# Narrow publication and Resource capacity: retained runtime candidates

The two original candidates failed and remain preserved. The repaired C4 candidate
also failed; the repaired Resource finite baseline passed. None establishes end-to-end
activation or sustained capacity. The latest freeze release is `C5F0742B`, after both
successors reached terminal evidence and resource review at 2026-10-17T12:51Z.

## Frozen source and admission

The complete source manifest was `5E2385AD`, covering 8,926 paths. Standard Release
Unit, Integration and Web builds passed with existing warnings. Portability scan
review accepted nine intentional additions, no stale entries, and final enforcement
without `--write-baseline` passed all 15,520 baseline entries. Rendering passed 1,201
entries and documentation passed 429 files. Those results describe the original
candidate, before the repairs below.

The narrow-owner runtime manifest was `C8AD6AC3` with 4,340 files across three output
roots. Resource capacity used `59078B21`, with 1,105 Integration output files. Both
complete source/runtime path sets and identities were checked again after execution.
Retained evidence is under `.artifacts/concurrent-mutations/validation`.

## Narrow publication failure

`c4-narrow-owner-validation` discovered all 703 selected rows: 247 Unit and 456
Integration. Execution stopped after the fourth lane. The first three lanes passed
51, 21 and 60 rows. The fourth passed 81 of 82: in total, 213 passed, one failed and
489 were not executed. All 14 discovery/execution commands and the owned observer
terminated. This is not a 703-case passing checkpoint.

The failed same-claim background reservation test exposed a real race. Both callers
read an absent claim, then release the workspace guard while acquiring publication
keys. The winner publishes; the loser reacquires the workspace guard and rejects the
changed workspace revision before saving. It must return the completed original
claim's reservation when that exact claim now exists, without replaying a write.

The reviewed two-file repair was transferred as `723B0BE2`: production `50EC2B95`
and test `49F81484`. Only a direct prepublication revision conflict permits one
read-only lookup of the original source/profile/owner claim. Missing, ambiguous or
incomplete publication evidence still refuses. Cleanup failures retain the original
conflict. The deterministic race holds the real Agent key until both callers wait,
then checks one publication and identical returned reservations. One new Fact covers
seven refusal arrangements. The prospective selection is 704 rows; discovery and
execution on the repaired build remain required. The extra witness capture and
whole-run enumeration cost are not claimed to be neutral.

The observer recorded 541 polls, 67 process identities, no faults or read races, and
two unrelated instances of completed PID reuse. Final reconciliation found no exact
process, database or specific temporary-family residue. Four anonymous TEMP files
appeared and four disappeared in the global TEMP observation scope; their ownership
was not established and they were not deleted. Raw reconciliation `AB16CB84` remains
`RefusedOrIncomplete`. Parent review `BD60DB9D` retains that observation gap. Fourteen
SDK logs were positively attributed by command, parent, PID and creation time, copied
to retained proof, and their exact original leaves removed.

## Resource capacity calibration failure

`resource-capacity-baseline-validation` discovered one LongRunning Fact, which failed
after 13.025 seconds during instrumentation calibration. Its selected TRX contains
exactly four records: payload calibration, environment, instrumentation control and
terminal evidence. It ran zero of 126 matrix batches and zero of 2,178 matrix calls.
The calibration control did create one Resource and acknowledge its transaction.
There are no latency, throughput or capacity conclusions from this attempt.

The failed assertion requires an observed connection close. It recorded one begin
and one commit but zero closes. The harness environment query explicitly opened a
connection, then returned its pooled context without balancing that open. Source
analysis identified retained EF open-count state affecting the next pooled use.
The successor must balance the explicit environment open before disposal and remove
the unnecessary explicit open in the wait-control query, retaining the close
assertion, pooled workload and lock checks. Provider advisory spans being classified
as `Other` is separate: parameter-aware EF events identify the lock domains; the
failed assertion was not a missing-lock check.

The owned observer recorded 174 polls, ten process identities and no faults or read
races. Both command processes and the observer terminated. Independent failed-result
reconciliation `84A58C71` verified all 16 TRX counters, exact selected method linkage,
zero matrix execution, and no remaining exact process/listener, PostgreSQL or delta
in either named temporary family. Two attributed SDK logs were retained and their
original leaves removed. Periodic observation and residue equality do not establish
a complete allocation ledger.

Parent freeze release `3DF869B2` allows reviewed repairs and new validation only.
Earlier blocked cleanup and the anonymous C4 TEMP gap remain explicit. Resource
shared-project optimization stays held until a fresh measured baseline passes.

## Required successor gates

Refresh standard Release outputs, rerun the protected-source portability procedure,
review every added/stale finding and require final non-writing enforcement. Discover
and execute the complete 704-row narrow selection, then run the fresh capacity
candidate and reconcile its actual measurements and resources. Keep failed evidence
immutable. Persisted policy, selected authority, Workflow protection, original-intent
recovery, remaining owners and final broad validation remain separate mandatory work.

## Repaired C4 candidate

Source `EEA36D99` covers8,927 paths. Standard Release Unit, Integration and Web builds
passed, followed by final non-writing portability enforcement of15,521 entries,
rendering1,201 and documentation429. The one additional portability finding was the
reviewed configured physical-path policy root, not a platform literal. Runtime
`029086F6` contains4,340 files; final complete path/byte verification passed.

`c4-narrow-owner-reservation-repair-validation` discovered all704 rows. Seven lanes
passed268 rows, including all83 reservation/admission cases. The store Unit lane then
passed108/115: total383 executed,376 passed, seven failed,321 unexecuted. All failures
are deletion projection-reconciliation arrangements, not the repaired reservation race.
Execution stops at that failed lane; this is not a704-row pass.

Deletion captured its conflict witness before execution/chat/usage index repair.
Those deliberate writes invalidated its own original bytes. Moving capture after
all plan reads would conceal real concurrent audit changes. A proposed preload/read-only
plan split also left a preload-to-capture window; review rejected that source checkpoint.
The successor must protect canonical source bytes across normalization, exclude only
the three derived projection files from that initial comparison, preserve all pending
publication checks, and capture a complete material witness before planning. Legacy
audit receipt/artifact writers can bypass publication keys; their complete producer
cutover remains an explicit safety gate, including the interval after final validation.

All18 commands and the owned observer terminated. The observer recorded1,395 polls,
90 process identities, no read/parse faults and two unrelated completed-PID reuses.
Final reconciliation `A31EA24D` found no remaining exact process/listener, new database
or client, or delta in113 named temporary families. The broad shared TEMP scope had
59 unowned changes; they remain evidence without deletion or ownership inference.
Raw status remains RefusedOrIncomplete for that gap. Parent review `BA1A15CE` retains
it and the seven failures. Eighteen exact SDK logs/34,035 bytes were attributed by
command, parent, PID and creation time, retained, then their original leaves removed.

## Repaired Resource finite baseline

`resource-capacity-baseline-validation-v2` uses the same `EEA36D99` source and
Integration runtime `1230D824` with1,105 files. Resource module MVID is
`9633d942-0a8c-469e-8826-b3603c29fd82`, Npgsql10.0.3, .NET10.0.12 and PostgreSQL18.6.
The corrected harness `EA361F7D` passed its single LongRunning Fact in66.3457 seconds.
Extraction `E0962260` verified263 records,126 batches,2,178 matrix calls and17 controls.
There are2,185 individual command maps; ten control calls rely on source assertions
and aggregate outcomes. Every emitted map has zero promotion-group locks, as required
for this exclusive-project baseline. Calibration and terminal cleanup report no
unexpected failure; injected cleanup faults retain their expected semantic outcomes.

Three finite samples include the first run and discard no warmup. At32 writers:

| Operation/shape | Same-project owner P95 | Independent-project owner P95 |
| --- | --- | --- |
| Save/small |425.2–429.1 ms |34.1–161.0 ms |
| Save/escaped |626.3–655.4 ms |171.6–201.3 ms |
| Delete/small |264.7–355.0 ms |21.8–31.8 ms |
| Promotion/escaped |590.7–726.8 ms |401.0–416.9 ms |

Escaped same-project Save allocates3.12–3.14 MB per attempted call as a precise
process-wide batch delta, including instrumentation/background activity. These results
show a material same-project serialization cost and justify the reviewed shared-project,
selected-resource protocol experiment. They do not yet establish its correctness,
improvement, fairness or production capacity. Provider spans and EF events are separate
observations, not summed network round trips; client-observed critical intervals are
not pure server lock durations.

Final source/runtime identity passed. Independent reconciliation `EBE47096` verified
no remaining exact process/listener, PostgreSQL/client or two-scope temporary delta.
The observer recorded275 polls with no fault or PID mismatch. Two attributed SDK logs
were retained and their exact original leaves removed. Parent acceptance `E08DA18C`
accepts this finite baseline within the declared observation scope; allocation ledger
completeness remains false. No host/browser was started by this workload.

Freeze release `C5F0742B` permits reviewed maintained edits and standard builds. It
preserves the failed C4 candidate, earlier blocked cleanup and anonymous TEMP gaps.
Next: close deletion and audit-writer safety, run its complete successor, implement and
measure Resource shared-project locking, integrate persisted policy and actual runtime
authority, and continue all remaining owner/caller/operational gates.

The later policy/deletion publication successor is recorded in
[the P07 checkpoint](P07-policy-owner-integration.md#fresh-c4-publication-contract-run).
It executed all 706 cases with 703 passes and three caller failures. Its terminal
source/runtime review releases the freeze for repairs (`5D230E60`), without accepting
the failed application gate or the 27 unowned temporary-directory deltas. This later
result does not replace or relabel the failed candidates above.
