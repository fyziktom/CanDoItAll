# C4 small coarse publication probe

The completed V5 probe confirms organization-wide contention during publication's
SQL wait. All effect, cancellation and guard-release checks completed; its sole failure
is the declared one-second responsiveness reference. The earlier fixture failures are
retained below. No candidate qualifies capacity or closes end-to-end correctness.

## First candidate

One LongRunning Fact was discovered and executed in 23.4124 command seconds under
selection `55BB05C0`, tooling `3DB1AB2B`, complete source manifest `FEEF7A27`
(8,815 paths) and runtime manifest `4A28691A` (4,340 files). Evidence is retained at
`.artifacts/concurrent-mutations/validation/c4-small-probe-validation`.

The fixture positively observed the actual C3 publisher blocked by its PostgreSQL
Project-lock holder. The synthetic authority held the actual file publication guard
for 5,047 ms. The publisher returned `Rejected/Unavailable`; all seven measured
operations completed without an operation exception.

| Unrelated operation | Same organization | Separate organization |
| --- | ---: | ---: |
| Checkpoint | 5,394 ms | 227 ms |
| Saved-effect cancellation | 5,785 ms | 657 ms |
| Agent grant | 5,124 ms | 926 ms |

All same-organization writers completed after the SQL holder was released; all
separate-organization controls completed while it remained held. The measured run
was 3,669 logical bytes and each catalog 816,071 bytes. Whole-cohort process allocation
was 179,461,152 bytes; the Workbench interceptor observed ten EF commands. These
are neither per-operation allocations nor physical I/O, complete SQL/network counts,
normal p95, sustained arrivals or fairness measurements. Live-provider provenance
was synthetic, and `CapacityQualified` remains false.

## Assertion defect and closure

The successful saved-effect cancellation produced proposal state `Cancelled`, approval
`Rejected`, effect `NotCommitted` and cancellation `NotDispatched/NeverDispatched`.
The fixture incorrectly expected proposal state `Rejected`. The failure occurred after
the zero SQL task/receipt assertion but before completing both file-workspace checks,
guard reacquisition and the final one-second reference assertion. A repaired candidate
must execute all those checks; this failed run cannot substitute for it.

Cleanup evidence reported no failures or unknown allocation. Parent reconciliation
confirmed the exact database and its clients absent, both file-environment roots and
the SQL-environment root absent, both command PIDs absent, and unchanged stable
temporary-root and PostgreSQL inventories. All source/runtime bytes and path sets
were independently rechecked before releasing the freeze for the test-only assertion
repair. No new residue was found; earlier blocked cleanup remains outstanding.
The parent's periodic process snapshot happened after completion and contains no
child ancestry proof. No application host or browser was part of this workload.

The initial Release build exposed redundant primary-constructor capture warning
CS9124. The exact one-expression repair uses the already initialized runtime field;
the rebuilt candidate passed with the previous 218 warnings. Only the Integration
DLL/PDB changed from repaired D1. Full scanning covered 8,746 files and 36,970 findings
without truncation; final no-write portability enforcement passed with all 15,504
reviewed findings unchanged. Rendering, documentation and diff checks passed.

The conditional narrow-lock design is supported by the observed contention, but its
complete cooperating-writer and recovery perimeter must be implemented and tested
before publication activation. The remaining full cost matrix and runtime policies
are still open.

## Cancellation assertion successor

V4 used fixture `2CAAEFEF`, selection `08A228CF` and tooling `4A20A5BF` in the fresh
`c4-small-probe-cancellation-repair-validation` root. It again executed one failed
case, in 24.2166 command seconds. Complete source `0D84803C` (8,823 paths) and runtime
`EDA00536` (4,340 files) remained unchanged. Same-organization operation durations
were 6,008/5,742/5,140 ms; separate-organization controls were 284/1,150/583 ms.
All separate controls completed while SQL remained held, but one exceeded one second;
this reinforces the distinction between lock isolation and a qualified response-time
target. The guard remained held for 5,049 ms.

The corrected state checks introduced an unjustified exact journal-revision increment.
The existing transition increments the journal once per changed proposal, then the
owner advances it for cancellation completion. Its contract is monotonic advancement,
not one increment per physical save. The exact run revision increased once as expected;
the journal moved from six to eight, causing the new assertion to fail before the
remaining checks. The minimal successor changes only that journal assertion to strict
advancement. Exact run revision, one proposal, cancellation state/effect/origin and
absent dispatch/result/receipt checks remain. Production behavior is unchanged.

V4's exact allocated database and three environment roots were removed. All five
periodically observed processes and both command PIDs were absent; temporary-root and
database/client inventories matched the baseline. The parent verified complete source
and runtime identities and released the freeze for the one-line repair. No new residue
was found. This is not complete short-lived ancestry capture. V4 static enforcement
passed unchanged after scanning 8,754 files and 36,979 findings without truncation;
the standard Release build, rendering, documentation and diff checks passed.

## Completed reference rejection

V5 uses the exact monotonic-journal repair `1FCFF5E3` with source overlay `5FF20B4D`,
selection `DB29407D` and tooling `334996FD`. The fresh
`c4-small-probe-journal-revision-validation` candidate discovered and executed one
case in 24.4102 command seconds. It failed only the unchanged final one-second
reference assertion. Every preceding effect/origin/cancellation assertion passed;
all six writer outcomes were verified, no task or receipt was committed, and both
file and Project guards were successfully reacquired. Workload and cleanup failure
types were null in the retained row result; this does not turn the failing TRX green.

| Unrelated operation | Same organization | Separate organization |
| --- | ---: | ---: |
| Checkpoint | 5,224 ms | 263 ms |
| Saved-effect cancellation | 5,943 ms | 1,065 ms |
| Agent grant | 5,470 ms | 501 ms |

The guard was held 5,049 ms during the observed SQL block. All same-organization
writers completed after release, all independent controls before release. The
independent cancellation still exceeded one second, so narrow locking alone cannot
be assumed to meet the response-time target. Whole-cohort allocation was 188,766,216
bytes; logical selected-run size remained 3,669 bytes and observed Workbench EF
command count ten. The same measurement limits apply.

Independent final verification confirmed all 8,824 source paths (`33E5211F`) and
4,340 runtime files (`FFACF701`) unchanged. The exact database and three allocated
environment roots were absent, as were all five periodically observed process
identities and both command PIDs. Resource inventories matched the baseline; no new
residue was found. Earlier blocked cleanup and the limited process-observation claim
remain explicit. The freeze was released for Resource integration and the reviewed
narrow owner implementation.

The final standard Release build passed with the same 218 warnings. Only the
Integration DLL/PDB changed from V4. Full scanning covered 8,755 files and 36,981
findings without truncation; final no-write enforcement passed with the 15,504
reviewed findings unchanged. Rendering, documentation and diff checks passed.

Decision: implement the [narrow publication protocol](../plan/P04-C4-narrow-publication-protocol-decision.md)
with the complete cooperating-writer/recovery perimeter and the
[callback compatibility requirements](../reviews/P04-C4-prepared-callback-compatibility-audit.md).
Keep activation held for its correctness, matched-cohort, sustained-load and actual
policy/provenance evidence. The coarse reference failure is a design rejection, not
an accepted performance regression or a reason to weaken the threshold.
