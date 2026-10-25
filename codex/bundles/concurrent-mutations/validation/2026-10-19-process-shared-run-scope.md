# Active shared Process material scope

Status: the targeted repair passes all 68 selected cases. The first failed run is retained.
This is not runtime executor activation or end-to-end bundle closure. The SPA branch remains
separate and unmerged. The last completed signed backend checkpoint is `bed62fc21`.

## Behavior and boundary

The original root admission declares one RunSnapshot scope. Root schema 2, step schema 5,
local inherited receipt schema 3 and a distinct prepared-launch hash domain prevent older
readers from ignoring the new authority. Existing encodings remain unchanged.

The head starts awaiting capture with no invented material. Begin-capture reserves one
original root grant after a complete bounded quiescence check. Completion commits closed
usage and canonical material together. Abort retains actual work and overruns. Unknown
capture keeps its grant reserved. Only successful shared automatic refresh advances the
parent material and charges that operation's single replan.

Local generations join by original stamp and retain metadata membership, without copying
or recapturing content. Reconciliation freezes joins, reservations, newly acquired/reopened
claims, enrollment and publication. It checks complete membership, actual child rows,
claims and all root allocations before another capture. Unfinished local owners rebind
explicitly; completed history remains readable. Publication uses the actual effect
transaction and the same root lock as scope changes.

No project references, provider callbacks or journal reads were added to the transaction.
Scope state/transitions belong to Runtime; immutable policy provenance remains in the
existing root admission; EF owns current authority and membership. The application producer
and real inherited Agent/Workflow journals remain subsequent work.

## Validation candidate

Candidate: `.artifacts/concurrent-mutations/validation/c4-process-shared-run-scope`.
Original directory ownership was recorded before creation. Standard Release builds refresh
the existing outputs; the current isolated PostgreSQL 18 fixture is reused. Expected exact
discovery before execution:

| Lane | Expected cases |
| --- | ---: |
| Shared scope pure transitions and original wire binding | 31 |
| Shared scope PostgreSQL ownership, recovery and races | 21 |
| Existing genuine root funding | 35 |
| Canonical material and shared content regression | 49 |
| Generation, budget and publication regression | 85 |
| Existing Process decision unit topics | 166 |
| Prepared admission, including prepared-only scope downgrade | 14 |
| Legacy original policy wire | 13 |
| Total | 414 |

New negative tests cover incomplete or active membership, uncleared claims, unknown root
work including a grant with zero work and one replan, overrun activation refusal, payload
recapture, missing original scope, stale parent references and downgrade. PostgreSQL proof
includes rollback after SQL flush, lost acknowledgement, actual host restart, a waiting
descendant join behind parent reconciliation and independent-root progress.

The first complete scan found four generated migration-model metadata entries: RootPathState,
RootPathSyntax, RaisesEscalation and EscalationCount. They copy existing EF properties and
introduce no executable platform assumption. Re-scan and final no-write enforcement are
required after the final source edits. Test results, source identities, architecture review
and resource reconciliation will be recorded after the frozen run; no pass is asserted here.

## Retained failure and repair

The first candidate executed all 414 cases: 397 passed and 17 failed, with no skipped
cases. Its source/runtime identities remained unchanged and all execution commands were
observed. Evidence is retained in
`proof/2026-10-19-planning-authority/c4-process-shared-run-scope.json`, SHA256
`321B8CFCA6EF104174E9682721E9C334BEED641685A3ECE7FDE9582D0E4F2AFD`.
The observer ended after 836.29 seconds; exact reconciliation found no remaining observed
processes or new databases. The 28 unowned TEMP deltas are preserved.

Fifteen scope tests exposed one real production error: no-tracking operators in the composed
query detached the scope head, so its receipt advanced without persisting the head. The
repair applies the tracking choice to the final query and immediately compares the
begin-capture receipt with an independent stored read. The other failures are the legacy
generation downgrade fixture's missing dependent-scope migration and a query assertion
that did not account for the new root scope check. The latter now requires exactly one
scope query by root run and null step, no payload and a bounded total of four to five reads;
it does not waive the event-history scan prohibition.

The fresh repair candidate is
`.artifacts/concurrent-mutations/validation/c4-process-shared-run-scope-repair`.
Its focused selection has 68 cases: all 21 scope persistence cases, both generation
migration cases, the enrollment query bound, 31 pure scope cases and 13 legacy wire cases.
These cover every failed behavior and the affected query/encoding contracts. The original
414-case result remains a failed historical run; it will not be relabeled as a green run
on repaired source. No repeat of unaffected funding/material topics or broad Stable run
is required for this isolated tracking repair.

## Repaired validation

The repaired frozen source contains 9,511 files, SHA256
`BECC353621CBEDBE8F7276449A3422C707E7E8B59081CFF335AA9912A75F4990`;
6,065 runtime inputs have SHA256
`7252A01A69D3382D1E26A673B20B0F3CBEB37CF7AEF6A25869F59B442A23AC28`.
Final verification found both unchanged. Exact discovery and execution were 68/68, all
passed with no skips. Eight direct Release builds and EF model consistency passed.
Full-source portability enforcement passed with 15,639 reviewed findings, zero repair
deltas, and no second baseline write. Rendering, documentation, diff and validation-tool
tests passed.

CodeAnalytics `snap-20261019233529-d1ce2c09` covers four projects, 1,061 types and
9,405 members, with zero cycles. The repair adds no findings; the 343 findings and 190
existing collector diagnostics are unchanged from the first scope candidate. The original
scope addition had six informational member-count findings and structural changes to
existing large owners; responsibility remains in the dedicated scope store and pure
transitions, with thin unit-of-work delegation and no new project references or runtime
partials. Direct builds establish compilation despite the collector's existing diagnostics.

The repair observer covered every command for 138.27 seconds. No observed process or new
database remained; 12 unowned TEMP deltas were preserved. Freeze release was
`2026-10-19T23:40:34.5103046+00:00`. Reviewed proof is
`proof/2026-10-19-planning-authority/c4-process-shared-run-scope-repair.json`, SHA256
`72278E9DD0F8BEF70B2436E04CD8569CB72DC3128FDDC857BF18B199E9B220BE`.

## Remaining qualification

Snapshot input must still reach actual Agent/Workflow child journals and effect producers.
Bounded grant sizing and fairness, capability negotiation, runtime settings, workload and
contention measurements, broad Stable repair and final end-to-end/UI proof remain open.
The historical broad run (16,182 passed, 17 failed, 2,444 unexecuted) remains non-green.
Earlier cleanup review blockers and unowned TEMP entries are retained; this candidate must
report its own observed delta without claiming global cleanup closure.
