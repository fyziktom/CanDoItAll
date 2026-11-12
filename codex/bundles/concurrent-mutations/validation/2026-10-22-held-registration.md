# Held cancellation registration qualification

Status: **Pass, Behavioral, for ordinary late registration and stale completion.**
Held inherited initial publication, automatic recovery activation and the final E2E
gates remain open. SPA remains on its separate branch; main is unchanged.

## Confirmed defect and repair

The two-case regression runs the actual Core and MAF producer against signed production
`2a53d6826`, with the registered Process policy, PostgreSQL root barrier and real stop
protocol. Registration before stopping passes. Registration after stopping makes no
provider call, but Core changes the saved cancellation into failure. That original
failure, source and 32 binary identities remain retained in `confirmed-race`.

The existing admission exception now carries the verified original session internally.
Core recognizes this as owner cancellation only when the session matches the current
execution. A foreign session or the public error code alone does not qualify. The atomic
saved-state merge independently refuses stale success, failure or repeated cancellation
over a saved background cancellation. Normal execution and approval continuation retain
the cancellation outcome without wrapping it as a persistence failure. No fresh read
before registration is used as a substitute for these publication checks.

The late-registration test compares the entire saved detail before and after the stale
finalization, including timestamps, journal, logs and metrics. Two supporting cases stop
through an independent File store and cancellation registry after the runtime releases
its dispatch lease, then attempt successful or failed completion. They exercise actual
journal transitions and the parent stop owner. They are not OS process-restart proof.

## Validation

All **93 cases pass**, with exact discovery and current source methods verified:

| Class | Suite | Cases |
| --- | --- | ---: |
| ProcessRegistrationCancellationRaceTests | Integration | 2 |
| ProcessAdmissionCancellationRaceTests | Integration | 2 |
| AgentToolBackgroundJournalIntegrationTests | Integration | 43 |
| AgentBackgroundParentStopTests | Integration | 11 |
| ProcessCancellationCompletionIntegrationTests | Integration | 2 |
| AgentExecutionCancellationRegistryTests | Unit | 20 |
| AgentPendingApprovalCancellationTests | Unit | 13 |

Seven affected/dependent production projects plus Unit and Integration build in standard
Release, with local libraries and serialized outputs. The final 46 source pins and 44
owning/copied outputs remain unchanged after execution. Full portability includes new
untracked source and passes without a baseline edit: **15674 reviewed findings unchanged**.
Tooling tests, rendering register, documentation and whitespace checks pass. The initial
Core compile missed the Models namespace import; that failure is retained. Broad Stable
remains reserved for the final integrated candidate.

## Architecture gate

Status: **Pass with existing follow-ups.** No new production type, partial file or project
reference. Core owns its execution outcome and existing journal exception. Process and
MAF implementation are unchanged. Exact source inspection supports the targeted builds.

Core snapshots `snap-20261022192031-3cb1910f` and `snap-20261022195405-3cb1910f` load 802
types, with 5933 → 5936 members and zero diagnostics. Dependency edges and the three
existing type cycles are unchanged. Findings rise from 228 to 229: the existing 21-line
admission exception reaches the informational nine-member threshold. Its added constant,
typed original-session evidence and cohesive factory preserve exception compatibility
and prevent unrelated failures being classified as cancellation. No unrelated ownership
was added to address or suppress this informational finding.

## Resources and proof

The existing isolated PostgreSQL fixture is reused. Both observed runs reconcile with
zero new processes, database names or temporary paths; the final observer covers all
seven execution commands. Ordinary 5032 and earlier denied cleanup targets are untouched.

[Compact proof](../proof/2026-10-22-held-registration.json): 134847 bytes, SHA256
`D06AF37606BECD443DB41A068420552FC752CB0C3E5677D51F2B50FB1A47587D`.
Exact commands, TRX, source/runtime identities and failures remain under
`.artifacts/concurrent-mutations/validation/c4-held-registration`.

Proceed to the actual inherited producer held before initial journal publication. Its
SQL enrollment must keep root recovery pending until the original measured work is
resolved. Worker/HTTP activation, bounded waits, process restart, capacity/fairness,
snapshot refresh/settings, remaining writers and final E2E are still required.
